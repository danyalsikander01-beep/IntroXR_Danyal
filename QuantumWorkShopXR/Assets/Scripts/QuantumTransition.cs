using UnityEngine;
using System.Collections;
using TMPro;

/// <summary>
/// Transition: pulse -> shrink -> voice -> spawn atom -> show table -> start game.
///
/// SETUP:
/// 1. Attach to QuantumTransition GameObject
/// 2. Also attach: AtomBuilder, PeriodicTableUI, ElectronGame (all on SAME object)
/// 3. Wire references in Inspector
/// </summary>
public class QuantumTransition : MonoBehaviour
{
    [Header("Drag These in Inspector")]
    public Transform specimenAnchor;
    public GameObject specimen;
    public TextMeshProUGUI scaleLabel;
    public ScaleLabel scaleLabelScript;

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
        if (specimen)
        {
            specimenRenderer = specimen.GetComponent<Renderer>();
            if (specimenRenderer) originalColor = specimenRenderer.material.color;
        }
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    void Update()
    {
        if (transitioned) return;
        if (PinchZoom.ZoomLevel >= triggerZoomLevel)
        {
            transitioned = true;
            StartCoroutine(TransitionSequence());
        }
    }

    IEnumerator TransitionSequence()
    {
        PinchZoom pz = specimen ? specimen.GetComponent<PinchZoom>() : null;
        if (pz) pz.enabled = false;
        if (scaleLabelScript) scaleLabelScript.enabled = false;
        if (scaleLabel) scaleLabel.text = "Entering the\nQuantum World...";

        // Pulse
        if (specimenRenderer)
        {
            float e = 0; Material mat = specimenRenderer.material;
            while (e < pulseTime)
            {
                e += Time.deltaTime;
                mat.color = Color.Lerp(originalColor, pulseColor, Mathf.Sin(e / pulseTime * Mathf.PI));
                yield return null;
            }
            mat.color = originalColor;
        }

        // Shrink
        if (specimenAnchor)
        {
            float e = 0; Vector3 ss = specimenAnchor.localScale;
            while (e < shrinkTime)
            {
                e += Time.deltaTime;
                float t = e / shrinkTime;
                specimenAnchor.localScale = Vector3.Lerp(ss, Vector3.zero, 1f - (1f - t) * (1f - t));
                yield return null;
            }
        }

        if (specimen) specimen.SetActive(false);

        // Voice
        if (welcomeVoice && audioSource)
        {
            audioSource.clip = welcomeVoice;
            audioSource.volume = voiceVolume;
            audioSource.Play();
        }

        yield return new WaitForSeconds(pauseBeforeAtom);
        if (specimenAnchor) specimenAnchor.localScale = Vector3.one;

        // Build atom
        AtomBuilder builder = GetComponent<AtomBuilder>();
        GameObject atomRoot = null;
        if (builder)
        {
            atomRoot = builder.BuildAtom(specimenAnchor);
            // Show electrons for initial display
            builder.FillAllElectrons();
        }

        // Grow in
        if (atomRoot)
        {
            atomRoot.transform.localScale = Vector3.zero;
            float e = 0;
            while (e < atomGrowTime)
            {
                e += Time.deltaTime;
                atomRoot.transform.localScale = Vector3.one * (1f - Mathf.Pow(1f - e / atomGrowTime, 3f));
                yield return null;
            }
            atomRoot.transform.localScale = Vector3.one;
        }

        if (scaleLabel && builder)
            scaleLabel.text = "Quantum World\n" + builder.GetCurrentElementName();

        // Show periodic table
        PeriodicTableUI table = GetComponent<PeriodicTableUI>();
        if (table) table.ShowTable(specimenAnchor, builder, scaleLabel);

        // Note: ElectronGame starts when user selects an element from periodic table
        // The initial random atom just shows with all electrons filled (display mode)

        InQuantumWorld = true;
        Debug.Log("[QT] Transition complete!");
    }
}