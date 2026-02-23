using UnityEngine;

public class PinchZoom : MonoBehaviour
{
    public Transform specimenAnchor;
    public float minZoom = 1f;
    public float maxZoom = 2000f;
    public float zoomSpeed = 25f;
    public float maxVisualScale = 0.4f;
    public float minVisualScale = 0.02f;

    public static float ZoomLevel = 1f;

    private Transform leftWrist;
    private Transform rightWrist;
    private bool handsFound = false;

    // Calibration
    private bool calibrated = false;
    private float calibrationTimer = 0f;
    private float calibrationDuration = 2f;
    private float calibrationSum = 0f;
    private int calibrationSamples = 0;
    private float neutralDistance = 0f;
    private float deadzone = 0.12f;

    void Start()
    {
        ZoomLevel = 1f;
        if (specimenAnchor != null)
            specimenAnchor.localScale = Vector3.one * minVisualScale;
    }

    void Update()
    {
        // Step 1: Find hand wrists at runtime
        if (!handsFound)
        {
            GameObject[] allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "XRHand_Wrist" && obj.activeInHierarchy)
                {
                    string path = GetPath(obj.transform);
                    if (path.Contains("LeftInteractions") || path.Contains("OVRLeftHand"))
                        leftWrist = obj.transform;
                    else if (path.Contains("RightInteractions") || path.Contains("OVRRightHand"))
                        rightWrist = obj.transform;
                }
            }
            if (leftWrist != null && rightWrist != null)
                handsFound = true;
            return;
        }

        float distance = Vector3.Distance(leftWrist.position, rightWrist.position);

        // Step 2: Calibrate for 2 seconds after hands found
        if (!calibrated)
        {
            calibrationTimer += Time.deltaTime;
            calibrationSum += distance;
            calibrationSamples++;

            if (calibrationTimer >= calibrationDuration)
            {
                neutralDistance = calibrationSum / calibrationSamples;
                calibrated = true;
            }
            return;
        }

        if (specimenAnchor == null) return;

        // Step 3: Zoom based on offset from user's own neutral
        float offset = distance - neutralDistance;

        if (offset > deadzone)
        {
            float intensity = Mathf.Clamp01((offset - deadzone) / 0.25f);
            ZoomLevel = Mathf.Clamp(
                ZoomLevel + zoomSpeed * intensity * Time.deltaTime,
                minZoom, maxZoom
            );
        }
        else if (offset < -deadzone)
        {
            float intensity = Mathf.Clamp01((-offset - deadzone) / 0.25f);
            ZoomLevel = Mathf.Clamp(
                ZoomLevel - zoomSpeed * intensity * Time.deltaTime,
                minZoom, maxZoom
            );
        }

        // Update visual scale
        float visualScale;
        if (ZoomLevel <= 50f)
            visualScale = Mathf.Lerp(minVisualScale, maxVisualScale,
                          Mathf.InverseLerp(1f, 50f, ZoomLevel));
        else
            visualScale = Mathf.Lerp(maxVisualScale, minVisualScale,
                          Mathf.InverseLerp(50f, 500f, ZoomLevel));

        specimenAnchor.localScale = Vector3.one * visualScale;
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}