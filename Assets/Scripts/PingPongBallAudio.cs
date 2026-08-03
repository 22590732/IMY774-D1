using UnityEngine;

public class PingPongBallAudio : MonoBehaviour
{
    [SerializeField] private AudioSource bounceSource;
    [SerializeField] private AudioSource rollSource;
    [SerializeField] private AudioSource OutputSource;

    [SerializeField] private float rollStartSpeed = 0.2f;
    [SerializeField] private float maxRollSpeed = 3.0f;
    [SerializeField] private float maxRollVolume = 0.8f;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 1.3f;

    private RigidBody rb;
    private bool touchingSurface;

    private void Awake() {
        rb = GetComponent<RigidBody>();

        OutputSource.playOnAwake = false;
        OutputSource.loop = false;
    }

    private void Update() {
        
    }
}
