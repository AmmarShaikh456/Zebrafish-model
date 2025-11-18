// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// Growth manager (3D)
// /// - builds an original 3D voxel target (cube + random bulges)
// /// - instantiates sphere cells where the target exists
// /// - creates a spherical damage hole (removes visible voxels)
// /// - uses CellDivision(StartDivisionSequence) + steering to regrow toward the original target
// /// </summary>
// public class model : MonoBehaviour
// {
//     [Header("Voxel / target")]
//     public GameObject cellPrefab;           // sphere prefab
//     public int sizeX = 20;
//     public int sizeY = 12;
//     public int sizeZ = 20;
//     public float spacing = 0.5f;
//     public Vector3 origin = Vector3.zero;   // world center of the grid

//     [Header("Bulges (random surface protrusions)")]
//     public int bulgeCount = 30;             // how many bulges to add
//     public int bulgeDepth = 2;              // how many voxels outward per bulge
//     public int baseInset = 2;               // inset for the base cube so bulges can protrude

//     [Header("Damage (spherical)")]
//     public Vector3 damageCenter = Vector3.zero; // world space; default at origin
//     public float damageRadius = 2.5f;           // radius of removed (visible) damage

//     [Header("Growth / steering")]
//     public int targetCells = 500;           // safety target
//     public float startDelay = 0.3f;
//     public float triggerDelay = 0.02f;      // stagger between divisions

//     // internal 3D grid
//     private GameObject[,,] cells;
//     private bool[,,] targetMask;            // the desired final shape (original target)
//     private List<GameObject> allInstances = new List<GameObject>();

//     // steering / pending list
//     private List<CellDivision> pending = new List<CellDivision>();
//     private int createdCount = 0;

//     void OnEnable() => CellDivision.OnDivided += HandleDivided;
//     void OnDisable() => CellDivision.OnDivided -= HandleDivided;

//     void Start()
//     {
//         if (cellPrefab == null)
//         {
//             Debug.LogError("model: assign cellPrefab in Inspector.", this);
//             return;
//         }

//         // allocate
//         cells = new GameObject[sizeX, sizeY, sizeZ];
//         targetMask = new bool[sizeX, sizeY, sizeZ];

//         // 1) Build original target (cube + bulges)
//         BuildTargetWithBulges();

//         // 2) Instantiate cells from target mask
//         InstantiateFromTarget();

//         // 3) Apply visible spherical damage (removes current cells but NOT targetMask)
//         DamageSphere(damageCenter == Vector3.zero ? origin : damageCenter, damageRadius);

//         // 4) enqueue survivors and start prioritized growth routine
//         EnqueueAllExistingCells();
//         StartCoroutine(GrowRoutine());
//     }

//     // Build targetMask: start with a smaller central cube then add bulges outward.
//     // Creates a base cube inset from edges and extends bulges beyond it so they are visible.
//     void BuildTargetWithBulges()
//     {
//         // clear mask
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                     targetMask[x, y, z] = false;

//         // fill base cube (inset) so bulges can extend beyond it
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

//         // center index for directional calculations
//         Vector3Int centerIdx = new Vector3Int(sizeX / 2, sizeY / 2, sizeZ / 2);

//         // add bulges by picking surface positions of the base cube and extending outward
//         for (int i = 0; i < bulgeCount; i++)
//         {
//             // pick a random position on or near the base-cube area
//             int rx = Random.Range(xMin, xMax);
//             int ry = Random.Range(yMin, yMax);
//             int rz = Random.Range(zMin, zMax);

//             // nudge randomly towards an outer face to bias surface picks
//             if (Random.value < 0.7f)
//             {
//                 if (Random.value < 0.5f) rx = (Random.value < 0.5f) ? 0 : sizeX - 1;
//                 if (Random.value < 0.5f) ry = (Random.value < 0.5f) ? 0 : sizeY - 1;
//                 if (Random.value < 0.5f) rz = (Random.value < 0.5f) ? 0 : sizeZ - 1;
//             }

//             // compute outward vector from center and step out to add voxels
//             Vector3 dir = new Vector3(rx - centerIdx.x, ry - centerIdx.y, rz - centerIdx.z);
//             if (dir == Vector3.zero) dir = Vector3.up;
//             dir.Normalize();

//             Vector3 posF = new Vector3(rx, ry, rz);
//             for (int d = 0; d < bulgeDepth; d++)
//             {
//                 Vector3 sample = posF + dir * (d + 1);
//                 Vector3Int idx = new Vector3Int(
//                     Mathf.RoundToInt(sample.x),
//                     Mathf.RoundToInt(sample.y),
//                     Mathf.RoundToInt(sample.z)
//                 );
//                 if (idx.x >= 0 && idx.x < sizeX && idx.y >= 0 && idx.y < sizeY && idx.z >= 0 && idx.z < sizeZ)
//                     targetMask[idx.x, idx.y, idx.z] = true;
//             }
//         }

//         Debug.Log($"BuildTargetWithBulges: target voxels = {CountTotalTargetVoxels()} (size {sizeX}x{sizeY}x{sizeZ}), baseInset={baseInset}, bulges={bulgeCount}");
//     }

//     // Instantiate GameObjects for every true voxel in targetMask
//     void InstantiateFromTarget()
//     {
//         int count = 0;
//         for (int x = 0; x < sizeX; x++)
//         {
//             for (int y = 0; y < sizeY; y++)
//             {
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
//             }
//         }
//         createdCount = count;
//         Debug.Log("Instantiated cells from target: " + count);
//     }

//     // Remove (destroy) any voxels whose world position lies inside a sphere (damage).
//     // targetMask remains unchanged: that is the "desired" target for regrowth.
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
//     }

//     // Put all existing CellDivision components into the pending list (initial survivors)
//     void EnqueueAllExistingCells()
//     {
//         for (int x = 0; x < sizeX; x++)
//             for (int y = 0; y < sizeY; y++)
//                 for (int z = 0; z < sizeZ; z++)
//                 {
//                     var go = cells[x, y, z];
//                     if (go == null) continue;
//                     var cd = go.GetComponent<CellDivision>();
//                     if (cd == null) cd = go.AddComponent<CellDivision>();
//                     cd.cellPrefab = cellPrefab;
//                     // set safe defaults
//                     cd.moveDistance = spacing;
//                     pending.Add(cd);
//                 }

//         Debug.Log("Enqueued initial survivors: " + pending.Count);
//     }

//     // Growth loop: dequeue best candidate (closest to missing target voxels), steer it, and trigger its division animation.
//     IEnumerator GrowRoutine()
//     {
//         yield return new WaitForSeconds(startDelay);

//         while (pending.Count > 0 && createdCount < Mathf.Min(targetCells, CountTotalTargetVoxels()))
//         {
//             // select highest-priority cell (lowest distance to nearest missing voxel)
//             int bestIdx = -1;
//             float bestScore = float.MaxValue;
//             for (int i = 0; i < pending.Count; i++)
//             {
//                 var cd = pending[i];
//                 if (cd == null) continue;
//                 float s = ComputePriority(cd);
//                 if (s < bestScore)
//                 {
//                     bestScore = s;
//                     bestIdx = i;
//                 }
//             }

//             if (bestIdx < 0) break;
//             CellDivision current = pending[bestIdx];
//             pending.RemoveAt(bestIdx);

//             if (current == null) continue;

//             // find nearest missing voxel position and set steering axis toward it
//             if (FindNearestMissingVoxelPosition(current.transform.position, out Vector3 targetWorld, out Vector3Int targetIdx))
//             {
//                 Vector3 dir = (targetWorld - current.transform.position);
//                 if (dir.sqrMagnitude < 0.0001f) dir = current.transform.right; // fallback
//                 dir.Normalize();

//                 current.stretchAxis = dir;
//                 current.moveDistance = spacing; // small step; children will be snapped on OnDivided
//             }
//             else
//             {
//                 // no missing voxels remain; stop
//                 Debug.Log("No missing voxels left to target.");
//                 break;
//             }

//             // trigger animation -> division
//             current.StartDivisionSequence();

//             // wait for expected time (stretch + delay) before continuing loop
//             yield return new WaitForSeconds(current.stretchDuration + current.divisionDelay + 0.01f);

//             // small stagger
//             yield return new WaitForSeconds(triggerDelay);
//         }

//         Debug.Log("Growth routine finished. Created: " + createdCount);
//     }

//     // event handler called by CellDivision when it created children (before parent destroyed)
//     void HandleDivided(CellDivision parent, GameObject[] children)
//     {
//         foreach (var child in children)
//         {
//             if (child == null) continue;

//             // make sure child uses same prefab reference/settings
//             var cd = child.GetComponent<CellDivision>();
//             if (cd == null) cd = child.AddComponent<CellDivision>();
//             cd.cellPrefab = cellPrefab;
//             cd.moveDistance = spacing;

//             // snap child to nearest missing voxel (if any)
//             if (FindNearestMissingVoxelIndex(child.transform.position, out Vector3Int idx))
//             {
//                 Vector3 snap = IndexToWorld(idx.x, idx.y, idx.z);
//                 child.transform.position = snap;

//                 // if there was an existing occupant at idx (unlikely) destroy it
//                 if (cells[idx.x, idx.y, idx.z] != null)
//                     Destroy(cells[idx.x, idx.y, idx.z]);

//                 cells[idx.x, idx.y, idx.z] = child;
//                 allInstances.Add(child);
//                 createdCount++;

//                 // enqueue the child for future divisions
//                 pending.Add(cd);
//             }
//             else
//             {
//                 // no missing voxel: place child where instantiated and still enqueue it (optional)
//                 allInstances.Add(child);
//                 createdCount++;
//                 pending.Add(cd);
//             }
//         }
//     }

//     // compute priority = squared distance from cell to nearest missing target voxel (lower = higher priority)
//     float ComputePriority(CellDivision cd)
//     {
//         if (cd == null) return float.MaxValue;
//         if (!FindNearestMissingVoxelPosition(cd.transform.position, out Vector3 pos, out Vector3Int _))
//             return float.MaxValue;
//         return (cd.transform.position - pos).sqrMagnitude;
//     }

//     // locate nearest missing voxel (targetMask==true but cells==null). Returns world pos and index.
//     bool FindNearestMissingVoxelPosition(Vector3 fromWorld, out Vector3 worldPos, out Vector3Int idxOut)
//     {
//         float best = float.MaxValue;
//         Vector3Int bestIdx = new Vector3Int(-1, -1, -1);
//         for (int x = 0; x < sizeX; x++)
//         {
//             for (int y = 0; y < sizeY; y++)
//             {
//                 for (int z = 0; z < sizeZ; z++)
//                 {
//                     if (!targetMask[x, y, z]) continue;         // not part of desired target
//                     if (cells[x, y, z] != null) continue;       // already filled
//                     Vector3 pos = IndexToWorld(x, y, z);
//                     float d = (pos - fromWorld).sqrMagnitude;
//                     if (d < best)
//                     {
//                         best = d;
//                         bestIdx = new Vector3Int(x, y, z);
//                     }
//                 }
//             }
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

//     // find nearest missing voxel index only
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

//     // helper: convert voxel index to world position (centered)
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

//     // Cleanup helper
//     public void ResetAll()
//     {
//         StopAllCoroutines();
//         foreach (var go in allInstances) if (go != null) Destroy(go);
//         allInstances.Clear();
//         pending.Clear();
//         cells = new GameObject[sizeX, sizeY, sizeZ];
//     }
// }