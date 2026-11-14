using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TemporaryAudio : MonoBehaviour
{
    private AudioSource audioSource;
    private bool fading;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void Play(AudioClip clip, float volume, float pitch)
    {
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;

        audioSource.Play();

        // A lower pitch plays the clip more slowly, so it lasts longer than clip.length.
        Destroy(gameObject, clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.05f);
    }

    /// <summary>Fades out over a few milliseconds (no click) and removes the object. Used by BounceVoiceLimiter.</summary>
    public void FadeOutAndDestroy(float seconds)
    {
        if (fading || !isActiveAndEnabled) return;
        fading = true;
        StartCoroutine(FadeRoutine(Mathf.Max(0.005f, seconds)));
    }

    private IEnumerator FadeRoutine(float seconds)
    {
        float start = audioSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            audioSource.volume = start * (1f - t / seconds);
            yield return null;
        }
        audioSource.Stop();
        Destroy(gameObject);
    }
}
