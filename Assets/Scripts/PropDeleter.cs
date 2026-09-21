using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Put one on each controller. While B is held, a laser shows where the controller points, and any
/// prop it touches is permanently deleted. A prop is anything with an XR Grab Interactable.
/// </summary>
public class PropDeleter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference deleteAction;   // B = Secondary Button
    [Tooltip("Where the ray starts and which way it points (its forward/blue axis). Defaults to this object.")]
    [SerializeField] private Transform rayOrigin;
    [Tooltip("Optional. Shown while B is held.")]
    [SerializeField] private LineRenderer laser;

    [Header("Settings")]
    [SerializeField] private float maxDistance = 15f;
    [Tooltip("Thickness of the aim ray. A little wider than a ping pong ball makes small balls easier to hit.")]
    [SerializeField] private float aimRadius = 0.04f;
    [SerializeField] private LayerMask hitMask = ~0;
    [Tooltip("On: holding B deletes everything you sweep across. Off: one delete per press.")]
    [SerializeField] private bool deleteWhileHeld = true;
    [Tooltip("Minimum time between deletes while B is held.")]
    [SerializeField] private float repeatDelay = 0.15f;

    private float nextDeleteTime;

    private void OnEnable()
    {
        if (deleteAction != null && deleteAction.action != null)
            deleteAction.action.Enable();
        SetLaserVisible(false);
    }

    private void Update()
    {
        if (deleteAction == null || deleteAction.action == null) return;

        bool held = deleteAction.action.IsPressed();
        if (!held)
        {
            SetLaserVisible(false);
            return;
        }

        Transform origin = rayOrigin != null ? rayOrigin : transform;
        Vector3 start = origin.position;
        Vector3 end = start + origin.forward * maxDistance;
        XRGrabInteractable target = null;

        if (Physics.SphereCast(start, aimRadius, origin.forward, out RaycastHit hit, maxDistance,
                               hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            target = hit.collider.GetComponentInParent<XRGrabInteractable>();
        }

        UpdateLaser(start, end);

        bool allowed = deleteWhileHeld || deleteAction.action.WasPressedThisFrame();
        if (target != null && allowed && Time.time >= nextDeleteTime)
        {
            Destroy(target.gameObject);
            nextDeleteTime = Time.time + repeatDelay;
        }
    }

    private void UpdateLaser(Vector3 start, Vector3 end)
    {
        if (laser == null) return;
        laser.enabled = true;
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.SetPosition(0, start);
        laser.SetPosition(1, end);
    }

    private void SetLaserVisible(bool visible)
    {
        if (laser != null) laser.enabled = visible;
    }
}
