using UnityEngine;
using TMPro;

/// <summary>
/// Builds a 3D atom model: nucleus + shells + orbitals + labels.
/// Fixes: text billboard direction, p-orbital lobe shape.
/// Supports both random and specific element building.
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
    public float pLobeWidth = 0.04f;

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

    public static readonly int[] MassNumbers = {
        1,4,7,9,11,12,14,16,19,20,
        23,24,27,28,31,32,35,40,39,40
    };

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
    private Transform currentParent;

    // =============================================
    //  PUBLIC API
    // =============================================

    /// <summary>Build a random element (1-20).</summary>
    public GameObject BuildAtom(Transform parent)
    {
        int z = Random.Range(1, 21);
        return BuildSpecificAtom(parent, z);
    }

    /// <summary>Build a specific element by atomic number (1-20).</summary>
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

    void CreateShellRing(int shellNumber, float radius)
    {
        GameObject ring1 = new GameObject("Shell_" + shellNumber + "_H");
        ring1.transform.SetParent(atomRoot.transform, false);
        LineRenderer lr1 = ring1.AddComponent<LineRenderer>();
        SetupLine(lr1, shellRingColor, ringLineWidth);
        SetCircle(lr1, radius, 64);

        GameObject ring2 = new GameObject("Shell_" + shellNumber + "_V");
        ring2.transform.SetParent(atomRoot.transform, false);
        ring2.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Color dimColor = shellRingColor * 0.6f;
        dimColor.a = shellRingColor.a * 0.6f;
        LineRenderer lr2 = ring2.AddComponent<LineRenderer>();
        SetupLine(lr2, dimColor, ringLineWidth * 0.7f);
        SetCircle(lr2, radius, 64);

        CreateLabel("n=" + shellNumber, atomRoot.transform,
                   new Vector3(radius + 0.05f, 0, 0), shellLabelSize);
    }

    // ---- S-ORBITAL: wireframe sphere at shell radius ----

    void CreateSOrbital(string label, int shell, float shellRadius)
    {
        GameObject sObj = new GameObject("Orbital_" + label);
        sObj.transform.SetParent(atomRoot.transform, false);

        float orbRadius = shellRadius;

        GameObject c1 = new GameObject("S_XZ");
        c1.transform.SetParent(sObj.transform, false);
        LineRenderer lr1 = c1.AddComponent<LineRenderer>();
        SetupLine(lr1, sOrbitalColor, sLineWidth);
        SetCircle(lr1, orbRadius, 48);

        GameObject c2 = new GameObject("S_Tilt1");
        c2.transform.SetParent(sObj.transform, false);
        c2.transform.localRotation = Quaternion.Euler(60, 0, 0);
        LineRenderer lr2 = c2.AddComponent<LineRenderer>();
        SetupLine(lr2, sOrbitalColor * 0.85f, sLineWidth);
        SetCircle(lr2, orbRadius, 48);

        GameObject c3 = new GameObject("S_Tilt2");
        c3.transform.SetParent(sObj.transform, false);
        c3.transform.localRotation = Quaternion.Euler(60, 90, 0);
        LineRenderer lr3 = c3.AddComponent<LineRenderer>();
        SetupLine(lr3, sOrbitalColor * 0.85f, sLineWidth);
        SetCircle(lr3, orbRadius, 48);

        CreateLabel(label, sObj.transform,
                   new Vector3(0, orbRadius + 0.04f, 0), orbitalLabelSize);
    }

    // ---- P-ORBITALS: 3 dumbbells with balloon-shaped lobes ----

    void CreatePOrbitals(string label, int shell, float shellRadius)
    {
        GameObject pObj = new GameObject("Orbitals_" + label);
        pObj.transform.SetParent(atomRoot.transform, false);

        float lobeCenterDist = shellRadius * 0.55f;
        float lobeLength = shellRadius * 0.45f;
        float lobeW = pLobeWidth + (shell - 1) * 0.01f;

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
        Color[] colors = { pxColor, pyColor, pzColor };
        string[] names = { "px", "py", "pz" };

        for (int i = 0; i < 3; i++)
        {
            CreateDumbbell(pObj.transform, label, names[i], axes[i],
                          lobeCenterDist, lobeLength, lobeW, colors[i],
                          shellRadius);
        }
    }

    void CreateDumbbell(Transform parent, string subLabel, string axisName,
                       Vector3 axis, float centerDist, float lobeLen,
                       float width, Color color, float shellRadius)
    {
        GameObject dbObj = new GameObject("Orbital_" + subLabel + "_" + axisName);
        dbObj.transform.SetParent(parent, false);

        // Positive lobe (outer end fatter)
        CreateBalloonLobe(dbObj.transform, axis, centerDist, lobeLen, width,
                         color, "Lobe_+", false);

        // Negative lobe (darker)
        Color darkColor = color * 0.75f;
        darkColor.a = color.a;
        CreateBalloonLobe(dbObj.transform, -axis, centerDist, lobeLen, width,
                         darkColor, "Lobe_-", false);

        // Axis line
        float lineExtent = centerDist + lobeLen * 0.6f + 0.01f;
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

        // Label beyond the positive lobe
        float labelDist = centerDist + lobeLen * 0.6f + 0.04f;
        CreateLabel(subLabel + axisName.Substring(1), dbObj.transform,
                   axis * labelDist, orbitalLabelSize);
    }

    /// <summary>
    /// Creates a balloon-shaped lobe using two overlapping spheres:
    /// a large "bulb" at the tip and a smaller "neck" near center.
    /// This gives the teardrop-like shape of real p-orbitals.
    /// </summary>
    void CreateBalloonLobe(Transform parent, Vector3 axis, float centerDist,
                          float lobeLen, float width, Color color,
                          string name, bool flip)
    {
        GameObject lobeGroup = new GameObject(name);
        lobeGroup.transform.SetParent(parent, false);

        // Main bulb: large sphere at the outer position (fat tip)
        float bulbSize = width * 1.1f;
        Vector3 bulbPos = axis * (centerDist + lobeLen * 0.15f);

        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulb.name = "Bulb";
        bulb.transform.SetParent(lobeGroup.transform, false);
        bulb.transform.localPosition = bulbPos;
        // Scale: fatter perpendicular to axis, elongated along axis
        Vector3 bulbScale;
        if (axis == Vector3.right || axis == -Vector3.right)
            bulbScale = new Vector3(lobeLen * 0.6f, bulbSize, bulbSize);
        else if (axis == Vector3.forward || axis == -Vector3.forward)
            bulbScale = new Vector3(bulbSize, bulbSize, lobeLen * 0.6f);
        else
            bulbScale = new Vector3(bulbSize, lobeLen * 0.6f, bulbSize);
        bulb.transform.localScale = bulbScale;

        Collider c1 = bulb.GetComponent<Collider>();
        if (c1 != null) Destroy(c1);
        Renderer r1 = bulb.GetComponent<Renderer>();
        if (r1 != null) r1.material = CreateTransparentMat(color);

        // Neck: smaller sphere closer to nucleus (thin connection)
        float neckSize = width * 0.5f;
        Vector3 neckPos = axis * (centerDist * 0.4f);

        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        neck.name = "Neck";
        neck.transform.SetParent(lobeGroup.transform, false);
        neck.transform.localPosition = neckPos;
        Vector3 neckScale;
        if (axis == Vector3.right || axis == -Vector3.right)
            neckScale = new Vector3(lobeLen * 0.35f, neckSize, neckSize);
        else if (axis == Vector3.forward || axis == -Vector3.forward)
            neckScale = new Vector3(neckSize, neckSize, lobeLen * 0.35f);
        else
            neckScale = new Vector3(neckSize, lobeLen * 0.35f, neckSize);
        neck.transform.localScale = neckScale;

        Collider c2 = neck.GetComponent<Collider>();
        if (c2 != null) Destroy(c2);
        Renderer r2 = neck.GetComponent<Renderer>();
        if (r2 != null)
        {
            Color neckColor = color;
            neckColor.a *= 0.8f;
            r2.material = CreateTransparentMat(neckColor);
        }
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
/// FIXED billboard: copies camera's horizontal forward direction
/// so text always faces the player and stays upright.
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
        // Match camera's forward direction (horizontal only) so text faces player
        Vector3 camForward = cam.forward;
        camForward.y = 0;
        if (camForward.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(camForward, Vector3.up);
    }
}