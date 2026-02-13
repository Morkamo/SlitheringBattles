using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class SnakeBodySegments : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private Transform segmentsRoot;
    [SerializeField] private Transform segmentPrefab;
    
    [Header("Setup")]
    [SerializeField] public int SnakeSegments = 5;
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
        StabilizeSegmentsRootRotation();
        CaptureHeadHistory();
        UpdateSegments();
        TrimHistory();
    }

    private void StabilizeSegmentsRootRotation()
    {
        if (segmentsRoot == null || head == null)
            return;

        segmentsRoot.localRotation = Quaternion.Inverse(head.localRotation);
    }

    private void BuildInitialBody()
    {
        ClearSegments();

        for (int i = 0; i < SnakeSegments; i++)
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
        int i = 0;
        Vector2 a = history[0];

        for (int s = 0; s < segments.Count; s++)
        {
            float targetDist = segmentSpacing * (s + 1);

            while (i + 1 < history.Count)
            {
                Vector2 b = history[i + 1];
                float d = Vector2.Distance(a, b);

                if (traveled + d >= targetDist)
                    break;

                traveled += d;
                a = b;
                i++;
            }

            Vector2 b2 = history[Mathf.Min(i + 1, history.Count - 1)];
            float segLen = Vector2.Distance(a, b2);
            float remain = targetDist - traveled;

            float t = segLen > 0.00001f ? Mathf.Clamp01(remain / segLen) : 0f;
            Vector2 exactPos = Vector2.Lerp(a, b2, t);

            var currentPos = (Vector2)segments[s].position;
            var smoothPos = Vector2.Lerp(currentPos, exactPos, positionLerp);
            segments[s].position = smoothPos;

            Vector2 tangent = GetTangentAt(i, t);

            if (tangent.sqrMagnitude > 0.0001f)
            {
                float targetAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;

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
                segmentAngles[s] = smoothed;

                segments[s].localRotation = Quaternion.Euler(0f, 0f, segmentAngles[s]);
            }
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
    
    private Vector2 GetTangentAt(int segmentIndex, float t)
    {
        int a = Mathf.Clamp(segmentIndex, 0, history.Count - 1);
        int b = Mathf.Clamp(segmentIndex + 1, 0, history.Count - 1);

        Vector2 dir = history[a] - history[b];

        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        int c = Mathf.Clamp(segmentIndex + 2, 0, history.Count - 1);
        dir = history[a] - history[c];

        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
    }
}
