using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One asset that says how each surface sounds (T14). Used by BallCollisionAudio (ball bounces)
/// and PropImpactAudio (props being dropped, set down or knocked over).
///
/// For a ball bounce, the ball's own bounce clip still plays, nudged by bouncePitch/bounceVolume
/// (e.g. quieter and lower on fabric), and one of the surface's clips is layered on top.
/// </summary>
[CreateAssetMenu(menuName = "Ping Pong/Surface Audio Library", fileName = "SurfaceAudioLibrary")]
public class SurfaceAudioLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public Surface surface;
        [Tooltip("Variations; a different one is picked each time.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Tooltip("Volume of the surface layer relative to the bounce.")]
        [Range(0f, 2f)] public float layerVolume = 0.7f;
        [Tooltip("Pitch multiplier for the ball's own bounce on this surface.")]
        [Range(0.5f, 2f)] public float bouncePitch = 1f;
        [Tooltip("Volume multiplier for the ball's own bounce on this surface (fabric damps it).")]
        [Range(0f, 1.5f)] public float bounceVolume = 1f;

        [NonSerialized] public int lastIndex = -1;

        /// <summary>A random clip, never the same one twice in a row.</summary>
        public AudioClip Pick()
        {
            if (clips == null || clips.Length == 0) return null;
            if (clips.Length == 1) return clips[0];
            int i;
            if (lastIndex < 0 || lastIndex >= clips.Length)
                i = UnityEngine.Random.Range(0, clips.Length);
            else
            {
                i = UnityEngine.Random.Range(0, clips.Length - 1);
                if (i >= lastIndex) i++;
            }
            lastIndex = i;
            return clips[i];
        }
    }

    public List<Entry> entries = new List<Entry>();

    /// <summary>Entry for a surface, or null (Default / not set up).</summary>
    public Entry Get(Surface surface)
    {
        if (surface == Surface.Default) return null;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].surface == surface) return entries[i];
        return null;
    }
}
