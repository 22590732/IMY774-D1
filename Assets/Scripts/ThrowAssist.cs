using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Better throws for the ping pong ball (T19). Replaces XR Grab Interactable's own throw.
///
/// 1. Release velocity from the HAND, not the lagging physics ball, taken at the fastest moment of
///    the last ~0.15 s instead of the moment the grip opens. On the Index the grip force sensor lets go
///    late, so in an overhand throw the hand is already slowing down and arcing downwards at release;
///    XRI's average then sends the ball low. Underhand throws hide this because the hand is still rising.
///    If the hand has almost stopped at release (the ball was placed, not thrown), the current velocity
///    is used, so gentle placing still works.
/// 2. Optional "snap" towards cups: if the throw would pass close to a cup on the way down, the velocity
///    is nudged so the ball lands in it at about the same time. Strength 0 = pure physics, 1 = always in
///    when inside the snap radius.
///
/// Every number is in the Inspector so it can be tuned in the headset. Debug Logging prints each throw
/// (hand speed at release vs at its peak, and any snap) to the Console.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class ThrowAssist : MonoBehaviour
{
    [Header("Release velocity")]
    [Tooltip("How far back (seconds) to look for the fastest moment of the throw.")]
    [SerializeField] private float lookBack = 0.15f;
    [Tooltip("Velocity is measured over this long (seconds) around that moment. Longer = smoother but blunter.")]
    [SerializeField] private float measureWindow = 0.04f;
    [Tooltip("Multiplier on the hand's speed. Real throws are about 1.0-1.3. (XRI's own throw was set to 2.)")]
    [SerializeField] private float velocityScale = 1.15f;
    [Tooltip("If the hand's speed at release is below this fraction of its peak, the ball was placed, not thrown: use the current velocity.")]
    [Range(0f, 1f)] [SerializeField] private float placeFraction = 0.25f;
    [Tooltip("Throws slower than this (m/s) are treated as placing.")]
    [SerializeField] private float minThrowSpeed = 0.6f;
    [SerializeField] private float maxSpeed = 20f;
    [Tooltip("How much of the wrist's spin goes onto the ball.")]
    [SerializeField] private float spinScale = 0.5f;

    [Header("Snap to cups")]
    [SerializeField] private bool snapToCups = true;
    [Tooltip("0 = off, 1 = a throw inside the radius always lands in the cup.")]
    [Range(0f, 1f)] [SerializeField] private float snapStrength = 0.7f;
    [Tooltip("A throw is helped if it would pass within this distance (m) of a cup while falling...")]
    [SerializeField] private float snapRadius = 0.25f;
    [Tooltip("...plus this fraction of the distance to the cup (far throws get more help).")]
    [SerializeField] private float snapRadiusPerMetre = 0.05f;
    [SerializeField] private float maxSnapDistance = 8f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private const int MaxSamples = 64;
    private readonly Vector3[] pos = new Vector3[MaxSamples];
    private readonly Quaternion[] rot = new Quaternion[MaxSamples];
    private readonly float[] time = new float[MaxSamples];
    private int count, head;

    private Rigidbody rb;
    private XRGrabInteractable grab;
    private Transform hand;               // the interactor's attach transform: follows the controller exactly
    private bool pending;
    private Vector3 pendingVelocity, pendingSpin;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        grab.throwOnDetach = false;       // this script sets the release velocity instead
    }

    private void OnEnable()
    {
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        hand = args.interactorObject.GetAttachTransform(grab);
        count = 0; head = 0;
        pending = false;
        Sample();
    }

    private void Update()
    {
        if (grab.isSelected && hand != null) Sample();
    }

    private void Sample()
    {
        if (hand == null) return;
        // Skip duplicate timestamps (two samples in one frame).
        if (count > 0 && Mathf.Approximately(time[(head - 1 + MaxSamples) % MaxSamples], Time.time)) return;
        pos[head] = hand.position;
        rot[head] = hand.rotation;
        time[head] = Time.time;
        head = (head + 1) % MaxSamples;
        count = Mathf.Min(count + 1, MaxSamples);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.isCanceled) return;
        Sample();
        if (count < 3) return;

        // Velocity over measureWindow ending at each sample in the look-back period; keep the fastest.
        int last = (head - 1 + MaxSamples) % MaxSamples;
        float tEnd = time[last];
        Vector3 current = WindowVelocity(last, out _);
        Vector3 best = current;
        Vector3 bestSpin = Vector3.zero;
        for (int n = 0; n < count; n++)
        {
            int i = (last - n + MaxSamples) % MaxSamples;
            if (tEnd - time[i] > lookBack) break;
            Vector3 v = WindowVelocity(i, out Vector3 spin);
            if (v.sqrMagnitude > best.sqrMagnitude) { best = v; bestSpin = spin; }
        }

        float peak = best.magnitude, now = current.magnitude;
        bool placed = peak < minThrowSpeed || now < placeFraction * peak;
        Vector3 velocity = placed ? current : best * velocityScale;
        velocity = Vector3.ClampMagnitude(velocity, maxSpeed);

        string snapInfo = "";
        if (!placed && snapToCups && snapStrength > 0f)
            velocity = Snap(rb.position, velocity, ref snapInfo);

        pendingVelocity = velocity;
        pendingSpin = placed ? Vector3.zero : bestSpin * spinScale;
        Apply();
        pending = true;   // and once more on the next physics step, after XRI has finished letting go

        if (debugLogging)
            Debug.Log($"[ThrowAssist] {(placed ? "placed" : "thrown")}: hand {now:F2} m/s at release, peak {peak:F2} m/s, " +
                      $"ball {velocity.magnitude:F2} m/s, {ElevationDeg(velocity):F0}° up{snapInfo}", this);
    }

    private void FixedUpdate()
    {
        if (!pending) return;
        pending = false;
        if (!grab.isSelected) Apply();
    }

    private void Apply()
    {
        if (rb.isKinematic) return;
        rb.linearVelocity = pendingVelocity;
        rb.angularVelocity = pendingSpin;
    }

    // Average velocity over the measureWindow that ends at sample i (and the wrist's angular velocity).
    private Vector3 WindowVelocity(int i, out Vector3 spin)
    {
        spin = Vector3.zero;
        int j = i;
        int oldest = (head - count + MaxSamples) % MaxSamples;
        int older = (i - oldest + MaxSamples) % MaxSamples;   // samples recorded before sample i
        for (int n = 1; n <= older; n++)
        {
            int k = (i - n + MaxSamples) % MaxSamples;
            j = k;
            if (time[i] - time[k] >= measureWindow) break;
        }
        float dt = time[i] - time[j];
        if (dt <= 1e-4f) return Vector3.zero;

        Quaternion delta = rot[i] * Quaternion.Inverse(rot[j]);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (!float.IsNaN(axis.x)) spin = axis * (angle * Mathf.Deg2Rad / dt);
        return (pos[i] - pos[j]) / dt;
    }

    // ------------------------------------------------------------------ snap to cups

    private Vector3 Snap(Vector3 start, Vector3 v, ref string info)
    {
        Vector3 g = Physics.gravity;
        NewCupTrigger bestCup = null;
        float bestScore = float.MaxValue, bestT = 0f;
        Vector3 bestTarget = Vector3.zero;

        foreach (NewCupTrigger cup in FindObjectsByType<NewCupTrigger>())
        {
            if (!cup.isActiveAndEnabled || cup.GetComponentInParent<PropMenu>() != null) continue;
            Vector3 target = CupMouth(cup);
            float dist = Vector3.Distance(start, target);
            if (dist > maxSnapDistance || dist < 0.3f) continue;

            // Closest approach of the throw's arc to the cup, only while the ball is falling.
            float bestHere = float.MaxValue, tHere = 0f;
            for (float t = 0.02f; t < 3f; t += 0.01f)
            {
                Vector3 vel = v + g * t;
                if (vel.y > 0f) continue;
                Vector3 p = start + v * t + 0.5f * t * t * g;
                float d = Vector3.Distance(p, target);
                if (d < bestHere) { bestHere = d; tHere = t; }
                if (p.y < target.y - 1f) break;
            }
            float radius = snapRadius + snapRadiusPerMetre * dist;
            if (bestHere > radius) continue;
            float score = bestHere / radius;
            if (score < bestScore) { bestScore = score; bestCup = cup; bestT = tHere; bestTarget = target; }
        }
        if (bestCup == null) return v;

        // Velocity that reaches the cup's mouth at the same moment (no air drag on the ball).
        float T = Mathf.Max(0.1f, bestT);
        Vector3 hit = (bestTarget - start - 0.5f * T * T * g) / T;
        // Full strength near the centre of the radius, fading to nothing at its edge.
        float w = snapStrength * Mathf.SmoothStep(1f, 0f, bestScore);
        info = $", snapped {w * 100f:F0}% towards '{bestCup.name}' (miss was {bestScore * 100f:F0}% of radius)";
        return Vector3.Lerp(v, hit, w);
    }

    // A point a little above the cup's trigger, so the ball drops into it.
    private static Vector3 CupMouth(NewCupTrigger cup)
    {
        foreach (Collider c in cup.GetComponentsInChildren<Collider>())
            if (c.isTrigger) return c.bounds.center + Vector3.up * c.bounds.extents.y * 0.5f;
        return cup.transform.position + Vector3.up * 0.08f;
    }

    private static float ElevationDeg(Vector3 v)
    {
        float h = new Vector2(v.x, v.z).magnitude;
        return Mathf.Atan2(v.y, h) * Mathf.Rad2Deg;
    }
}
