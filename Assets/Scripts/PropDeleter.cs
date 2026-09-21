using System.Text;
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

    [Header("Debug")]
    [Tooltip("Writes what is happening to the Console, and draws a red line in the Scene view while B is held.")]
    [SerializeField] private bool debugLogging = true;

    private float nextDeleteTime;
    private bool wasHeld;
    private string lastReport;

    private void OnEnable()
    {
        if (deleteAction != null && deleteAction.action != null)
            deleteAction.action.Enable();
        SetLaserVisible(false);

        if (debugLogging) DescribeSetup();
    }

    private void DescribeSetup()
    {
        if (deleteAction == null || deleteAction.action == null)
        {
            Debug.LogWarning($"{name}: PropDeleter has no Delete Action assigned.", this);
            return;
        }

        InputAction a = deleteAction.action;
        var sb = new StringBuilder();
        foreach (InputBinding b in a.bindings)
            sb.Append(b.effectivePath).Append("  ");

        Debug.Log($"{name}: PropDeleter using action '{a.actionMap?.name}/{a.name}' (type {a.type}), " +
                  $"enabled: {a.enabled}, bindings: {sb}", this);

        if (laser == null)
            Debug.LogWarning($"{name}: no Laser assigned, so nothing will be visible in the headset.", this);
        if (rayOrigin == null)
            Debug.Log($"{name}: no Ray Origin assigned, using this object's own forward direction.", this);
    }

    private void Update()
    {
        if (deleteAction == null || deleteAction.action == null) return;

        bool held = deleteAction.action.IsPressed();

        if (held != wasHeld)
        {
            wasHeld = held;
            lastReport = null;
            if (debugLogging) Debug.Log($"{name}: Delete button {(held ? "PRESSED" : "released")}.", this);
        }

        if (!held)
        {
            SetLaserVisible(false);
            return;
        }

        Transform origin = rayOrigin != null ? rayOrigin : transform;
        Vector3 start = origin.position;
        Vector3 end = start + origin.forward * maxDistance;
        XRGrabInteractable target = null;
        string report;

        if (Physics.SphereCast(start, aimRadius, origin.forward, out RaycastHit hit, maxDistance,
                               hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            target = hit.collider.GetComponentInParent<XRGrabInteractable>();
            report = target != null
                ? $"aiming at prop '{target.name}'"
                : $"aiming at '{hit.collider.name}' (not a prop, no XR Grab Interactable)";
        }
        else
        {
            report = "aiming at nothing";
        }

        if (debugLogging)
        {
            Debug.DrawRay(start, origin.forward * maxDistance, Color.red);
            if (report != lastReport)
            {
                lastReport = report;
                Debug.Log($"{name}: {report}.", this);
            }
        }

        UpdateLaser(start, end);

        bool allowed = deleteWhileHeld || deleteAction.action.WasPressedThisFrame();
        if (target != null && allowed && Time.time >= nextDeleteTime)
        {
            if (debugLogging) Debug.Log($"{name}: deleting '{target.name}'.", this);
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
