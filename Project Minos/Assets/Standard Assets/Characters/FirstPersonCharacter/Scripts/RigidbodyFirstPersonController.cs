using System;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;



namespace UnityStandardAssets.Characters.FirstPerson
{
    [RequireComponent(typeof (Rigidbody))]
    [RequireComponent(typeof (CapsuleCollider))]
    public class RigidbodyFirstPersonController : MonoBehaviour
    {
        [Serializable]
        public class MovementSettings
        {
            public float ForwardSpeed = 8.0f;   // Speed when walking forward
            public float BackwardSpeed = 4.0f;  // Speed when walking backwards
            public float StrafeSpeed = 4.0f;    // Speed when walking sideways
            public float RunMultiplier = 2.0f;   // Speed when sprinting
	        public KeyCode RunKey = KeyCode.LeftShift;
            public float JumpForce = 30f;
            public AnimationCurve SlopeCurveModifier = new AnimationCurve(new Keyframe(-90.0f, 1.0f), new Keyframe(0.0f, 1.0f), new Keyframe(90.0f, 0.0f));
            [HideInInspector] public float CurrentTargetSpeed = 8f;


#if !MOBILE_INPUT
            private bool m_Running;
#endif

            public void UpdateDesiredTargetSpeed(Vector2 input)
            {
	            if (input == Vector2.zero) return;
				if (input.x > 0 || input.x < 0)
				{
					//strafe
					CurrentTargetSpeed = StrafeSpeed;
				}
				if (input.y < 0)
				{
					//backwards
					CurrentTargetSpeed = BackwardSpeed;
				}
				if (input.y > 0)
				{
					//forwards
					//handled last as if strafing and moving forward at the same time forwards speed should take precedence
					CurrentTargetSpeed = ForwardSpeed;
					//Debug.Log (CurrentTargetSpeed);
				}
#if !MOBILE_INPUT
	            if (Input.GetKey(RunKey))
	            {
		            CurrentTargetSpeed *= RunMultiplier;
		            m_Running = true;
	            }
	            else
	            {
		            m_Running = false;
	            }
#endif
            }

#if !MOBILE_INPUT
            public bool Running
            {
                get { return m_Running; }
            }
#endif
        }


        [Serializable]
        public class AdvancedSettings
        {
            public float groundCheckDistance = 0.01f; // distance for checking if the controller is grounded ( 0.01f seems to work best for this )
            public float stickToGroundHelperDistance = 0.5f; // stops the character
            public float slowDownRate = 20f; // rate at which the controller comes to a stop when there is no input
            public bool airControl; // can the user control the direction that is being moved in the air
            [Tooltip("set it to 0.1 or more if you get stuck in wall")]
            public float shellOffset; //reduce the radius by that ratio to avoid getting stuck in wall (a value of 0.1f is nice)
        }


        public Camera cam;
        public MovementSettings movementSettings = new MovementSettings();
        public MouseLook mouseLook = new MouseLook();
        public AdvancedSettings advancedSettings = new AdvancedSettings();


        private Rigidbody m_RigidBody;
        private CapsuleCollider m_Capsule;
        private float m_YRotation;
        private Vector3 m_GroundContactNormal;
        private bool m_Jump, m_PreviouslyGrounded, m_Jumping, m_IsGrounded;


		private const int FREQUENCY = 2000;
		private float[] samples;           // Samples
		public int iStartTimeout;
		private float fAverage = 0.0f;
		private float fHighWaterMark = 0.0f;
		public float fMinInputThreshold = 0.1f;
		private float fAudioAmplitude = 0.0f;
		private bool bFixedHWM;
		private float[] fAverageArray;
		private int iArrayCount;
		private int ARRAYSIZE;
		public float Speed_Mulitplier = 2.0f;

        public Vector3 Velocity
        {
            get { return m_RigidBody.velocity; }
        }

        public bool Grounded
        {
            get { return m_IsGrounded; }
        }

        public bool Jumping
        {
            get { return m_Jumping; }
        }

        public bool Running
        {
            get
            {
 #if !MOBILE_INPUT
				return movementSettings.Running;
#else
	            return false;
#endif
            }
        }


        private void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            m_Capsule = GetComponent<CapsuleCollider>();
            mouseLook.Init (transform, cam.transform);
 
			//added
			samples = new float[FREQUENCY];
			iStartTimeout = 100;				//when program starts wait until mic input transient noise subsides
			fHighWaterMark = 0.1f;				//loudest sound so far heard
			fAudioAmplitude = 0.0f;
			ARRAYSIZE = 3;

			fAverageArray = new float[ARRAYSIZE];
			for (iArrayCount = 0; iArrayCount < ARRAYSIZE; iArrayCount++)
				fAverageArray[iArrayCount] = 0;
			iArrayCount = 0;

//			fHighWaterMark = PlayerPrefs.GetFloat("HighWaterMark");// Load HWM and prevent changes to value used
//			bFixedHWM = false;
//			if(fHighWaterMark > 0)
//				bFixedHWM = true;

			StartMicListener();
			}


        private void Update()
        {
            RotateView();

            if (CrossPlatformInputManager.GetButtonDown("Jump") && !m_Jump)
            {
                m_Jump = true;
            }
        }

		 
        private void FixedUpdate()
        {
            GroundCheck();
            Vector2 input = GetInput();

            if ((Mathf.Abs(input.x) > float.Epsilon || Mathf.Abs(input.y) > float.Epsilon) && (advancedSettings.airControl || m_IsGrounded))
            {
                // always move along the camera forward as it is the direction that it being aimed at
                Vector3 desiredMove = cam.transform.forward*input.y + cam.transform.right*input.x;
				desiredMove = Vector3.ProjectOnPlane(desiredMove, m_GroundContactNormal).normalized;

				desiredMove.x = desiredMove.x*movementSettings.CurrentTargetSpeed;
                desiredMove.z = desiredMove.z*movementSettings.CurrentTargetSpeed;
                desiredMove.y = desiredMove.y*movementSettings.CurrentTargetSpeed;
                if (m_RigidBody.velocity.sqrMagnitude <
                    (movementSettings.CurrentTargetSpeed*movementSettings.CurrentTargetSpeed))
                {
					//														ROVR added
					m_RigidBody.AddForce((desiredMove*SlopeMultiplier()) * fAudioAmplitude * Speed_Mulitplier, ForceMode.Impulse);
                }
            }

            if (m_IsGrounded)
            {
                m_RigidBody.drag = 5f;

                if (m_Jump)
                {
                    m_RigidBody.drag = 0f;
                    m_RigidBody.velocity = new Vector3(m_RigidBody.velocity.x, 0f, m_RigidBody.velocity.z);
                    m_RigidBody.AddForce(new Vector3(0f, movementSettings.JumpForce, 0f), ForceMode.Impulse);
                    m_Jumping = true;
                }

                if (!m_Jumping && Mathf.Abs(input.x) < float.Epsilon && Mathf.Abs(input.y) < float.Epsilon && m_RigidBody.velocity.magnitude < 1f)
                {
                    m_RigidBody.Sleep();
                }
            }
            else
            {
                m_RigidBody.drag = 0f;
                if (m_PreviouslyGrounded && !m_Jumping)
                {
                    StickToGroundHelper();
                }
            }
            m_Jump = false;


			//added
			// If the audio has stopped playing, this will restart the mic play the clip.
			if (!GetComponent<AudioSource>().isPlaying) {
				StartMicListener();
			}
			//put fist debug here to check if the loop finishes later

			// Gets volume value
			AnalyzeSound();

			if(iStartTimeout > 0)
				iStartTimeout = iStartTimeout - 1;


			//		var x = Input.GetAxis("Horizontal") * Time.deltaTime * 150.0f;
//			var z = Input.GetAxis("Vertical") * Time.deltaTime * 3.0f;

			//		transform.Rotate(0, x, 0);
			//transform.Translate(0, 0, 0.01f);
//			transform.Translate(0, 0, fAudioAmplitude);

        }
		 

        private float SlopeMultiplier()
        {
            float angle = Vector3.Angle(m_GroundContactNormal, Vector3.up);
            return movementSettings.SlopeCurveModifier.Evaluate(angle);
        }


        private void StickToGroundHelper()
        {
            RaycastHit hitInfo;
            if (Physics.SphereCast(transform.position, m_Capsule.radius * (1.0f - advancedSettings.shellOffset), Vector3.down, out hitInfo,
                                   ((m_Capsule.height/2f) - m_Capsule.radius) +
                                   advancedSettings.stickToGroundHelperDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (Mathf.Abs(Vector3.Angle(hitInfo.normal, Vector3.up)) < 85f)
                {
                    m_RigidBody.velocity = Vector3.ProjectOnPlane(m_RigidBody.velocity, hitInfo.normal);
                }
            }
        }


        private Vector2 GetInput()
        {
            
            Vector2 input = new Vector2
                {
                    x = CrossPlatformInputManager.GetAxis("Horizontal"),
					//ROVR added
                    y = 1
					//y = CrossPlatformInputManager.GetAxis("Vertical")
                };
			movementSettings.UpdateDesiredTargetSpeed(input);
			//Debug.Log (input.x);
            return input;
        }


        private void RotateView()
        {
            //avoids the mouse looking if the game is effectively paused
            if (Mathf.Abs(Time.timeScale) < float.Epsilon) return;

            // get the rotation before it's changed
            float oldYRotation = transform.eulerAngles.y;

            mouseLook.LookRotation (transform, cam.transform);

            if (m_IsGrounded || advancedSettings.airControl)
            {
                // Rotate the rigidbody velocity to match the new direction that the character is looking
                Quaternion velRotation = Quaternion.AngleAxis(transform.eulerAngles.y - oldYRotation, Vector3.up);
                m_RigidBody.velocity = velRotation*m_RigidBody.velocity;
            }
        }

        /// sphere cast down just beyond the bottom of the capsule to see if the capsule is colliding round the bottom
        private void GroundCheck()
        {
            m_PreviouslyGrounded = m_IsGrounded;
            RaycastHit hitInfo;
            if (Physics.SphereCast(transform.position, m_Capsule.radius * (1.0f - advancedSettings.shellOffset), Vector3.down, out hitInfo,
                                   ((m_Capsule.height/2f) - m_Capsule.radius) + advancedSettings.groundCheckDistance, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                m_IsGrounded = true;
                m_GroundContactNormal = hitInfo.normal;
            }
            else
            {
                m_IsGrounded = false;
                m_GroundContactNormal = Vector3.up;
            }
            if (!m_PreviouslyGrounded && m_IsGrounded && m_Jumping)
            {
                m_Jumping = false;
            }
        }
    


	//added
	/// Starts the Mic, and plays the audio back in (near) real-time.

	private void StartMicListener()
	{
		if (Microphone.devices.Length > 0) // check if we have any microphone devices available
		{
			//   this.gameObject.AddComponent<AudioSource>(); // creates this.audio
			GetComponent<AudioSource>().clip = Microphone.Start(null , true, 1, FREQUENCY);
			GetComponent<AudioSource>().loop = true; // Set the AudioClip to loop
			// GetComponent<AudioSource>().se
			//  GetComponent<AudioSource>().mute = false; // Mute the sound, we don’t want the player to hear it
			// HACK - Forces the function to wait until the microphone has started, before moving onto the play function.

			while (!(Microphone.GetPosition(null ) > 0)) {}															//THIS LINE LOCKS IT UP (might only be in v5.5)
			GetComponent<AudioSource>().Play();
			Debug.Log("mic ready");
		}
	}

	/// Credits to aldonaletto for the function, http://goo.gl/VGwKt
	/// Analyzes the sound, to get volume and pitch values.
	private void AnalyzeSound() {

		// Get all of our samples from the mic.
		samples = GetComponent<AudioSource>().GetOutputData(2000, 0);
		//Unity say GetOutputData IS NOW OBSOLETE 
		float sum = 0;

		for (int i = 0; i < 2000; i++)
		{

			if (samples[i] < 0)
				sum -= samples[i];
			else
				sum += samples[i];
		}
		fAverage = sum / 2000;

		fAverageArray[iArrayCount] = fAverage;
		iArrayCount++;
		if (iArrayCount >= ARRAYSIZE)
			iArrayCount = 0;

		sum = 0;
		for (int i = 0; i < ARRAYSIZE; i++)
			sum = sum + fAverageArray[i];

		float fArrayAverage = sum / ARRAYSIZE;

		//compare most recent audio sample to loudest so far heard
//		if ((fArrayAverage > fHighWaterMark) && (bFixedHWM == false))
//		{
//			fHighWaterMark = fArrayAverage; //if louder set new high water mark
//		}


		if (iStartTimeout > 1) return;


		//this is the current mic level that is accessed by OVRPlayerController and used to determine forward speed

		fAudioAmplitude = fArrayAverage;//(fArrayAverage / fHighWaterMark);   //fAudioAmplitude is the current sound level as a proportion of the maximum so far seen, which is represented by 1.0f
		if(fAudioAmplitude < fMinInputThreshold)
		{
			fAudioAmplitude = 0;
		}

/*
		//a few optional tweaks while the game runs
		if(Input.GetKeyDown(KeyCode.R))
		{
			fHighWaterMark = 0;//Recalibrate - i.e.reset high water mark to zero. Now run fast for short period to set maximum speed to be compared against
			bFixedHWM = false;
		}
		if(Input.GetKeyDown(KeyCode.PageUp))
		{
			fHighWaterMark = fHighWaterMark * 0.8f;//decrease high water mark adds sensitivity - go faster for same input
		}
		if(Input.GetKeyDown(KeyCode.PageDown))
		{
			fHighWaterMark = fHighWaterMark * 1.25f;//increase high water mark decreases sensitivity - go slower for same input
		}
		if(Input.GetKeyDown(KeyCode.S))
		{
			PlayerPrefs.SetFloat("HighWaterMark", fHighWaterMark);// Save HWM and prevent changes to value used
			bFixedHWM = true;
		}
		if(Input.GetKeyDown(KeyCode.L))
		{
			fHighWaterMark = PlayerPrefs.GetFloat("HighWaterMark");// Load HWM and prevent changes to value used
			bFixedHWM = true;
		}
*/
		//clear whole buffer - avoids having to maintain pointers to where the latest sample starts because we throw it away after calculating the power anyway
		for (int i = 0; i < 2000; i++) {
			samples [i] = 0;
		}
	}
	}

}
