using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Creates an interactive periodic table (first 20 elements) in 3D space.
/// Detects when the player's index fingertip touches a block to select it.
/// Calls AtomBuilder to spawn the selected element's atom.
///
/// SETUP: Attach to the SAME GameObject as QuantumTransition and AtomBuilder.
/// Call ShowTable() after transition completes.
/// </summary>
public class PeriodicTableUI : MonoBehaviour
{
    [Header("Table Layout")]
    public float blockSize = 0.04f;
    public float blockGap = 0.006f;
    public float blockDepth = 0.008f;
    public Vector3 tableOffset = new Vector3(0, 0.35f, 0);

    [Header("Interaction")]
    public float highlightDistance = 0.06f;
    public float selectDistance = 0.03f;
    public float selectCooldown = 1.0f;

    [Header("Category Colors")]
    public Color reactiveNonmetalColor = new Color(0.5f, 0.9f, 0.5f);
    public Color nobleGasColor = new Color(0.7f, 0.5f, 1f);
    public Color alkaliMetalColor = new Color(1f, 0.4f, 0.35f);
    public Color alkalineEarthColor = new Color(1f, 0.7f, 0.2f);
    public Color metalloidColor = new Color(0.3f, 0.8f, 0.8f);
    public Color nonmetalColor = new Color(0.4f, 0.85f, 0.4f);
    public Color halogenColor = new Color(0.4f, 0.7f, 1f);
    public Color postTransitionColor = new Color(0.7f, 0.7f, 0.8f);

    // =============================================
    //  PERIODIC TABLE DATA
    // =============================================

    // Grid positions: row, col for standard periodic table layout
    static readonly int[,] GridPositions = {
        {0, 0},  // H
        {0, 17}, // He
        {1, 0},  // Li
        {1, 1},  // Be
        {1, 12}, // B
        {1, 13}, // C
        {1, 14}, // N
        {1, 15}, // O
        {1, 16}, // F
        {1, 17}, // Ne
        {2, 0},  // Na
        {2, 1},  // Mg
        {2, 12}, // Al
        {2, 13}, // Si
        {2, 14}, // P
        {2, 15}, // S
        {2, 16}, // Cl
        {2, 17}, // Ar
        {3, 0},  // K
        {3, 1},  // Ca
    };

    // Category for each element (index into color array)
    // 0=reactive nonmetal, 1=noble gas, 2=alkali, 3=alkaline earth,
    // 4=metalloid, 5=nonmetal, 6=halogen, 7=post-transition metal
    static readonly int[] Categories = {
        0, 1, 2, 3, 4, 5, 5, 5, 6, 1,
        2, 3, 7, 4, 5, 5, 6, 1, 2, 3
    };

    // =============================================
    //  RUNTIME STATE
    // =============================================

    private GameObject tableRoot;
    private Transform specimenAnchor;
    private AtomBuilder atomBuilder;
    private TextMeshProUGUI scaleLabel;

    private List<GameObject> blockObjects = new List<GameObject>();
    private List<Renderer> blockRenderers = new List<Renderer>();
    private List<Material> originalMaterials = new List<Material>();
    private List<Transform> fingerTips = new List<Transform>();
    private bool fingerTipsFound = false;
    private int currentHighlight = -1;
    private int selectedElement = -1;
    private float lastSelectTime = -10f;
    private bool tableVisible = false;

    // =============================================
    //  PUBLIC API
    // =============================================

    /// <summary>
    /// Creates and shows the periodic table above the atom.
    /// </summary>
    public void ShowTable(Transform anchor, AtomBuilder builder, TextMeshProUGUI label)
    {
        specimenAnchor = anchor;
        atomBuilder = builder;
        scaleLabel = label;

        if (tableRoot != null) Destroy(tableRoot);

        tableRoot = new GameObject("PeriodicTable");
        tableRoot.transform.SetParent(anchor, false);
        tableRoot.transform.localPosition = tableOffset;
        tableRoot.transform.localRotation = Quaternion.identity;
        tableRoot.transform.localScale = Vector3.one;

        // Add billboard to face the player
        tableRoot.AddComponent<FaceCamera>();

        CreateBlocks();
        tableVisible = true;

        Debug.Log("[PT] Periodic table created with " + blockObjects.Count + " blocks");
    }

    public void HideTable()
    {
        if (tableRoot != null)
        {
            Destroy(tableRoot);
            tableRoot = null;
        }
        blockObjects.Clear();
        blockRenderers.Clear();
        originalMaterials.Clear();
        tableVisible = false;
    }

    // =============================================
    //  UPDATE -- Finger interaction
    // =============================================

    void Update()
    {
        if (!tableVisible || tableRoot == null) return;

        if (!fingerTipsFound)
        {
            FindFingerTips();
            return;
        }

        // Check for closest block to any fingertip
        int closest = -1;
        float closestDist = float.MaxValue;

        for (int fi = 0; fi < fingerTips.Count; fi++)
        {
            Transform tip = fingerTips[fi];
            if (tip == null) continue;

            for (int bi = 0; bi < blockObjects.Count; bi++)
            {
                if (blockObjects[bi] == null) continue;
                float dist = Vector3.Distance(tip.position, blockObjects[bi].transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = bi;
                }
            }
        }

        // Update highlight
        if (closest >= 0 && closestDist < highlightDistance)
        {
            SetHighlight(closest);

            // Select on touch
            if (closestDist < selectDistance && Time.time - lastSelectTime > selectCooldown)
            {
                SelectElement(closest);
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    // =============================================
    //  BLOCK CREATION
    // =============================================

    void CreateBlocks()
    {
        blockObjects.Clear();
        blockRenderers.Clear();
        originalMaterials.Clear();

        float step = blockSize + blockGap;

        // Center the table: 18 columns, 4 rows
        float centerX = 8.5f * step;
        float centerY = 1.5f * step;

        Color[] categoryColors = {
            reactiveNonmetalColor, nobleGasColor, alkaliMetalColor,
            alkalineEarthColor, metalloidColor, nonmetalColor,
            halogenColor, postTransitionColor
        };

        for (int z = 0; z < 20; z++)
        {
            int row = GridPositions[z, 0];
            int col = GridPositions[z, 1];

            float x = col * step - centerX;
            float y = -row * step + centerY; // negative because rows go down

            GameObject block = CreateSingleBlock(z, x, y, categoryColors[Categories[z]]);
            blockObjects.Add(block);
        }
    }

    GameObject CreateSingleBlock(int elementIndex, float x, float y, Color color)
    {
        // Create block cube
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = "Block_" + AtomBuilder.ElementSymbols[elementIndex];
        block.transform.SetParent(tableRoot.transform, false);
        block.transform.localPosition = new Vector3(x, y, 0);
        block.transform.localScale = new Vector3(blockSize, blockSize, blockDepth);

        // Set color
        Renderer rend = block.GetComponent<Renderer>();
        Material mat = CreateBlockMaterial(color);
        rend.material = mat;
        blockRenderers.Add(rend);
        originalMaterials.Add(new Material(mat)); // store copy for reset

        // Make collider a trigger for interaction
        BoxCollider bc = block.GetComponent<BoxCollider>();
        if (bc != null) bc.isTrigger = true;

        // Add text label on the front face
        int atomicNum = elementIndex + 1;
        int massNum = AtomBuilder.MassNumbers[elementIndex];
        string symbol = AtomBuilder.ElementSymbols[elementIndex];
        string elName = AtomBuilder.ElementNames[elementIndex];

        // Format: atomic number top-left, mass top-right, symbol center, name bottom
        string labelText = "<size=40%><align=left>" + atomicNum + "  " + massNum + "</align></size>\n" +
                          "<b>" + symbol + "</b>\n" +
                          "<size=30%>" + elName + "</size>";

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(block.transform, false);
        // Position slightly in front of the cube face
        labelObj.transform.localPosition = new Vector3(0, 0, -0.6f);
        labelObj.transform.localScale = Vector3.one * 0.8f;
        labelObj.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = labelText;
        tmp.fontSize = 3f;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        // Make text readable (TMP size relative to parent cube)
        RectTransform rt = labelObj.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.sizeDelta = new Vector2(1f, 1f);
        }

        return block;
    }

    // =============================================
    //  HIGHLIGHT & SELECTION
    // =============================================

    void SetHighlight(int index)
    {
        if (currentHighlight == index) return;

        ClearHighlight();
        currentHighlight = index;

        if (index >= 0 && index < blockRenderers.Count && blockRenderers[index] != null)
        {
            // Scale up and brighten
            blockObjects[index].transform.localScale = new Vector3(
                blockSize * 1.3f, blockSize * 1.3f, blockDepth * 2f);

            Material mat = blockRenderers[index].material;
            Color c = mat.color;
            mat.color = new Color(
                Mathf.Min(c.r + 0.3f, 1f),
                Mathf.Min(c.g + 0.3f, 1f),
                Mathf.Min(c.b + 0.3f, 1f));

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * 0.5f);
            }
        }
    }

    void ClearHighlight()
    {
        if (currentHighlight >= 0 && currentHighlight < blockObjects.Count)
        {
            // Reset scale
            if (blockObjects[currentHighlight] != null)
                blockObjects[currentHighlight].transform.localScale =
                    new Vector3(blockSize, blockSize, blockDepth);

            // Reset material
            if (blockRenderers[currentHighlight] != null &&
                currentHighlight < originalMaterials.Count)
            {
                blockRenderers[currentHighlight].material =
                    new Material(originalMaterials[currentHighlight]);
            }
        }
        currentHighlight = -1;
    }

    void SelectElement(int blockIndex)
    {
        int atomicNumber = blockIndex + 1;
        lastSelectTime = Time.time;
        selectedElement = atomicNumber;

        Debug.Log("[PT] Selected: " + AtomBuilder.ElementNames[blockIndex] +
                  " (Z=" + atomicNumber + ")");

        // Rebuild atom
        if (atomBuilder != null && specimenAnchor != null)
        {
            atomBuilder.DestroyAtom();
            GameObject newAtom = atomBuilder.BuildSpecificAtom(specimenAnchor, atomicNumber);

            // Quick scale-in animation
            if (newAtom != null)
                StartCoroutine(ScaleIn(newAtom.transform, 0.5f));

            // Update label
            if (scaleLabel != null)
                scaleLabel.text = "Quantum World\n" + atomBuilder.GetCurrentElementName();
        }

        // Flash the selected block
        if (blockRenderers[blockIndex] != null)
        {
            Material mat = blockRenderers[blockIndex].material;
            mat.color = Color.white;
        }
    }

    System.Collections.IEnumerator ScaleIn(Transform t, float duration)
    {
        t.localScale = Vector3.zero;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float curve = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);
            t.localScale = Vector3.one * curve;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    // =============================================
    //  FINGER TIP DETECTION
    // =============================================

    void FindFingerTips()
    {
        fingerTips.Clear();

        GameObject[] all = FindObjectsOfType<GameObject>();
        foreach (var obj in all)
        {
            if (!obj.activeInHierarchy) continue;
            if (obj.name == "XRHand_IndexTip")
            {
                fingerTips.Add(obj.transform);
            }
        }

        if (fingerTips.Count > 0)
        {
            fingerTipsFound = true;
            Debug.Log("[PT] Found " + fingerTips.Count + " index tip bones");
        }
    }

    // =============================================
    //  HELPERS
    // =============================================

    Material CreateBlockMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        mat.color = color;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.15f);
        }

        return mat;
    }
}