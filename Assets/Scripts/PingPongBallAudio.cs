using UnityEngine;

public class PingPongBallAudio : MonoBehaviour
{
    [Header("Audio Source References")]
    [SerializeField] private AudioSource bounceSource;
    [SerializeField] private AudioSource rollSource;
    [SerializeField] private AudioSource outputSource;

    [Header("Rolling Settings")]
    [SerializeField] private float rollStartSpeed = 0.2f;
    [SerializeField] private float maxRollSpeed = 3.0f;
    [SerializeField] private float maxRollVolume = 0.8f;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.3f;

    private Rigidbody rb;
    private bool touchingSurface;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        outputSource.playOnAwake = false;
        outputSource.loop = true;
    }

    private void Update()
    {
        float speed = rb.linearVelocity.magnitude;

        if (touchingSurface && speed > rollStartSpeed)
        {
            if (outputSource.clip != rollSource.clip)
            {
                outputSource.Stop();
                outputSource.clip = rollSource.clip;
                outputSource.loop = true;
                outputSource.Play();
            }

            float t = Mathf.InverseLerp(rollStartSpeed, maxRollSpeed, speed);

            outputSource.volume = Mathf.Lerp(0f, maxRollVolume, t);
            outputSource.pitch = Mathf.Lerp(minPitch, maxPitch, t);
        }
        else
        {
            if (outputSource.clip == rollSource.clip && outputSource.isPlaying)
            {
                outputSource.Stop();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        touchingSurface = true;

        float impactSpeed = collision.relativeVelocity.magnitude;

        outputSource.Stop();
        outputSource.loop = false;
        outputSource.clip = bounceSource.clip;
        outputSource.volume = Mathf.Clamp01(impactSpeed / 5f);
        outputSource.pitch = Random.Range(0.95f, 1.05f);
        outputSource.Play();
    }

    private void OnCollisionStay(Collision collision)
    {
        touchingSurface = true;
    }

    private void OnCollisionExit(Collision collision)
    {
        touchingSurface = false;

        if (outputSource.clip == rollSource.clip)
            outputSource.Stop();
    }
}
