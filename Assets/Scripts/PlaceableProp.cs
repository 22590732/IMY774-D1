using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Add to a grabbable prop (next to its Rigidbody and XR Grab Interactable) so it stays where the player
/// puts it instead of falling. Grabbing it unfreezes it again.
///
/// FreezeIfReleasedSlowly: letting go gently places the prop and it stays put; releasing it with a
/// throw leaves physics on, so it flies and falls normally. Use this on cups, boards, pots and so on.
/// AlwaysFreeze: stays wherever it is released, even if thrown.
/// NeverFreeze: same as not having the script. Use it for ping pong balls (or just don't add it).
///
/// This works through Rigidbody constraints only. It never changes gravity or kinematic, because
/// XRGrabInteractable saves and restores those around a grab (see FloatingBall).
/// A frozen prop is still a dynamic body, so it is solid to balls but cannot be pushed by them.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlaceableProp : MonoBehaviour
{
    public enum Mode { FreezeIfReleasedSlowly, AlwaysFreeze, NeverFreeze }

    [SerializeField] private Mode mode = Mode.FreezeIfReleasedSlowly;
    [Tooltip("Release speed (m/s) above which the prop counts as thrown and stays unfrozen.")]
    [SerializeField] private float throwSpeed = 1f;
    [Tooltip("Start frozen (e.g. sitting on a shelf) until the player grabs it.")]
    [SerializeField] private bool startFrozen = false;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private Coroutine pending;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grab == null) return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void Start()
    {
        if (startFrozen && mode != Mode.NeverFreeze) Freeze();
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (pending != null) { StopCoroutine(pending); pending = null; }
        rb.constraints = RigidbodyConstraints.None;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        if (mode == Mode.NeverFreeze) return;
        if (grab != null && grab.isSelected) return;   // still held by the other hand

        if (pending != null) StopCoroutine(pending);
        pending = StartCoroutine(DecideAfterRelease());
    }

    // XRI applies the throw velocity as it lets go, so read the speed one physics step later.
    private IEnumerator DecideAfterRelease()
    {
        yield return new WaitForFixedUpdate();
        pending = null;

        if (mode == Mode.AlwaysFreeze || rb.linearVelocity.magnitude < throwSpeed)
            Freeze();
    }

    private void Freeze()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }
}
