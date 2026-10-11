using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Safety net under the house (T01). Put on a large trigger BoxCollider well below the ground floor.
/// - If the player rig falls into it, the rig is moved back to the respawn point (or where it started).
/// - Any prop (anything with an XR Grab Interactable) or ping pong ball that falls into it is destroyed,
///   so PropSpawners refill their slots instead of props piling up out of sight.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    [Tooltip("Where the player is put back. Leave empty to use the XR Origin's position when the scene started.")]
    [SerializeField] private Transform respawnPoint;

    private XROrigin origin;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        origin = FindFirstObjectByType<XROrigin>();
        if (origin != null)
        {
            startPosition = origin.transform.position;
            startRotation = origin.transform.rotation;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        XROrigin rig = other.GetComponentInParent<XROrigin>();
        if (rig != null)
        {
            Respawn(rig);
            return;
        }

        XRGrabInteractable prop = other.GetComponentInParent<XRGrabInteractable>();
        if (prop != null)
        {
            Destroy(prop.gameObject);
            return;
        }

        GameObject body = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (body.CompareTag("PingPongBall"))
            Destroy(body);
    }

    private void Respawn(XROrigin rig)
    {
        Vector3 position = respawnPoint != null ? respawnPoint.position : startPosition;
        Quaternion rotation = respawnPoint != null ? respawnPoint.rotation : startRotation;

        // A CharacterController overrides transform changes while enabled, so switch it off for the move.
        CharacterController controller = rig.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        rig.transform.SetPositionAndRotation(position, rotation);

        if (controller != null) controller.enabled = true;

        Debug.Log($"KillZone: player fell out of the house and was moved back to {position}.", this);
    }
}
