using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Electron drag game.
/// Electron source placed to user's LEFT using cam.right directly (not a blend).
/// Shell snap distance increased significantly for reliable placement.
/// Uses ALL hand bone copies for pinch detection.
/// </summary>
public class ElectronGame : MonoBehaviour
{
    [Header("Interaction")]
    public float grabDist = 0.10f;
    public float pinchThreshold = 0.03f;
    public float pinchRelease = 0.05f;
    public float shellSnapDist = 0.18f;  // WAS 0.08 -- much more forgiving now

    [Header("Placement")]
    public float sourceDistance = 0.80f;   // How far to the left
    public float sourceForward = 0.15f;    // Slight forward offset
    public float sourceDown = 0.20f;       // Below eye level

    [Header("Colors")]
    public Color sourceColor = new Color(1f, 1f, 0.3f, 1f);
    public Color correctColor = new Color(0f, 1f, 0.3f);
    public Color wrongColor = new Color(1f, 0.2f, 0.2f);

    private AtomBuilder builder;
    private GameObject electronSource;
    private GameObject sourceParent;
    private GameObject heldElectron;
    private bool isHolding = false;
    private bool gameActive = false;
    private int targetZ = -1;
    private int totalPlaced = 0;
    private int totalNeeded = 0;

    // Hand tracking: collect ALL copies, check all for pinch
    private List<Transform> allIndexTips = new List<Transform>();
    private List<Transform> allThumbTips = new List<Transform>();
    private bool handsSearched = false;
    private int searchFrame = 0;
    private bool anyPinching = false;
    private Vector3 bestPinchPos;

    // UI
    private TextMeshPro statusTMP;

    public void StartGame(int atomicNumber)
    {
        builder = GetComponent<AtomBuilder>();
        targetZ = atomicNumber;
        totalPlaced = 0;

        int[] config = AtomBuilder.ShellConfigs[targetZ - 1];
        totalNeeded = 0;
        for (int i = 0; i < config.Length; i++) totalNeeded += config[i];

        // Create placeholder slots on the atom shells
        if (builder) builder.CreatePlaceholderSlots();

        CreateSource();
        UpdateStatus();
        gameActive = true;
        handsSearched = false;
        searchFrame = 0;

        Debug.Log("[Game] Started Z=" + targetZ + " need " + totalNeeded + " e-" +
                  " shells=" + config.Length + " snapDist=" + shellSnapDist);
    }

    void Update()
    {
        if (!gameActive) return;

        // Search for hands after a delay (let bones spawn)
        if (!handsSearched)
        {
            searchFrame++;
            if (searchFrame > 30) { FindAllBones(); searchFrame = 0; }
            return;
        }

        UpdatePinchState();

        if (!isHolding)
            TryGrab();
        else
        {
            MoveHeld();
            TryRelease();
        }
    }

    // =============================================
    // CREATE SOURCE: Straight to the LEFT of user
    // =============================================
    void CreateSource()
    {
        // Destroy old
        if (sourceParent) Destroy(sourceParent);

        sourceParent = new GameObject("ElectronSourceAnchor");

        Transform cam = Camera.main ? Camera.main.transform : null;
        if (cam)
        {
            Vector3 camPos = cam.position;

            // Get user's right direction (flattened)
            Vector3 right = cam.right;
            right.y = 0f;
            right.Normalize();

            // Get user's forward direction (flattened)
            Vector3 fwd = cam.forward;
            fwd.y = 0f;
            fwd.Normalize();

            // LEFT = negative right
            Vector3 sourcePos = camPos - right * sourceDistance + fwd * sourceForward;
            sourcePos.y = camPos.y - sourceDown;

            sourceParent.transform.position = sourcePos;

            // Face toward user
            Vector3 toUser = camPos - sourcePos;
            toUser.y = 0f;
            if (toUser.sqrMagnitude > 0.001f)
                sourceParent.transform.rotation = Quaternion.LookRotation(toUser.normalized, Vector3.up);

            Debug.Log("[Game] Source at " + sourcePos +
                      " | cam at " + camPos +
                      " | left=" + (-right) +
                      " | facing user");
        }

        // Glowing electron source sphere
        electronSource = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        electronSource.name = "ElectronSource";
        electronSource.transform.SetParent(sourceParent.transform, false);
        electronSource.transform.localPosition = Vector3.zero;
        electronSource.transform.localScale = Vector3.one * 0.055f;

        Collider c = electronSource.GetComponent<Collider>();
        if (c) Destroy(c);
        Renderer r = electronSource.GetComponent<Renderer>();
        if (r) r.material = MakeGlow(sourceColor);

        electronSource.AddComponent<PulseGlow>();

        // Status label BELOW source
        var labelObj = new GameObject("GameStatus");
        labelObj.transform.SetParent(sourceParent.transform, false);
        labelObj.transform.localPosition = new Vector3(0, -0.10f, 0);
        labelObj.transform.localScale = Vector3.one * 0.012f;
        statusTMP = labelObj.AddComponent<TextMeshPro>();
        statusTMP.fontSize = 7f;
        statusTMP.color = Color.white;
        statusTMP.alignment = TextAlignmentOptions.Center;
        labelObj.AddComponent<FaceCamera>();

        // "Grab here" label ABOVE source
        var grabLabel = new GameObject("GrabLabel");
        grabLabel.transform.SetParent(sourceParent.transform, false);
        grabLabel.transform.localPosition = new Vector3(0, 0.08f, 0);
        grabLabel.transform.localScale = Vector3.one * 0.012f;
        var glTMP = grabLabel.AddComponent<TextMeshPro>();
        glTMP.text = "Pinch Here\nto Grab e-";
        glTMP.fontSize = 7f;
        glTMP.color = sourceColor;
        glTMP.alignment = TextAlignmentOptions.Center;
        grabLabel.AddComponent<FaceCamera>();
    }

    void UpdateStatus()
    {
        if (!statusTMP) return;
        if (totalPlaced >= totalNeeded)
            statusTMP.text = "Complete!\n" + totalPlaced + "/" + totalNeeded;
        else
            statusTMP.text = "Electrons\n" + totalPlaced + "/" + totalNeeded;
    }

    // =============================================
    //  GRAB / DRAG / RELEASE
    // =============================================

    void TryGrab()
    {
        if (!electronSource || !anyPinching) return;
        if (totalPlaced >= totalNeeded) return;

        float dist = Vector3.Distance(bestPinchPos, electronSource.transform.position);

        if (dist < grabDist)
        {
            heldElectron = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            heldElectron.name = "HeldElectron";
            heldElectron.transform.localScale = Vector3.one * 0.018f;

            Collider co = heldElectron.GetComponent<Collider>();
            if (co) Destroy(co);
            Renderer rn = heldElectron.GetComponent<Renderer>();
            if (rn) rn.material = MakeGlow(sourceColor * 1.5f);

            isHolding = true;
            Debug.Log("[Game] Grabbed! dist=" + dist.ToString("F3"));
        }
    }

    void MoveHeld()
    {
        if (!heldElectron) { isHolding = false; return; }
        heldElectron.transform.position = bestPinchPos;
    }

    void TryRelease()
    {
        if (anyPinching) return; // Still pinching = still holding

        if (!heldElectron || !builder || !builder.GetAtomRoot())
        {
            CancelHold();
            return;
        }

        Vector3 ePos = heldElectron.transform.position;
        Vector3 center = builder.GetAtomRoot().transform.position;
        float distFromCenter = Vector3.Distance(ePos, center);

        Debug.Log("[Game] Released at dist=" + distFromCenter.ToString("F3") +
                  " from center " + center.ToString("F3") +
                  ". Shells: " + builder.shellRadii.Count +
                  " snapThresh=" + shellSnapDist.ToString("F3"));

        // =============================================
        // FIND CLOSEST SHELL by comparing distance-from-center
        // to each shell radius
        // =============================================
        int closestShell = -1;
        float closestDiff = float.MaxValue;

        for (int i = 0; i < builder.shellRadii.Count; i++)
        {
            float shellR = builder.shellRadii[i];
            float diff = Mathf.Abs(distFromCenter - shellR);
            Debug.Log("[Game]   Shell " + i +
                      " radius=" + shellR.ToString("F3") +
                      " diff=" + diff.ToString("F3") +
                      (diff < shellSnapDist ? " WITHIN RANGE" : " out of range"));
            if (diff < closestDiff)
            {
                closestDiff = diff;
                closestShell = i;
            }
        }

        if (closestShell >= 0 && closestDiff < shellSnapDist)
        {
            // Found a shell within snap range
            int[] config = AtomBuilder.ShellConfigs[targetZ - 1];
            int correctShell = GetNextShellToFill(config);

            Debug.Log("[Game] Snapping to shell=" + closestShell +
                      " (correct next=" + correctShell + ")" +
                      " diff=" + closestDiff.ToString("F3"));

            if (closestShell == correctShell)
            {
                // CORRECT placement
                builder.AddElectronToShell(closestShell);
                builder.RemovePlaceholderSlot(closestShell);
                totalPlaced++;
                Debug.Log("[Game] CORRECT! Shell " + (closestShell + 1) +
                          " total=" + totalPlaced + "/" + totalNeeded);
                StartCoroutine(FlashFeedback(correctColor));

                if (totalPlaced >= totalNeeded)
                {
                    Debug.Log("[Game] WIN!");
                    StartCoroutine(WinSequence());
                }
            }
            else
            {
                Debug.Log("[Game] WRONG shell " + (closestShell + 1) +
                          " needed " + (correctShell + 1));
                StartCoroutine(FlashFeedback(wrongColor));
            }
        }
        else
        {
            // Too far from any shell
            Debug.Log("[Game] Too far from any shell. closestDiff=" +
                      closestDiff.ToString("F3") +
                      " threshold=" + shellSnapDist +
                      " | Try getting hand closer to the atom rings!");
            StartCoroutine(FlashFeedback(wrongColor));
        }

        CancelHold();
        UpdateStatus();
    }

    int GetNextShellToFill(int[] config)
    {
        for (int s = 0; s < config.Length; s++)
        {
            int cur = builder.GetElectronsInShell(s);
            if (cur < config[s]) return s;
        }
        return -1;
    }

    void CancelHold()
    {
        if (heldElectron) Destroy(heldElectron);
        heldElectron = null;
        isHolding = false;
    }

    IEnumerator FlashFeedback(Color c)
    {
        if (!electronSource) yield break;
        Renderer r = electronSource.GetComponent<Renderer>();
        if (!r) yield break;
        Color orig = sourceColor;
        r.material.color = c;
        if (r.material.HasProperty("_EmissionColor"))
            r.material.SetColor("_EmissionColor", c * 2f);
        yield return new WaitForSeconds(0.4f);
        r.material.color = orig;
        if (r.material.HasProperty("_EmissionColor"))
            r.material.SetColor("_EmissionColor", orig * 2f);
    }

    IEnumerator WinSequence()
    {
        if (statusTMP)
        {
            statusTMP.text = "Complete!\nAll electrons!";
            statusTMP.color = correctColor;
            statusTMP.fontSize = 8f;
        }
        yield return new WaitForSeconds(3f);
        if (statusTMP)
        {
            statusTMP.text = "Select another\nelement!";
            statusTMP.color = Color.white;
            statusTMP.fontSize = 7f;
        }
        gameActive = false;
    }

    // =============================================
    //  HAND TRACKING -- Find ALL bones, check ALL for pinch
    // =============================================

    void UpdatePinchState()
    {
        anyPinching = false;
        bestPinchPos = Vector3.zero;
        float bestDist = float.MaxValue;

        // Check every index-thumb pair for pinch
        for (int i = 0; i < allIndexTips.Count; i++)
        {
            Transform idx = allIndexTips[i];
            if (!idx) continue;

            for (int j = 0; j < allThumbTips.Count; j++)
            {
                Transform thm = allThumbTips[j];
                if (!thm) continue;

                float d = Vector3.Distance(idx.position, thm.position);

                // Use wider threshold when already holding
                float threshold = isHolding ? pinchRelease : pinchThreshold;

                if (d < threshold)
                {
                    Vector3 mid = (idx.position + thm.position) * 0.5f;

                    // Pick the pinch closest to relevant target
                    Vector3 target = isHolding && heldElectron
                        ? heldElectron.transform.position
                        : (electronSource ? electronSource.transform.position : Vector3.zero);

                    float toTarget = Vector3.Distance(mid, target);
                    if (toTarget < bestDist)
                    {
                        bestDist = toTarget;
                        bestPinchPos = mid;
                        anyPinching = true;
                    }
                }
            }
        }
    }

    void FindAllBones()
    {
        allIndexTips.Clear();
        allThumbTips.Clear();

        foreach (var obj in FindObjectsOfType<GameObject>())
        {
            if (!obj.activeInHierarchy) continue;
            if (obj.name == "XRHand_IndexTip")
                allIndexTips.Add(obj.transform);
            else if (obj.name == "XRHand_ThumbTip")
                allThumbTips.Add(obj.transform);
        }

        if (allIndexTips.Count > 0 && allThumbTips.Count > 0)
        {
            handsSearched = true;
            Debug.Log("[Game] Found " + allIndexTips.Count + " index, " +
                      allThumbTips.Count + " thumb tips");
        }
    }

    // =============================================
    //  HELPERS
    // =============================================

    Material MakeGlow(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (!s) s = Shader.Find("Standard");
        if (!s) s = Shader.Find("Diffuse");
        Material m = new Material(s);
        m.color = c;
        if (m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * 2.5f);
        }
        return m;
    }
}

public class PulseGlow : MonoBehaviour
{
    Renderer rend;
    Color baseCol;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend) baseCol = rend.material.color;
    }

    void Update()
    {
        if (!rend) return;
        float p = 1f + Mathf.Sin(Time.time * 3f) * 0.3f;
        Color c = baseCol * p;
        c.a = 1f;
        rend.material.color = c;
    }
}