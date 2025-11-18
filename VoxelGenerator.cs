// using UnityEngine;
// using System.Collections.Generic;

// public class VoxelGenerator : MonoBehaviour
// {
//     [Header("Voxel Settings")]
//     public GameObject cellPrefab;
//     public GameObject zebrafishModel;
//     public float voxelSpacing = 0.5f;
//     public int maxVoxels = 50000; // Prevent crash
//     public float growthRate = 0.1f; // For simple growth test

//     private List<GameObject> voxels = new List<GameObject>();
//     private MeshCollider fishCollider;

//     void Start()
//     {
//         if (cellPrefab == null || zebrafishModel == null)
//         {
//             Debug.LogError("Missing references: Assign CellPrefab and ZebrafishModel.");
//             return;
//         }

//         fishCollider = zebrafishModel.GetComponent<MeshCollider>();
//         if (fishCollider == null)
//         {
//             Debug.LogError("ZebrafishModel needs a MeshCollider.");
//             return;
//         }

//         Debug.Log("Voxelizing zebrafish model...");
//         GenerateVoxelsInsideFish();
//         Debug.Log("Voxelization complete. Voxels: " + voxels.Count);
//     }

//     void GenerateVoxelsInsideFish()
//     {
//         Bounds bounds = fishCollider.bounds;
//         int count = 0;

//         for (float x = bounds.min.x; x <= bounds.max.x; x += voxelSpacing)
//         {
//             for (float y = bounds.min.y; y <= bounds.max.y; y += voxelSpacing)
//             {
//                 for (float z = bounds.min.z; z <= bounds.max.z; z += voxelSpacing)
//                 {
//                     if (count > maxVoxels) return;

//                     Vector3 point = new Vector3(x, y, z);

//                     // Check if the point is inside the fish
//                     if (IsPointInsideMesh(point))
//                     {
//                         GameObject voxel = Instantiate(cellPrefab, point, Quaternion.identity, transform);
//                         voxel.transform.localScale = Vector3.one * voxelSpacing * 0.9f;
//                         voxel.name = $"Cell_{count}";
//                         voxels.Add(voxel);
//                         count++;
//                     }
//                 }
//             }
//         }
//     }

//     // Simple inside-mesh check using raycasts
//     bool IsPointInsideMesh(Vector3 point)
//     {
//         Vector3 direction = Vector3.up;
//         Ray ray = new Ray(point, direction);
//         int hitCount = 0;

//         RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
//         foreach (RaycastHit hit in hits)
//         {
//             if (hit.collider == fishCollider)
//                 hitCount++;
//         }

//         return hitCount % 2 == 1; // odd = inside
//     }


// }
