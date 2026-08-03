using UnityEngine;

public class CupTrigger : MonoBehaviour
{
    [SerializeField] private AudioSource splashAudio;
    [SerializeField] private ParticleSystem Splash;

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("PingPongBall")) {
            return;
        }

        Destroy(other.gameObject);
        splashAudio.Play();
        Splash.Play();
    }
}
