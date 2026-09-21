using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Keeps one prop sitting at the spawn point. When the player picks it up, carries it away, or it is
/// destroyed (deleted, or a ball lands in a cup), a fresh copy appears after a short delay.
/// Use one PropSpawner per slot (one for balls, one for cups, one per pot, etc.).
///
/// Unlike the old BallSpawner this tracks the exact instance it spawned instead of using a tag and
/// trigger callbacks, so it works for any prefab and still notices when the prop is destroyed
/// (OnTriggerExit is never called for a destroyed object, which left the old spawner stuck).
///
/// Spawned props start at the prefab's own scale, so a resized prop never carries its size into the respawn.
/// If the spawn point is a child of a moving part (e.g. the kitchen drawer), the slot moves with it.
/// </summary>
public class PropSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject propPrefab;
    [Tooltip("Where the prop appears. Place it slightly above the shelf surface. Falls back to this object.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [Tooltip("Seconds the slot must be empty before a new prop appears.")]
    [SerializeField] private float respawnDelay = 1.5f;
    [Tooltip("Radius (m) around the spawn point that counts as 'still in the slot'.")]
    [SerializeField] private float slotRadius = 0.15f;

    [Header("Tutorial gate (optional)")]
    [Tooltip("Don't spawn anything until BallSpawnGate is unlocked.")]
    [SerializeField] private bool requireTutorialGate = false;
    [Tooltip("Grabbing the prop from this spawner calls BallSpawnGate.Unlock(). Tick on the ball container's spawner.")]
    [SerializeField] private bool unlockGateWhenGrabbed = false;

    private GameObject current;
    private XRGrabInteractable currentGrab;
    private float emptySince;
    private readonly Collider[] overlapBuffer = new Collider[16];

    private Transform Point => spawnPoint != null ? spawnPoint : transform;

    private void Start()
    {
        // Makes the first spawn happen on the first Update (subject to the tutorial gate).
        emptySince = Time.time - respawnDelay;
    }

    private void Update()
    {
        if (IsSlotOccupied())
        {
            emptySince = -1f;
            return;
        }

        if (emptySince < 0f) emptySince = Time.time;
        if (Time.time - emptySince < respawnDelay) return;
        if (requireTutorialGate && !BallSpawnGate.IsUnlocked) return;
        if (IsBlockedByOtherProp()) return;

        Spawn();
    }

    // Occupied = our prop still exists, isn't in a hand, and is within the slot radius.
    private bool IsSlotOccupied()
    {
        if (current == null) return false;   // Unity null check: also true if destroyed
        if (currentGrab != null && currentGrab.isSelected) return false;

        return (current.transform.position - Point.position).sqrMagnitude <= slotRadius * slotRadius;
    }

    // Something else physical (a dropped prop, a stray ball) is sitting where we would spawn.
    private bool IsBlockedByOtherProp()
    {
        int count = Physics.OverlapSphereNonAlloc(Point.position, slotRadius * 0.5f, overlapBuffer,
                                                  ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Rigidbody body = overlapBuffer[i].attachedRigidbody;
            if (body != null && !body.isKinematic) return true;
        }
        return false;
    }

    private void Spawn()
    {
        current = Instantiate(propPrefab, Point.position, Point.rotation);
        currentGrab = current.GetComponentInChildren<XRGrabInteractable>();
        emptySince = -1f;

        if (unlockGateWhenGrabbed && currentGrab != null)
            currentGrab.selectEntered.AddListener(_ => BallSpawnGate.Unlock());
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Point.position, slotRadius);
    }
}
