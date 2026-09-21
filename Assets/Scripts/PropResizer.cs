using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Put one on each controller. While this hand is holding a prop and the trackpad is force-pressed,
/// moving the controller up grows the prop and moving it down shrinks it. Releasing the trackpad
/// keeps the current size.
/// </summary>
public class PropResizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Analog trackpad force, 0-1.")]
    [SerializeField] private InputActionReference resizeAction;
    [Tooltip("This hand's grab interactor (the Near-Far Interactor under the controller).")]
    [SerializeField] private XRBaseInteractor interactor;
    [Tooltip("What to measure up/down movement from. Usually the controller itself.")]
    [SerializeField] private Transform handReference;

    [Header("Settings")]
    [Tooltip("How hard the trackpad must be pressed (0-1) to start resizing.")]
    [SerializeField] private float pressThreshold = 0.3f;
    [Tooltip("How far (metres) to raise the controller to double the prop's size. Lowering by the same amount halves it.")]
    [SerializeField] private float metresToDouble = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    private ResizableProp active;
    private float startHeight;
    private float startMultiplier;
    private bool errorLogged;
    private bool wasPressed;
    private bool loggedNoSelection;

    private void Reset()
    {
        handReference = transform;
        interactor = GetComponentInChildren<XRBaseInteractor>();
    }

    private void OnEnable()
    {
        if (resizeAction != null && resizeAction.action != null)
            resizeAction.action.Enable();

        if (debugLogging)
        {
            if (resizeAction == null || resizeAction.action == null)
                Debug.LogWarning($"{name}: PropResizer has no Resize Action assigned.", this);
            else if (interactor == null)
                Debug.LogWarning($"{name}: PropResizer has no Interactor assigned.", this);
            else
                Debug.Log($"{name}: PropResizer ready. Action '{resizeAction.action.name}', interactor '{interactor.name}'.", this);
        }
    }

    private void Update()
    {
        bool pressed = ReadForce() > pressThreshold;
        IXRSelectInteractable held = interactor != null ? interactor.firstInteractableSelected : null;

        if (debugLogging && pressed != wasPressed)
        {
            Debug.Log($"{name}: trackpad {(pressed ? "PRESSED" : "released")}. Holding: {(held != null ? held.transform.name : "nothing")}", this);
            loggedNoSelection = false;
        }
        wasPressed = pressed;

        if (pressed && held != null)
        {
            if (active == null)
                Begin(held);
            else
                Apply();
        }
        else
        {
            if (debugLogging && pressed && held == null && !loggedNoSelection)
            {
                loggedNoSelection = true;
                Debug.LogWarning($"{name}: trackpad pressed but interactor '{(interactor != null ? interactor.name : "none")}' " +
                                 "is not holding anything. If you ARE holding a prop, the wrong interactor is assigned.", this);
            }

            if (active != null && debugLogging)
                Debug.Log($"{name}: resize ended at {active.CurrentMultiplier:F2}x original size.", this);
            active = null;   // trackpad released or prop dropped: keep whatever size it has
        }
    }

    private void Begin(IXRSelectInteractable held)
    {
        Transform prop = held.transform;

        active = prop.GetComponent<ResizableProp>();
        if (active == null)
            active = prop.gameObject.AddComponent<ResizableProp>();

        Transform reference = handReference != null ? handReference : transform;
        startHeight = reference.position.y;
        startMultiplier = active.CurrentMultiplier;

        if (debugLogging)
            Debug.Log($"{name}: resize started on '{prop.name}' at {startMultiplier:F2}x.", this);
    }

    private void Apply()
    {
        // The held prop may have been destroyed (e.g. deleted) mid-gesture.
        if (active == null) return;

        Transform reference = handReference != null ? handReference : transform;
        float delta = reference.position.y - startHeight;
        float factor = Mathf.Pow(2f, delta / metresToDouble);

        active.ApplyFactor(startMultiplier, factor);
    }

    private float ReadForce()
    {
        if (resizeAction == null || resizeAction.action == null) return 0f;

        try
        {
            return resizeAction.action.ReadValue<float>();
        }
        catch (System.InvalidOperationException)
        {
            if (!errorLogged)
            {
                errorLogged = true;
                Debug.LogError($"{name}: Resize Action '{resizeAction.action.name}' is not a single-number action. " +
                               "It should be a Value action of type Axis bound to trackpadForce.", this);
            }
            return 0f;
        }
    }
}
