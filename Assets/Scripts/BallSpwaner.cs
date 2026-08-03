using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 1.5f;

    private GameObject currentBall;
    private bool waitingForRespawn;

    private void Start()
    {
        SpawnBall();
    }

    private void Update()
    {
        if (currentBall == null && !waitingForRespawn)
        {
            waitingForRespawn = true;
            Invoke(nameof(SpawnBall), respawnDelay);
        }
    }

    private void SpawnBall()
    {
        currentBall = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
        waitingForRespawn = false;
    }
}