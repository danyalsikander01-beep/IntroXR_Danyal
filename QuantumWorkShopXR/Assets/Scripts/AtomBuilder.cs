using UnityEngine;
using TMPro;

/// <summary>
/// STEP 3 (FIXED): Nucleus + shell rings + orbital shapes.
/// Fixes:
///   - S-orbitals now sized to match their shell radius (not tiny at center)
///   - P-orbitals lobes extend from center out to shell radius (not hidden in nucleus)
///   - Labels much larger for VR readability
///
/// SETUP: Attach to the SAME GameObject as QuantumTransition.
/// </summary>
public class AtomBuilder : MonoBehaviour
{
    [Header("Nucleus Settings")]
    public float nucleusRadius = 0.06f;
    public float nucleonSize = 0.02f;

    [Header("Shell Settings")]
    public float shellBaseRadius = 0.15f;
    public float shellSpacing = 0.14f;
    public float ringLineWidth = 0.003f;

    [Header("S-Orbital Settings")]
    public float sLineWidth = 0.004f;

    [Header("P-Orbital Settings")]
    public float pLobeWidth = 0.03f;

    [Header("Label Settings")]
    public float shellLabelSize = 6f;
    public float orbitalLabelSize = 5f;
    public float elementLabelSize = 8f;

    [Header("Nucleus Colors")]
    public Color protonColor = new Color(0.95f, 0.25f, 0.25f);
    public Color neutronColor = new Color(0.3f, 0.5f, 1f);

    [Header("Shell & Orbital Colors")]
    public Color shellRingColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
    public Color sOrbitalColor = new Color(0f, 0.9f, 1f, 0.6f);
    public Color pxColor = new Color(1f, 0.55f, 0f, 0.5f);
    public Color pyColor = new Color(0.5f, 1f, 0.2f, 0.5f);
    public Color pzColor = new Color(1f, 0.3f, 0.8f, 0.5f);
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

    // Electrons per subshell in Aufbau order: 1s, 2s, 2p, 3s, 3p, 4s
    public static readonly int[][] ElectronConfigs = {
        new[]{1},             // H
        new[]{2},             // He
        new[]{2,1},           // Li
        new[]{2,2},           // Be
        new[]{2,2,1},         // B
        new[]{2,2,2},         // C
        new[]{2,2,3},         // N
        new[]{2,2,4},         // O
        new[]{2,2,5},         // F
        new[]{2,2,6},         // Ne
        new[]{2,2,6,1},       // Na
        new[]{2,2,6,2},       // Mg
        new[]{2,2,6,2,1},     // Al
        new[]{2,2,6,2,2},     // Si
        new[]{2,2,6,2,3},     // P
        new[]{2,2,6,2,4},     // S
        new[]{2,2,6,2,5},     // Cl
        new[]{2,2,6,2,6},     // Ar
        new[]{2,2,6,2,6,1},   // K
        new[]{2,2,6,2,6,2},   // Ca
    };

    static readonly string[] SubshellLabels = { "1s", "2s", "2p", "3s", "3p", "4s" };
    static readonly int[] SubshellShells = { 1, 2, 2, 3, 3, 4 };
    static readonly char[] SubshellTypes = { 's', 's', 'p', 's', 'p', 's' };

    // =============================================
    //  RUNTIME STATE
    // =============================================

    private GameObject atomRoot;
    private int currentAtomicNumber = -1;
    private Material lineMaterial;

    // =============================================
    //  PUBLIC API
    // =============================================

    public GameObject BuildAtom(Transform parent)
    {
        DestroyAtom();
        currentAtomicNumber = Random.Range(1, 21);

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
        if (atomRoot != null)
        {
            Destroy(atomRoot);
            atomRoot = null;
        }
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

        Debug.Log("[Atom] Nucleus: " + protons + "p + " + neutrons + "n");

        if (total <= 1)
        {
            CreateNucleon(nucleusObj.transform, Vector3.zero, true, "Proton_0");
            return;
        }

        float adjustedRadius = nucleusRadius * Mathf.Pow((float)total / 10f, 0.33f);
        Vector3[] positions = FibonacciSphere(total, adjustedRadius);
        Shuffle(positions);

        for (int i = 0; i < total; i++)
        {
            bool isProton = i < protons;
            string name = isProton ? "Proton_" + i : "Neutron_" + (i - protons);
            CreateNucleon(nucleusObj.transform, positions[i], isProton, name);
        }
    }

    void CreateNucleon(Transform parent, Vector3 localPos, bool isProton, string name)
    {
        GameObject nucleon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nucleon.name = name;
        nucleon.transform.SetParent(parent, false);
        nucleon.transform.localPosition = localPos;
        nucleon.transform.localScale = Vector3.one * nucleonSize;

        Collider col = nucleon.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = nucleon.GetComponent<Renderer>();
        if (rend != null)
            rend.material = CreateOpaqueMat(isProton ? protonColor : neutronColor);
    }

    // =============================================
    //  SHELLS & ORBITALS
    // =============================================

    void CreateShellsAndOrbitals()
    {
        int[] config = ElectronConfigs[currentAtomicNumber - 1];
        int subshellCount = config.Length;
        bool[] shellDrawn = new bool[5];

        for (int si = 0; si < subshellCount; si++)
        {
            int shell = SubshellShells[si];
            char type = SubshellTypes[si];
            string label = SubshellLabels[si];
            float shellRadius = shellBaseRadius + (shell - 1) * shellSpacing;

            if (!shellDrawn[shell])
            {
                CreateShellRing(shell, shellRadius);
                shellDrawn[shell] = true;
            }

            if (type == 's')
                CreateSOrbital(label, shell, shellRadius);
            else if (type == 'p')
                CreatePOrbitals(label, shell, shellRadius);
        }
    }

    // ---- SHELL RING ----

    void CreateShellRing(int shellNumber, float radius)
    {
        // Horizontal ring (XZ plane)
        GameObject ring1 = new GameObject("Shell_" + shellNumber + "_H");
        ring1.transform.SetParent(atomRoot.transform, false);
        LineRenderer lr1 = ring1.AddComponent<LineRenderer>();
        SetupLine(lr1, shellRingColor, ringLineWidth);
        SetCircle(lr1, radius, 64);

        // Vertical ring (XY plane)
        GameObject ring2 = new GameObject("Shell_" + shellNumber + "_V");
        ring2.transform.SetParent(atomRoot.transform, false);
        ring2.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Color dimColor = shellRingColor * 0.6f;
        dimColor.a = shellRingColor.a * 0.6f;
        LineRenderer lr2 = ring2.AddComponent<LineRenderer>();
        SetupLine(lr2, dimColor, ringLineWidth * 0.7f);
        SetCircle(lr2, radius, 64);

        // Shell label -- positioned to the right of the ring
        CreateLabel("n=" + shellNumber, atomRoot.transform,
                   new Vector3(radius + 0.05f, 0, 0), shellLabelSize);
    }

    // ---- S-ORBITAL: wireframe sphere sized to match shell radius ----
    // The wireframe circle radius = shellRadius so it visually sits ON the shell

    void CreateSOrbital(string label, int shell, float shellRadius)
    {
        GameObject sObj = new GameObject("Orbital_" + label);
        sObj.transform.SetParent(atomRoot.transform, false);

        // Use shell radius as the wireframe size
        // so the s-orbital visually wraps around at the shell level
        float orbRadius = shellRadius;

        // 3 great circles to show a spherical shape
        // Circle 1: XZ plane (same as shell ring but in cyan)
        GameObject c1 = new GameObject("S_XZ");
        c1.transform.SetParent(sObj.transform, false);
        LineRenderer lr1 = c1.AddComponent<LineRenderer>();
        SetupLine(lr1, sOrbitalColor, sLineWidth);
        SetCircle(lr1, orbRadius, 48);

        // Circle 2: tilted 60 degrees for visual variety (not overlapping shell ring exactly)
        GameObject c2 = new GameObject("S_Tilt1");
        c2.transform.SetParent(sObj.transform, false);
        c2.transform.localRotation = Quaternion.Euler(60, 0, 0);
        LineRenderer lr2 = c2.AddComponent<LineRenderer>();
        SetupLine(lr2, sOrbitalColor * 0.85f, sLineWidth);
        SetCircle(lr2, orbRadius, 48);

        // Circle 3: tilted 60 degrees the other way
        GameObject c3 = new GameObject("S_Tilt2");
        c3.transform.SetParent(sObj.transform, false);
        c3.transform.localRotation = Quaternion.Euler(60, 90, 0);
        LineRenderer lr3 = c3.AddComponent<LineRenderer>();
        SetupLine(lr3, sOrbitalColor * 0.85f, sLineWidth);
        SetCircle(lr3, orbRadius, 48);

        // Label above the s-orbital
        CreateLabel(label, sObj.transform,
                   new Vector3(0, orbRadius + 0.04f, 0), orbitalLabelSize);

        Debug.Log("[Atom] S-orbital: " + label + " radius=" + orbRadius.ToString("F3"));
    }

    // ---- P-ORBITALS: 3 dumbbells extending outward from nucleus to shell ----
    // Lobes are sized so they extend from near the nucleus out to the shell radius

    void CreatePOrbitals(string label, int shell, float shellRadius)
    {
        GameObject pObj = new GameObject("Orbitals_" + label);
        pObj.transform.SetParent(atomRoot.transform, false);

        // Lobe half-length: extends from nucleus edge to shell radius
        // Center of each lobe sits at shellRadius * 0.5
        // So the lobe stretches from near center to the shell ring
        float lobeHalfLen = shellRadius * 0.45f;
        float lobeCenterDist = shellRadius * 0.55f;
        float lobeW = pLobeWidth + (shell - 1) * 0.008f;

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        Color[] colors = { pxColor, pyColor, pzColor };
        string[] names = { "px", "py", "pz" };

        for (int i = 0; i < 3; i++)
        {
            CreateDumbbell(pObj.transform, label, names[i], axes[i],
                          lobeCenterDist, lobeHalfLen, lobeW, colors[i],
                          shellRadius);
        }

        Debug.Log("[Atom] P-orbitals: " + label +
                  " lobeDist=" + lobeCenterDist.ToString("F3"));
    }

    void CreateDumbbell(Transform parent, string subLabel, string axisName,
                       Vector3 axis, float centerDist, float halfLen,
                       float width, Color color, float shellRadius)
    {
        GameObject dbObj = new GameObject("Orbital_" + subLabel + "_" + axisName);
        dbObj.transform.SetParent(parent, false);

        // Positive lobe: centered at +axis * centerDist
        CreateLobe(dbObj.transform, axis * centerDist, axis, halfLen, width,
                  color, "Lobe_+");

        // Negative lobe: centered at -axis * centerDist (slightly darker)
        Color darkColor = color * 0.75f;
        darkColor.a = color.a;
        CreateLobe(dbObj.transform, -axis * centerDist, axis, halfLen, width,
                  darkColor, "Lobe_-");

        // Axis line through both lobes
        float lineExtent = centerDist + halfLen + 0.01f;
        GameObject axisLine = new GameObject("Axis");
        axisLine.transform.SetParent(dbObj.transform, false);
        LineRenderer lr = axisLine.AddComponent<LineRenderer>();
        Color lineColor = color * 0.5f;
        lineColor.a = 0.4f;
        SetupLine(lr, lineColor, 0.002f);
        lr.loop = false;
        lr.positionCount = 2;
        lr.SetPosition(0, -axis * lineExtent);
        lr.SetPosition(1, axis * lineExtent);

        // Label beyond the positive lobe tip
        float labelDist = centerDist + halfLen + 0.04f;
        CreateLabel(subLabel + axisName.Substring(1), dbObj.transform,
                   axis * labelDist, orbitalLabelSize);
    }

    void CreateLobe(Transform parent, Vector3 position, Vector3 axis,
                   float halfLength, float width, Color color, string name)
    {
        GameObject lobe = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        lobe.name = name;
        lobe.transform.SetParent(parent, false);
        lobe.transform.localPosition = position;

        // Capsule default is along Y -- rotate to match target axis
        if (axis == Vector3.right || axis == -Vector3.right)
            lobe.transform.localRotation = Quaternion.Euler(0, 0, 90);
        else if (axis == Vector3.forward || axis == -Vector3.forward)
            lobe.transform.localRotation = Quaternion.Euler(90, 0, 0);

        lobe.transform.localScale = new Vector3(width, halfLength, width);

        Collider col = lobe.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = lobe.GetComponent<Renderer>();
        if (rend != null)
            rend.material = CreateTransparentMat(color);
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
        {
            if (SubshellShells[i] > maxShell)
                maxShell = SubshellShells[i];
        }

        float topY = shellBaseRadius + (maxShell - 1) * shellSpacing + 0.1f;

        string text = ElementSymbols[currentAtomicNumber - 1] + "\n" +
                      ElementNames[currentAtomicNumber - 1] + "\n" +
                      "Z = " + currentAtomicNumber;

        CreateLabel(text, atomRoot.transform, new Vector3(0, topY, 0), elementLabelSize);
    }

    // =============================================
    //  HELPERS -- Geometry
    // =============================================

    Vector3[] FibonacciSphere(int count, float radius)
    {
        if (count <= 0) return new Vector3[0];
        if (count == 1) return new Vector3[] { Vector3.zero };

        Vector3[] points = new Vector3[count];
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

        for (int i = 0; i < count; i++)
        {
            float y = 1f - (2f * i / (float)(count - 1));
            float r = Mathf.Sqrt(Mathf.Max(0, 1f - y * y));
            float theta = goldenAngle * i;
            points[i] = new Vector3(
                r * Mathf.Cos(theta), y, r * Mathf.Sin(theta)
            ) * radius;
        }
        return points;
    }

    void Shuffle(Vector3[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Vector3 temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }
    }

    // =============================================
    //  HELPERS -- LineRenderer
    // =============================================

    void SetupLine(LineRenderer lr, Color color, float width)
    {
        lr.useWorldSpace = false;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = color;
        lr.endColor = color;
        lr.loop = true;
        lr.numCornerVertices = 4;

        if (lineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader != null) lineMaterial = new Material(shader);
        }

        if (lineMaterial != null)
        {
            lr.material = new Material(lineMaterial);
            lr.material.color = color;
        }
    }

    void SetCircle(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments;
        lr.loop = true;
        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius
            ));
        }
    }

    // =============================================
    //  HELPERS -- Text Labels
    // =============================================

    void CreateLabel(string text, Transform parent, Vector3 localPos, float fontSize)
    {
        GameObject labelObj = new GameObject("Label_" + text.Replace("\n", "_"));
        labelObj.transform.SetParent(parent, false);
        labelObj.transform.localPosition = localPos;
        labelObj.transform.localScale = Vector3.one * 0.01f;

        TextMeshPro tmp = labelObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = labelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;

        labelObj.AddComponent<FaceCamera>();
    }

    // =============================================
    //  HELPERS -- Materials
    // =============================================

    Material CreateOpaqueMat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");

        Material mat = new Material(shader);
        mat.color = color;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 0.3f);
        }
        return mat;
    }

    Material CreateTransparentMat(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.2f);
            }
            return mat;
        }

        shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        Material fb = new Material(shader);
        fb.color = color;
        return fb;
    }
}

/// <summary>
/// Billboard: makes text always face the player camera.
/// </summary>
public class FaceCamera : MonoBehaviour
{
    private Transform cam;

    void Start()
    {
        if (Camera.main != null) cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null) cam = Camera.main.transform;
            return;
        }
        Vector3 dir = cam.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}