

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class testworks : MonoBehaviour
{
    [Header("Cell Prefab & Animation")]
    public GameObject cellPrefab;
    public int generations = 6;
    public float divisionDelay = 0.5f;
    public float moveDuration = 1.0f;
    public ShapeType shape = ShapeType.Ring;
    public float shapeRadius = 3.0f;

    [Header("Growth Logic")]
    public bool usePrioritySteering = true;
    public bool allowFloating = true;

    private List<GameObject> allCells = new List<GameObject>();
    private List<CellDivision> pending = new List<CellDivision>();
    private Dictionary<GameObject, Brain> cellBrains = new Dictionary<GameObject, Brain>();
    private Vector3[] targets;
    private bool[] targetOccupied;
    private int totalCells;

    public enum ShapeType { Ring, Sphere }

    void OnEnable() => CellDivision.OnDivided += HandleDivided;
    void OnDisable() => CellDivision.OnDivided -= HandleDivided;

    void Start()
    {
        totalCells = (int)Mathf.Pow(2, generations);
        targets = GenerateShapeTargets(shape, totalCells, shapeRadius);
        targetOccupied = new bool[totalCells];

        // 1. Start with one visible cell at the center
        GameObject root = Instantiate(cellPrefab, transform.position, Quaternion.identity, transform);
        SetCellVisible(root, true);
        allCells.Add(root);

        // Add CellDivision and configure
        var cd = root.GetComponent<CellDivision>();
        if (cd == null) cd = root.AddComponent<CellDivision>();
        cd.cellPrefab = cellPrefab;
        cd.stretchAxis = Vector3.right;
        cd.moveDistance = shapeRadius / generations;

        pending.Add(cd);

        // Give the root cell a new Brain
        Brain rootBrain = new Brain(new int[] { 4, 4, 3 });
        cellBrains[root] = rootBrain;

        // Place root at the first target
        root.transform.position = targets[0];
        targetOccupied[0] = true;

        StartCoroutine(GrowRoutine());
    }

    IEnumerator GrowRoutine()
{
    int currentGen = 0;
    while (pending.Count > 0 && allCells.Count < totalCells && currentGen < generations)
    {
        var dividingNow = new List<CellDivision>(pending);
        pending.Clear();

        foreach (var parent in dividingNow)
        {
            // Always divide if there are still unfilled targets
            int nearestIdx = FindNearestEmptyTarget(parent.transform.position);

            if (nearestIdx >= 0)
            {
                Vector3 toTarget = (targets[nearestIdx] - parent.transform.position).normalized;
                parent.stretchAxis = toTarget;
                parent.moveDistance = Vector3.Distance(parent.transform.position, targets[nearestIdx]);
                parent.StartDivisionSequence();

                // If this cell is already at a filled target, mark it as temporarily "free" to move elsewhere if needed
                // This allows the system to shuffle cells for optimal filling
                if (IsAtOccupiedTarget(parent.transform.position))
                {
                    // Temporarily mark this spot as unoccupied so a better cell can fill it later
                    int idx = FindOccupiedTargetIndex(parent.transform.position);
                    if (idx >= 0) targetOccupied[idx] = false;
                }
            }
        }

        yield return new WaitForSeconds(dividingNow.Count > 0 ? dividingNow[0].stretchDuration + dividingNow[0].divisionDelay + 0.01f : 0.5f);

        currentGen++;
    }
}

// Helper to find the index of the occupied target at a given position
int FindOccupiedTargetIndex(Vector3 pos)
{
    for (int i = 0; i < targets.Length; i++)
    {
        if (targetOccupied[i] && (targets[i] - pos).sqrMagnitude < 0.01f)
            return i;
    }
    return -1;
}
    void HandleDivided(CellDivision parent, GameObject[] children)
    {
        Brain parentBrain = cellBrains.ContainsKey(parent.gameObject) ? cellBrains[parent.gameObject] : new Brain(new int[] { 4, 4, 3 });

        foreach (var child in children)
        {
            if (child == null) continue;
            var cd = child.GetComponent<CellDivision>();
            if (cd == null) cd = child.AddComponent<CellDivision>();
            cd.cellPrefab = cellPrefab;
            cd.moveDistance = shapeRadius / generations;

            // Snap child to nearest empty target and mark as occupied
            int idx = FindNearestEmptyTarget(child.transform.position);
            if (idx >= 0)
            {
                child.transform.position = targets[idx];
                targetOccupied[idx] = true;
            }

            SetCellVisible(child, true);
            allCells.Add(child);

            // Only add to pending if not already at a filled target (so only necessary cells can divide)
            if (!IsAtOccupiedTarget(child.transform.position))
                pending.Add(cd);

            // Each child gets a mutated copy of the parent's brain
            Brain babyBrain = new Brain(parentBrain);
            babyBrain.Mutate();
            cellBrains[child] = babyBrain;

            // After division, allow floating to a new position (if enabled)
            if (allowFloating)
                StartCoroutine(FloatingMove(child, babyBrain));
        }
    }

    IEnumerator FloatingMove(GameObject cell, Brain brain)
    {
        Vector3 start = cell.transform.position;
        Vector3 randomTarget = start + UnityEngine.Random.onUnitSphere * (shapeRadius * 0.5f);

        float[] inputs = new float[4] { start.x, start.y, start.z, 1f };
        float[] output = brain.feedforward(inputs);
        Vector3 brainDir = new Vector3(output[0], output[1], output[2]).normalized;
        Vector3 finalTarget = start + (randomTarget - start) * 0.5f + brainDir * (shapeRadius * 0.3f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            cell.transform.position = Vector3.Lerp(start, finalTarget, t);
            yield return null;
        }
        cell.transform.position = finalTarget;
    }

    Vector3[] GenerateShapeTargets(ShapeType shape, int count, float radius)
    {
        Vector3[] positions = new Vector3[count];
        if (shape == ShapeType.Ring)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                positions[i] = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
            }
        }
        else if (shape == ShapeType.Sphere)
        {
            float offset = 2f / count;
            float increment = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = ((i * offset) - 1) + (offset / 2);
                float r = Mathf.Sqrt(1 - y * y);
                float phi = i * increment;
                positions[i] = transform.position + new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r) * radius;
            }
        }
        return positions;
    }

    int FindNearestEmptyTarget(Vector3 pos)
    {
        float best = float.MaxValue;
        int idx = -1;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targetOccupied[i]) continue;
            float d = (targets[i] - pos).sqrMagnitude;
            if (d < best)
            {
                best = d;
                idx = i;
            }
        }
        return idx;
    }

    bool IsAtOccupiedTarget(Vector3 pos)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (!targetOccupied[i]) continue;
            if ((targets[i] - pos).sqrMagnitude < 0.01f)
                return true;
        }
        return false;
    }

    float DistanceToNearestEmptyTarget(Vector3 pos)
    {
        float best = float.MaxValue;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targetOccupied[i]) continue;
            float d = (targets[i] - pos).sqrMagnitude;
            if (d < best) best = d;
        }
        return best;
    }

    void SetCellVisible(GameObject cell, bool visible)
    {
        var rend = cell.GetComponentInChildren<Renderer>();
        if (rend != null) rend.enabled = visible;
    }
}