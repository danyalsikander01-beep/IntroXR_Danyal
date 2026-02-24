using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Periodic table placed to the RIGHT of the user.
/// Uses cam.right directly for placement.
/// Clears ScaleLabel to avoid overlap with atom's built-in label.
/// </summary>
public class PeriodicTableUI : MonoBehaviour
{
    [Header("Layout")]
    public float blockSize = 0.065f;
    public float blockGap = 0.006f;
    public float blockDepth = 0.025f;

    [Header("Interaction")]
    public float highlightDist = 0.08f;
    public float selectDist = 0.04f;
    public float cooldown = 1.0f;

    [Header("Colors")]
    public Color nonmetalC = new Color(0.5f, 0.9f, 0.5f);
    public Color nobleC = new Color(0.7f, 0.5f, 1f);
    public Color alkaliC = new Color(1f, 0.4f, 0.35f);
    public Color alkalineC = new Color(1f, 0.7f, 0.2f);
    public Color metalloidC = new Color(0.3f, 0.8f, 0.8f);
    public Color postTransC = new Color(0.7f, 0.7f, 0.8f);
    public Color halogenC = new Color(0.4f, 0.7f, 1f);

    static readonly int[,] Grid = {
        {0,0},{0,7},
        {1,0},{1,1},{1,2},{1,3},{1,4},{1,5},{1,6},{1,7},
        {2,0},{2,1},{2,2},{2,3},{2,4},{2,5},{2,6},{2,7},
        {3,0},{3,1}
    };
    static readonly int[] Cat = { 0, 1, 2, 3, 4, 0, 0, 0, 6, 1, 2, 3, 5, 4, 0, 0, 6, 1, 2, 3 };

    private GameObject tableRoot;
    private AtomBuilder builder;
    private TextMeshProUGUI label;
    private Transform specimenAnchor;
    private List<GameObject> blocks = new List<GameObject>();
    private List<Renderer> rends = new List<Renderer>();
    private List<Color> baseCols = new List<Color>();
    private List<Transform> tips = new List<Transform>();
    private bool tipsOK = false;
    private int hl = -1;
    private float lastSel = -10f;
    private bool vis = false;
    private ElectronGame electronGame;

    public void ShowTable(Transform anchor, AtomBuilder b, TextMeshProUGUI l)
    {
        specimenAnchor = anchor;
        builder = b;
        label = l;
        electronGame = GetComponent<ElectronGame>();

        // OVERLAP FIX: Clear the ScaleLabel text
        // The atom already has its own label from AtomBuilder.BuildElementLabel()
        // so having the ScaleUI text too causes overlap
        if (label) label.text = "";

        if (tableRoot) Destroy(tableRoot);
        tableRoot = new GameObject("PeriodicTable");

        Transform cam = Camera.main ? Camera.main.transform : null;
        if (cam)
        {
            Vector3 camPos = cam.position;

            Vector3 right = cam.right;
            right.y = 0f;
            right.Normalize();

            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            fwd.Normalize();

            // Place to the RIGHT: mostly right, tiny forward nudge
            Vector3 tablePos = camPos + right * 0.85f + fwd * 0.15f;
            tablePos.y = camPos.y - 0.10f;

            tableRoot.transform.position = tablePos;

            // +Z points AWAY from user, text at z=-0.55 faces user
            Vector3 awayFromUser = tablePos - camPos;
            awayFromUser.y = 0f;
            if (awayFromUser.sqrMagnitude > 0.001f)
                tableRoot.transform.rotation = Quaternion.LookRotation(awayFromUser.normalized, Vector3.up);

            Debug.Log("[PT] Table at " + tablePos + " | right=" + right);
        }
        else
        {
            tableRoot.transform.position = anchor.position + new Vector3(0.8f, 0, 0);
        }

        MakeTitle();
        MakeBlocks();
        vis = true;
    }

    public void HideTable()
    {
        if (tableRoot) Destroy(tableRoot);
        tableRoot = null;
        blocks.Clear(); rends.Clear(); baseCols.Clear();
        vis = false;
    }

    void Update()
    {
        if (!vis || !tableRoot) return;
        if (!tipsOK) { FindTips(); return; }

        int closest = -1;
        float cDist = float.MaxValue;

        for (int fi = tips.Count - 1; fi >= 0; fi--)
        {
            if (!tips[fi])
            {
                tips.RemoveAt(fi);
                if (tips.Count == 0) tipsOK = false;
                continue;
            }
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                if (!blocks[bi]) continue;
                float d = Vector3.Distance(tips[fi].position, blocks[bi].transform.position);
                if (d < cDist) { cDist = d; closest = bi; }
            }
        }

        if (closest >= 0 && cDist < highlightDist)
        {
            SetHL(closest);
            if (cDist < selectDist && Time.time - lastSel > cooldown)
                Select(closest);
        }
        else
        {
            ClearHL();
        }
    }

    void MakeTitle()
    {
        var t = new GameObject("Title");
        t.transform.SetParent(tableRoot.transform, false);

        float step = blockSize + blockGap;
        t.transform.localPosition = new Vector3(0f, step * 1.6f, 0f);
        t.transform.localScale = Vector3.one * 0.025f;

        var tmp = t.AddComponent<TextMeshPro>();
        tmp.text = "Select Element";
        tmp.fontSize = 8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;

        var rt = t.GetComponent<RectTransform>();
        if (rt) rt.sizeDelta = new Vector2(25f, 4f);
    }

    void MakeBlocks()
    {
        blocks.Clear(); rends.Clear(); baseCols.Clear();
        float step = blockSize + blockGap;
        float cx = 3.5f * step;
        float cy = 1.5f * step;
        Color[] cc = { nonmetalC, nobleC, alkaliC, alkalineC, metalloidC, postTransC, halogenC };

        for (int z = 0; z < 20; z++)
        {
            float x = Grid[z, 1] * step - cx;
            float y = -Grid[z, 0] * step + cy;
            Color col = cc[Cat[z]];

            var blk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blk.name = "B_" + AtomBuilder.Sym[z];
            blk.transform.SetParent(tableRoot.transform, false);
            blk.transform.localPosition = new Vector3(x, y, 0);
            blk.transform.localScale = new Vector3(blockSize, blockSize, blockDepth);

            var rn = blk.GetComponent<Renderer>();
            rn.material = MakeMat(col);
            rends.Add(rn);
            baseCols.Add(col);

            var bc = blk.GetComponent<BoxCollider>();
            if (bc) bc.isTrigger = true;

            int atomicNum = z + 1;
            int mass = AtomBuilder.Mass[z];
            string sym = AtomBuilder.Sym[z];
            string nm = AtomBuilder.Names[z];

            // Text on -Z face (faces user)
            var lbl = new GameObject("Lbl");
            lbl.transform.SetParent(blk.transform, false);
            lbl.transform.localPosition = new Vector3(0, 0, -0.55f);
            lbl.transform.localScale = Vector3.one * 0.5f;

            var tp = lbl.AddComponent<TextMeshPro>();
            tp.text = "<size=45%>" + atomicNum + "</size>\n" +
                      "<b><size=150%>" + sym + "</size></b>\n" +
                      "<size=35%>" + nm + "</size>\n" +
                      "<size=30%>" + mass + "</size>";
            tp.fontSize = 4f;
            tp.color = Color.white;
            tp.alignment = TextAlignmentOptions.Center;
            tp.overflowMode = TextOverflowModes.Overflow;
            tp.enableWordWrapping = false;

            var rt = lbl.GetComponent<RectTransform>();
            if (rt) rt.sizeDelta = new Vector2(1.4f, 1.4f);

            blocks.Add(blk);
        }
    }

    void SetHL(int i)
    {
        if (hl == i) return;
        ClearHL();
        hl = i;
        if (i < 0 || i >= rends.Count || !rends[i]) return;
        blocks[i].transform.localScale = new Vector3(
            blockSize * 1.25f, blockSize * 1.25f, blockDepth * 2f);
        var m = rends[i].material;
        Color c = baseCols[i];
        m.color = new Color(
            Mathf.Min(c.r + 0.3f, 1f),
            Mathf.Min(c.g + 0.3f, 1f),
            Mathf.Min(c.b + 0.3f, 1f));
    }

    void ClearHL()
    {
        if (hl >= 0 && hl < blocks.Count && blocks[hl])
        {
            blocks[hl].transform.localScale = new Vector3(blockSize, blockSize, blockDepth);
            if (rends[hl]) rends[hl].material.color = baseCols[hl];
        }
        hl = -1;
    }

    void Select(int idx)
    {
        lastSel = Time.time;
        int z = idx + 1;
        Debug.Log("[PT] Selected: " + AtomBuilder.Names[idx] + " Z=" + z);

        if (builder && specimenAnchor)
        {
            builder.DestroyAtom();
            var a = builder.BuildSpecificAtom(specimenAnchor, z);
            if (a) StartCoroutine(ScaleIn(a.transform, 0.4f));

            // OVERLAP FIX: Clear ScaleLabel - atom has its own label
            if (label) label.text = "";

            if (electronGame) electronGame.StartGame(z);
        }
    }

    System.Collections.IEnumerator ScaleIn(Transform t, float d)
    {
        t.localScale = Vector3.zero;
        float e = 0;
        while (e < d)
        {
            e += Time.deltaTime;
            t.localScale = Vector3.one * (1f - Mathf.Pow(1f - e / d, 3f));
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    void FindTips()
    {
        tips.Clear();
        foreach (var o in FindObjectsOfType<GameObject>())
            if (o.activeInHierarchy && o.name == "XRHand_IndexTip")
                tips.Add(o.transform);
        if (tips.Count > 0) tipsOK = true;
    }

    Material MakeMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s);
        m.color = c;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 0.15f);
        }
        return m;
    }
}