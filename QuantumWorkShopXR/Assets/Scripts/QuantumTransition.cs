using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// STEP 2: Transition + atom spawn.
/// Pulse -> shrink -> hide specimen -> spawn atom nucleus -> grow in -> voice.
///
/// SETUP:
/// 1. Attach to QuantumTransition GameObject (same as before)
/// 2. Also attach AtomBuilder.cs to the SAME GameObject
/// 3. Wire references in Inspector (same 4 fields as before + voice clip)
/// </summary>
public class QuantumTransition : MonoBehaviour
{
    [Header("Drag These in Inspector")]
    public Transform specimenAnchor;       // SpecimenAnchor
    public GameObject specimen;             // Specimen (the cyan sphere)
    public TextMeshProUGUI scaleLabel;      // The ScaleLabel TextMeshProUGUI component
    public ScaleLabel scaleLabelScript;     // The ScaleLabel.cs script component

    [Header("Voice Line (Optional)")]
    public AudioClip welcomeVoice;
    [Range(0f, 1f)]
    public float voiceVolume = 0.8f;

    [Header("Transition Settings")]
    public float triggerZoomLevel = 2400f;
    public float pulseTime = 0.6f;
    public float shrinkTime = 0.8f;
    public float pauseBeforeAtom = 0.5f;
    public float atomGrowTime = 1.0f;

    [Header("Pulse Color")]
    public Color pulseColor = new Color(0.5f, 1f, 1f, 1f);

    public static bool InQuantumWorld { get; private set; }

    private bool transitioned = false;
    private Renderer specimenRenderer;
    private Color originalColor;
    private AudioSource audioSource;

    void Start()
    {
        InQuantumWorld = false;

        if (specimen != null)
        {
            specimenRenderer = specimen.GetComponent<Renderer>();
            if (specimenRenderer != null)
                originalColor = specimenRenderer.material.color;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        Debug.Log("[QT] QuantumTransition ready. Trigger at zoom: " + triggerZoomLevel);
    }

    void Update()
    {
        if (transitioned) return;

        if (Time.frameCount % 120 == 0 && PinchZoom.ZoomLevel > 10f)
        {
            Debug.Log("[QT] Current zoom: " + PinchZoom.ZoomLevel.ToString("F0"));
        }

        if (PinchZoom.ZoomLevel >= triggerZoomLevel)
        {
            Debug.Log("[QT] TRIGGER! Zoom reached " + PinchZoom.ZoomLevel.ToString("F0"));
            transitioned = true;
            StartCoroutine(TransitionSequence());
        }
    }

    IEnumerator TransitionSequence()
    {
        // ---- Freeze zooming ----
        PinchZoom pinchZoom = specimen != null ? specimen.GetComponent<PinchZoom>() : null;
        if (pinchZoom != null)
            pinchZoom.enabled = false;

        // ---- Disable ScaleLabel updates ----
        if (scaleLabelScript != null)
            scaleLabelScript.enabled = false;

        // ---- Update label ----
        if (scaleLabel != null)
            scaleLabel.text = "Entering the\nQuantum World...";

        Debug.Log("[QT] Step 1: Pulse");

        // ---- Pulse bright ----
        if (specimenRenderer != null)
        {
            float elapsed = 0f;
            Material mat = specimenRenderer.material;
            while (elapsed < pulseTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseTime;
                float intensity = Mathf.Sin(t * Mathf.PI);
                mat.color = Color.Lerp(originalColor, pulseColor, intensity);
                yield return null;
            }
            mat.color = originalColor;
        }

        Debug.Log("[QT] Step 2: Shrink");

        // ---- Shrink specimen to zero ----
        if (specimenAnchor != null)
        {
            float elapsed = 0f;
            Vector3 startScale = specimenAnchor.localScale;
            while (elapsed < shrinkTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / shrinkTime;
                float curve = 1f - (1f - t) * (1f - t);
                specimenAnchor.localScale = Vector3.Lerp(startScale, Vector3.zero, curve);
                yield return null;
            }
        }

        // ---- Deactivate specimen ----
        if (specimen != null)
            specimen.SetActive(false);

        Debug.Log("[QT] Step 3: Specimen hidden");

        // ---- Play voice ----
        if (welcomeVoice != null && audioSource != null)
        {
            audioSource.clip = welcomeVoice;
            audioSource.volume = voiceVolume;
            audioSource.Play();
        }

        // ---- Pause before atom ----
        yield return new WaitForSeconds(pauseBeforeAtom);

        // ---- Reset anchor scale so atom has normal size ----
        if (specimenAnchor != null)
            specimenAnchor.localScale = Vector3.one;

        // ---- Build atom ----
        AtomBuilder builder = GetComponent<AtomBuilder>();
        GameObject atomRoot = null;

        if (builder != null)
        {
            atomRoot = builder.BuildAtom(specimenAnchor);
            Debug.Log("[QT] Step 4: Atom built - " + builder.GetCurrentElementName());
        }
        else
        {
            Debug.LogError("[QT] AtomBuilder not found! Add it to the same GameObject.");
        }

        // ---- Grow atom in from zero ----
        if (atomRoot != null)
        {
            atomRoot.transform.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < atomGrowTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / atomGrowTime;
                // Ease-out cubic for a nice pop-in
                float curve = 1f - Mathf.Pow(1f - t, 3f);
                atomRoot.transform.localScale = Vector3.one * curve;
                yield return null;
            }
            atomRoot.transform.localScale = Vector3.one;
        }

        // ---- Update label with element name ----
        if (scaleLabel != null && builder != null)
            scaleLabel.text = "Quantum World\n" + builder.GetCurrentElementName();

        Debug.Log("[QT] Transition complete!");
        InQuantumWorld = true;
    }
}