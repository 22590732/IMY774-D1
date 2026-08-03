using UnityEngine;

public class BallCollisionAudio : MonoBehaviour
{
    [SerializeField] private AudioClip bounceClip;
    [SerializeField] private TemporaryAudio impactAudioPrefab;

    [SerializeField] private float minImpactSpeed = 0.2f;

    private void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minImpactSpeed)
            return;

        ContactPoint contact = collision.contacts[0];

        TemporaryAudio audioInstance =
            Instantiate(
                impactAudioPrefab,
                contact.point,
                Quaternion.identity);

        float volume = Mathf.Clamp01(impactSpeed / 5f);

        audioInstance.Play(
            bounceClip,
            volume,
            Random.Range(0.95f, 1.05f));
    }
}