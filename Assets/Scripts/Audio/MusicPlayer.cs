using UnityEngine;

/// <summary>
/// Background music (T14): plays the tracks in a shuffled order with no immediate repeats and
/// crossfades between them. 2D, so it sounds the same wherever the player stands.
/// The tutorial turns it down while the voiceover talks by setting <see cref="Duck"/>.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip[] tracks = new AudioClip[0];
    [Tooltip("Music volume. 0.12 is about -18 dB, quiet enough to hear the bounces.")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.12f;
    [SerializeField] private float crossfadeSeconds = 2f;
    [SerializeField] private bool shuffle = true;
    [SerializeField] private bool playOnStart = true;

    /// <summary>Extra volume multiplier (1 = normal). The tutorial lowers it under the voiceover.</summary>
    [HideInInspector] public float Duck = 1f;

    public float Volume { get => volume; set => volume = Mathf.Clamp01(value); }

    private AudioSource a, b;          // current and next, swapped on each crossfade
    private float fade = 1f;           // 0 -> 1 while crossfading from b (old) to a (new)
    private int current = -1;

    private void Awake()
    {
        a = MakeSource("Music A");
        b = MakeSource("Music B");
    }

    private AudioSource MakeSource(string n)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.priority = 0;
        s.ignoreListenerPause = true;
        return s;
    }

    private void Start()
    {
        if (playOnStart) PlayNext();
    }

    private void Update()
    {
        if (tracks == null || tracks.Length == 0 || current < 0) return;

        if (fade < 1f) fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds));
        float v = volume * Duck;
        a.volume = v * fade;
        b.volume = v * (1f - fade);
        if (fade >= 1f && b.isPlaying) b.Stop();

        // Start the next track so it fades in while this one ends.
        if (a.clip != null && a.isPlaying && a.time >= a.clip.length - crossfadeSeconds && fade >= 1f)
            PlayNext();
        else if (!a.isPlaying && fade >= 1f)
            PlayNext();     // e.g. very short track, or audio was interrupted
    }

    /// <summary>Crossfades to the next track.</summary>
    public void PlayNext()
    {
        if (tracks == null || tracks.Length == 0) return;
        int next;
        if (tracks.Length == 1) next = 0;
        else if (shuffle)
        {
            next = Random.Range(0, tracks.Length - 1);
            if (current >= 0 && next >= current) next++;
        }
        else next = (current + 1) % tracks.Length;
        current = next;

        // swap: the old track goes to b and fades out, the new one starts on a
        (a, b) = (b, a);
        a.clip = tracks[current];
        a.time = 0f;
        a.volume = 0f;
        a.Play();
        fade = b.isPlaying ? 0f : 0.999f;   // nothing to fade from on the first track: start (almost) at full
    }
}
