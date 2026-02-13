using System;
using UnityEngine;

public class FeedingController : MonoBehaviour
{
    [SerializeField] private SnakeBodySegments snakeBodySegments;
    [SerializeField] private Collider2D grabCollider;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Destroy(other.gameObject);
    }
}
