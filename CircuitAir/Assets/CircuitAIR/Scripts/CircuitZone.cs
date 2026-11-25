using UnityEngine;

public enum CircuitZoneType
{
    Wall,
    Finish
}

/// <summary>
/// Trigger volume for either a circuit wall (error) or the finish point.
/// Add this to an object with a Collider configured as Is Trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CircuitZone : MonoBehaviour
{
    public CircuitZoneType zoneType = CircuitZoneType.Wall;

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<LupaTip>() == null)
            return;

        CircuitGameManager gameManager = CircuitGameManager.Instance;
        if (gameManager == null || !gameManager.IsPlaying)
            return;

        if (zoneType == CircuitZoneType.Wall)
            gameManager.RegisterError();
        else if (zoneType == CircuitZoneType.Finish)
            gameManager.FinishChallenge();
    }
}
