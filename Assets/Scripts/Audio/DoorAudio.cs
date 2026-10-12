using UnityEngine;

/// <summary>
/// Door sounds (T14, from T09): a hinge creak that follows how fast the door swings, and a latch
/// click when it shuts. Reads the angle from HingedDoor, so it needs nothing else.
/// </summary>
[RequireComponent(typeof(HingedDoor))]
public class DoorAudio : MonoBehaviour
{
    [SerializeField] private AudioClip creakLoop;
    [SerializeField] private AudioClip latchClip;

    [Tooltip("Swing speed (degrees/s) where the creak starts.")]
    [SerializeField] private float creakStartSpeed = 15f;
    [Tooltip("Swing speed (degrees/s) for the loudest creak.")]
    [SerializeField] private float creakFullSpeed = 120f;
    [Range(0f, 1f)] [SerializeField] private float maxCreakVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] private float latchVolume = 0.8f;
    [Tooltip("The latch clicks when the door closes from more than this many degrees open.")]
    [SerializeField] private float latchArmAngle = 3f;

    private HingedDoor door;
    private AudioSource creak, shot;
    private float lastAngle, smoothSpeed;
    private bool armed;

    private void Awake()
    {
        door = GetComponent<HingedDoor>();
        creak = MakeSource(true);
        shot = MakeSource(false);
        lastAngle = door.OpenAngle;
    }

    private AudioSource MakeSource(bool loop)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 0.8f;
        s.maxDistance = 20f;
        s.dopplerLevel = 0f;
        return s;
    }

    private void Update()
    {
        float angle = door.OpenAngle;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        float speed = Mathf.Abs(angle - lastAngle) / dt;
        smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-12f * dt));

        // creak
        if (creakLoop != null)
        {
            float t = Mathf.InverseLerp(creakStartSpeed, creakFullSpeed, smoothSpeed);
            if (t > 0f && !creak.isPlaying)
            {
                creak.clip = creakLoop;
                creak.time = Random.Range(0f, creakLoop.length * 0.9f);
                creak.Play();
            }
            creak.volume = maxCreakVolume * t;
            creak.pitch = Mathf.Lerp(0.85f, 1.15f, t);
            if (t <= 0f && creak.isPlaying && smoothSpeed < creakStartSpeed * 0.5f) creak.Stop();
        }

        // latch
        if (angle > latchArmAngle) armed = true;
        if (armed && angle <= 0.5f)
        {
            armed = false;
            if (latchClip != null)
            {
                shot.pitch = Random.Range(0.95f, 1.05f);
                shot.PlayOneShot(latchClip, latchVolume);
            }
        }

        lastAngle = angle;
    }
}
