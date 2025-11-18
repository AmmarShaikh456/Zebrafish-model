// // ...existing code...
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// model.cs - modular growth manager (3D)
// /// - Organized into logical chunks: Config, Data, TargetBuilder, Instantiation, Damage, Steering/Prioritizer,
// ///   GrowthController, AutomataFallback, Utilities.
// /// - Starts from a target mask (cube + bulges), instantiates voxels, applies visible spherical damage,
// ///   then regrows toward the target using CellDivision.StartDivisionSequence() with steering.
// /// - If priority-steering can't fill remaining voxels, optional 3D automata fallback will refill based on rules.
// /// 
// /// Key assumptions:
// /// - cellPrefab is assigned in Inspector and is a visible sphere.
// /// - CellDivision component exposes StartDivisionSequence() and static event OnDivided(CellDivision, GameObject[]).
// /// </summary>
// /// 
// /// 



// public class model : MonoBehaviour
// {
//     // -------------------------
//     // CONFIGURATION (tweak here)
//     // -------------------------
//     [Header("Voxel / target")]
//     public GameObject cellPrefab;           // sphere prefab (must be visible)
//     public int sizeX = 20;
//     public int sizeY = 12;
//     public int sizeZ = 20;
//     public float spacing = 0.5f;
//     public Vector3 origin = Vector3.zero;   // world center of the grid

//     [Header("Base cube + bulges")]
//     public int baseInset = 2;               // inset for the base cube so bulges can protrude
//     public int bulgeCount = 30;             // how many bulges
//     public int bulgeDepth = 2;              // bulge extension

//     [Header("Damage")]
//     public Vector3 damageCenter = Vector3.zero; // if zero uses origin
//     public float damageRadius = 2.5f;

//     [Header("Growth / steering")]
//     public int targetCells = 500;           // safety cap
//     public float startDelay = 0.3f;
//     public float triggerDelay = 0.02f;      // stagger between divisions
//     public bool usePrioritySteering = true; // steer parents toward missing voxels
//     public bool useAutomataFallback = true; // if steering stalls, use CA to finish

//     [Header("Automata fallback rules (3D Moore)")]
//     public int automataSteps = 8;
//     public int birthThreshold = 5;
//     public int surviveMin = 4;
//     public int surviveMax = 5;

//     [Header("Runtime / debug")]
//     public bool visualizeTargetMask = false; // instantiate small gizmo spheres for mask (optional)
//     public bool usePooling = false;          // optional pooling (not implemented heavy) - reserved

//     // -------------------------
//     // INTERNAL DATA
//     // -------------------------
//     private GameObject[,,] cells;           // current placed GameObjects (null = empty)
//     private bool[,,] targetMask;            // true = voxel desired in final shape
//     private List<GameObject> allInstances = new List<GameObject>();

//     // steering queue (priority = squared distance to nearest missing voxel)
//     private List<CellDivision> pending = new List<CellDivision>();
//     private int createdCount = 0;

//     // cache of missing voxel world positions for acceleration (recomputed as needed)
//     private List<Vector3Int> missingVoxelIndices = new List<Vector3Int>();
//     private bool missingCacheDirty = true;

//     // -------------------------
//     // LIFECYCLE
//     // -------------------------
//     void OnEnable() => CellDivision.OnDivided += HandleDivided;
//     void OnDisable() => CellDivision.OnDivided -= HandleDivided;

//     void Start()
//     {
//         ValidateConfig();
//         AllocateGrids();

//         // Build target mask: base cube inset + bulges outward
//         BuildTargetWithBulges();

//         // Optionally visualize target mask (cheap, toggle only for debugging)
//         if (visualizeTargetMask) VisualizeMask();

//         // Instantiate visible cells for the target (full shape)
//         InstantiateFromTarget();

//         // Apply visible damage: removes only current visible cells; targetMask keeps desired shape
//         DamageSphere(damageCenter == Vector3.zero ? origin : damageCenter, damageRadius);

//         // Fill initial pending list with survivors and start growth controller
//         EnqueueAllExistingCells();
//         StartCoroutine(GrowthController());
//     }

//     // -------------------------
//     // VALIDATION + ALLOCATION
//     // -------------------------
//     void ValidateConfig()
//     {
//         if (cellPrefab == null)
//             Debug.LogError("model: assign cellPrefab in Inspector.", this);

//         // small safety clamps
//         sizeX = Mathf.Max(1, sizeX);
//         sizeY = Mathf.Max(1, sizeY);
//         sizeZ = Mathf.Max(1, sizeZ);
//         spacing = Mathf.Max(0.01f, spacing);
//     }

//     void AllocateGrids()
//     {
//         cells = new GameObject[sizeX, sizeY, sizeZ];
//         targetMask = new bool[sizeX, sizeY, sizeZ];
//     }

//     // -------------------------
//     // TARGET BUILDING (cube + bulges)
//     // -------------------------
//     void BuildTargetWithBulges()
//     {
//         // clear
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                     targetMask[x, y, z] = false;

//         // base cube (inset from edges so bulges are visible)
//         int xMin = Mathf.Clamp(baseInset, 0, sizeX - 1);
//         int xMax = Mathf.Clamp(sizeX - baseInset, xMin + 1, sizeX);
//         int yMin = Mathf.Clamp(baseInset, 0, sizeY - 1);
//         int yMax = Mathf.Clamp(sizeY - baseInset, yMin + 1, sizeY);
//         int zMin = Mathf.Clamp(baseInset, 0, sizeZ - 1);
//         int zMax = Mathf.Clamp(sizeZ - baseInset, zMin + 1, sizeZ);

//         for (int x = xMin; x < xMax; x++)
//             for (int y = yMin; y < yMax; y++)
//                 for (int z = zMin; z < zMax; z++)
//                     targetMask[x, y, z] = true;

//         // add bulges outward from random base-cube surface points
//         Vector3Int centerIdx = new Vector3Int(sizeX / 2, sizeY / 2, sizeZ / 2);
//         for (int i = 0; i < bulgeCount; i++)
//         {
//             int rx = Random.Range(xMin, xMax);
//             int ry = Random.Range(yMin, yMax);
//             int rz = Random.Range(zMin, zMax);

//             // bias toward outer faces sometimes
//             if (Random.value < 0.7f)
//             {
//                 if (Random.value < 0.5f) rx = (Random.value < 0.5f) ? 0 : sizeX - 1;
//                 if (Random.value < 0.5f) ry = (Random.value < 0.5f) ? 0 : sizeY - 1;
//                 if (Random.value < 0.5f) rz = (Random.value < 0.5f) ? 0 : sizeZ - 1;
//             }

//             Vector3 dir = new Vector3(rx - centerIdx.x, ry - centerIdx.y, rz - centerIdx.z);
//             if (dir == Vector3.zero) dir = Vector3.up;
//             dir.Normalize();

//             Vector3 posF = new Vector3(rx, ry, rz);
//             for (int d = 0; d < bulgeDepth; d++)
//             {
//                 Vector3 sample = posF + dir * (d + 1);
//                 Vector3Int idx = new Vector3Int(Mathf.RoundToInt(sample.x), Mathf.RoundToInt(sample.y), Mathf.RoundToInt(sample.z));
//                 if (IsInBounds(idx)) targetMask[idx.x, idx.y, idx.z] = true;
//             }
//         }

//         Debug.Log($"BuildTargetWithBulges: target voxels = {CountTotalTargetVoxels()}");
//         MarkMissingCacheDirty();
//     }

//     // -------------------------
//     // INSTANTIATION (from target mask)
//     // -------------------------
//     void InstantiateFromTarget()
//     {
//         int count = 0;
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                 {
//                     if (!targetMask[x, y, z]) continue;
//                     Vector3 pos = IndexToWorld(x, y, z);
//                     GameObject go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
//                     go.name = $"cell_{x}_{y}_{z}";
//                     cells[x, y, z] = go;
//                     allInstances.Add(go);
//                     count++;
//                 }
//         createdCount = count;
//         Debug.Log("Instantiated cells from target: " + count);
//     }

//     // -------------------------
//     // DAMAGE (visible removal only)
//     // -------------------------
//     void DamageSphere(Vector3 worldCenter, float radius)
//     {
//         float r2 = radius * radius;
//         int removed = 0;
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                 {
//                     if (cells[x, y, z] == null) continue;
//                     Vector3 pos = IndexToWorld(x, y, z);
//                     if ((pos - worldCenter).sqrMagnitude <= r2)
//                     {
//                         Destroy(cells[x, y, z]);
//                         cells[x, y, z] = null;
//                         removed++;
//                         createdCount--;
//                     }
//                 }
//         Debug.Log($"DamageSphere removed {removed} voxels (radius {radius})");
//         MarkMissingCacheDirty();
//     }

//     // -------------------------
//     // QUEUE / ENQUEUE HELPERS
//     // -------------------------
//     void EnqueueAllExistingCells()
//     {
//         pending.Clear();
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                 {
//                     var go = cells[x, y, z];
//                     if (go == null) continue;
//                     var cd = go.GetComponent<CellDivision>();
//                     if (cd == null) cd = go.AddComponent<CellDivision>();
//                     cd.cellPrefab = cellPrefab;
//                     cd.moveDistance = spacing;
//                     pending.Add(cd);
//                 }
//         Debug.Log("Enqueued initial survivors: " + pending.Count);
//     }

//     // -------------------------
//     // GROWTH CONTROLLER (main loop)
//     // -------------------------
//     IEnumerator GrowthController()
//     {
//         yield return new WaitForSeconds(startDelay);

//         // while there are missing target voxels and we haven't exceeded targetCells
//         while (HasMissingVoxels() && createdCount < Mathf.Min(targetCells, CountTotalTargetVoxels()))
//         {
//             if (pending.Count == 0)
//             {
//                 // nothing queued: optionally fallback to automata or rebuild queue from all instances
//                 if (useAutomataFallback)
//                 {
//                     RunAutomataFallback();
//                     yield break;
//                 }
//                 else
//                 {
//                     EnqueueAllExistingCells();
//                     yield return null;
//                     continue;
//                 }
//             }

//             // select best candidate if steering enabled, otherwise FIFO
//             CellDivision selected = null;
//             if (usePrioritySteering)
//             {
//                 // find candidate with smallest distance to nearest missing voxel
//                 int bestIdx = -1;
//                 float bestScore = float.MaxValue;
//                 for (int i = 0; i < pending.Count; i++)
//                 {
//                     var cd = pending[i];
//                     if (cd == null) continue;
//                     float s = ComputePriority(cd);
//                     if (s < bestScore)
//                     {
//                         bestScore = s;
//                         bestIdx = i;
//                     }
//                 }
//                 if (bestIdx >= 0)
//                 {
//                     selected = pending[bestIdx];
//                     pending.RemoveAt(bestIdx);
//                 }
//             }
//             else
//             {
//                 // simple FIFO
//                 selected = pending[0];
//                 pending.RemoveAt(0);
//             }

//             if (selected == null) { yield return null; continue; }

//             // steer selected parent toward nearest missing voxel
//             if (FindNearestMissingVoxelPosition(selected.transform.position, out Vector3 missingWorld, out Vector3Int missingIdx))
//             {
//                 Vector3 dir = (missingWorld - selected.transform.position);
//                 if (dir.sqrMagnitude < 1e-6f) dir = selected.transform.right;
//                 dir.Normalize();

//                 // set stretchAxis (CellDivision supports arbitrary Vector3 axis) and step distance
//                 selected.stretchAxis = dir;
//                 selected.moveDistance = spacing; // small movement; child will snap to voxel
//             }

//             // trigger animated division
//             selected.StartDivisionSequence();

//             // wait for roughly animation + divisionDelay; the OnDivided handler will enqueue children
//             yield return new WaitForSeconds(selected.stretchDuration + selected.divisionDelay + 0.01f);

//             // small stagger for visual clarity
//             yield return new WaitForSeconds(triggerDelay);
//         }

//         Debug.Log("GrowthController finished. Created: " + createdCount);
//     }

//     // -------------------------
//     // EVENT: children created by CellDivision
//     // - snap children into nearest missing voxels if possible
//     // - add children to pending list for continued growth
//     // -------------------------
//     void HandleDivided(CellDivision parent, GameObject[] children)
//     {
//         foreach (var child in children)
//         {
//             if (child == null) continue;

//             var cd = child.GetComponent<CellDivision>();
//             if (cd == null) cd = child.AddComponent<CellDivision>();
//             cd.cellPrefab = cellPrefab;
//             cd.moveDistance = spacing;

//             // snap child to nearest missing voxel index
//             if (FindNearestMissingVoxelIndex(child.transform.position, out Vector3Int idx))
//             {
//                 Vector3 snap = IndexToWorld(idx.x, idx.y, idx.z);
//                 child.transform.position = snap;

//                 // replace any existing occupant (should be null)
//                 if (cells[idx.x, idx.y, idx.z] != null)
//                     Destroy(cells[idx.x, idx.y, idx.z]);

//                 cells[idx.x, idx.y, idx.z] = child;
//                 allInstances.Add(child);
//                 createdCount++;

//                 // mark cache update
//                 MarkMissingCacheDirty();

//                 // enqueue child to continue growth
//                 pending.Add(cd);
//             }
//             else
//             {
//                 // no missing voxel: keep child at spawn pos and still enqueue (optional)
//                 allInstances.Add(child);
//                 createdCount++;
//                 pending.Add(cd);
//             }
//         }
//     }

//     // -------------------------
//     // PRIORITY & MISSING-VOXEL SEARCH
//     // -------------------------
//     // compute priority: squared distance from cd to nearest missing voxel (lower = higher priority)
//     float ComputePriority(CellDivision cd)
//     {
//         if (cd == null) return float.MaxValue;
//         if (!FindNearestMissingVoxelPosition(cd.transform.position, out Vector3 pos, out Vector3Int _))
//             return float.MaxValue;
//         return (cd.transform.position - pos).sqrMagnitude;
//     }

//     bool HasMissingVoxels()
//     {
//         if (missingCacheDirty) RebuildMissingCache();
//         return missingVoxelIndices.Count > 0;
//     }

//     void MarkMissingCacheDirty() => missingCacheDirty = true;

//     // Build list of indices where targetMask==true and cells==null
//     void RebuildMissingCache()
//     {
//         missingVoxelIndices.Clear();
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                     if (targetMask[x, y, z] && cells[x, y, z] == null)
//                         missingVoxelIndices.Add(new Vector3Int(x, y, z));
//         missingCacheDirty = false;
//     }

//     // nearest missing voxel (returns world pos and index)
//     bool FindNearestMissingVoxelPosition(Vector3 fromWorld, out Vector3 worldPos, out Vector3Int idxOut)
//     {
//         if (missingCacheDirty) RebuildMissingCache();
//         if (missingVoxelIndices.Count == 0)
//         {
//             idxOut = new Vector3Int(-1, -1, -1);
//             worldPos = Vector3.zero;
//             return false;
//         }

//         float best = float.MaxValue;
//         Vector3Int bestIdx = new Vector3Int(-1, -1, -1);
//         for (int i = 0; i < missingVoxelIndices.Count; i++)
//         {
//             Vector3Int idx = missingVoxelIndices[i];
//             Vector3 pos = IndexToWorld(idx.x, idx.y, idx.z);
//             float d = (pos - fromWorld).sqrMagnitude;
//             if (d < best) { best = d; bestIdx = idx; }
//         }

//         if (bestIdx.x >= 0)
//         {
//             idxOut = bestIdx;
//             worldPos = IndexToWorld(bestIdx.x, bestIdx.y, bestIdx.z);
//             return true;
//         }

//         idxOut = new Vector3Int(-1, -1, -1);
//         worldPos = Vector3.zero;
//         return false;
//     }

//     bool FindNearestMissingVoxelIndex(Vector3 fromWorld, out Vector3Int idxOut)
//     {
//         if (FindNearestMissingVoxelPosition(fromWorld, out _, out Vector3Int idx))
//         {
//             idxOut = idx;
//             return true;
//         }
//         idxOut = new Vector3Int(-1, -1, -1);
//         return false;
//     }

//     // -------------------------
//     // AUTOMATA FALLBACK (3D Moore neighborhood)
//     // -------------------------
//     void RunAutomataFallback()
//     {
//         Debug.Log("Running automata fallback...");
//         for (int step = 0; step < automataSteps; step++)
//         {
//             // simple boolean next mask
//             bool[,,] next = new bool[sizeX, sizeY, sizeZ];

//             for (int x = 0; x < sizeX; x++)
//                 for (int y = 0; y < sizeY; y++)
//                     for (int z = 0; z < sizeZ; z++)
//                     {
//                         int neighbors = CountNeighbors3D(x, y, z);
//                         bool alive = cells[x, y, z] != null;

//                         // birth only allowed where targetMask says so
//                         if (!alive && targetMask[x, y, z] && neighbors >= birthThreshold)
//                         {
//                             // spawn cell
//                             Vector3 pos = IndexToWorld(x, y, z);
//                             GameObject go = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
//                             go.name = $"cell_auto_{x}_{y}_{z}";
//                             cells[x, y, z] = go;
//                             allInstances.Add(go);
//                             createdCount++;
//                             next[x, y, z] = true;
//                         }
//                         else if (alive && (neighbors < surviveMin || neighbors > surviveMax))
//                         {
//                             Destroy(cells[x, y, z]);
//                             cells[x, y, z] = null;
//                             next[x, y, z] = false;
//                         }
//                         else
//                         {
//                             next[x, y, z] = alive;
//                         }
//                     }

//             // after step, rebuild missing cache
//             MarkMissingCacheDirty();
//         }
//         Debug.Log("Automata fallback finished. Created: " + createdCount);
//     }
//     // count occupied neighbors in 3D Moore neighborhood
//     //SCANS IT DOWNWARD 
    
//     int CountNeighbors3D(int cx, int cy, int cz)
//     {
//         int count = 0;
//         for (int ox = -1; ox <= 1; ox++)
//             for (int oy = -1; oy <= 1; oy++)
//                 for (int oz = -1; oz <= 1; oz++)
//                 {
//                     if (ox == 0 && oy == 0 && oz == 0) continue;
//                     int nx = cx + ox, ny = cy + oy, nz = cz + oz;
//                     if (nx >= 0 && ny >= 0 && nz >= 0 && nx < sizeX && ny < sizeY && nz < sizeZ)
//                         if (cells[nx, ny, nz] != null) count++;
//                 }
//         return count;
//     }

//     // -------------------------
//     // UTILITIES
//     // -------------------------
//     bool IsInBounds(Vector3Int idx)
//     {
//         return idx.x >= 0 && idx.y >= 0 && idx.z >= 0 && idx.x < sizeX && idx.y < sizeY && idx.z < sizeZ;
//     }

//     Vector3 IndexToWorld(int x, int y, int z)
//     {
//         return origin + new Vector3((x - sizeX / 2f) * spacing, (y - sizeY / 2f) * spacing, (z - sizeZ / 2f) * spacing);
//     }

//     int CountTotalTargetVoxels()
//     {
//         int c = 0;
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                     if (targetMask[x, y, z]) c++;
//         return c;
//     }

//     // optional simple visualization of mask (instantiates tiny debug spheres)
//     void VisualizeMask()
//     {
//         GameObject g = new GameObject("MaskViz");
//         g.transform.parent = transform;
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                     if (targetMask[x, y, z])
//                     {
//                         GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
//                         s.transform.localScale = Vector3.one * (spacing * 0.15f);
//                         s.transform.position = IndexToWorld(x, y, z);
//                         s.transform.parent = g.transform;
//                         Destroy(s.GetComponent<Collider>()); // remove collider to avoid interference
//                     }
//     }

//     // Cleanup
//     public void ResetAll()
//     {
//         StopAllCoroutines();
//         foreach (var go in allInstances) if (go != null) Destroy(go);
//         allInstances.Clear();
//         pending.Clear();
//         cells = new GameObject[sizeX, sizeY, sizeZ];
//         targetMask = new bool[sizeX, sizeY, sizeZ];
//         MarkMissingCacheDirty();
//     }
// }
// // ...existing code...