using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour{
    public string ID;
    public bool isKing;
    public Vector3 lastPosition;
    // Use this for initialization
    private void Start()
    {

    }
    //    GetComponent<Transform>().position = lastPosition;

    // Update is called once per frame


    // Update is called once per frame
    private void Update()
    {
        if (transform.position != lastPosition) {
            transform.LookAt(2 * transform.position - lastPosition);
            lastPosition = transform.position;
        }
    }

}
