using UnityEngine;
using System.Collections;

public class BallSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject ballPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("Settings")]
    [SerializeField] private float respawnDelay = 1.5f;

    private bool ballOnPedestal;
    private Coroutine respawnRoutine;

    private void Start()
    {
        SpawnBall();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("PingPongBall"))
            return;

        ballOnPedestal = true;

        // Cancel any pending respawn.
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("PingPongBall"))
            return;

        ballOnPedestal = false;

        if (respawnRoutine == null)
            respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (!ballOnPedestal)
        {
            SpawnBall();
        }

        respawnRoutine = null;
    }

    private void SpawnBall()
    {
        Instantiate(ballPrefab, spawnPoint.position, spawnPoint.rotation);
    }
}