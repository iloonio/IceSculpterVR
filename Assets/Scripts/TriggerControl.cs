using System;
using UnityEngine;

public class TriggerControl : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnTriggerEnter(Collider other)
    {
     Debug.Log("Trigger entered by: " + other.name + " at point: " + other.ClosestPoint(transform.position));  
    }

    void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger stayed by: " + other.name);  
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger exited by: " + other.name);  
    }


}
