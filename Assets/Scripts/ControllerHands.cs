using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shows animated hand models on the controllers instead of controller models (T07).
///
/// At start it puts a copy of the hand model under each controller of the XR Origin, hides the
/// controller models, and every frame curls the fingers from the controller input:
///   Grip (XRI "Select Value")      curls the middle, ring and little fingers and the thumb.
///   Trigger (XRI "Activate Value") curls the index finger.
/// Each finger bone is turned around its own X axis (towards the palm) by
///   relaxed + grip * gripAmount + trigger * triggerAmount   degrees,
/// on top of the model's rest pose. The angles were worked out from the XR Hands sample rig and
/// checked visually so the fist wraps a controller handle.
///
/// Placement: the wrist is put at Wrist Position / Wrist Rotation relative to the controller's grip
/// pose. These can be adjusted live in Play mode (copy the component, exit Play mode, then
/// Paste Component Values to keep them).
///
/// Grabbing is unchanged: the interactors stay on the controllers. Only the visuals change.
/// </summary>
public class ControllerHands : MonoBehaviour
{
    [Serializable]
    public class BoneCurl
    {
        [Tooltip("Bone name without the L_/R_ prefix, e.g. IndexProximal.")]
        public string bone;
        public float relaxed;
        public float grip;
        public float trigger;

        public BoneCurl(string bone, float relaxed, float grip, float trigger)
        {
            this.bone = bone; this.relaxed = relaxed; this.grip = grip; this.trigger = trigger;
        }
    }

    [Header("Hand models (XR Hands sample LeftHand.fbx / RightHand.fbx)")]
    [SerializeField] private GameObject leftHandModel;
    [SerializeField] private GameObject rightHandModel;
    [Tooltip("Optional material for the hand meshes.")]
    [SerializeField] private Material handMaterial;

    [Header("Input")]
    [Tooltip("The XRI Default Input Actions asset.")]
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private string leftGripAction = "XRI Left Interaction/Select Value";
    [SerializeField] private string leftTriggerAction = "XRI Left Interaction/Activate Value";
    [SerializeField] private string rightGripAction = "XRI Right Interaction/Select Value";
    [SerializeField] private string rightTriggerAction = "XRI Right Interaction/Activate Value";

    [Header("Controllers")]
    [SerializeField] private string leftControllerName = "Left Controller";
    [SerializeField] private string rightControllerName = "Right Controller";
    [SerializeField] private string leftControllerVisualName = "Left Controller Visual";
    [SerializeField] private string rightControllerVisualName = "Right Controller Visual";
    [SerializeField] private bool hideControllerModels = true;

    [Header("Placement: wrist relative to the controller grip pose (m / degrees)")]
    [SerializeField] private Vector3 leftWristPosition = new Vector3(-0.0068f, 0.0856f, 0f);
    [SerializeField] private Vector3 leftWristRotation = new Vector3(90f, -90f, 0f);
    [SerializeField] private Vector3 rightWristPosition = new Vector3(0.0068f, 0.0856f, 0f);
    [SerializeField] private Vector3 rightWristRotation = new Vector3(90f, -90f, 180f);

    [Header("Fingers")]
    [Tooltip("How quickly fingers follow the input. Higher = snappier.")]
    [SerializeField] private float smoothing = 20f;
    [SerializeField] private List<BoneCurl> curls = DefaultCurls();

    private class HandRig
    {
        public Transform root, wrist;
        public Vector3 wristPosInRoot;
        public Quaternion wristRotInRoot;
        public readonly List<(Transform bone, Quaternion rest, BoneCurl curl)> bones = new List<(Transform, Quaternion, BoneCurl)>();
        public InputAction grip, trigger;
        public float g, t;
    }

    private HandRig left, right;

    public static List<BoneCurl> DefaultCurls() => new List<BoneCurl>
    {
        //                       relaxed  grip  trigger
        new BoneCurl("IndexProximal",       5, 35, 35),
        new BoneCurl("IndexIntermediate",   8, 40, 45),
        new BoneCurl("IndexDistal",         5, 20, 20),
        new BoneCurl("MiddleProximal",      5, 55,  0),
        new BoneCurl("MiddleIntermediate",  8, 75,  0),
        new BoneCurl("MiddleDistal",        5, 40,  0),
        new BoneCurl("RingProximal",        5, 58,  0),
        new BoneCurl("RingIntermediate",    8, 75,  0),
        new BoneCurl("RingDistal",          5, 40,  0),
        new BoneCurl("LittleMetacarpal",    0,  8,  0),
        new BoneCurl("LittleProximal",      5, 60,  0),
        new BoneCurl("LittleIntermediate",  8, 75,  0),
        new BoneCurl("LittleDistal",        5, 40,  0),
        new BoneCurl("ThumbMetacarpal",     0, 10,  0),
        new BoneCurl("ThumbProximal",       3, 25,  0),
        new BoneCurl("ThumbDistal",         3, 20,  0),
    };

    private void Reset()
    {
        curls = DefaultCurls();
    }

    private void Start()
    {
        XROrigin origin = FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            Debug.LogWarning("ControllerHands: no XR Origin in the scene.", this);
            return;
        }

        Transform leftController = null, rightController = null;
        foreach (Transform t in origin.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == leftControllerName) leftController = t;
            else if (t.name == rightControllerName) rightController = t;
            else if (hideControllerModels && (t.name == leftControllerVisualName || t.name == rightControllerVisualName))
                t.gameObject.SetActive(false);
        }

        left = Build(leftHandModel, leftController, "L_", leftGripAction, leftTriggerAction);
        right = Build(rightHandModel, rightController, "R_", rightGripAction, rightTriggerAction);

        Debug.Log($"ControllerHands: left hand {(left != null ? "attached to " + leftController.name : "NOT attached")}, " +
                  $"right hand {(right != null ? "attached to " + rightController.name : "NOT attached")}; " +
                  $"controller models {(hideControllerModels ? "hidden" : "shown")}.", this);
    }

    private HandRig Build(GameObject model, Transform controller, string prefix, string gripName, string triggerName)
    {
        if (model == null || controller == null)
        {
            Debug.LogWarning($"ControllerHands: missing {(model == null ? "hand model" : "controller")} for {prefix}hand.", this);
            return null;
        }

        var rig = new HandRig();
        GameObject instance = Instantiate(model, controller, false);
        instance.name = prefix == "L_" ? "Left Hand (controller)" : "Right Hand (controller)";
        rig.root = instance.transform;

        foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Destroy(c);
        if (handMaterial != null)
            foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = handMaterial;

        var byName = new Dictionary<string, Transform>();
        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            if (!byName.ContainsKey(t.name)) byName[t.name] = t;

        if (!byName.TryGetValue(prefix + "Wrist", out rig.wrist))
        {
            Debug.LogWarning($"ControllerHands: no '{prefix}Wrist' bone in {model.name}.", this);
            rig.wrist = rig.root;
        }
        rig.wristRotInRoot = Quaternion.Inverse(rig.root.rotation) * rig.wrist.rotation;
        rig.wristPosInRoot = rig.root.InverseTransformPoint(rig.wrist.position);

        foreach (BoneCurl curl in curls)
            if (byName.TryGetValue(prefix + curl.bone, out Transform bone))
                rig.bones.Add((bone, bone.localRotation, curl));

        if (actions != null)
        {
            rig.grip = actions.FindAction(gripName);
            rig.trigger = actions.FindAction(triggerName);
        }
        if (rig.grip == null || rig.trigger == null)
            Debug.LogWarning($"ControllerHands: couldn't find input actions '{gripName}' / '{triggerName}'.", this);

        return rig;
    }

    private void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        UpdateHand(left, leftWristPosition, leftWristRotation, k);
        UpdateHand(right, rightWristPosition, rightWristRotation, k);
    }

    private static void UpdateHand(HandRig rig, Vector3 wristPos, Vector3 wristEuler, float k)
    {
        if (rig == null || rig.root == null) return;

        rig.g = Mathf.Lerp(rig.g, Read(rig.grip), k);
        rig.t = Mathf.Lerp(rig.t, Read(rig.trigger), k);

        foreach (var (bone, rest, curl) in rig.bones)
        {
            float angle = curl.relaxed + curl.grip * rig.g + curl.trigger * rig.t;
            bone.localRotation = rest * Quaternion.AngleAxis(angle, Vector3.right);
        }

        // Place the model so its wrist lands on the requested pose (local to the controller, so it
        // follows the controller with no lag).
        Quaternion wristRot = Quaternion.Euler(wristEuler);
        rig.root.localRotation = wristRot * Quaternion.Inverse(rig.wristRotInRoot);
        rig.root.localPosition = wristPos - rig.root.localRotation * rig.wristPosInRoot;
    }

    private static float Read(InputAction action)
    {
        if (action == null) return 0f;
        try { return Mathf.Clamp01(action.ReadValue<float>()); }
        catch (InvalidOperationException) { return 0f; }
    }
}
