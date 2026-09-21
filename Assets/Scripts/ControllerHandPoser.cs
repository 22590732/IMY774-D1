using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Poses a rigged hand (bones named like L_IndexProximal / R_IndexProximal) from controller input,
/// instead of from hand tracking. Put it on the root of a COPY of the Left/Right Hand Tracking prefab
/// that has the XR Hand Tracking Events, Skeleton Driver and Mesh Controller components removed.
///
/// Grip curls middle, ring and little. Trigger curls the index finger.
/// Thumb touch (optional) curls the thumb.
///
/// The finger curl axis is worked out automatically from the bone directions and the palm normal,
/// so you don't have to know the rig's local axes. Use Flip Curl if a hand bends backward.
/// </summary>
public class ControllerHandPoser : MonoBehaviour
{
    [Header("Bones")]
    [Tooltip("Left hand: L_    Right hand: R_")]
    [SerializeField] private string bonePrefix = "L_";

    [Header("Input")]
    [Tooltip("Analog grip, 0-1. Try XRI Left Interaction/Select Value.")]
    [SerializeField] private InputActionReference gripAction;
    [Tooltip("Analog trigger, 0-1. Try XRI Left Interaction/Activate Value.")]
    [SerializeField] private InputActionReference triggerAction;
    [Tooltip("Optional. A button action that is active while the thumb touches A, B, thumbstick or trackpad.")]
    [SerializeField] private InputActionReference thumbTouchAction;

    [Header("Finger curl")]
    [Tooltip("Work out each finger's bend axis automatically. Turn off only to use the manual axis below.")]
    [SerializeField] private bool autoFingerAxis = true;
    [Tooltip("Tick if the fingers bend backward (away from the palm). May differ between left and right hands.")]
    [SerializeField] private bool flipCurl = false;
    [Tooltip("Manual axis, only used when Auto Finger Axis is off.")]
    [SerializeField] private Vector3 manualFingerAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("Full-curl angle for (Proximal, Intermediate, Distal) joints, in degrees.")]
    [SerializeField] private Vector3 fingerAngles = new Vector3(70f, 90f, 60f);

    [Header("Thumb curl (manual axis)")]
    [SerializeField] private Vector3 thumbCurlAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("Full-curl angle for the thumb (Metacarpal, Proximal, Distal).")]
    [SerializeField] private Vector3 thumbAngles = new Vector3(15f, 30f, 30f);

    [Tooltip("How quickly the fingers follow the input. Higher = snappier.")]
    [SerializeField] private float smoothing = 20f;

    [Header("Testing (Play mode)")]
    [SerializeField] private bool useTestValues = false;
    [Range(0f, 1f)] [SerializeField] private float testGrip;
    [Range(0f, 1f)] [SerializeField] private float testTrigger;
    [Range(0f, 1f)] [SerializeField] private float testThumb;

    private class Digit
    {
        public Transform[] bones;
        public Quaternion[] rest;
        public Vector3[] autoAxes;   // local-space axis per bone; zero vector = not available
        public float curl;
    }

    private Digit index, middle, ring, little, thumb;
    private bool inputErrorLogged;

    private void Awake()
    {
        index  = Build("IndexProximal",  "IndexIntermediate",  "IndexDistal");
        middle = Build("MiddleProximal", "MiddleIntermediate", "MiddleDistal");
        ring   = Build("RingProximal",   "RingIntermediate",   "RingDistal");
        little = Build("LittleProximal", "LittleIntermediate", "LittleDistal");
        thumb  = Build("ThumbMetacarpal", "ThumbProximal",     "ThumbDistal");

        ComputeAutoAxes();
    }

    private void OnEnable()
    {
        Enable(gripAction);
        Enable(triggerAction);
        Enable(thumbTouchAction);
    }

    private static void Enable(InputActionReference r)
    {
        if (r != null && r.action != null) r.action.Enable();
    }

    private Digit Build(params string[] boneNames)
    {
        var d = new Digit
        {
            bones = new Transform[boneNames.Length],
            rest = new Quaternion[boneNames.Length],
            autoAxes = new Vector3[boneNames.Length]
        };

        for (int i = 0; i < boneNames.Length; i++)
        {
            string full = bonePrefix + boneNames[i];
            Transform t = FindDeep(transform, full);
            if (t == null)
                Debug.LogWarning($"{name}: bone '{full}' not found. Check Bone Prefix.", this);
            d.bones[i] = t;
            if (t != null) d.rest[i] = t.localRotation;
        }
        return d;
    }

    // Bend axis = perpendicular to both the finger's direction and the palm normal.
    // Computed once from the rest (open) pose.
    private void ComputeAutoAxes()
    {
        Transform wrist = FindDeep(transform, bonePrefix + "Wrist");
        Transform idx = index.bones[0];
        Transform mid = middle.bones[0];
        Transform lit = little.bones[0];

        if (wrist == null || idx == null || mid == null || lit == null)
        {
            Debug.LogWarning($"{name}: couldn't work out the palm plane. Using the manual axis.", this);
            return;
        }

        Vector3 forward = (mid.position - wrist.position).normalized;
        Vector3 across = (lit.position - idx.position).normalized;
        Vector3 palmNormal = Vector3.Cross(forward, across).normalized;

        foreach (Digit d in new[] { index, middle, ring, little })
        {
            for (int i = 0; i < d.bones.Length; i++)
            {
                Transform bone = d.bones[i];
                if (bone == null || bone.childCount == 0) continue;

                Vector3 dir = (bone.GetChild(0).position - bone.position).normalized;
                Vector3 worldAxis = Vector3.Cross(dir, palmNormal);
                if (worldAxis.sqrMagnitude < 1e-6f) continue;

                d.autoAxes[i] = bone.InverseTransformDirection(worldAxis.normalized);
            }
        }
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    private float Read(InputActionReference r)
    {
        if (r == null || r.action == null) return 0f;

        try
        {
            return r.action.ReadValue<float>();
        }
        catch (System.InvalidOperationException)
        {
            if (!inputErrorLogged)
            {
                inputErrorLogged = true;
                Debug.LogError($"{name}: action '{r.action.name}' is not a single-number (float or button) action. " +
                               "Choose an analog action such as Select Value or Activate Value.", this);
            }
            return 0f;
        }
    }

    private void LateUpdate()
    {
        float grip, trigger, thumbTouch;

        if (useTestValues)
        {
            grip = testGrip;
            trigger = testTrigger;
            thumbTouch = testThumb;
        }
        else
        {
            grip = Read(gripAction);
            trigger = Read(triggerAction);
            thumbTouch = Read(thumbTouchAction) > 0.5f ? 1f : 0f;
        }

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

        ApplyFinger(index,  trigger, t);
        ApplyFinger(middle, grip,    t);
        ApplyFinger(ring,   grip,    t);
        ApplyFinger(little, grip,    t);
        ApplyThumb(thumb, thumbTouch, t);
    }

    private void ApplyFinger(Digit d, float target, float t)
    {
        d.curl = Mathf.Lerp(d.curl, target, t);
        float sign = flipCurl ? -1f : 1f;

        for (int i = 0; i < d.bones.Length; i++)
        {
            if (d.bones[i] == null) continue;

            Vector3 axis = (autoFingerAxis && d.autoAxes[i] != Vector3.zero)
                ? d.autoAxes[i]
                : manualFingerAxis.normalized;

            float angle = fingerAngles[i] * d.curl * sign;
            d.bones[i].localRotation = d.rest[i] * Quaternion.AngleAxis(angle, axis);
        }
    }

    private void ApplyThumb(Digit d, float target, float t)
    {
        d.curl = Mathf.Lerp(d.curl, target, t);
        Vector3 axis = thumbCurlAxis.normalized;

        for (int i = 0; i < d.bones.Length; i++)
        {
            if (d.bones[i] == null) continue;
            d.bones[i].localRotation = d.rest[i] * Quaternion.AngleAxis(thumbAngles[i] * d.curl, axis);
        }
    }
}
