using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class AtomBuilder : MonoBehaviour
{
    [Header("Nucleus")]
    public float nucleusRadius = 0.035f;
    public float nucleonSize = 0.01f;

    [Header("Shells")]
    public float shellBaseRadius = 0.10f;
    public float shellSpacing = 0.10f;
    public float ringLineWidth = 0.002f;

    [Header("Orbitals")]
    public float sAlpha = 0.12f;
    public float pLobeFat = 0.02f;

    [Header("Colors")]
    public Color protonColor = new Color(0.95f, 0.25f, 0.25f);
    public Color neutronColor = new Color(0.3f, 0.5f, 1f);
    public Color shellRingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
    public Color sColor = new Color(0f, 0.9f, 1f, 0.12f);
    public Color pxColor = new Color(1f, 0.55f, 0f, 0.3f);
    public Color pyColor = new Color(0.5f, 1f, 0.2f, 0.3f);
    public Color pzColor = new Color(1f, 0.3f, 0.8f, 0.3f);
    public Color electronColor = new Color(1f, 1f, 0.3f, 1f);
    public Color slotColor = new Color(1f, 1f, 0.3f, 0.25f);

    // Element data
    public static readonly string[] Names = {
        "Hydrogen","Helium","Lithium","Beryllium","Boron",
        "Carbon","Nitrogen","Oxygen","Fluorine","Neon",
        "Sodium","Magnesium","Aluminium","Silicon","Phosphorus",
        "Sulfur","Chlorine","Argon","Potassium","Calcium"
    };
    public static readonly string[] Sym = {
        "H","He","Li","Be","B","C","N","O","F","Ne",
        "Na","Mg","Al","Si","P","S","Cl","Ar","K","Ca"
    };
    static readonly int[] Neutrons = { 0, 2, 4, 5, 6, 6, 7, 8, 10, 10, 12, 12, 14, 14, 16, 16, 18, 22, 20, 20 };
    public static readonly int[] Mass = { 1, 4, 7, 9, 11, 12, 14, 16, 19, 20, 23, 24, 27, 28, 31, 32, 35, 40, 39, 40 };

    // Max electrons per shell: K=2, L=8, M=8, N=2 (for first 20)
    public static readonly int[] ShellMax = { 2, 8, 8, 2 };

    // Correct shell fill for each element (electrons in shell 1,2,3,4)
    public static readonly int[][] ShellConfigs = {
        new[]{1},       new[]{2},       new[]{2,1},     new[]{2,2},
        new[]{2,3},     new[]{2,4},     new[]{2,5},     new[]{2,6},
        new[]{2,7},     new[]{2,8},     new[]{2,8,1},   new[]{2,8,2},
        new[]{2,8,3},   new[]{2,8,4},   new[]{2,8,5},   new[]{2,8,6},
        new[]{2,8,7},   new[]{2,8,8},   new[]{2,8,8,1}, new[]{2,8,8,2},
    };

    static readonly string[] SubLabels = { "1s", "2s", "2p", "3s", "3p", "4s" };
    static readonly int[] SubShells = { 1, 2, 2, 3, 3, 4 };
    static readonly char[] SubTypes = { 's', 's', 'p', 's', 'p', 's' };
    static readonly int[][] EConfigs = {
        new[]{1},new[]{2},new[]{2,1},new[]{2,2},new[]{2,2,1},new[]{2,2,2},
        new[]{2,2,3},new[]{2,2,4},new[]{2,2,5},new[]{2,2,6},
        new[]{2,2,6,1},new[]{2,2,6,2},new[]{2,2,6,2,1},new[]{2,2,6,2,2},
        new[]{2,2,6,2,3},new[]{2,2,6,2,4},new[]{2,2,6,2,5},new[]{2,2,6,2,6},
        new[]{2,2,6,2,6,1},new[]{2,2,6,2,6,2},
    };

    private GameObject atomRoot;
    private int curZ = -1;
    private Material lineMat;
    private Transform curParent;

    // Shell ring GameObjects (for game collision detection)
    [HideInInspector] public List<GameObject> shellRingObjects = new List<GameObject>();
    [HideInInspector] public List<float> shellRadii = new List<float>();
    [HideInInspector] public int shellCount = 0;

    // Orbiting electrons storage
    private List<List<GameObject>> orbitingElectrons = new List<List<GameObject>>();

    // Placeholder slots for game mode
    private List<List<GameObject>> placeholderSlots = new List<List<GameObject>>();

    public GameObject BuildAtom(Transform p) { return BuildSpecificAtom(p, Random.Range(1, 21)); }

    public GameObject BuildSpecificAtom(Transform parent, int z)
    {
        DestroyAtom();
        curParent = parent;
        curZ = Mathf.Clamp(z, 1, 20);
        shellRingObjects.Clear(); shellRadii.Clear(); orbitingElectrons.Clear();
        placeholderSlots.Clear();

        atomRoot = new GameObject("Atom_" + Sym[curZ - 1]);
        atomRoot.transform.SetParent(parent, false);
        atomRoot.transform.localPosition = Vector3.zero;
        atomRoot.transform.localRotation = Quaternion.identity;
        atomRoot.transform.localScale = Vector3.one;

        BuildNucleus();
        BuildShellsOrbitals();

        int[] sc = ShellConfigs[curZ - 1];
        shellCount = sc.Length;

        // Initialize orbit and placeholder lists
        for (int i = 0; i < shellCount; i++)
        {
            orbitingElectrons.Add(new List<GameObject>());
            placeholderSlots.Add(new List<GameObject>());
        }

        BuildElementLabel();
        return atomRoot;
    }

    /// <summary>
    /// Create visible placeholder slots on each shell showing where electrons go.
    /// Call this for GAME mode (not display mode).
    /// Shows pulsing transparent yellow spheres evenly distributed around each shell.
    /// </summary>
    public void CreatePlaceholderSlots()
    {
        if (curZ < 1 || !atomRoot) return;
        int[] sc = ShellConfigs[curZ - 1];

        for (int s = 0; s < sc.Length && s < shellRadii.Count; s++)
        {
            int needed = sc[s];
            float r = shellRadii[s];

            for (int i = 0; i < needed; i++)
            {
                GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                slot.name = "Slot_S" + (s + 1) + "_" + i;
                slot.transform.SetParent(atomRoot.transform, false);
                slot.transform.localScale = Vector3.one * 0.018f;

                // Position evenly around the shell ring
                float angle = (float)i / needed * Mathf.PI * 2f;
                // Add slight tilt per electron for 3D distribution
                float tiltX = i * 12f * Mathf.Deg2Rad;
                float tiltZ = i * 8f * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(tiltX) * r * 0.15f,
                    Mathf.Sin(angle) * r
                );
                slot.transform.localPosition = pos;

                // Remove collider (we detect by distance, not collision)
                Collider c = slot.GetComponent<Collider>(); if (c) Destroy(c);

                // Transparent pulsing material
                Renderer rn = slot.GetComponent<Renderer>();
                if (rn) rn.material = MakeSlotMaterial(slotColor);

                // Add pulse component
                slot.AddComponent<SlotPulse>();

                placeholderSlots[s].Add(slot);
            }

            // Shell count label: "0/N" next to shell
            MakeShellCountLabel(s, sc[s]);
        }

        Debug.Log("[Atom] Created placeholder slots for " + sc.Length + " shells");
    }

    /// <summary>Remove one placeholder slot when an electron is correctly placed.</summary>
    public void RemovePlaceholderSlot(int shellIndex)
    {
        if (shellIndex < 0 || shellIndex >= placeholderSlots.Count) return;
        var slots = placeholderSlots[shellIndex];
        if (slots.Count > 0)
        {
            var last = slots[slots.Count - 1];
            if (last) Destroy(last);
            slots.RemoveAt(slots.Count - 1);
        }
        // Update count label
        UpdateShellCountLabel(shellIndex);
    }

    /// <summary>Add one orbiting electron to a shell visually.</summary>
    public GameObject AddElectronToShell(int shellIndex)
    {
        if (shellIndex < 0 || shellIndex >= shellRadii.Count) return null;

        float r = shellRadii[shellIndex];
        int existing = orbitingElectrons[shellIndex].Count;

        // Create glowing electron sphere
        GameObject e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        e.name = "Electron_S" + (shellIndex + 1) + "_" + existing;
        e.transform.SetParent(atomRoot.transform, false);
        e.transform.localScale = Vector3.one * 0.012f;

        Collider c = e.GetComponent<Collider>(); if (c) Destroy(c);
        Renderer rn = e.GetComponent<Renderer>();
        if (rn) rn.material = MakeEmissive(electronColor);

        // Add orbit behavior
        OrbitMotion om = e.AddComponent<OrbitMotion>();
        om.center = atomRoot.transform;
        om.radius = r;
        om.speed = 40f + shellIndex * 15f;
        om.angleOffset = existing * (360f / Mathf.Max(ShellMax[shellIndex], 1));
        om.tiltX = existing * 15f;
        om.tiltZ = existing * 10f;

        orbitingElectrons[shellIndex].Add(e);
        return e;
    }

    /// <summary>Fill all electrons for current element (for display mode).</summary>
    public void FillAllElectrons()
    {
        if (curZ < 1) return;
        int[] sc = ShellConfigs[curZ - 1];
        for (int s = 0; s < sc.Length; s++)
            for (int i = 0; i < sc[s]; i++)
                AddElectronToShell(s);
    }

    public int GetElectronsInShell(int shellIndex)
    {
        if (shellIndex < 0 || shellIndex >= orbitingElectrons.Count) return 0;
        return orbitingElectrons[shellIndex].Count;
    }

    public void DestroyAtom()
    {
        if (atomRoot) Destroy(atomRoot);
        atomRoot = null; curZ = -1;
        shellRingObjects.Clear(); shellRadii.Clear(); orbitingElectrons.Clear();
        placeholderSlots.Clear(); shellCountLabels.Clear();
    }

    public string GetCurrentElementName()
    {
        if (curZ < 1 || curZ > 20) return "Unknown";
        return Sym[curZ - 1] + " - " + Names[curZ - 1] + " (Z=" + curZ + ")";
    }
    public int GetCurrentAtomicNumber() { return curZ; }
    public Transform GetCurrentParent() { return curParent; }
    public GameObject GetAtomRoot() { return atomRoot; }

    // =============================================
    //  SHELL COUNT LABELS (for game feedback)
    // =============================================
    private List<GameObject> shellCountLabels = new List<GameObject>();

    void MakeShellCountLabel(int shellIndex, int total)
    {
        if (!atomRoot) return;
        float r = shellRadii[shellIndex];
        var g = new GameObject("ShellCount_" + shellIndex);
        g.transform.SetParent(atomRoot.transform, false);
        g.transform.localPosition = new Vector3(-r - 0.04f, 0, 0);
        g.transform.localScale = Vector3.one * 0.008f;
        var tmp = g.AddComponent<TextMeshPro>();
        tmp.text = "0/" + total;
        tmp.fontSize = 6f;
        tmp.color = new Color(1f, 1f, 0.3f);
        tmp.alignment = TextAlignmentOptions.Center;
        g.AddComponent<FaceCamera>();

        // Ensure list is big enough
        while (shellCountLabels.Count <= shellIndex) shellCountLabels.Add(null);
        shellCountLabels[shellIndex] = g;
    }

    void UpdateShellCountLabel(int shellIndex)
    {
        if (shellIndex < 0 || shellIndex >= shellCountLabels.Count) return;
        var g = shellCountLabels[shellIndex];
        if (!g) return;
        var tmp = g.GetComponent<TextMeshPro>();
        if (!tmp) return;
        int cur = GetElectronsInShell(shellIndex);
        int[] sc = ShellConfigs[curZ - 1];
        int total = (shellIndex < sc.Length) ? sc[shellIndex] : 0;
        tmp.text = cur + "/" + total;
        if (cur >= total) tmp.color = new Color(0.3f, 1f, 0.3f); // green when full
    }

    // =============================================
    //  BUILD INTERNALS
    // =============================================

    void BuildNucleus()
    {
        GameObject nObj = new GameObject("Nucleus");
        nObj.transform.SetParent(atomRoot.transform, false);
        int p = curZ, n = Neutrons[curZ - 1], t = p + n;
        if (t <= 1) { MakeNucleon(nObj.transform, Vector3.zero, true); return; }
        float maxR = shellBaseRadius * 0.25f;
        float r = Mathf.Min(nucleusRadius * Mathf.Pow(t / 10f, 0.33f), maxR);
        var pos = FibSphere(t, r); Shuffle(pos);
        for (int i = 0; i < t; i++) MakeNucleon(nObj.transform, pos[i], i < p);
    }

    void MakeNucleon(Transform par, Vector3 lp, bool isP)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.transform.SetParent(par, false); g.transform.localPosition = lp;
        g.transform.localScale = Vector3.one * nucleonSize;
        var c = g.GetComponent<Collider>(); if (c) Destroy(c);
        var rn = g.GetComponent<Renderer>();
        if (rn) rn.material = MakeOpaque(isP ? protonColor : neutronColor);
    }

    void BuildShellsOrbitals()
    {
        int[] cfg = EConfigs[curZ - 1];
        bool[] drawn = new bool[5];
        for (int si = 0; si < cfg.Length; si++)
        {
            int sh = SubShells[si]; float r = shellBaseRadius + (sh - 1) * shellSpacing;
            if (!drawn[sh])
            {
                MakeShellRing(sh, r);
                drawn[sh] = true;
            }
            if (SubTypes[si] == 's') MakeSOrbital(SubLabels[si], r);
            else if (SubTypes[si] == 'p') MakePOrbitals(SubLabels[si], sh, r);
        }
    }

    void MakeShellRing(int n, float radius)
    {
        // Horizontal ring
        var r1 = new GameObject("Shell" + n);
        r1.transform.SetParent(atomRoot.transform, false);
        var lr1 = r1.AddComponent<LineRenderer>();
        SetupLR(lr1, shellRingColor, ringLineWidth); MakeCircle(lr1, radius, 64);

        // Add sphere collider for game interaction (trigger)
        SphereCollider sc = r1.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = radius;
        sc.center = Vector3.zero;

        shellRingObjects.Add(r1);
        shellRadii.Add(radius);

        // Vertical ring
        var r2 = new GameObject("Shell" + n + "V");
        r2.transform.SetParent(atomRoot.transform, false);
        r2.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Color dim = shellRingColor * 0.4f; dim.a = 0.3f;
        var lr2 = r2.AddComponent<LineRenderer>();
        SetupLR(lr2, dim, ringLineWidth * 0.6f); MakeCircle(lr2, radius, 64);

        // Shell label
        string[] shellNames = { "K", "L", "M", "N" };
        string label = "n=" + n;
        if (n <= 4) label += " (" + shellNames[n - 1] + ")";
        MakeLabel(label, atomRoot.transform, new Vector3(radius + 0.03f, 0, 0), 5f);
    }

    void MakeSOrbital(string label, float r)
    {
        var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sph.name = "S_" + label;
        sph.transform.SetParent(atomRoot.transform, false);
        sph.transform.localScale = Vector3.one * r * 2f;
        var c = sph.GetComponent<Collider>(); if (c) Destroy(c);
        var rn = sph.GetComponent<Renderer>();
        if (rn) { Color co = sColor; co.a = sAlpha; rn.material = MakeTransparent(co); }
    }

    void MakePOrbitals(string label, int shell, float r)
    {
        var pObj = new GameObject("P_" + label);
        pObj.transform.SetParent(atomRoot.transform, false);
        float lobeHL = shellSpacing * 0.3f;
        float w = pLobeFat + (shell - 1) * 0.005f;
        Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };
        Color[] cl = { pxColor, pyColor, pzColor };
        for (int i = 0; i < 3; i++)
        {
            MakeLobe(pObj.transform, ax[i] * r, ax[i], lobeHL, w, cl[i]);
            Color dk = cl[i] * 0.6f; dk.a = cl[i].a;
            MakeLobe(pObj.transform, -ax[i] * r, ax[i], lobeHL, w, dk);
        }
    }

    void MakeLobe(Transform par, Vector3 pos, Vector3 ax, float hl, float w, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.transform.SetParent(par, false); g.transform.localPosition = pos;
        float st = hl * 2f;
        if (ax == Vector3.right || ax == -Vector3.right) g.transform.localScale = new Vector3(st, w, w);
        else if (ax == Vector3.forward || ax == -Vector3.forward) g.transform.localScale = new Vector3(w, w, st);
        else g.transform.localScale = new Vector3(w, st, w);
        var co = g.GetComponent<Collider>(); if (co) Destroy(co);
        var rn = g.GetComponent<Renderer>(); if (rn) rn.material = MakeTransparent(c);
    }

    void BuildElementLabel()
    {
        if (curZ < 1) return;
        int[] cfg = EConfigs[curZ - 1];
        int maxSh = 1;
        for (int i = 0; i < cfg.Length; i++) if (SubShells[i] > maxSh) maxSh = SubShells[i];
        float topY = shellBaseRadius + (maxSh - 1) * shellSpacing + 0.06f;
        string txt = Sym[curZ - 1] + "\n" + Names[curZ - 1] + "\nZ=" + curZ;
        MakeLabel(txt, atomRoot.transform, new Vector3(0, topY, 0), 7f);
    }

    // =============================================
    //  HELPERS
    // =============================================

    Vector3[] FibSphere(int c, float r)
    {
        if (c <= 0) return new Vector3[0];
        if (c == 1) return new[] { Vector3.zero };
        var pts = new Vector3[c]; float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < c; i++)
        {
            float y = 1f - 2f * i / (float)(c - 1);
            float ri = Mathf.Sqrt(Mathf.Max(0, 1 - y * y));
            float t = ga * i;
            pts[i] = new Vector3(ri * Mathf.Cos(t), y, ri * Mathf.Sin(t)) * r;
        }
        return pts;
    }

    void Shuffle(Vector3[] a) { for (int i = a.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var t = a[i]; a[i] = a[j]; a[j] = t; } }

    void SetupLR(LineRenderer lr, Color c, float w)
    {
        lr.useWorldSpace = false; lr.startWidth = w; lr.endWidth = w;
        lr.startColor = c; lr.endColor = c; lr.loop = true; lr.numCornerVertices = 4;
        if (!lineMat) { Shader s = Shader.Find("Sprites/Default"); if (!s) s = Shader.Find("UI/Default"); if (s) lineMat = new Material(s); }
        if (lineMat) { lr.material = new Material(lineMat); lr.material.color = c; }
    }

    void MakeCircle(LineRenderer lr, float r, int seg)
    {
        lr.positionCount = seg; lr.loop = true;
        for (int i = 0; i < seg; i++) { float a = (float)i / seg * Mathf.PI * 2f; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
    }

    void MakeLabel(string text, Transform par, Vector3 lp, float fs)
    {
        var g = new GameObject("Lbl"); g.transform.SetParent(par, false);
        g.transform.localPosition = lp; g.transform.localScale = Vector3.one * 0.01f;
        var tmp = g.AddComponent<TextMeshPro>(); tmp.text = text; tmp.fontSize = fs;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        g.AddComponent<FaceCamera>();
    }

    Material MakeOpaque(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit"); if (!s) s = Shader.Find("Standard"); if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s); m.color = c;
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.3f); }
        return m;
    }

    Material MakeTransparent(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s) { Material m = new Material(s); m.color = c; m.SetFloat("_Surface", 1f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f); m.renderQueue = 3000; m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); return m; }
        s = Shader.Find("Standard"); if (!s) s = Shader.Find("Diffuse"); return new Material(s) { color = c };
    }

    Material MakeEmissive(Color c)
    {
        Material m = MakeOpaque(c);
        if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 2f); }
        return m;
    }

    Material MakeSlotMaterial(Color c)
    {
        Material m = MakeTransparent(c);
        if (m.HasProperty("_EmissionColor"))
        { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 0.5f); }
        return m;
    }
}

public class FaceCamera : MonoBehaviour
{
    Transform cam;
    void Start() { if (Camera.main) cam = Camera.main.transform; }
    void LateUpdate()
    {
        if (!cam) { if (Camera.main) cam = Camera.main.transform; return; }
        Vector3 f = cam.forward; f.y = 0;
        if (f.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(f, Vector3.up);
    }
}

public class OrbitMotion : MonoBehaviour
{
    public Transform center;
    public float radius = 0.1f;
    public float speed = 50f;
    public float angleOffset = 0f;
    public float tiltX = 0f;
    public float tiltZ = 0f;
    private float angle;

    void Start() { angle = angleOffset; }
    void Update()
    {
        if (!center) return;
        angle += speed * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 pos = new Vector3(Mathf.Cos(rad) * radius, 0, Mathf.Sin(rad) * radius);
        Quaternion tilt = Quaternion.Euler(tiltX, 0, tiltZ);
        pos = tilt * pos;
        transform.position = center.position + pos;
    }
}

/// <summary>Pulsing transparency effect for placeholder electron slots.</summary>
public class SlotPulse : MonoBehaviour
{
    Renderer rend;
    Color baseCol;
    float phase;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend) baseCol = rend.material.color;
        phase = Random.Range(0f, Mathf.PI * 2f); // random offset so they don't all pulse together
    }

    void Update()
    {
        if (!rend) return;
        // Pulse alpha between 0.1 and 0.4
        float pulse = 0.25f + Mathf.Sin(Time.time * 2.5f + phase) * 0.15f;
        Color c = baseCol; c.a = pulse;
        rend.material.color = c;
    }
}