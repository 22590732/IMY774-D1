using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Add to the ping pong ball prefab. When BeginFloat() is called (by HandBallSpawner) the ball hovers
/// for a few seconds so the player can grab it, then falls normally. Grabbing it ends the float immediately.
///
/// This script never changes any Rigidbody property (gravity, damping, kinematic). XRGrabInteractable
/// saves and restores those around a grab, so changing them here can leave the ball with wrong values
/// after it is thrown. Instead it applies forces each physics step while floating.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FloatingBall : MonoBehaviour
{
    [SerializeField] private float floatDuration = 4f;
    [Tooltip("How quickly the ball's motion settles while floating (per second). 0 = no settling.")]
    [SerializeField] private float settleRate = 3f;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private bool floating;
    private float floatEndTime;

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
            floating = false;
            return;
        }

        // Cancel gravity.
        rb.AddForce(-Physics.gravity * rb.mass, ForceMode.Force);

        // Let the ball settle instead of drifting, without touching Rigidbody damping.
        float k = Mathf.Clamp01(settleRate * Time.fixedDeltaTime);
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, k);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        floating = false;
    }
}