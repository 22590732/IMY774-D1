using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Makes grabbing and releasing reliable on Valve Index controllers (T04).
///
/// The XRI Select actions are bound to the generic "GripButton" usage. On the Index that is
/// OpenXR /input/squeeze/value, which follows how far the fingers are curled around the handle
/// (capacitive), so a relaxed hand resting on the grip keeps Select held and objects won't drop.
///
/// While an Index controller is connected, this swaps that one binding (at runtime only, the input
/// asset is not changed) for the grip FORCE sensor with separate press and release points:
/// squeeze to grab, relax your hand to let go. Other controllers keep the normal binding.
/// </summary>
public class IndexGripSelect : MonoBehaviour
{
    [Tooltip("The XRI Default Input Actions asset. If empty, the assets on the scene's Input Action Manager are used.")]
    [SerializeField] private InputActionAsset actions;

    [Tooltip("Select actions to fix (map/action).")]
    [SerializeField] private string[] selectActions = { "XRI Left Interaction/Select", "XRI Right Interaction/Select" };

    [Tooltip("Grip force (0-1) needed to grab.")]
    [Range(0.05f, 1f)] [SerializeField] private float pressPoint = 0.25f;
    [Tooltip("Grip force (0-1) below which the object is released. Lower = harder to drop by accident, higher = easier to let go.")]
    [Range(0f, 0.95f)] [SerializeField] private float releasePoint = 0.15f;

    [SerializeField] private bool debugLogging = true;

    private const string GenericGripUsage = "{GripButton}";
    private const string IndexLayout = "ValveIndexController";

    [Tooltip("Seconds without any Index controller before the default binding is restored. " +
             "Stops a controller that drops out for a moment (sleep, tracking loss) from switching the grip back and forth.")]
    [SerializeField] private float restoreDelay = 3f;

    private readonly List<(InputAction action, int bindingIndex)> overridden = new List<(InputAction, int)>();
    private float nextCheck;
    private float noIndexSince = -1f;

    private void OnEnable()
    {
        HysteresisPressInteraction.Register();
        Refresh();
    }

    private void OnDisable()
    {
        bool log = debugLogging;
        debugLogging = false;      // leaving Play mode: restore quietly
        RemoveOverrides();
        debugLogging = log;
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 0.5f;
        Refresh();
    }

    private static bool IndexConnected()
    {
        foreach (InputDevice d in InputSystem.devices)
            if (d.added && InputSystem.IsFirstLayoutBasedOnSecond(d.layout, IndexLayout))
                return true;
        return false;
    }

    private void Refresh()
    {
        if (IndexConnected())
        {
            noIndexSince = -1f;
            if (overridden.Count == 0) ApplyOverrides();
            return;
        }

        if (overridden.Count == 0) return;
        if (noIndexSince < 0f) noIndexSince = Time.unscaledTime;
        else if (Time.unscaledTime - noIndexSince >= restoreDelay) RemoveOverrides();
    }

    private IEnumerable<InputActionAsset> Assets()
    {
        if (actions != null)
        {
            yield return actions;
            yield break;
        }

        InputActionManager manager = FindFirstObjectByType<InputActionManager>();
        if (manager != null && manager.actionAssets != null)
            foreach (InputActionAsset a in manager.actionAssets)
                if (a != null) yield return a;
    }

    private void ApplyOverrides()
    {
        // Invariant culture: on PCs whose decimal separator is a comma, "0,2" would split the parameter list.
        string interaction = string.Format(CultureInfo.InvariantCulture, "{0}(pressPoint={1},releasePoint={2})",
                                           HysteresisPressInteraction.Name, pressPoint, releasePoint);

        foreach (InputActionAsset asset in Assets())
        {
            foreach (string actionPath in selectActions)
            {
                InputAction action = asset.FindAction(actionPath);
                if (action == null) continue;

                for (int i = 0; i < action.bindings.Count; i++)
                {
                    InputBinding b = action.bindings[i];
                    if (b.isComposite || b.isPartOfComposite || b.path == null || !b.path.Contains(GenericGripUsage))
                        continue;

                    // "<XRController>{LeftHand}/{GripButton}" -> "<ValveIndexController>{LeftHand}/gripForce"
                    string hand = b.path.Contains("{RightHand}") ? "{RightHand}" : "{LeftHand}";
                    string newPath = $"<{IndexLayout}>{hand}/gripForce";

                    action.ApplyBindingOverride(i, new InputBinding { overridePath = newPath, overrideInteractions = interaction });
                    overridden.Add((action, i));

                    if (debugLogging)
                        Debug.Log($"IndexGripSelect: '{actionPath}' now uses {newPath} with {interaction}.", this);
                }
            }
        }

        if (debugLogging && overridden.Count == 0)
            Debug.LogWarning("IndexGripSelect: Index controller found, but no Select binding using {GripButton} was found to replace.", this);
    }

    private void RemoveOverrides()
    {
        foreach (var (action, i) in overridden)
            if (action != null) action.RemoveBindingOverride(i);

        if (debugLogging && overridden.Count > 0)
            Debug.Log($"IndexGripSelect: no Index controller for {restoreDelay:0} s, restored the default grip binding.", this);
        overridden.Clear();
    }
}
