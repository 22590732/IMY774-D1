using UnityEngine;

/// <summary>What a surface is made of, for impact sounds (T14).</summary>
public enum Surface
{
    Default,    // no surface layer: only the ball's own bounce
    Wood,
    Metal,
    Plastic,
    Glass,
    Ceramic,
    Stone,
    Fabric,
    Plaster
}

/// <summary>
/// Marks a collider (or everything under this object) as a surface type. The ball and the props
/// look it up with GetComponentInParent, so one component on a parent covers all its colliders.
/// Tools > Ping Pong > Set Up Audio (T14) adds these by object name; add or change them by hand
/// for anything new.
/// </summary>
[DisallowMultipleComponent]
public class SurfaceMaterial : MonoBehaviour
{
    public Surface surface = Surface.Wood;

    /// <summary>Surface of a collider, or Default when nothing is tagged.</summary>
    public static Surface Of(Collider collider)
    {
        if (collider == null) return Surface.Default;
        SurfaceMaterial s = collider.GetComponentInParent<SurfaceMaterial>();
        return s != null ? s.surface : Surface.Default;
    }
}
