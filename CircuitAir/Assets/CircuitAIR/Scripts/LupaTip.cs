using UnityEngine;

/// <summary>
/// Physical collision proxy for the tip of the magnifying glass.
/// The AR image marker should be attached near the real tip, or this object should be offset
/// inside the LupaMarker prefab so the sphere lines up with the real tip.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(Rigidbody))]
public class LupaTip : MonoBehaviour
{
    [SerializeField, Min(0.001f)] private float radiusMeters = 0.008f;

    private void Awake()
    {
        ConfigurePhysics();
    }

    private void OnValidate()
    {
        ConfigurePhysics();
    }

    private void ConfigurePhysics()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.isTrigger = true;
            sphere.radius = Mathf.Max(0.001f, radiusMeters);
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
    }
}
