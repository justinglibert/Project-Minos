using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Soul : MonoBehaviour
{

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Transform player = GameObject.Find("/OVRPlayerController").transform;
        if (LineOfSight(player))
        {
            GetComponent<Renderer>().enabled = false;
            transform.FindChild("Lightning").GetComponent<Renderer>().enabled = false;
        }
        else
        {
            GetComponent<Renderer>().enabled = true;
            transform.FindChild("Lightning").GetComponent<Renderer>().enabled = true;
        }
    }

    bool LineOfSight(Transform finalDetected)
    {
        //precompute our ray settings
        Vector3 start = transform.position;
        Vector3 direction = (finalDetected.transform.position - transform.position).normalized;
        float distance = 100;

        //draw the ray in the editor
        Debug.DrawRay(start, direction * distance);

        //do the ray test
        RaycastHit hit;
        if (Physics.Raycast(start, direction, out hit))
        {
            if (hit.collider.gameObject.tag == "Player")
            {
                finalDetected = null;
                return true;
            }
        }
        return false;
    }
}
