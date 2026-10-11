using UnityEngine;

/// <summary>
/// Makes an object bob, pulse and spin so it draws the eye. Put it on a tutorial marker and assign
/// the marker as a step's Highlight; the TutorialManager switches it on only during that step.
/// </summary>
public class TutorialHighlight : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.02f;
    [SerializeField] private float bobSpeed = 2.5f;
    [Tooltip("How much bigger it gets at the top of each pulse (0.2 = 20%).")]
    [SerializeField] private float pulseAmount = 0.2f;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float spinSpeed = 90f;

    private Vector3 basePosition;
    private Vector3 baseScale;

    private void OnEnable()
    {
        basePosition = transform.localPosition;
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        transform.localPosition = basePosition;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        float t = Time.time;
        transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobHeight);
        float pulse = 1f + pulseAmount * 0.5f * (1f + Mathf.Sin(t * pulseSpeed));
        transform.localScale = baseScale * pulse;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }
}
