using UnityEngine;

public class IceGunProjectile : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        MarchingCubes marchingCubes = other.GetComponentInParent<MarchingCubes>();

        if (marchingCubes != null)
        {
            Vector3 hitPoint = (transform.position);

            marchingCubes.SetDensityAtPos(hitPoint, 1.0f);

            Destroy(gameObject, 0.0f); // destroy the game object after it has been applied. 
        }
    }
}
