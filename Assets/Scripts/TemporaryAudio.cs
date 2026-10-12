using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class TemporaryAudio : MonoBehaviour
{
    private AudioSource audioSource;

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
}
