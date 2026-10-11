using System.Collections.Generic;
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
    [Range(0.05f, 1f)] [SerializeField] private float pressPoint = 0.2f;
    [Tooltip("Grip force (0-1) below which the object is released. Lower = harder to drop by accident, higher = easier to let go.")]
    [Range(0f, 0.95f)] [SerializeField] private float releasePoint = 0.08f;

    [SerializeField] private bool debugLogging = true;

    private const string GenericGripUsage = "{GripButton}";
    private const string IndexLayout = "ValveIndexController";

    private readonly List<(InputAction action, int bindingIndex)> overridden = new List<(InputAction, int)>();

    private void OnEnable()
    {
        HysteresisPressInteraction.Register();
        InputSystem.onDeviceChange += OnDeviceChange;
        Refresh();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        RemoveOverrides();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Reconnected || change == InputDeviceChange.Disconnected)
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
        bool index = IndexConnected();
        if (index && overridden.Count == 0) ApplyOverrides();
        else if (!index && overridden.Count > 0) RemoveOverrides();
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
        string interaction = $"{HysteresisPressInteraction.Name}(pressPoint={pressPoint},releasePoint={releasePoint})";

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
            Debug.Log("IndexGripSelect: no Index controller connected, restored the default grip binding.", this);
        overridden.Clear();
    }
}
