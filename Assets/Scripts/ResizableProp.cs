using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Remembers a prop's original size so resizing can be limited to a sensible range.
/// PropResizer adds this automatically the first time a prop is resized, so you don't have to add it
/// to prefabs. Add it manually to a prefab only if you want different limits for that prop.
///
/// Smallest size: whichever is larger of Min Multiplier (relative to the original) and
/// Min World Size (the prop's longest side in metres). A prop that is already smaller than
/// Min World Size can't be shrunk below its original size.
///
/// While a prop is held, XRGrabInteractable rewrites the object's scale every frame from its own
/// stored "target scale", so the new size is also given to it through SetTargetLocalScale.
/// </summary>
public class ResizableProp : MonoBehaviour
{
    public enum Limit { None, Min, Max }

    [Tooltip("Smallest size as a multiple of the original.")]
    [SerializeField] private float minMultiplier = 0.2f;
    [Tooltip("Smallest allowed longest side, in metres. 0.08 m = two ping pong balls, so a cup still fits a ball.")]
    [SerializeField] private float minWorldSize = 0.08f;
    [Tooltip("Largest size as a multiple of the original.")]
    [SerializeField] private float maxMultiplier = 5f;

    private Vector3 baseScale;
    private float baseLongestSide;
    private XRGrabInteractable grab;

    private void Awake()
    {
        baseScale = transform.localScale;
        grab = GetComponent<XRGrabInteractable>();
        baseLongestSide = MeasureLongestSide();
    }

    /// <summary>Makes the current size the new "original" (used when a prop leaves the prop menu at full size).</summary>
    public void ResetBase()
    {
        baseScale = transform.localScale;
        baseLongestSide = MeasureLongestSide();
    }

    /// <summary>Current size as a multiple of the original.</summary>
    public float CurrentMultiplier => transform.localScale.x / baseScale.x;

    /// <summary>The effective smallest multiplier for this prop.</summary>
    public float MinMultiplier
    {
        get
        {
            float bySize = baseLongestSide > 1e-4f ? minWorldSize / baseLongestSide : 0f;
            return Mathf.Min(1f, Mathf.Max(minMultiplier, bySize));
        }
    }

    public float MaxMultiplier => Mathf.Max(1f, maxMultiplier);

    /// <summary>
    /// Scales to (multiplier at the start of the gesture) x factor, clamped to the limits.
    /// Returns which limit (if any) the prop is sitting on afterwards.
    /// </summary>
    public Limit ApplyFactor(float startMultiplier, float factor)
    {
        float min = MinMultiplier, max = MaxMultiplier;
        float wanted = startMultiplier * factor;
        float m = Mathf.Clamp(wanted, min, max);
        Vector3 newScale = baseScale * m;

        transform.localScale = newScale;
        if (grab != null)
            grab.SetTargetLocalScale(newScale);

        if (wanted <= min + 1e-4f) return Limit.Min;
        if (wanted >= max - 1e-4f) return Limit.Max;
        return Limit.None;
    }

    // Longest side of the prop's renderers (world space, at its current = original size).
    private float MeasureLongestSide()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds b = default;
        foreach (Renderer r in renderers)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return 0f;
        Vector3 s = b.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }
}
