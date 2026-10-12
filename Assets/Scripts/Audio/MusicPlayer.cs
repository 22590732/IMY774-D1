using System.Collections.Generic;
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
    public AudioClip CurrentTrack => a != null ? a.clip : null;

    private AudioSource a, b;          // a = current track, b = the one fading out
    private float fade = 1f;           // 0 -> 1 while crossfading from b to a
    private int current = -1;
    private float startedAt;
    private readonly List<int> valid = new List<int>();

    private void Awake()
    {
        a = MakeSource();
        b = MakeSource();
    }

    private AudioSource MakeSource()
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
        valid.Clear();
        for (int i = 0; i < tracks.Length; i++)
            if (tracks[i] != null) valid.Add(i);

        if (valid.Count == 0)
        {
            Debug.LogWarning($"[MusicPlayer] '{name}' has no music tracks assigned, so no music will play. " +
                             "Put tracks in Assets/Sound/Music and run Tools > Ping Pong > Set Up Audio (T14).", this);
            enabled = false;
            return;
        }
        if (playOnStart) PlayNext();
    }

    private void Update()
    {
        if (current < 0 || a.clip == null) return;

        if (fade < 1f) fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds));
        float v = volume * Duck;
        a.volume = v * fade;
        b.volume = v * (1f - fade);
        if (fade >= 1f && b.isPlaying) b.Stop();
        if (fade < 1f) return;

        // Start the next track so it fades in while this one ends.
        bool nearEnd = a.isPlaying && a.time >= a.clip.length - crossfadeSeconds;
        // Finished (or failed to load). Ignored for the first second, while a streamed track starts,
        // and while the app is unfocused (headset off), when audio may be paused.
        bool stopped = !a.isPlaying && Application.isFocused && Time.unscaledTime - startedAt > 1f;
        if (nearEnd || stopped) PlayNext();
    }

    /// <summary>Crossfades to the next track.</summary>
    public void PlayNext()
    {
        if (valid.Count == 0) return;
        int pick;
        if (valid.Count == 1) pick = valid[0];
        else if (shuffle)
        {
            do pick = valid[Random.Range(0, valid.Count)]; while (pick == current);
        }
        else pick = valid[(valid.IndexOf(current) + 1) % valid.Count];
        current = pick;

        // swap: the old track moves to b and fades out, the new one starts on a
        (a, b) = (b, a);
        a.clip = tracks[current];
        a.volume = 0f;
        a.Play();
        startedAt = Time.unscaledTime;
        fade = b.isPlaying ? 0f : 0.999f;   // first track: nothing to fade from, start (almost) at full
    }
}
