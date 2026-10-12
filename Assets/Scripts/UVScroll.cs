using UnityEngine;

/// <summary>
/// Slowly scrolls a material's texture offset so the pool water's normal map ripples (T15).
/// URP Lit uses the Base Map's tiling/offset for every map, so scrolling "_BaseMap" moves the
/// normal map too. Works on an instance of the material, so the asset itself never changes.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class UVScroll : MonoBehaviour
{
    [Tooltip("Texture offset change per second, in tiles.")]
    [SerializeField] private Vector2 speed = new Vector2(0.012f, 0.007f);
    [SerializeField] private string textureProperty = "_BaseMap";

    private Material material;
    private Vector2 offset;

    private void Start()
    {
        material = GetComponent<Renderer>().material;   // per-object instance
        if (material.HasProperty(textureProperty))
            offset = material.GetTextureOffset(textureProperty);
        else
            enabled = false;
    }

    private void Update()
    {
        offset += speed * Time.deltaTime;
        offset.x %= 1f;
        offset.y %= 1f;
        material.SetTextureOffset(textureProperty, offset);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
