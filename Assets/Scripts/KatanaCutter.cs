using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KatanaCutter : MonoBehaviour
{
    //[SerializeField] private float cutRadius = 0.5f;

    private void OnTriggerStay(Collider other)
    {
        if (other.TryGetComponent(out MarchingCubes marchingCubes))
        {
            StartCoroutine(Stall()); // Stall to prevent multiple cuts in the same frame
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Debug.Log("Cutting at: " + hitPoint);
            marchingCubes.SetDensityAtPos(hitPoint, 0.0f);
        } else
        {
            Debug.Log("Hit non-marching cubes object: " + other.name);
        }
    }

    private IEnumerator Stall()
    {
        yield return new WaitForSeconds(0.5f);
    }

    
}
