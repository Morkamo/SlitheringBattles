using System.Collections.Generic;
using UnityEngine;

public sealed class SnakeBodySegments2D : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform segmentsRoot;
    [SerializeField] private Transform segmentPrefab;

    [Header("Setup")]
    [SerializeField] private int initialSegments = 6;
    [SerializeField] private float segmentSpacing = 0.25f;

    [Header("History")]
    [SerializeField] private int maxHistoryPoints = 8192;

    [Header("Smoothing")]
    [SerializeField, Range(0.5f, 1f)]
    private float positionLerp = 0.85f;

    [SerializeField]
    private float segmentTurnSpeed = 720f;

    [SerializeField, Range(0.01f, 0.25f)]
    private float rotationSmoothTime = 0.07f;

    [SerializeField, Range(1, 6)]
    private int tangentWindow = 2;

    private readonly List<Transform> segments = new();
    private readonly List<Vector2> history = new();

    private readonly List<float> segmentAngles = new();
    private readonly List<float> segmentAngleVelocities = new();

    private void Awake()
    {
        if (segmentsRoot == null)
            segmentsRoot = transform;

        history.Clear();
        history.Add(head.position);

        BuildInitialBody();
    }

    private void FixedUpdate()
    {
        CaptureHeadHistory();
        UpdateSegments();
        TrimHistory();
    }

    private void LateUpdate()
    {
        if (segmentsRoot == null)
            return;

        segmentsRoot.rotation = Quaternion.identity;
    }

    private void BuildInitialBody()
    {
        ClearSegments();

        for (int i = 0; i < initialSegments; i++)
            AddSegmentInternal();
    }

    public void AddSegment()
    {
        AddSegmentInternal();
    }

    private void AddSegmentInternal()
    {
        Vector2 spawnPos = segments.Count == 0
            ? history[^1]
            : segments[^1].position;

        var seg = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, segmentsRoot);
        segments.Add(seg);

        float startAngle = segments.Count >= 2
            ? segmentAngles[^1]
            : (head != null ? head.eulerAngles.z : 0f);

        segmentAngles.Add(startAngle);
        segmentAngleVelocities.Add(0f);
    }

    private void ClearSegments()
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i] != null)
                Destroy(segments[i].gameObject);

        segments.Clear();
        segmentAngles.Clear();
        segmentAngleVelocities.Clear();
    }

    private void CaptureHeadHistory()
    {
        Vector2 headPos = head.position;
        Vector2 last = history[0];

        float dist = Vector2.Distance(last, headPos);
        if (dist < segmentSpacing * 0.25f)
            return;

        int steps = Mathf.Clamp(Mathf.FloorToInt(dist / segmentSpacing), 1, 4);

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            history.Insert(0, Vector2.Lerp(last, headPos, t));
        }
    }

    private void UpdateSegments()
    {
        if (segments.Count == 0 || history.Count < 2)
            return;

        float traveled = 0f;
        int histIndex = 0;
        Vector2 prev = history[0];

        for (int s = 0; s < segments.Count; s++)
        {
            float targetDist = segmentSpacing * (s + 1);

            while (histIndex + 1 < history.Count)
            {
                Vector2 next = history[histIndex + 1];
                float d = Vector2.Distance(prev, next);

                if (traveled + d >= targetDist)
                    break;

                traveled += d;
                prev = next;
                histIndex++;
            }

            var currentPos = (Vector2)segments[s].position;
            var smoothPos = Vector2.Lerp(currentPos, prev, positionLerp);
            segments[s].position = smoothPos;

            float targetAngle = ComputeStableTangentAngle(histIndex);
            float maxStep = segmentTurnSpeed * Time.fixedDeltaTime;

            float vel = segmentAngleVelocities[s];
            float smoothed = Mathf.SmoothDampAngle(
                segmentAngles[s],
                targetAngle,
                ref vel,
                rotationSmoothTime,
                segmentTurnSpeed,
                Time.fixedDeltaTime
            );

            segmentAngleVelocities[s] = vel;
            segmentAngles[s] = Mathf.MoveTowardsAngle(segmentAngles[s], smoothed, maxStep);

            segments[s].rotation = Quaternion.Euler(0f, 0f, segmentAngles[s]);
        }
    }

    private float ComputeStableTangentAngle(int histIndex)
    {
        int a = Mathf.Clamp(histIndex, 0, history.Count - 1);
        int b = Mathf.Clamp(histIndex + tangentWindow, 0, history.Count - 1);

        Vector2 dir = history[a] - history[b];
        if (dir.sqrMagnitude < 0.0001f)
            dir = history[0] - history[Mathf.Min(1, history.Count - 1)];

        return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
    }

    private void TrimHistory()
    {
        int required = Mathf.CeilToInt((segments.Count + 2) * (segmentSpacing / 0.1f));

        if (history.Count > required)
            history.RemoveRange(required, history.Count - required);

        if (history.Count > maxHistoryPoints)
            history.RemoveRange(maxHistoryPoints, history.Count - maxHistoryPoints);
    }
}
