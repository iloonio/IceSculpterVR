using UnityEngine;

public class IceGunProjectile : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        MarchingCubes marchingCubes = other.GetComponentInParent<MarchingCubes>();

        if (marchingCubes != null)
        {
            marchingCubes.SetDensityAtPos(transform.position, 1.0f, 0.15f);
        }
    }

    public void OnTriggerStay(Collider other)
    {
        MarchingCubes marchingCubes = other.GetComponentInParent<MarchingCubes>();

        if (marchingCubes != null)
        {
            marchingCubes.SetDensityAtPos(transform.position, 1.0f, 0.15f);
            Destroy(gameObject, 0.0f);
        }
    }
}
