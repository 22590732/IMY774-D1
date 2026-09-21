using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Add to the ping pong ball prefab. When BeginFloat() is called (by HandBallSpawner) the ball hovers
/// for a few seconds so the player can grab it, then falls normally. Grabbing it ends the float immediately.
/// Gravity is cancelled with a counter-force rather than useGravity = false, because XRGrabInteractable
/// restores the Rigidbody's gravity flag when released and would leave a thrown ball floating forever.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FloatingBall : MonoBehaviour
{
    [SerializeField] private float floatDuration = 4f;
    [Tooltip("Temporary drag while floating so the ball settles instead of drifting.")]
    [SerializeField] private float floatDamping = 3f;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private bool floating;
    private float floatEndTime;
    private float originalDamping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grab != null) grab.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        if (grab != null) grab.selectEntered.RemoveListener(OnGrabbed);
    }

    public void BeginFloat()
    {
        originalDamping = rb.linearDamping;
        rb.linearDamping = floatDamping;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        floatEndTime = Time.time + floatDuration;
        floating = true;
    }

    private void FixedUpdate()
    {
        if (!floating) return;

        if (Time.time >= floatEndTime)
        {
            EndFloat();
            return;
        }

        // Cancel gravity.
        rb.AddForce(-Physics.gravity * rb.mass, ForceMode.Force);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (floating) EndFloat();
    }

    private void EndFloat()
    {
        floating = false;
        rb.linearDamping = originalDamping;
    }
}
