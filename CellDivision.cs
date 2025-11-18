// using UnityEngine;

// public class CellDivision : MonoBehaviour
// {
//     public GameObject cellPrefab;     // sphere prefab
//     public float stretchDuration = 1f;
//     public float stretchFactor = 2f;
//     public float divisionDelay = 0.5f;
//     public int maxGenerations = 100;
//     public int maxCells = 1000;
//     public float moveDistance = 1.5f;

//     private static int cellCount = 0;
//     private float timer;
//     private bool stretching = true;
//     private bool divided = false;
//     private int generation = 0;
//     private Vector3 originalScale;
//     [SerializeField] private Vector3 stretchAxis = Vector3.right; // Default to X for the first cell

//     void Start()
//     {
//         originalScale = transform.localScale;
//         timer = 0f;
//         cellCount++;

//         // Only set axis for the very first cell (generation 0)
//         if (generation == 0)
//             stretchAxis = Vector3.right;

//         if (cellPrefab == null)
//         {
//             Debug.LogError("CellDivision: cellPrefab is not assigned!", this);
//         }
//     }

//     void Update()
//     {
//         timer += Time.deltaTime;

//         // 1. Stretch phase
//         if (stretching)
//         {
//             float t = Mathf.Clamp01(timer / stretchDuration);
//             Vector3 scale = originalScale + stretchAxis * (stretchFactor - 1) * originalScale.x * t;
//             transform.localScale = new Vector3(
//                 Mathf.Abs(scale.x),
//                 Mathf.Abs(scale.y),
//                 Mathf.Abs(scale.z)
//             );

//             if (t >= 1f)
//             {
//                 stretching = false;
//                 timer = 0f;
//             }
//         }
//         // 2. Wait then divide
//         else if (!divided && timer >= divisionDelay)
//         {
//             Divide();
//         }
//     }

//     void Divide()
//     {
//         divided = true;

//         if (generation >= maxGenerations || cellCount + 2 > maxCells)
//         {
//             Debug.Log("Max generations or cells reached. Destroying: " + gameObject.name);
//             Destroy(gameObject);
//             return;
//         }

//         if (cellPrefab == null)
//         {
//             Debug.LogError("CellDivision: cellPrefab is not assigned!", this);
//             Destroy(gameObject);
//             return;
//         }

//         // Opposite axis for daughters
//         Vector3 nextAxis = (stretchAxis == Vector3.right) ? Vector3.up : Vector3.right;

//         for (int i = 0; i < 2; i++)
//         {
//             // Offset along the current stretch axis, in opposite directions
//             Vector3 offset = (i == 0 ? 1 : -1) * stretchAxis * moveDistance;
//             GameObject child = Instantiate(cellPrefab, transform.position + offset, Quaternion.identity);

//             if (child == null)
//             {
//                 Debug.LogError("CellDivision: Failed to instantiate cellPrefab!", this);
//                 continue;
//             }

//             child.transform.localScale = originalScale;
//             child.transform.rotation = Quaternion.identity;

//             var division = child.AddComponent<CellDivision>();
//             division.cellPrefab = cellPrefab;
//             division.stretchDuration = stretchDuration;
//             division.stretchFactor = stretchFactor;
//             division.divisionDelay = divisionDelay;
//             division.maxGenerations = maxGenerations;
//             division.maxCells = maxCells;
//             division.moveDistance = moveDistance;
//             division.generation = generation + 1;
//             division.stretchAxis = nextAxis; // alternate axis!
//         }

//         Debug.Log("Destroying parent cell: " + gameObject.name);
//         Destroy(gameObject);
//     }
// }
using System;
using System.Collections;
using UnityEngine;

public class CellDivision : MonoBehaviour
{
    public static Action<CellDivision, GameObject[]> OnDivided; // event raised when this cell divides

    public GameObject cellPrefab;     // sphere prefab (used when creating children)
    public float stretchDuration = 1f;
    public float stretchFactor = 2f;
    public float divisionDelay = 0.5f;
    public int maxGenerations = 100;
    public int maxCells = 1000;
    public float moveDistance = 1.5f;

    private static int cellCount = 0;
    private float timer;
    private bool stretching = false;   // start disabled; triggered by StartDivisionSequence()
    private bool divided = false;
    public int generation = 0;
    private Vector3 originalScale;
    [SerializeField] public Vector3 stretchAxis = Vector3.right; // Default to X for generation 0

    void Start()
    {
        originalScale = transform.localScale;
        timer = 0f;
        cellCount++;

        if (generation == 0)
            stretchAxis = Vector3.right;

        if (cellPrefab == null)
            Debug.LogWarning("CellDivision: cellPrefab not assigned on " + name, this);
    }

    void Update()
    {
        // animation runs only when stretching flag set
        if (!stretching || divided) return;

        timer += Time.deltaTime;

        // stretch over stretchDuration
        float t = Mathf.Clamp01(timer / stretchDuration);
        Vector3 scale = originalScale + stretchAxis * (stretchFactor - 1f) * originalScale.x * t;
        transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        // when finished stretching, wait divisionDelay then divide
        if (t >= 1f && timer >= stretchDuration + divisionDelay)
        {
            stretching = false;
            timer = 0f;
            StartCoroutine(DoDivide()); // run divide slightly delayed so animation completes
        }
    }

    // Public API: start the stretch->divide sequence. Safe to call multiple times (will ignore if already dividing).
    public void StartDivisionSequence()
    {
        if (divided || stretching) return;
        stretching = true;
        timer = 0f;
    }

    IEnumerator DoDivide()
    {
        // small buffer to ensure final frame of animation visible
        yield return null;
        Divide();
    }

    // Keep Divide() internal — manager should call StartDivisionSequence() to use animation.
    void Divide()
    {
        if (divided) return;
        divided = true;

        if (generation >= maxGenerations || cellCount + 2 > maxCells)
        {
            Destroy(gameObject);
            return;
        }

        // ensure prefab exists
        if (cellPrefab == null)
        {
            Debug.LogError("CellDivision: cellPrefab is not assigned for " + name, this);
            Destroy(gameObject);
            return;
        }

        Vector3 nextAxis = (stretchAxis == Vector3.right) ? Vector3.up : Vector3.right;
        GameObject[] created = new GameObject[2];

        for (int i = 0; i < 2; i++)
        {
            Vector3 offset = (i == 0 ? 1f : -1f) * stretchAxis * moveDistance;
            GameObject child = Instantiate(cellPrefab, transform.position + offset, Quaternion.identity);

            if (child == null) continue;

            // ensure child has a CellDivision component (if prefab already included it, use that; otherwise add one)
            CellDivision cd = child.GetComponent<CellDivision>();
            if (cd == null) cd = child.AddComponent<CellDivision>();

            // copy important settings
            cd.cellPrefab = this.cellPrefab;
            cd.stretchDuration = this.stretchDuration;
            cd.stretchFactor = this.stretchFactor;
            cd.divisionDelay = this.divisionDelay;
            cd.maxGenerations = this.maxGenerations;
            cd.maxCells = this.maxCells;
            cd.moveDistance = this.moveDistance;
            cd.generation = this.generation + 1;
            cd.stretchAxis = nextAxis;
            cd.originalScale = child.transform.localScale; // keep child's default scale

            created[i] = child;
        }

        // notify listeners (before destroying parent)
        OnDivided?.Invoke(this, created);

        Destroy(gameObject);
    }
}