using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KatanaCutter : MonoBehaviour
{
    //[SerializeField] private float cutRadius = 0.5f;

    Collider cutCollider;

    private void Start()
    {
        cutCollider = GetComponent<Collider>();
    }

    private void OnTriggerStay(Collider other)
    {
        MarchingCubes marchingCubes = other.GetComponentInParent<MarchingCubes>();

        if (marchingCubes != null)
        {
            Vector3 hitPoint = (transform.position);

            _ = new WaitForSeconds(0.5f);
            StartCoroutine(Stall(hitPoint)); // Stall to prevent multiple cuts in the same frame
            
            marchingCubes.SetDensityAtPos(hitPoint, 0f); // Set density to 0 to create a hole
        } else
        {
            Debug.Log("Hit non-marching cubes object: " + other.name);
        }
    }

    private IEnumerator Stall(Vector3 hitPoint)
    {
        Debug.Log("Cutting at: " + hitPoint);
        yield return new WaitForSeconds(2f);
    }

    
}
