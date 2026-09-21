using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put one on each controller (Left Controller / Right Controller under the XR Origin).
/// Pressing the assigned button (A = Primary Button on Valve Index) spawns a ball at the spawn point,
/// which floats briefly so the player can grab it.
/// </summary>
public class HandBallSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionReference spawnAction;   // e.g. XRI Right/Primary Button
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;               // empty child ~6 cm above the palm

    [Header("Settings")]
    [SerializeField] private float cooldown = 0.25f;
    [Tooltip("Testing only: ignore the tutorial gate so balls spawn without picking up the container.")]
    [SerializeField] private bool bypassTutorialGate = false;

    private float lastSpawnTime = -999f;

    private void OnEnable()
    {
        if (spawnAction == null || spawnAction.action == null)
        {
            Debug.LogWarning($"{name}: HandBallSpawner has no Spawn Action assigned.", this);
            return;
        }

        spawnAction.action.performed += OnSpawnPressed;
        spawnAction.action.Enable();
    }

    private void OnDisable()
    {
        if (spawnAction != null && spawnAction.action != null)
            spawnAction.action.performed -= OnSpawnPressed;
    }

    private void OnSpawnPressed(InputAction.CallbackContext ctx)
    {
        if (!bypassTutorialGate && !BallSpawnGate.IsUnlocked) return;
        if (Time.time - lastSpawnTime < cooldown) return;

        lastSpawnTime = Time.time;
        SpawnBall();
    }

    private void SpawnBall()
    {
        Transform point = spawnPoint != null ? spawnPoint : transform;
        GameObject ball = Instantiate(ballPrefab, point.position, point.rotation);

        FloatingBall floating = ball.GetComponent<FloatingBall>();
        if (floating != null)
            floating.BeginFloat();
    }
}
