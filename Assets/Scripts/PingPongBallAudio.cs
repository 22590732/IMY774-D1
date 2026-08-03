using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(AudioSource))]
public class PingPongBallAudio : MonoBehaviour
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip bounceClip;
    [SerializeField] private AudioClip rollClip;

    [Header("Rolling")]
    [SerializeField] private float rollStartSpeed = 0.2f;
    [SerializeField] private float maxRollSpeed = 3.0f;
    [SerializeField] private float maxRollVolume = 0.75f;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.2f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private bool touchingSurface;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;   // Fully 3D
        audioSource.loop = false;
    }

    private void Update()
    {
        float speed = rb.linearVelocity.magnitude;

        if (touchingSurface && speed > rollStartSpeed)
        {
            if (audioSource.clip != rollClip)
            {
                audioSource.clip = rollClip;
                audioSource.loop = true;
                audioSource.Play();
            }

            float t = Mathf.InverseLerp(rollStartSpeed, maxRollSpeed, speed);

            audioSource.volume = Mathf.Lerp(0f, maxRollVolume, t);
            audioSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
        }
        else
        {
            if (audioSource.clip == rollClip && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        touchingSurface = true;

        float impactSpeed = collision.relativeVelocity.magnitude;

        audioSource.Stop();

        audioSource.loop = false;
        audioSource.clip = bounceClip;
        audioSource.volume = Mathf.Clamp01(impactSpeed / 5f);
        audioSource.pitch = Random.Range(0.95f, 1.05f);

        audioSource.Play();
    }

    private void OnCollisionStay(Collision collision)
    {
        touchingSurface = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        touchingSurface = false;

        if (audioSource.clip == rollClip)
            audioSource.Stop();
    }
}