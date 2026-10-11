using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// A door that turns on its hinge to follow the player's hand (T09).
///
/// Put it on the door leaf (the Door_Leaf model, whose origin is on the hinge axis) together with a
/// kinematic Rigidbody, colliders, and an XR Simple Interactable whose Colliders list contains only
/// the handle colliders. While a handle is held, the door angle follows the hand around the hinge,
/// clamped between closed (0) and Max Open Angle. When let go it coasts briefly and stops.
///
/// This replaces a HingeJoint + XR Grab Interactable setup: a velocity-tracked grab overpowers the
/// joint and pulls the door out of its frame, so here the door is moved kinematically instead.
/// Being kinematic, it still pushes balls and props out of the way and they bounce off it.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HingedDoor : MonoBehaviour
{
    [Tooltip("The XR Simple Interactable on this door (Colliders = the handle colliders only). Found automatically if empty.")]
    [SerializeField] private XRSimpleInteractable handle;
    [SerializeField] private float maxOpenAngle = 95f;
    [Tooltip("Tick if this door tries to open the wrong way (into its frame).")]
    [SerializeField] private bool flipDirection = false;
    [Tooltip("How quickly the door follows the hand. Higher = snappier.")]
    [SerializeField] private float followSpeed = 15f;
    [Tooltip("How quickly a released, still-swinging door slows down (degrees per second, per second).")]
    [SerializeField] private float releaseDeceleration = 360f;

    private Rigidbody rb;
    private Quaternion closedRotation;
    private Vector3 axis;          // hinge axis in world space (the model's up)
    private Vector3 closedEdge;    // direction from hinge to the free edge when closed
    private float openSign;        // +1 or -1: which way round the axis opens the door
    private float angle;           // current opening angle, 0 = closed
    private float speed;           // degrees per second
    private float grabOffset;
    private IXRSelectInteractor holder;

    /// <summary>Current opening angle in degrees (0 = closed).</summary>
    public float OpenAngle => angle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (handle == null) handle = GetComponent<XRSimpleInteractable>();

        // Door_Leaf axes (Blender model imported into Unity): local Z = up (hinge axis),
        // the leaf runs along local -X from the hinge, and it swings towards local +Y.
        closedRotation = transform.rotation;
        axis = transform.TransformDirection(Vector3.forward).normalized;
        closedEdge = Flat(transform.TransformDirection(Vector3.left));
        Vector3 swingSide = Flat(transform.TransformDirection(Vector3.up));
        openSign = Mathf.Sign(Vector3.SignedAngle(closedEdge, swingSide, axis)) * (flipDirection ? -1f : 1f);
    }

    private void OnEnable()
    {
        if (handle == null) return;
        handle.selectEntered.AddListener(OnGrabbed);
        handle.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (handle == null) return;
        handle.selectEntered.RemoveListener(OnGrabbed);
        handle.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        // Keep the door where it is at the moment of grabbing, wherever on the handle the hand is.
        grabOffset = angle - HandAngle();
        speed = 0f;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (args.interactorObject == holder) holder = null;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (holder != null)
        {
            float target = Mathf.Clamp(HandAngle() + grabOffset, 0f, maxOpenAngle);
            float next = Mathf.Lerp(angle, target, 1f - Mathf.Exp(-followSpeed * dt));
            speed = (next - angle) / dt;
            angle = next;
        }
        else if (!Mathf.Approximately(speed, 0f))
        {
            speed = Mathf.MoveTowards(speed, 0f, releaseDeceleration * dt);
            angle += speed * dt;
            if (angle <= 0f || angle >= maxOpenAngle)
            {
                angle = Mathf.Clamp(angle, 0f, maxOpenAngle);
                speed = 0f;
            }
        }

        rb.MoveRotation(Quaternion.AngleAxis(angle * openSign, axis) * closedRotation);
    }

    // Angle of the hand around the hinge, measured from the closed position towards open.
    private float HandAngle()
    {
        if (holder == null) return angle;
        Vector3 toHand = Flat(holder.GetAttachTransform(handle).position - transform.position);
        if (toHand.sqrMagnitude < 1e-4f) return angle;
        return Vector3.SignedAngle(closedEdge, toHand, axis) * openSign;
    }

    private Vector3 Flat(Vector3 v)
    {
        Vector3 up = axis.sqrMagnitude > 0f ? axis : Vector3.up;
        return Vector3.ProjectOnPlane(v, up).normalized;
    }
}
