using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put on a cup with a small trigger collider just inside the rim (separate from the cup's solid colliders).
/// When a ball drops in: the ball is removed, the splash plays, and a victory sound plays.
/// Sounds are AudioClips played as 3D sound at the splash position, so no AudioSource is needed on the cup.
/// The Scored event is there for later (score counter, confetti, tutorial step, etc.).
/// </summary>
public class NewCupTrigger : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private AudioClip splashClip;
    [Range(0f, 1f)][SerializeField] private float splashVolume = 1f;
    [SerializeField] private AudioClip victoryClip;
    [Range(0f, 1f)][SerializeField] private float victoryVolume = 1f;
    [SerializeField] private ParticleSystem Splash;
    [Tooltip("Where the sounds play from. Falls back to the splash particles, then this object.")]
    [SerializeField] private Transform soundPoint;

    [Header("Events")]
    public UnityEvent Scored;

    private void OnTriggerEnter(Collider other)
    {
        // Use the rigidbody's object so the tag can sit on the ball root even if a child collider hits.
        GameObject ball = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!ball.CompareTag("PingPongBall")) return;

        Destroy(ball);

        Vector3 pos = soundPoint != null ? soundPoint.position
                    : Splash != null ? Splash.transform.position
                    : transform.position;

        if (splashClip != null) AudioSource.PlayClipAtPoint(splashClip, pos, splashVolume);
        if (victoryClip != null) AudioSource.PlayClipAtPoint(victoryClip, pos, victoryVolume);

        if (Splash != null) Splash.Play();

        Scored?.Invoke();
    }
}
