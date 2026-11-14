using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Limits how many ball bounce sounds play at the same time (default 5), so a room full of balls
/// doesn't turn into noise. One bounce counts as one voice, even though it may play two layers
/// (the ball's own click plus the surface it hit).
///
/// When all voices are busy, a new bounce only gets through if it is louder than the quietest one
/// still playing; that one is faded out quickly to make room. Quieter new bounces are skipped.
/// Hard, close hits stay audible while the background chatter is thinned out.
/// </summary>
public static class BounceVoiceLimiter
{
    public sealed class Voice
    {
        internal readonly List<TemporaryAudio> parts = new List<TemporaryAudio>(2);
        internal float volume;
        internal float startTime;
        internal float endTime;

        /// <summary>Loudness right now: decays linearly over the sound's length.</summary>
        internal float Loudness(float now)
        {
            float length = Mathf.Max(0.01f, endTime - startTime);
            return volume * Mathf.Clamp01((endTime - now) / length);
        }

        public void Add(TemporaryAudio audio)
        {
            if (audio != null) parts.Add(audio);
        }
    }

    /// <summary>Maximum bounce sounds at once. Set by BallCollisionAudio.</summary>
    public static int MaxVoices = 5;
    /// <summary>How quickly a stolen voice fades out (seconds).</summary>
    public static float StealFade = 0.04f;

    private static readonly List<Voice> active = new List<Voice>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active.Clear();   // works with domain reload switched off

    public static int ActiveCount
    {
        get { Prune(Time.time); return active.Count; }
    }

    /// <summary>
    /// Asks for a voice. Returns null if the bounce should stay silent; otherwise add the
    /// TemporaryAudio objects you play to the returned voice.
    /// </summary>
    public static Voice Claim(float volume, float duration)
    {
        float now = Time.time;
        Prune(now);

        if (MaxVoices > 0 && active.Count >= MaxVoices)
        {
            int quietest = -1;
            float quietestLoudness = float.MaxValue;
            for (int i = 0; i < active.Count; i++)
            {
                float l = active[i].Loudness(now);
                if (l < quietestLoudness) { quietestLoudness = l; quietest = i; }
            }
            if (quietest < 0 || volume <= quietestLoudness) return null;

            foreach (TemporaryAudio a in active[quietest].parts)
                if (a != null) a.FadeOutAndDestroy(StealFade);
            active.RemoveAt(quietest);
        }

        var voice = new Voice { volume = volume, startTime = now, endTime = now + Mathf.Max(0.05f, duration) };
        active.Add(voice);
        return voice;
    }

    private static void Prune(float now)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Voice v = active[i];
            bool alive = false;
            foreach (TemporaryAudio a in v.parts)
                if (a != null) { alive = true; break; }
            // A voice with no parts yet is only kept for the frame it was claimed in.
            if (now >= v.endTime || (!alive && now > v.startTime)) active.RemoveAt(i);
        }
    }
}
