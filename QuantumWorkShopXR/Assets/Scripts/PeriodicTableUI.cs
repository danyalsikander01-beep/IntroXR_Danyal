using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Interactive periodic table (first 20 elements).
/// FIXED: Positioned to the RIGHT of the atom in world space,
/// not parented to specimenAnchor (avoids overlap).
///
/// SETUP: Attach to the SAME GameObject as QuantumTransition and AtomBuilder.
/// </summary>
public class PeriodicTableUI : MonoBehaviour
{
    [Header("Table Position (relative to anchor)")]
    public float tableRightOffset = 0.55f;
    public float tableUpOffset = 0.05f;
    public float tableForwardOffset = 0f;

    [Header("Table Layout")]
    public float blockSize = 0.045f;
    public float blockGap = 0.005f;
    public float blockDepth = 0.006f;

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
    public Color postTransitionColor = new Color(0.7f, 0.7f, 0.8f);
    public Color halogenColor = new Color(0.4f, 0.7f, 1f);

    // Compact grid: 8 columns, 4 rows
    static readonly int[,] Grid = {
        {0,0},{0,7},
        {1,0},{1,1},{1,2},{1,3},{1,4},{1,5},{1,6},{1,7},
        {2,0},{2,1},{2,2},{2,3},{2,4},{2,5},{2,6},{2,7},
        {3,0},{3,1},
    };

    static readonly int[] Cat = {
        0,1,2,3,4,0,0,0,6,1,
        2,3,5,4,0,0,6,1,2,3
    };

    private GameObject tableRoot;
    private Transform specimenAnchor;
    private AtomBuilder atomBuilder;
    private TextMeshProUGUI scaleLabel;

    private List<GameObject> blocks = new List<GameObject>();
    private List<Renderer> renderers = new List<Renderer>();
    private List<Color> baseColors = new List<Color>();
    private List<Transform> tips = new List<Transform>();
    private bool tipsFound = false;
    private int highlight = -1;
    private float lastSelect = -10f;
    private bool visible = false;

    public void ShowTable(Transform anchor, AtomBuilder builder, TextMeshProUGUI label)
    {
        specimenAnchor = anchor;
        atomBuilder = builder;
        scaleLabel = label;

        if (tableRoot) Destroy(tableRoot);

        // IMPORTANT: Create as root object (NOT child of anchor)
        // so it doesn't move with the atom or get overlapped
        tableRoot = new GameObject("PeriodicTable");

        // Position to the RIGHT of the anchor in world space
        Vector3 anchorPos = anchor.position;
        tableRoot.transform.position = anchorPos +
            new Vector3(tableRightOffset, tableUpOffset, tableForwardOffset);
        tableRoot.transform.rotation = Quaternion.identity;
        tableRoot.transform.localScale = Vector3.one;

        tableRoot.AddComponent<FaceCamera>();

        CreateTitle();
        CreateBlocks();
        visible = true;

        Debug.Log("[PT] Table at world pos: " + tableRoot.transform.position);
    }

    public void HideTable()
    {
        if (tableRoot) Destroy(tableRoot);
        tableRoot = null;
        blocks.Clear(); renderers.Clear(); baseColors.Clear();
        visible = false;
    }

    void Update()
    {
        if (!visible || !tableRoot) return;

        if (!tipsFound) { FindTips(); return; }

        int closest = -1;
        float closestDist = float.MaxValue;

        for (int fi = tips.Count - 1; fi >= 0; fi--)
        {
            if (!tips[fi]) { tips.RemoveAt(fi); if (tips.Count == 0) tipsFound = false; continue; }
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                if (!blocks[bi]) continue;
                float d = Vector3.Distance(tips[fi].position, blocks[bi].transform.position);
                if (d < closestDist) { closestDist = d; closest = bi; }
            }
        }

        if (closest >= 0 && closestDist < highlightDistance)
        {
            SetHL(closest);
            if (closestDist < selectDistance && Time.time - lastSelect > selectCooldown)
                Select(closest);
        }
        else
        {
            ClearHL();
        }
    }

    void CreateTitle()
    {
        GameObject t = new GameObject("Title");
        t.transform.SetParent(tableRoot.transform, false);
        float step = blockSize + blockGap;
        t.transform.localPosition = new Vector3(0, step * 1.0f, 0);
        t.transform.localScale = Vector3.one * 0.008f;
        TextMeshPro tmp = t.AddComponent<TextMeshPro>();
        tmp.text = "Select Element"; tmp.fontSize = 6f;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
    }

    void CreateBlocks()
    {
        blocks.Clear(); renderers.Clear(); baseColors.Clear();
        float step = blockSize + blockGap;
        float cx = 3.5f * step, cy = 1.5f * step;

        Color[] cc = { reactiveNonmetalColor, nobleGasColor, alkaliMetalColor,
                       alkalineEarthColor, metalloidColor, postTransitionColor, halogenColor };

        for (int z = 0; z < 20; z++)
        {
            float x = Grid[z, 1] * step - cx;
            float y = -Grid[z, 0] * step + cy;
            Color col = cc[Cat[z]];

            GameObject blk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blk.name = "Block_" + AtomBuilder.ElementSymbols[z];
            blk.transform.SetParent(tableRoot.transform, false);
            blk.transform.localPosition = new Vector3(x, y, 0);
            blk.transform.localScale = new Vector3(blockSize, blockSize, blockDepth);

            Renderer rn = blk.GetComponent<Renderer>();
            rn.material = MakeBlockMat(col);
            renderers.Add(rn); baseColors.Add(col);

            BoxCollider bc = blk.GetComponent<BoxCollider>();
            if (bc) bc.isTrigger = true;

            // Block label
            int aN = z + 1;
            string sym = AtomBuilder.ElementSymbols[z];
            string nm = AtomBuilder.ElementNames[z];
            int mass = AtomBuilder.MassNumbers[z];

            string txt = "<size=35%>" + aN + "   " + mass + "</size>\n" +
                        "<b><size=130%>" + sym + "</size></b>\n" +
                        "<size=28%>" + nm + "</size>";

            GameObject lbl = new GameObject("Lbl");
            lbl.transform.SetParent(blk.transform, false);
            lbl.transform.localPosition = new Vector3(0, 0, -0.55f);
            lbl.transform.localScale = Vector3.one * 0.65f;

            TextMeshPro tp = lbl.AddComponent<TextMeshPro>();
            tp.text = txt; tp.fontSize = 4f; tp.color = Color.white;
            tp.alignment = TextAlignmentOptions.Center;
            tp.overflowMode = TextOverflowModes.Overflow;
            tp.fontStyle = FontStyles.Bold;
            RectTransform rt = lbl.GetComponent<RectTransform>();
            if (rt) rt.sizeDelta = new Vector2(1f, 1f);

            blocks.Add(blk);
        }
    }

    void SetHL(int i)
    {
        if (highlight == i) return;
        ClearHL(); highlight = i;
        if (i < 0 || i >= renderers.Count || !renderers[i]) return;

        blocks[i].transform.localScale = new Vector3(
            blockSize * 1.3f, blockSize * 1.3f, blockDepth * 2f);
        Material m = renderers[i].material;
        Color c = baseColors[i];
        m.color = new Color(Mathf.Min(c.r + 0.3f, 1), Mathf.Min(c.g + 0.3f, 1), Mathf.Min(c.b + 0.3f, 1));
        if (m.HasProperty("_EmissionColor"))
        { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.6f); }
    }

    void ClearHL()
    {
        if (highlight >= 0 && highlight < blocks.Count && blocks[highlight])
        {
            blocks[highlight].transform.localScale = new Vector3(blockSize, blockSize, blockDepth);
            if (renderers[highlight])
            {
                renderers[highlight].material.color = baseColors[highlight];
                if (renderers[highlight].material.HasProperty("_EmissionColor"))
                    renderers[highlight].material.SetColor("_EmissionColor", baseColors[highlight] * 0.15f);
            }
        }
        highlight = -1;
    }

    void Select(int idx)
    {
        int z = idx + 1;
        lastSelect = Time.time;

        Debug.Log("[PT] Selected: " + AtomBuilder.ElementNames[idx] + " (Z=" + z + ")");

        if (atomBuilder && specimenAnchor)
        {
            atomBuilder.DestroyAtom();
            GameObject a = atomBuilder.BuildSpecificAtom(specimenAnchor, z);
            if (a) StartCoroutine(ScaleIn(a.transform, 0.4f));
            if (scaleLabel) scaleLabel.text = "Quantum World\n" + atomBuilder.GetCurrentElementName();
        }

        if (renderers[idx]) renderers[idx].material.color = Color.white;
    }

    System.Collections.IEnumerator ScaleIn(Transform t, float dur)
    {
        t.localScale = Vector3.zero;
        float e = 0;
        while (e < dur)
        {
            e += Time.deltaTime;
            t.localScale = Vector3.one * (1f - Mathf.Pow(1f - e / dur, 3f));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    void FindTips()
    {
        tips.Clear();
        foreach (var obj in FindObjectsOfType<GameObject>())
        {
            if (obj.activeInHierarchy && obj.name == "XRHand_IndexTip")
                tips.Add(obj.transform);
        }
        if (tips.Count > 0) { tipsFound = true; Debug.Log("[PT] Tips found: " + tips.Count); }
    }

    Material MakeBlockMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s); m.color = c;
        if (m.HasProperty("_EmissionColor"))
        { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.15f); }
        return m;
    }
}