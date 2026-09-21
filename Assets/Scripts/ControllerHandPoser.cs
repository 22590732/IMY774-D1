using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Poses a rigged hand (bones named like L_IndexProximal / R_IndexProximal) from controller input,
/// instead of from hand tracking. Put it on the root of a COPY of the Left/Right Hand Tracking prefab
/// that has the XR Hand Tracking Events, Skeleton Driver and Mesh Controller components removed.
///
/// Grip curls middle, ring and little. Trigger curls the index finger.
/// Thumb touch (optional) curls the thumb.
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

    [Header("Curl (tune these in Play mode)")]
    [Tooltip("Local axis each finger bone rotates around to curl. Find it by rotating L_IndexProximal in the Scene view.")]
    [SerializeField] private Vector3 fingerCurlAxis = new Vector3(0f, 0f, 1f);
    [Tooltip("Full-curl angle for (Proximal, Intermediate, Distal) joints, in degrees. Use negative values to flip direction.")]
    [SerializeField] private Vector3 fingerAngles = new Vector3(70f, 90f, 60f);

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
        public float curl;
    }

    private Digit index, middle, ring, little, thumb;

    private void Awake()
    {
        index  = Build("IndexProximal",  "IndexIntermediate",  "IndexDistal");
        middle = Build("MiddleProximal", "MiddleIntermediate", "MiddleDistal");
        ring   = Build("RingProximal",   "RingIntermediate",   "RingDistal");
        little = Build("LittleProximal", "LittleIntermediate", "LittleDistal");
        thumb  = Build("ThumbMetacarpal", "ThumbProximal",     "ThumbDistal");
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
            rest = new Quaternion[boneNames.Length]
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

    private static Transform FindDeep(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == boneName) return t;
        return null;
    }

    private static float Read(InputActionReference r)
    {
        if (r == null || r.action == null) return 0f;
        return r.action.ReadValue<float>();
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

        Apply(index,  trigger,    t, fingerCurlAxis, fingerAngles);
        Apply(middle, grip,       t, fingerCurlAxis, fingerAngles);
        Apply(ring,   grip,       t, fingerCurlAxis, fingerAngles);
        Apply(little, grip,       t, fingerCurlAxis, fingerAngles);
        Apply(thumb,  thumbTouch, t, thumbCurlAxis,  thumbAngles);
    }

    private static void Apply(Digit d, float target, float t, Vector3 axis, Vector3 angles)
    {
        d.curl = Mathf.Lerp(d.curl, target, t);
        axis = axis.normalized;

        for (int i = 0; i < d.bones.Length; i++)
        {
            if (d.bones[i] == null) continue;
            float angle = angles[i] * d.curl;
            d.bones[i].localRotation = d.rest[i] * Quaternion.AngleAxis(angle, axis);
        }
    }
}
