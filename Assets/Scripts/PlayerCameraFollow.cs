using UnityEngine;

public sealed class PlayerCameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new(0f, 0f, -10f);
    [SerializeField, Range(0f, 30f)] private float followSharpness = 12f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        var desired = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            desired,
            1f - Mathf.Exp(-followSharpness * Time.deltaTime)
        );

        transform.rotation = Quaternion.identity;
    }
}