using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 1.5f;

    private GameObject currentBall;
    private float timer;
    private bool countingDown;

    private void Start()
    {
        SpawnBall();
    }

    private void Update()
    {
        // Ball has been picked up or destroyed
        if (currentBall == null)
        {
            if (!countingDown)
            {
                countingDown = true;
                timer = respawnDelay;
            }

            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                SpawnBall();
                countingDown = false;
            }
        }
    }

    private void SpawnBall()
    {
        currentBall = Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}