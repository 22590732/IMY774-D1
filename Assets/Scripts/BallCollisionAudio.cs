using UnityEngine;

/// <summary>
/// Ball bounce sound. Plays the ball's own bounce at the contact point (louder for harder hits),
/// then, if the thing it hit has a SurfaceMaterial, adjusts that bounce for the surface and layers
/// one of the surface's clips on top (T14). Bigger (resized) props sound lower.
/// At most Max Simultaneous Bounces play at once across all balls (BounceVoiceLimiter).
/// </summary>
public class BallCollisionAudio : MonoBehaviour
{
    [SerializeField] private AudioClip bounceClip;
    [SerializeField] private TemporaryAudio impactAudioPrefab;

    [SerializeField] private float minImpactSpeed = 0.2f;

    [Header("Surfaces (T14)")]
    [Tooltip("How each surface sounds. Assigned by Tools > Ping Pong > Set Up Audio (T14).")]
    [SerializeField] private SurfaceAudioLibrary surfaces;
    [Tooltip("Impact speed (m/s) that gives full volume.")]
    [SerializeField] private float fullVolumeSpeed = 5f;
    [Tooltip("Ignore new contacts for this long after a sound, so a jittering ball doesn't buzz.")]
    [SerializeField] private float minInterval = 0.03f;

    [Header("Voice limit")]
    [Tooltip("Most bounce sounds playing at the same time, counted over every ball in the scene. " +
             "When full, a new bounce is heard only if it is louder than the quietest one playing. 0 = no limit.")]
    [SerializeField] private int maxSimultaneousBounces = 5;

    private float lastSoundTime = -1f;

    private void Awake()
    {
        BounceVoiceLimiter.MaxVoices = maxSimultaneousBounces;
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed || impactAudioPrefab == null)
            return;
        if (Time.time - lastSoundTime < minInterval)
            return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        float volume = Mathf.Clamp01(impactSpeed / fullVolumeSpeed);

        SurfaceAudioLibrary.Entry entry = surfaces != null ? surfaces.Get(SurfaceMaterial.Of(collision.collider)) : null;
        float bouncePitch = entry != null ? entry.bouncePitch : 1f;
        float bounceVolume = entry != null ? entry.bounceVolume : 1f;

        // Work out what would play, then ask the limiter for a voice.
        bool playBounce = bounceClip != null && bounceVolume > 0f;
        float pitchA = Random.Range(0.95f, 1.05f) * bouncePitch;
        float volA = volume * bounceVolume;

        AudioClip layer = entry?.Pick();
        float pitchB = 1f, volB = 0f;
        if (layer != null)
        {
            // A prop resized to 2x rings about a fifth lower; 0.5x rings higher.
            float sizePitch = 1f;
            ResizableProp resized = collision.collider.GetComponentInParent<ResizableProp>();
            if (resized != null) sizePitch = 1f / Mathf.Sqrt(Mathf.Max(0.2f, resized.CurrentMultiplier));
            pitchB = Random.Range(0.94f, 1.06f) * sizePitch;
            volB = volume * entry.layerVolume;
        }
        if (!playBounce && layer == null) return;

        float duration = Mathf.Max(playBounce ? bounceClip.length / Mathf.Max(0.1f, pitchA) : 0f,
                                   layer != null ? layer.length / Mathf.Max(0.1f, pitchB) : 0f);
        BounceVoiceLimiter.Voice voice = BounceVoiceLimiter.Claim(Mathf.Max(volA, volB), duration);
        if (voice == null) return;   // too many bounces already playing, and this one is quieter

        lastSoundTime = Time.time;
        if (playBounce) voice.Add(Spawn(point, bounceClip, volA, pitchA));
        if (layer != null) voice.Add(Spawn(point, layer, volB, pitchB));
    }

    private TemporaryAudio Spawn(Vector3 point, AudioClip clip, float volume, float pitch)
    {
        TemporaryAudio audioInstance = Instantiate(impactAudioPrefab, point, Quaternion.identity);
        audioInstance.Play(clip, volume, pitch);
        return audioInstance;
    }
}
