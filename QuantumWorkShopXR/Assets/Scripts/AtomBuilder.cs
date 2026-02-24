using UnityEngine;
using TMPro;

/// <summary>
/// Builds a 3D atom: nucleus + shells + orbitals + labels.
/// CLEAN VERSION: transparent spheres for s-orbitals, confined p-lobes, no overlap.
///
/// SETUP: Attach to the SAME GameObject as QuantumTransition.
/// </summary>
public class AtomBuilder : MonoBehaviour
{
    [Header("Nucleus Settings")]
    public float nucleusRadius = 0.04f;
    public float nucleonSize = 0.012f;

    [Header("Shell Settings")]
    public float shellBaseRadius = 0.12f;
    public float shellSpacing = 0.12f;
    public float ringLineWidth = 0.002f;

    [Header("Orbital Settings")]
    public float sOrbitalAlpha = 0.2f;
    public float pLobeFatness = 0.025f;

    [Header("Label Settings")]
    public float shellLabelSize = 6f;
    public float orbitalLabelSize = 5f;
    public float elementLabelSize = 8f;

    [Header("Nucleus Colors")]
    public Color protonColor = new Color(0.95f, 0.25f, 0.25f);
    public Color neutronColor = new Color(0.3f, 0.5f, 1f);

    [Header("Shell & Orbital Colors")]
    public Color shellRingColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
    public Color sOrbitalColor = new Color(0f, 0.9f, 1f, 0.15f);
    public Color pxColor = new Color(1f, 0.55f, 0f, 0.35f);
    public Color pyColor = new Color(0.5f, 1f, 0.2f, 0.35f);
    public Color pzColor = new Color(1f, 0.3f, 0.8f, 0.35f);
    public Color labelColor = Color.white;

    // =============================================
    //  ELEMENT DATA
    // =============================================

    public static readonly string[] ElementNames = {
        "Hydrogen","Helium","Lithium","Beryllium","Boron",
        "Carbon","Nitrogen","Oxygen","Fluorine","Neon",
        "Sodium","Magnesium","Aluminium","Silicon","Phosphorus",
        "Sulfur","Chlorine","Argon","Potassium","Calcium"
    };

    public static readonly string[] ElementSymbols = {
        "H","He","Li","Be","B","C","N","O","F","Ne",
        "Na","Mg","Al","Si","P","S","Cl","Ar","K","Ca"
    };

    static readonly int[] NeutronCounts = {
        0,2,4,5,6,6,7,8,10,10,
        12,12,14,14,16,16,18,22,20,20
    };

    public static readonly int[] MassNumbers = {
        1,4,7,9,11,12,14,16,19,20,
        23,24,27,28,31,32,35,40,39,40
    };

    public static readonly int[][] ElectronConfigs = {
        new[]{1},             new[]{2},
        new[]{2,1},           new[]{2,2},
        new[]{2,2,1},         new[]{2,2,2},
        new[]{2,2,3},         new[]{2,2,4},
        new[]{2,2,5},         new[]{2,2,6},
        new[]{2,2,6,1},       new[]{2,2,6,2},
        new[]{2,2,6,2,1},     new[]{2,2,6,2,2},
        new[]{2,2,6,2,3},     new[]{2,2,6,2,4},
        new[]{2,2,6,2,5},     new[]{2,2,6,2,6},
        new[]{2,2,6,2,6,1},   new[]{2,2,6,2,6,2},
    };

    static readonly string[] SubshellLabels = { "1s", "2s", "2p", "3s", "3p", "4s" };
    static readonly int[] SubshellShells = { 1, 2, 2, 3, 3, 4 };
    static readonly char[] SubshellTypes = { 's', 's', 'p', 's', 'p', 's' };

    private GameObject atomRoot;
    private int currentAtomicNumber = -1;
    private Material lineMaterial;
    private Transform currentParent;

    // =============================================
    //  PUBLIC API
    // =============================================

    public GameObject BuildAtom(Transform parent)
    {
        return BuildSpecificAtom(parent, Random.Range(1, 21));
    }

    public GameObject BuildSpecificAtom(Transform parent, int atomicNumber)
    {
        DestroyAtom();
        currentParent = parent;
        currentAtomicNumber = Mathf.Clamp(atomicNumber, 1, 20);

        Debug.Log("[Atom] Building: " + ElementNames[currentAtomicNumber - 1] +
                  " (Z=" + currentAtomicNumber + ")");

        atomRoot = new GameObject("Atom_" + ElementSymbols[currentAtomicNumber - 1]);
        atomRoot.transform.SetParent(parent, false);
        atomRoot.transform.localPosition = Vector3.zero;
        atomRoot.transform.localRotation = Quaternion.identity;
        atomRoot.transform.localScale = Vector3.one;

        CreateNucleus();
        CreateShellsAndOrbitals();
        CreateElementLabel();

        return atomRoot;
    }

    public void DestroyAtom()
    {
        if (atomRoot != null) Destroy(atomRoot);
        atomRoot = null;
        currentAtomicNumber = -1;
    }

    public string GetCurrentElementName()
    {
        if (currentAtomicNumber < 1 || currentAtomicNumber > 20) return "Unknown";
        return ElementSymbols[currentAtomicNumber - 1] + " - " +
               ElementNames[currentAtomicNumber - 1] +
               " (Z=" + currentAtomicNumber + ")";
    }

    public int GetCurrentAtomicNumber() { return currentAtomicNumber; }
    public Transform GetCurrentParent() { return currentParent; }

    // =============================================
    //  NUCLEUS
    // =============================================

    void CreateNucleus()
    {
        GameObject nucleusObj = new GameObject("Nucleus");
        nucleusObj.transform.SetParent(atomRoot.transform, false);

        int protons = currentAtomicNumber;
        int neutrons = NeutronCounts[currentAtomicNumber - 1];
        int total = protons + neutrons;

        if (total <= 1)
        {
            MakeNucleon(nucleusObj.transform, Vector3.zero, true, "Proton_0");
            return;
        }

        float maxR = shellBaseRadius * 0.3f;
        float adjR = Mathf.Min(nucleusRadius * Mathf.Pow((float)total / 10f, 0.33f), maxR);
        Vector3[] pos = FibSphere(total, adjR);
        Shuffle(pos);

        for (int i = 0; i < total; i++)
        {
            bool p = i < protons;
            MakeNucleon(nucleusObj.transform, pos[i], p,
                       p ? "Proton_" + i : "Neutron_" + (i - protons));
        }
    }

    void MakeNucleon(Transform parent, Vector3 lp, bool isProton, string name)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = Vector3.one * nucleonSize;
        Collider c = g.GetComponent<Collider>(); if (c) Destroy(c);
        Renderer r = g.GetComponent<Renderer>();
        if (r) r.material = MakeOpaque(isProton ? protonColor : neutronColor);
    }

    // =============================================
    //  SHELLS & ORBITALS
    // =============================================

    void CreateShellsAndOrbitals()
    {
        int[] config = ElectronConfigs[currentAtomicNumber - 1];
        bool[] shellDrawn = new bool[5];

        for (int si = 0; si < config.Length; si++)
        {
            int shell = SubshellShells[si];
            char type = SubshellTypes[si];
            string label = SubshellLabels[si];
            float r = shellBaseRadius + (shell - 1) * shellSpacing;

            if (!shellDrawn[shell])
            {
                MakeShellRing(shell, r);
                shellDrawn[shell] = true;
            }

            if (type == 's')
                MakeSOrbital(label, shell, r);
            else if (type == 'p')
                MakePOrbitals(label, shell, r);
        }
    }

    void MakeShellRing(int n, float radius)
    {
        // Horizontal ring
        GameObject r1 = new GameObject("Shell" + n + "_H");
        r1.transform.SetParent(atomRoot.transform, false);
        LineRenderer lr1 = r1.AddComponent<LineRenderer>();
        SetupLR(lr1, shellRingColor, ringLineWidth);
        MakeCircle(lr1, radius, 64);

        // Vertical ring
        GameObject r2 = new GameObject("Shell" + n + "_V");
        r2.transform.SetParent(atomRoot.transform, false);
        r2.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Color dim = shellRingColor * 0.5f; dim.a = shellRingColor.a * 0.5f;
        LineRenderer lr2 = r2.AddComponent<LineRenderer>();
        SetupLR(lr2, dim, ringLineWidth * 0.7f);
        MakeCircle(lr2, radius, 64);

        MakeLabel("n=" + n, atomRoot.transform,
                 new Vector3(radius + 0.04f, 0, 0), shellLabelSize);
    }

    // ---- S-ORBITAL: transparent sphere ----

    void MakeSOrbital(string label, int shell, float shellRadius)
    {
        GameObject sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sph.name = "Orbital_" + label;
        sph.transform.SetParent(atomRoot.transform, false);
        sph.transform.localPosition = Vector3.zero;

        // Sphere diameter = shell radius (so it fills up to the shell ring)
        float diameter = shellRadius * 2f;
        sph.transform.localScale = Vector3.one * diameter;

        Collider col = sph.GetComponent<Collider>(); if (col) Destroy(col);
        Renderer rend = sph.GetComponent<Renderer>();
        if (rend)
        {
            Color c = sOrbitalColor;
            c.a = sOrbitalAlpha;
            rend.material = MakeTransparent(c);
        }

        MakeLabel(label, atomRoot.transform,
                 new Vector3(0, shellRadius + 0.03f, 0), orbitalLabelSize);
    }

    // ---- P-ORBITALS: lobes positioned at shell radius, extending outward ----

    void MakePOrbitals(string label, int shell, float shellRadius)
    {
        GameObject pObj = new GameObject("Orbitals_" + label);
        pObj.transform.SetParent(atomRoot.transform, false);

        // Each lobe center sits at shell radius
        // Lobe extends half a shell-spacing in each direction
        float lobeHalfLen = shellSpacing * 0.35f;
        float lobeW = pLobeFatness + (shell - 1) * 0.005f;

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        Color[] colors = { pxColor, pyColor, pzColor };
        string[] ax = { "x", "y", "z" };

        for (int i = 0; i < 3; i++)
        {
            string orbName = label + ax[i];
            GameObject dbObj = new GameObject("Orbital_" + orbName);
            dbObj.transform.SetParent(pObj.transform, false);

            // Positive lobe at +axis * shellRadius
            MakeLobe(dbObj.transform, axes[i] * shellRadius, axes[i],
                    lobeHalfLen, lobeW, colors[i], "Lobe+");

            // Negative lobe at -axis * shellRadius
            Color dark = colors[i] * 0.65f; dark.a = colors[i].a;
            MakeLobe(dbObj.transform, -axes[i] * shellRadius, axes[i],
                    lobeHalfLen, lobeW, dark, "Lobe-");

            // Axis line
            float lineLen = shellRadius + lobeHalfLen + 0.01f;
            GameObject aLine = new GameObject("Axis");
            aLine.transform.SetParent(dbObj.transform, false);
            LineRenderer lr = aLine.AddComponent<LineRenderer>();
            Color lc = colors[i] * 0.3f; lc.a = 0.25f;
            SetupLR(lr, lc, 0.0012f);
            lr.loop = false; lr.positionCount = 2;
            lr.SetPosition(0, -axes[i] * lineLen);
            lr.SetPosition(1, axes[i] * lineLen);

            // Label at tip
            MakeLabel(orbName, dbObj.transform,
                     axes[i] * (shellRadius + lobeHalfLen + 0.04f), orbitalLabelSize);
        }
    }

    void MakeLobe(Transform parent, Vector3 pos, Vector3 axis,
                 float halfLen, float width, Color color, string name)
    {
        GameObject lobe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lobe.name = name;
        lobe.transform.SetParent(parent, false);
        lobe.transform.localPosition = pos;

        // Elongated ellipsoid along axis
        float stretch = halfLen * 2f;
        if (axis == Vector3.right || axis == -Vector3.right)
            lobe.transform.localScale = new Vector3(stretch, width, width);
        else if (axis == Vector3.forward || axis == -Vector3.forward)
            lobe.transform.localScale = new Vector3(width, width, stretch);
        else
            lobe.transform.localScale = new Vector3(width, stretch, width);

        Collider c = lobe.GetComponent<Collider>(); if (c) Destroy(c);
        Renderer r = lobe.GetComponent<Renderer>();
        if (r) r.material = MakeTransparent(color);
    }

    // =============================================
    //  ELEMENT LABEL
    // =============================================

    void CreateElementLabel()
    {
        if (currentAtomicNumber < 1) return;

        int[] config = ElectronConfigs[currentAtomicNumber - 1];
        int maxShell = 1;
        for (int i = 0; i < config.Length; i++)
            if (SubshellShells[i] > maxShell) maxShell = SubshellShells[i];

        float topY = shellBaseRadius + (maxShell - 1) * shellSpacing + 0.08f;

        string text = ElementSymbols[currentAtomicNumber - 1] + "\n" +
                      ElementNames[currentAtomicNumber - 1] + "\n" +
                      "Z = " + currentAtomicNumber;

        MakeLabel(text, atomRoot.transform, new Vector3(0, topY, 0), elementLabelSize);
    }

    // =============================================
    //  HELPERS
    // =============================================

    Vector3[] FibSphere(int count, float radius)
    {
        if (count <= 0) return new Vector3[0];
        if (count == 1) return new Vector3[] { Vector3.zero };
        Vector3[] pts = new Vector3[count];
        float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < count; i++)
        {
            float y = 1f - (2f * i / (float)(count - 1));
            float r = Mathf.Sqrt(Mathf.Max(0, 1f - y * y));
            float t = ga * i;
            pts[i] = new Vector3(r * Mathf.Cos(t), y, r * Mathf.Sin(t)) * radius;
        }
        return pts;
    }

    void Shuffle(Vector3[] a)
    {
        for (int i = a.Length - 1; i > 0; i--)
        { int j = Random.Range(0, i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; }
    }

    void SetupLR(LineRenderer lr, Color c, float w)
    {
        lr.useWorldSpace = false;
        lr.startWidth = w; lr.endWidth = w;
        lr.startColor = c; lr.endColor = c;
        lr.loop = true; lr.numCornerVertices = 4;
        if (lineMaterial == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("UI/Default");
            if (s) lineMaterial = new Material(s);
        }
        if (lineMaterial) { lr.material = new Material(lineMaterial); lr.material.color = c; }
    }

    void MakeCircle(LineRenderer lr, float radius, int seg)
    {
        lr.positionCount = seg; lr.loop = true;
        for (int i = 0; i < seg; i++)
        {
            float a = (float)i / seg * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
        }
    }

    void MakeLabel(string text, Transform parent, Vector3 lp, float fontSize)
    {
        GameObject g = new GameObject("Label_" + text.Replace("\n", "_"));
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = Vector3.one * 0.01f;
        TextMeshPro tmp = g.AddComponent<TextMeshPro>();
        tmp.text = text; tmp.fontSize = fontSize;
        tmp.color = labelColor; tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        g.AddComponent<FaceCamera>();
    }

    Material MakeOpaque(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s); m.color = c;
        if (m.HasProperty("_EmissionColor"))
        { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.3f); }
        return m;
    }

    Material MakeTransparent(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s)
        {
            Material m = new Material(s); m.color = c;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty("_EmissionColor"))
            { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.15f); }
            return m;
        }
        s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material fb = new Material(s); fb.color = c; return fb;
    }
}

/// <summary>
/// Billboard: faces camera forward direction so text reads correctly.
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Transform cam;
    void Start() { if (Camera.main) cam = Camera.main.transform; }
    void LateUpdate()
    {
        if (!cam) { if (Camera.main) cam = Camera.main.transform; return; }
        Vector3 fwd = cam.forward; fwd.y = 0;
        if (fwd.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }
}