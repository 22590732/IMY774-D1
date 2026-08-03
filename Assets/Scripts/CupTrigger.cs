using UnityEngine;

public class CupTrigger : MonoBehaviour
{
    [SerializeField] private AudioSource splashAudio;

    private void OnTriggerEnter(Collider other) {
        if (!other.CompareTag("PingPongBall")) {
            return;
        }

        splashAudio.Play();
    }
}
