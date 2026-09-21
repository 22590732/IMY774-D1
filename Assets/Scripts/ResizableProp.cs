using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Remembers a prop's original size so resizing can be limited to a sensible range.
/// PropResizer adds this automatically the first time a prop is resized, so you don't have to add it
/// to prefabs. Add it manually to a prefab only if you want different limits for that prop.
///
/// While a prop is held, XRGrabInteractable rewrites the object's scale every frame from its own
/// stored "target scale", so the new size is also given to it through SetTargetLocalScale.
/// </summary>
public class ResizableProp : MonoBehaviour
{
    [Tooltip("Smallest size as a multiple of the original.")]
    [SerializeField] private float minMultiplier = 0.2f;
    [Tooltip("Largest size as a multiple of the original.")]
    [SerializeField] private float maxMultiplier = 5f;

    private Vector3 baseScale;
    private XRGrabInteractable grab;

    private void Awake()
    {
        baseScale = transform.localScale;
        grab = GetComponent<XRGrabInteractable>();
    }

    /// <summary>Current size as a multiple of the original.</summary>
    public float CurrentMultiplier => transform.localScale.x / baseScale.x;

    /// <summary>Scales to (multiplier at the start of the gesture) x factor, clamped to the limits.</summary>
    public void ApplyFactor(float startMultiplier, float factor)
    {
        float m = Mathf.Clamp(startMultiplier * factor, minMultiplier, maxMultiplier);
        Vector3 newScale = baseScale * m;

        transform.localScale = newScale;
        if (grab != null)
            grab.SetTargetLocalScale(newScale);
    }
}
