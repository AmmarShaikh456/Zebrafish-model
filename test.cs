// ...existing code...
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CellGrowthManager : MonoBehaviour
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
    public float moveThreshold = 0.2f;
    public float divisionThreshold = 0.5f;

    private List<GameObject> allCells = new List<GameObject>();
    private List<CellDivision> pending = new List<CellDivision>();
    private Dictionary<GameObject, Brain> cellBrains = new Dictionary<GameObject, Brain>();
    private Vector3[] targets;
    private bool[] targetOccupied;
    private int totalCells;

    // NEW: keep track of cells that are "settled" (in right spot) and should not divide/move
    private HashSet<GameObject> settledCells = new HashSet<GameObject>();

    public enum ShapeType { Ring, Sphere }

    void OnEnable() => CellDivision.OnDivided += HandleDivided;
    void OnDisable() => CellDivision.OnDivided -= HandleDivided;

    void Start()
    {
        totalCells = (int)Mathf.Pow(2, generations);
        targets = GenerateShapeTargets(shape, totalCells, shapeRadius);
        targetOccupied = new bool[totalCells];

        GameObject root = Instantiate(cellPrefab, transform.position, Quaternion.identity, transform);
        SetCellVisible(root, true);
        allCells.Add(root);

        // place root at first target and mark settled (root shouldn't divide/move further unless you reset)
        root.transform.position = targets[0];
        targetOccupied[0] = true;
        settledCells.Add(root); // once in right spot -> stop dividing/moving until overall finish

        var cd = root.GetComponent<CellDivision>();
        if (cd == null) cd = root.AddComponent<CellDivision>();
        cd.cellPrefab = cellPrefab;
        cd.stretchAxis = Vector3.right;
        cd.moveDistance = shapeRadius / generations;

        // only add to pending if root is not settled
        if (!settledCells.Contains(root))
            pending.Add(cd);

        Brain rootBrain = new Brain(new int[] { 4, 4, 3 });
        cellBrains[root] = rootBrain;

        StartCoroutine(GrowRoutine());
    }

    IEnumerator GrowRoutine()
    {
        int currentGen = 0;
        // Phase 1: divide until enough cells exist
        while (allCells.Count < totalCells)
        {
            var dividingNow = new List<CellDivision>(pending);
            pending.Clear();

            foreach (var parent in dividingNow)
            {
                // skip settled parents
                if (parent == null || settledCells.Contains(parent.gameObject)) continue;

                int targetIdx = FindNearestEmptyTarget(parent.transform.position);
                if (targetIdx < 0) continue;
                DivideCell(parent, targetIdx);
            }

            // wait for animation to finish
            yield return new WaitForSeconds(dividingNow.Count > 0 ? dividingNow[0].stretchDuration + dividingNow[0].divisionDelay + 0.01f : 0.5f);
            currentGen++;
        }

        // Phase 2: priority steering & differentiation
        while (pending.Count > 0)
        {
            var dividingNow = new List<CellDivision>(pending);
            pending.Clear();

            foreach (var parent in dividingNow)
            {
                if (parent == null || settledCells.Contains(parent.gameObject)) continue; // settled are inert

                int targetIdx = FindNearestEmptyTarget(parent.transform.position);
                if (targetIdx < 0) continue;

                float distToTarget = Vector3.Distance(parent.transform.position, targets[targetIdx]);

                if (distToTarget <= moveThreshold)
                {
                    // move parent to its assigned spot and mark settled (no further divide/move)
                    MoveCell(parent.gameObject, targetIdx);
                    settledCells.Add(parent.gameObject);
                    continue;
                }

                // otherwise divide toward target (outer cells / progenitors)
                DivideCell(parent, targetIdx);
            }

            yield return new WaitForSeconds(dividingNow.Count > 0 ? dividingNow[0].stretchDuration + dividingNow[0].divisionDelay + 0.01f : 0.5f);
        }

        // Growth finished (either all targets filled or no pending). settledCells remain inert.
    }

    // Helper to divide a cell and stretch toward its target
    void DivideCell(CellDivision cellDiv, int targetIdx)
    {
        if (cellDiv == null || settledCells.Contains(cellDiv.gameObject)) return;
        Vector3 toTarget = (targets[targetIdx] - cellDiv.transform.position).normalized;
        // smooth axis to reduce jank: lerp current axis toward desired direction
        cellDiv.stretchAxis = Vector3.Slerp(cellDiv.stretchAxis.normalized, toTarget, 0.9f).normalized;
        cellDiv.moveDistance = Vector3.Distance(cellDiv.transform.position, targets[targetIdx]);
        cellDiv.StartDivisionSequence();
    }

    // Helper to move a cell to its target position
    void MoveCell(GameObject cell, int targetIdx)
    {
        if (cell == null || settledCells.Contains(cell)) return; // already settled -> ignore
        StartCoroutine(MoveCellToTarget(cell, targets[targetIdx], true));
        targetOccupied[targetIdx] = true;
    }

    // Move coroutine: optionally mark settled on completion
    IEnumerator MoveCellToTarget(GameObject cell, Vector3 target, bool markSettledOnFinish = true)
    {
        if (cell == null) yield break;
        // don't move if already settled
        if (settledCells.Contains(cell)) yield break;

        Vector3 start = cell.transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / moveDuration;
            cell.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }
        cell.transform.position = target;
        if (markSettledOnFinish)
            settledCells.Add(cell);
    }

    void HandleDivided(CellDivision parent, GameObject[] children)
    {
        // parent might be settled or not; children inherit brain but will be evaluated
        Brain parentBrain = cellBrains.ContainsKey(parent.gameObject) ? cellBrains[parent.gameObject] : new Brain(new int[] { 4, 4, 3 });

        foreach (var child in children)
        {
            if (child == null) continue;
            // new child's CellDivision component
            var cd = child.GetComponent<CellDivision>();
            if (cd == null) cd = child.AddComponent<CellDivision>();
            cd.cellPrefab = cellPrefab;
            cd.moveDistance = shapeRadius / generations;

            // find nearest empty target
            int idx = FindNearestEmptyTarget(child.transform.position);
            if (idx >= 0)
            {
                float distToTarget = Vector3.Distance(child.transform.position, targets[idx]);

                // if close, move and mark settled; else snap into target and mark occupied
                if (distToTarget <= moveThreshold)
                {
                    // move child into place and mark settled when done
                    StartCoroutine(MoveCellToTarget(child, targets[idx], true));
                    targetOccupied[idx] = true;
                }
                else
                {
                    // snap to target now and mark occupied; if snapped exactly, consider settled
                    child.transform.position = targets[idx];
                    targetOccupied[idx] = true;
                    // if within threshold after snap, mark settled
                    if (Vector3.Distance(child.transform.position, targets[idx]) <= moveThreshold)
                        settledCells.Add(child);
                    else
                        pending.Add(cd); // not settled -> can be pending for further division/moves
                }
            }
            else
            {
                // no empty target -> child can float or be pending depending on policy
                if (allowFloating && !settledCells.Contains(child))
                    StartCoroutine(FloatingMove(child, parentBrain));
                // add to pending only if not settled
                if (!settledCells.Contains(child))
                    pending.Add(cd);
            }

            SetCellVisible(child, true);
            allCells.Add(child);

            // child brain = mutated copy
            Brain babyBrain = new Brain(parentBrain);
            babyBrain.Mutate();
            cellBrains[child] = babyBrain;
        }
    }

    IEnumerator FloatingMove(GameObject cell, Brain brain)
    {
        if (cell == null || settledCells.Contains(cell)) yield break;

        Vector3 start = cell.transform.position;
        Vector3 randomTarget = start + UnityEngine.Random.onUnitSphere * (shapeRadius * 0.5f);

        float[] inputs = new float[4] { start.x, start.y, start.z, 1f };
        float[] output = brain.feedforward(inputs);
        Vector3 brainDir = new Vector3(output[0], output[1], output[2]).normalized;
        Vector3 finalTarget = start + (randomTarget - start) * 0.5f + brainDir * (shapeRadius * 0.3f);

        float t = 0f;
        while (t < 1f)
        {
            // abort floating if cell becomes settled elsewhere
            if (settledCells.Contains(cell)) yield break;
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
// ...existing code...