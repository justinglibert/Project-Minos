/************************************************************************************

Copyright   :   Copyright 2014 Oculus VR, LLC. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.3 (the "License");
you may not use the Oculus VR Rift SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

http://www.oculus.com/licenses/LICENSE-3.3

Unless required by applicable law or agreed to in writing, the Oculus VR SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the player's movement in virtual reality.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class OVRPlayerControllerROVR : MonoBehaviour
{
    /// <summary>
    /// The rate acceleration during movement.
    /// </summary>
    public float Acceleration = 0.1f;

    /// <summary>
    /// The rate of damping on movement.
    /// </summary>
    public float Damping = 0.3f;

    /// <summary>
    /// The rate of additional damping when moving sideways or backwards.
    /// </summary>
    public float BackAndSideDampen = 0.5f;

    /// <summary>
    /// The force applied to the character when jumping.
    /// </summary>
    public float JumpForce = 0.3f;

    /// <summary>
    /// The rate of rotation when using a gamepad.
    /// </summary>
    public float RotationAmount = 1.5f;

    /// <summary>
    /// The rate of rotation when using the keyboard.
    /// </summary>
    public float RotationRatchet = 45.0f;

    /// <summary>
    /// If true, reset the initial yaw of the player controller when the Hmd pose is recentered.
    /// </summary>
    public bool HmdResetsY = true;

    /// <summary>
    /// If true, tracking data from a child OVRCameraRig will update the direction of movement.
    /// </summary>
    public bool HmdRotatesY = true;

    /// <summary>
    /// Modifies the strength of gravity.
    /// </summary>
    public float GravityModifier = 0.379f;

    /// <summary>
    /// If true, each OVRPlayerController will use the player's physical height.
    /// </summary>
    public bool useProfileData = true;

    protected CharacterController Controller = null;
    protected OVRCameraRig CameraRig = null;

    public float MoveScale = 1.0f;
    private Vector3 MoveThrottle = Vector3.zero;
    private float FallSpeed = 0.0f;
    private OVRPose? InitialPose;
    private float InitialYRotation = 0.0f;
    private float MoveScaleMultiplier = 1.0f;
    private float RotationScaleMultiplier = 1.0f;
    private bool SkipMouseRotation = false;
    private bool HaltUpdateMovement = false;
    private bool prevHatLeft = false;
    private bool prevHatRight = false;
    private float SimulationRate = 60f;
    private float buttonRotation = 0f;

    private const int FREQUENCY = 2000;
    private float[] samples;           // Samples
    public int iStartTimeout;
    public float fAverage = 0.0f;
    public float fHighWaterMark = 0.0f;
    public float fAudioAmplitude = 0.0f;
    private bool bFixedHWM;
    private float[] fAverageArray;
    private int iArrayCount;
    private int ARRAYSIZE;


    void Start()
    {
        // Add eye-depth as a camera offset from the player controller
        var p = CameraRig.transform.localPosition;
        p.z = OVRManager.profile.eyeDepth;
        CameraRig.transform.localPosition = p;

        // < ROVR
        samples = new float[FREQUENCY];
        iStartTimeout = 25;             //when program starts wait until mic input transient noise subsides
        fHighWaterMark = 0.1f;				//loudest sound so far heard
        fAudioAmplitude = 0.0f;
        ARRAYSIZE = 3;

        fAverageArray = new float[ARRAYSIZE];
        for (iArrayCount = 0; iArrayCount < ARRAYSIZE; iArrayCount++)
            fAverageArray[iArrayCount] = 0;
        iArrayCount = 0;

        fHighWaterMark = PlayerPrefs.GetFloat("HighWaterMark");// Load HWM and prevent changes to value used
        bFixedHWM = false;
        if (fHighWaterMark > 0)
            bFixedHWM = true;

        StartMicListener();
        // ROVR />
    }

    void Awake()
    {
        Controller = gameObject.GetComponent<CharacterController>();

        if (Controller == null)
            Debug.LogWarning("OVRPlayerController: No CharacterController attached.");

        // We use OVRCameraRig to set rotations to cameras,
        // and to be influenced by rotation
        OVRCameraRig[] CameraRigs = gameObject.GetComponentsInChildren<OVRCameraRig>();

        if (CameraRigs.Length == 0)
            Debug.LogWarning("OVRPlayerController: No OVRCameraRig attached.");
        else if (CameraRigs.Length > 1)
            Debug.LogWarning("OVRPlayerController: More then 1 OVRCameraRig attached.");
        else
            CameraRig = CameraRigs[0];

        InitialYRotation = transform.rotation.eulerAngles.y;
    }

    void OnEnable()
    {
        OVRManager.display.RecenteredPose += ResetOrientation;

        if (CameraRig != null)
        {
            CameraRig.UpdatedAnchors += UpdateTransform;
        }
    }

    void OnDisable()
    {
        OVRManager.display.RecenteredPose -= ResetOrientation;

        if (CameraRig != null)
        {
            CameraRig.UpdatedAnchors -= UpdateTransform;
        }
    }

    void FixedUpdate()
    {
        //Use keys to ratchet rotation
        if (Input.GetKeyDown(KeyCode.Q))
            buttonRotation -= RotationRatchet;

        if (Input.GetKeyDown(KeyCode.E))
            buttonRotation += RotationRatchet;

        // < ROVR
        //added
        // If the audio has stopped playing, this will restart the mic play the clip.
        if (!GetComponent<AudioSource>().isPlaying)
        {
            StartMicListener();
        }

        // Gets microphone value
        AnalyzeSound();

        if (iStartTimeout > 0)
            iStartTimeout = iStartTimeout - 1;
        // ROVR />
    }

    protected virtual void UpdateController()
    {
        if (useProfileData)
        {
            if (InitialPose == null)
            {
                // Save the initial pose so it can be recovered if useProfileData
                // is turned off later.
                InitialPose = new OVRPose()
                {
                    position = CameraRig.transform.localPosition,
                    orientation = CameraRig.transform.localRotation
                };
            }

            var p = CameraRig.transform.localPosition;
            if (OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.EyeLevel)
            {
                p.y = OVRManager.profile.eyeHeight - (0.5f * Controller.height) + Controller.center.y;
            }
            else if (OVRManager.instance.trackingOriginType == OVRManager.TrackingOrigin.FloorLevel)
            {
                p.y = -(0.5f * Controller.height) + Controller.center.y;
            }
            CameraRig.transform.localPosition = p;
        }
        else if (InitialPose != null)
        {
            // Return to the initial pose if useProfileData was turned off at runtime
            CameraRig.transform.localPosition = InitialPose.Value.position;
            CameraRig.transform.localRotation = InitialPose.Value.orientation;
            InitialPose = null;
        }

        UpdateMovement();

        Vector3 moveDirection = Vector3.zero;

        float motorDamp = (1.0f + (Damping * SimulationRate * Time.deltaTime));

        MoveThrottle.x /= motorDamp;
        MoveThrottle.y = (MoveThrottle.y > 0.0f) ? (MoveThrottle.y / motorDamp) : MoveThrottle.y;
        MoveThrottle.z /= motorDamp;

        moveDirection += MoveThrottle * SimulationRate * Time.deltaTime;

        // Gravity
        if (Controller.isGrounded && FallSpeed <= 0)
            FallSpeed = ((Physics.gravity.y * (GravityModifier * 0.002f)));
        else
            FallSpeed += ((Physics.gravity.y * (GravityModifier * 0.002f)) * SimulationRate * Time.deltaTime);

        moveDirection.y += FallSpeed * SimulationRate * Time.deltaTime;

        // Offset correction for uneven ground
        float bumpUpOffset = 0.0f;

        if (Controller.isGrounded && MoveThrottle.y <= transform.lossyScale.y * 0.001f)
        {
            bumpUpOffset = Mathf.Max(Controller.stepOffset, new Vector3(moveDirection.x, 0, moveDirection.z).magnitude);
            moveDirection -= bumpUpOffset * Vector3.up;
        }

        Vector3 predictedXZ = Vector3.Scale((Controller.transform.localPosition + moveDirection), new Vector3(1, 0, 1));

        // Move contoller
        Controller.Move(moveDirection);

        Vector3 actualXZ = Vector3.Scale(Controller.transform.localPosition, new Vector3(1, 0, 1));

        if (predictedXZ != actualXZ)
            MoveThrottle += (actualXZ - predictedXZ) / (SimulationRate * Time.deltaTime);
    }

    public virtual void UpdateMovement()
    {
        if (HaltUpdateMovement)
            return;

        bool moveForward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        bool moveBack = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

        bool dpad_move = false;

        // < ROVR		
        //**** get the speed, based on mic input, from MicInput.cs - added
        //		float MicSpeed = GameObject.Find("OVRPlayerController").GetComponent<MicInput>().fAudioAmplitude;
        float MicSpeed = fAudioAmplitude;
        MicSpeed *= 30;
        Debug.Log(MicSpeed);
        if (MicSpeed > .1)
        {
            Debug.Log("Moving");
            moveForward = true;
        }
        else
        {
            moveForward = false;
            MicSpeed = 0;
        }
        // ROVR />


        if (OVRInput.Get(OVRInput.Button.DpadUp))
        {
            moveForward = true;
            dpad_move = true;

        }

        if (OVRInput.Get(OVRInput.Button.DpadDown))
        {
            moveBack = true;
            dpad_move = true;
        }

        //		MoveScale = 1.0f;

        if ((moveForward && moveLeft) || (moveForward && moveRight) ||
             (moveBack && moveLeft) || (moveBack && moveRight))
            MoveScale = 0.70710678f;

        // No positional movement if we are in the air
        if (!Controller.isGrounded)
            MoveScale = 0.0f;

        //		MoveScale *= SimulationRate * Time.deltaTime;
        // **** changed the standard code here to use amplitude value from MicInput.cs. change the x2 scaling if required - added
        MoveScale = MicSpeed * 4f;//   // *= SimulationRate * Time.deltaTime; 

        // Compute this for key movement
        float moveInfluence = Acceleration * 0.1f * MoveScale * MoveScaleMultiplier;

        // Run!
        if (dpad_move || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            moveInfluence *= 2.0f;

        Quaternion ort = transform.rotation;
        Vector3 ortEuler = ort.eulerAngles;
        ortEuler.z = ortEuler.x = 0f;
        ort = Quaternion.Euler(ortEuler);

        if (moveForward)
            MoveThrottle += ort * (transform.lossyScale.z * moveInfluence * Vector3.forward);
        if (moveBack)
            MoveThrottle += ort * (transform.lossyScale.z * moveInfluence * BackAndSideDampen * Vector3.back);
        if (moveLeft)
            MoveThrottle += ort * (transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.left);
        if (moveRight)
            MoveThrottle += ort * (transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.right);

        Vector3 euler = transform.rotation.eulerAngles;

        bool curHatLeft = OVRInput.Get(OVRInput.Button.PrimaryShoulder);

        if (curHatLeft && !prevHatLeft)
            euler.y -= RotationRatchet;

        prevHatLeft = curHatLeft;

        bool curHatRight = OVRInput.Get(OVRInput.Button.SecondaryShoulder);

        if (curHatRight && !prevHatRight)
            euler.y += RotationRatchet;

        prevHatRight = curHatRight;

        euler.y += buttonRotation;
        buttonRotation = 0f;

        float rotateInfluence = SimulationRate * Time.deltaTime * RotationAmount * RotationScaleMultiplier;

#if !UNITY_ANDROID || UNITY_EDITOR
        if (!SkipMouseRotation)
            euler.y += Input.GetAxis("Mouse X") * rotateInfluence * 3.25f;
#endif

        moveInfluence = Acceleration * 0.1f * MoveScale * MoveScaleMultiplier;

#if !UNITY_ANDROID // LeftTrigger not avail on Android game pad
        moveInfluence *= 1.0f + OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger);
#endif

        Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);

        if (primaryAxis.y > 0.0f)
            MoveThrottle += ort * (primaryAxis.y * transform.lossyScale.z * moveInfluence * Vector3.forward);

        if (primaryAxis.y < 0.0f)
            MoveThrottle += ort * (Mathf.Abs(primaryAxis.y) * transform.lossyScale.z * moveInfluence * BackAndSideDampen * Vector3.back);

        if (primaryAxis.x < 0.0f)
            MoveThrottle += ort * (Mathf.Abs(primaryAxis.x) * transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.left);

        if (primaryAxis.x > 0.0f)
            MoveThrottle += ort * (primaryAxis.x * transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.right);

        Vector2 secondaryAxis = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        euler.y += secondaryAxis.x * rotateInfluence;

        transform.rotation = Quaternion.Euler(euler);
    }

    /// <summary>
    /// Invoked by OVRCameraRig's UpdatedAnchors callback. Allows the Hmd rotation to update the facing direction of the player.
    /// </summary>
    public void UpdateTransform(OVRCameraRig rig)
    {
        Transform root = CameraRig.trackingSpace;
        Transform centerEye = CameraRig.centerEyeAnchor;

        if (HmdRotatesY)
        {
            Vector3 prevPos = root.position;
            Quaternion prevRot = root.rotation;

            transform.rotation = Quaternion.Euler(0.0f, centerEye.rotation.eulerAngles.y, 0.0f);

            root.position = prevPos;
            root.rotation = prevRot;
        }

        UpdateController();
    }

    /// <summary>
    /// Jump! Must be enabled manually.
    /// </summary>
    public bool Jump()
    {
        if (!Controller.isGrounded)
            return false;

        MoveThrottle += new Vector3(0, transform.lossyScale.y * JumpForce, 0);

        return true;
    }

    /// <summary>
    /// Stop this instance.
    /// </summary>
    public void Stop()
    {
        Controller.Move(Vector3.zero);
        MoveThrottle = Vector3.zero;
        FallSpeed = 0.0f;
    }

    /// <summary>
    /// Gets the move scale multiplier.
    /// </summary>
    /// <param name="moveScaleMultiplier">Move scale multiplier.</param>
    public void GetMoveScaleMultiplier(ref float moveScaleMultiplier)
    {
        moveScaleMultiplier = MoveScaleMultiplier;
    }

    /// <summary>
    /// Sets the move scale multiplier.
    /// </summary>
    /// <param name="moveScaleMultiplier">Move scale multiplier.</param>
    public void SetMoveScaleMultiplier(float moveScaleMultiplier)
    {
        MoveScaleMultiplier = moveScaleMultiplier;
    }

    /// <summary>
    /// Gets the rotation scale multiplier.
    /// </summary>
    /// <param name="rotationScaleMultiplier">Rotation scale multiplier.</param>
    public void GetRotationScaleMultiplier(ref float rotationScaleMultiplier)
    {
        rotationScaleMultiplier = RotationScaleMultiplier;
    }

    /// <summary>
    /// Sets the rotation scale multiplier.
    /// </summary>
    /// <param name="rotationScaleMultiplier">Rotation scale multiplier.</param>
    public void SetRotationScaleMultiplier(float rotationScaleMultiplier)
    {
        RotationScaleMultiplier = rotationScaleMultiplier;
    }

    /// <summary>
    /// Gets the allow mouse rotation.
    /// </summary>
    /// <param name="skipMouseRotation">Allow mouse rotation.</param>
    public void GetSkipMouseRotation(ref bool skipMouseRotation)
    {
        skipMouseRotation = SkipMouseRotation;
    }

    /// <summary>
    /// Sets the allow mouse rotation.
    /// </summary>
    /// <param name="skipMouseRotation">If set to <c>true</c> allow mouse rotation.</param>
    public void SetSkipMouseRotation(bool skipMouseRotation)
    {
        SkipMouseRotation = skipMouseRotation;
    }

    /// <summary>
    /// Gets the halt update movement.
    /// </summary>
    /// <param name="haltUpdateMovement">Halt update movement.</param>
    public void GetHaltUpdateMovement(ref bool haltUpdateMovement)
    {
        haltUpdateMovement = HaltUpdateMovement;
    }

    /// <summary>
    /// Sets the halt update movement.
    /// </summary>
    /// <param name="haltUpdateMovement">If set to <c>true</c> halt update movement.</param>
    public void SetHaltUpdateMovement(bool haltUpdateMovement)
    {
        HaltUpdateMovement = haltUpdateMovement;
    }

    /// <summary>
    /// Resets the player look rotation when the device orientation is reset.
    /// </summary>
    public void ResetOrientation()
    {
        if (HmdResetsY)
        {
            Vector3 euler = transform.rotation.eulerAngles;
            euler.y = InitialYRotation;
            transform.rotation = Quaternion.Euler(euler);
        }
    }



    //ROVR
    /// Starts the Mic, and plays the audio back in (near) real-time.
    private void StartMicListener()
    {
        if (Microphone.devices.Length > 0) // check if we have any microphone devices available
        {
            //   this.gameObject.AddComponent<AudioSource>(); // creates this.audio
            GetComponent<AudioSource>().clip = Microphone.Start(null, true, 1, FREQUENCY);
            GetComponent<AudioSource>().loop = true; // Set the AudioClip to loop
                                                     // GetComponent<AudioSource>().se
                                                     //  GetComponent<AudioSource>().mute = false; // Mute the sound, we don’t want the player to hear it
                                                     // HACK - Forces the function to wait until the microphone has started, before moving onto the play function.

            while (!(Microphone.GetPosition(null) > 0)) { }                                                         //THIS LINE LOCKS IT UP (might only be in v5.5)
            GetComponent<AudioSource>().Play();
            Debug.Log("mic ready");
        }
        /*		if (Microphone.devices.Length > 0) // check if we have any microphone devices available
                {
                    this.gameObject.AddComponent<AudioSource>(); // creates this.audio
                    GetComponent<AudioSource>().clip = Microphone.Start( Microphone.devices[0] , true, 1, FREQUENCY);
                    GetComponent<AudioSource>().loop = true; // Set the AudioClip to loop
                    GetComponent<AudioSource>().mute = true; // Mute the sound, we don’t want the player to hear it
                    // HACK - Forces the function to wait until the microphone has started, before moving onto the play function.
                    while (!(Microphone.GetPosition(Microphone.devices[0] ) > 0)) {}
                    GetComponent<AudioSource>().Play();
                }
        */
    }

    /// Credits to aldonaletto for the function, http://goo.gl/VGwKt
    /// Analyzes the sound, to get volume and pitch values.
    private void AnalyzeSound()
    {


        // Get all of our samples from the mic.
        samples = GetComponent<AudioSource>().GetOutputData(2000, 0);
        float sum = 0;

        if (iStartTimeout < 1)
        {
            for (int i = 0; i < 2000; i++)
            {
                if (samples[i] < 0)
                    sum -= samples[i];
                else
                    sum += samples[i];
            }
            fAverage = sum / 2000;

        }

        fAverageArray[iArrayCount] = fAverage;
        iArrayCount++;
        if (iArrayCount >= ARRAYSIZE)
            iArrayCount = 0;

        sum = 0;
        for (int i = 0; i < ARRAYSIZE; i++)
            sum = sum + fAverageArray[i];

        float fArrayAverage = sum / ARRAYSIZE; ;
        
        //this is the current mic level that is accessed by OVRPlayerController and used to determine forward speed
        fAudioAmplitude = fArrayAverage;// / fHighWaterMark;	//fAudioAmplitude is the current sound level as a proportion of the maximum so far seen, which is represented by 1.0f

        //		amplitudeTextVar.text = fAudioAmplitude.ToString();


        //clear whole buffer - avoids having to maintain pointers to where the latest sample starts because we throw it away after calculating the power anyway
        for (int i = 0; i < 2000; i++)
        {
            samples[i] = 0;
        }
    }

}