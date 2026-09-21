using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put one on each controller. While B is held, a laser shows where the controller points, and any
/// prop it touches is permanently deleted. A prop is anything with an XR Grab Interactable.
///
/// Aim direction, in priority order: the Ray Origin you assign, else the Near-Far Interactor's own
/// aim transform (the same ray the player already uses for far grabbing), else this object's forward.
/// </summary>
public class PropDeleter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference deleteAction;   // B = Secondary Button
    [Tooltip("Optional. Leave empty to use the Near-Far Interactor's aim (recommended).")]
    [SerializeField] private Transform rayOrigin;
    [Tooltip("Optional. Found automatically if it is on this same object.")]
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
    [Tooltip("Laser thickness in metres. Too thin is invisible in a headset.")]
    [SerializeField] private float laserWidth = 0.012f;

    [Header("Debug")]
    [Tooltip("Writes what is happening to the Console, and draws a red line in the Scene view while B is held.")]
    [SerializeField] private bool debugLogging = true;

    private NearFarInteractor nearFar;
    private float nextDeleteTime;
    private bool wasHeld;
    private string lastReport;

    private void Awake()
    {
        nearFar = GetComponentInChildren<NearFarInteractor>(true);
        if (laser == null) laser = GetComponent<LineRenderer>();
    }

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
            Debug.LogWarning($"{name}: no Laser found, so nothing will be visible in the headset. " +
                             "Add a Line Renderer and assign it to the Laser field.", this);
        else if (laser.sharedMaterial == null)
            Debug.LogWarning($"{name}: the Laser has no material and will render magenta.", this);

        if (rayOrigin == null && nearFar == null)
            Debug.LogWarning($"{name}: no Near-Far Interactor found under this controller; " +
                             "aiming with this object's forward direction instead.", this);
    }

    private Transform ResolveOrigin()
    {
        if (rayOrigin != null) return rayOrigin;
        if (nearFar != null && nearFar.isActiveAndEnabled && nearFar.curveOrigin != null)
            return nearFar.curveOrigin;
        return transform;
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

        Transform origin = ResolveOrigin();
        Vector3 start = origin.position;
        Vector3 dir = origin.forward;
        Vector3 end = start + dir * maxDistance;

        XRGrabInteractable target = null;
        string report;

        if (TryGetNearestHit(start, dir, out RaycastHit hit))
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
            Debug.DrawRay(start, dir * maxDistance, Color.red);
            if (report != lastReport)
            {
                lastReport = report;
                Debug.Log($"{name}: {report}. (ray from '{origin.name}')", this);
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

    // Nearest hit that is not part of the player's own rig (body, controllers, hands).
    private bool TryGetNearestHit(Vector3 start, Vector3 dir, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDistance = float.MaxValue;
        Transform myRoot = transform.root;

        RaycastHit[] hits = Physics.SphereCastAll(start, aimRadius, dir, maxDistance,
                                                  hitMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(myRoot)) continue;
            if (h.distance < bestDistance)
            {
                bestDistance = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    private void UpdateLaser(Vector3 start, Vector3 end)
    {
        if (laser == null) return;
        laser.enabled = true;
        laser.useWorldSpace = true;
        laser.startWidth = laserWidth;
        laser.endWidth = laserWidth;
        laser.positionCount = 2;
        laser.SetPosition(0, start);
        laser.SetPosition(1, end);
    }

    private void SetLaserVisible(bool visible)
    {
        if (laser != null) laser.enabled = visible;
    }
}
