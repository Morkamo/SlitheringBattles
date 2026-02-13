using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class SnakeSlitherMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset actions;
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private float inputDeadzone = 0.15f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float turnSpeedDeg = 540f;

    private Rigidbody2D rb;
    private InputAction moveAction;

    private Vector2 lastAimDir = Vector2.right;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (actions != null)
        {
            var map = actions.FindActionMap(actionMapName, true);
            moveAction = map.FindAction(moveActionName, true);
        }
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.Enable();
    }

    private void OnDisable()
    {
        if (moveAction != null)
            moveAction.Disable();
    }

    private void FixedUpdate()
    {
        var input = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (input.sqrMagnitude >= inputDeadzone * inputDeadzone)
            lastAimDir = input.normalized;

        var targetAngle = Mathf.Atan2(lastAimDir.y, lastAimDir.x) * Mathf.Rad2Deg;
        var newAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, turnSpeedDeg * Time.fixedDeltaTime);

        rb.MoveRotation(newAngle);

        var forward = AngleToDir(newAngle);
        var newPos = rb.position + forward * (moveSpeed * Time.fixedDeltaTime);

        rb.MovePosition(newPos);
    }

    private static Vector2 AngleToDir(float angleDeg)
    {
        var rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }
}