using UnityEngine;

/// <summary>
/// Sound for a prop hitting something: set down gently = soft, dropped or thrown = loud (T14).
/// Plays the prop's own surface (its SurfaceMaterial) and, if what it hit is not another prop,
/// that surface too, quieter. Bigger (resized) props sound lower. Ball hits are left to the ball.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PropImpactAudio : MonoBehaviour
{
    [SerializeField] private SurfaceAudioLibrary surfaces;
    [SerializeField] private TemporaryAudio impactAudioPrefab;

    [Tooltip("Below this impact speed (m/s) nothing plays.")]
    [SerializeField] private float minImpactSpeed = 0.12f;
    [Tooltip("Impact speed (m/s) that gives full volume.")]
    [SerializeField] private float fullVolumeSpeed = 3f;
    [Range(0f, 1f)] [SerializeField] private float maxVolume = 0.8f;
    [Tooltip("Volume of the other surface's layer, relative to the prop's own sound.")]
    [Range(0f, 1f)] [SerializeField] private float otherSurfaceVolume = 0.5f;
    [Tooltip("Pitch of the prop's sound at its original size. Heavier props sound better a bit lower.")]
    [SerializeField] private float basePitch = 1f;
    [SerializeField] private float minInterval = 0.08f;
    [Tooltip("No sounds for this long after the prop appears, so props settling on the shelves stay quiet.")]
    [SerializeField] private float quietAfterSpawn = 0.75f;

    private SurfaceMaterial own;
    private ResizableProp resizable;
    private Vector3 startScale;
    private float spawnTime;
    private float lastSoundTime = -1f;

    private void Awake()
    {
        own = GetComponentInParent<SurfaceMaterial>();
        startScale = transform.localScale;
        spawnTime = Time.time;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (surfaces == null || impactAudioPrefab == null) return;
        if (Time.time - spawnTime < quietAfterSpawn || Time.time - lastSoundTime < minInterval) return;
        if (collision.collider.CompareTag("PingPongBall")) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;
        lastSoundTime = Time.time;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        float t = Mathf.InverseLerp(minImpactSpeed, fullVolumeSpeed, speed);
        float volume = maxVolume * t * t * 0.85f + 0.15f * maxVolume * t;   // soft set-downs stay soft

        if (resizable == null) resizable = GetComponent<ResizableProp>();
        float size = resizable != null ? resizable.CurrentMultiplier : transform.localScale.x / Mathf.Max(1e-4f, startScale.x);
        float pitch = basePitch / Mathf.Sqrt(Mathf.Max(0.2f, size));

        SurfaceAudioLibrary.Entry mine = own != null ? surfaces.Get(own.surface) : null;
        AudioClip clip = mine?.Pick();
        if (clip != null) Spawn(point, clip, volume, pitch * Random.Range(0.95f, 1.05f));

        // The other surface, unless it is a prop with its own PropImpactAudio (that one plays itself).
        bool otherIsProp = collision.rigidbody != null && collision.rigidbody.GetComponent<PropImpactAudio>() != null;
        if (!otherIsProp)
        {
            SurfaceAudioLibrary.Entry other = surfaces.Get(SurfaceMaterial.Of(collision.collider));
            AudioClip otherClip = other?.Pick();
            if (otherClip != null && other != mine)
                Spawn(point, otherClip, volume * otherSurfaceVolume, Random.Range(0.9f, 1.0f));
        }
    }

    private void Spawn(Vector3 point, AudioClip clip, float volume, float pitch)
    {
        if (volume <= 0.001f) return;
        TemporaryAudio a = Instantiate(impactAudioPrefab, point, Quaternion.identity);
        a.Play(clip, volume, pitch);
    }
}
