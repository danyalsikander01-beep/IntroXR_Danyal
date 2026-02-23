using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PinchZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minZoom = 1f;
    public float maxZoom = 2000f;
    public float maxVisualScale = 0.4f;
    public float minVisualScale = 0.05f;

    [Header("Pinch Settings")]
    public float pinchThreshold = 0.025f;
    public float pinchReleaseThreshold = 0.045f;
    public float activationRadius = 0.5f;

    [Header("Smoothing")]
    public float deadZone = 0.015f;
    [Range(0f, 0.95f)]
    public float smoothing = 0.93f;
    public float zoomSensitivity = 1.0f;

    [Header("Debug Display")]
    public TextMeshProUGUI debugLabel;

    public static float ZoomLevel = 1f;

    private Transform leftIndexTip;
    private Transform leftThumbTip;
    private Transform rightIndexTip;
    private Transform rightThumbTip;
    private bool handsFound = false;

    private bool leftPinching = false;
    private bool rightPinching = false;
    private bool zoomActive = false;
    private float startPinchDistance;
    private float startZoom;
    private float smoothedZoom;
    private string debugText = "Starting...";

    // Phase 1: Show paths so we can debug
    private bool pathsLogged = false;
    private int frameCount = 0;
    private List<string> allPaths = new List<string>();

    // Phase 2: After we know paths, track positions
    private List<Transform> allIndexTips = new List<Transform>();
    private Dictionary<Transform, Vector3> prevPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, float> totalMovement = new Dictionary<Transform, float>();

    void Start()
    {
        ZoomLevel = 1f;
        smoothedZoom = 1f;
    }

    void Update()
    {
        frameCount++;

        if (!handsFound)
        {
            FindAndTrackBones();
            UpdateDebugDisplay();
            return;
        }

        if (leftIndexTip == null || rightIndexTip == null)
        {
            handsFound = false;
            pathsLogged = false;
            allIndexTips.Clear();
            prevPositions.Clear();
            totalMovement.Clear();
            return;
        }

        UpdatePinchStates();
        UpdateZoom();
        UpdateVisualScale();
        UpdateDebugDisplay();
    }

    void FindAndTrackBones()
    {
        // Collect all candidates on first pass
        if (allIndexTips.Count == 0)
        {
            allPaths.Clear();
            prevPositions.Clear();
            totalMovement.Clear();

            GameObject[] all = FindObjectsOfType<GameObject>();
            foreach (var obj in all)
            {
                if (!obj.activeInHierarchy) continue;

                if (obj.name == "XRHand_IndexTip")
                {
                    allIndexTips.Add(obj.transform);
                    prevPositions[obj.transform] = obj.transform.position;
                    totalMovement[obj.transform] = 0f;
                    allPaths.Add(GetPath(obj.transform));
                }
            }

            if (allIndexTips.Count == 0)
            {
                debugText = "No XRHand_IndexTip found yet...";
                return;
            }

            frameCount = 0;
            debugText = $"Found {allIndexTips.Count} IndexTip bones\nTracking for 60 frames...";
            return;
        }

        // Track movement every frame
        foreach (var t in allIndexTips)
        {
            if (t == null) continue;
            float moved = Vector3.Distance(t.position, prevPositions[t]);
            totalMovement[t] += moved;
            prevPositions[t] = t.position;
        }

        // Show live positions and movement per bone
        string info = $"Frame {frameCount}/60\n";
        for (int i = 0; i < allIndexTips.Count && i < allPaths.Count; i++)
        {
            Transform t = allIndexTips[i];
            if (t == null) continue;
            string shortPath = GetShortPath(t, 4);
            float moved = totalMovement[t];
            Vector3 pos = t.position;
            info += $"#{i}: moved={moved:F4} pos=({pos.x:F2},{pos.y:F2},{pos.z:F2})\n  {shortPath}\n";
        }
        debugText = info;

        // After 60 frames, pick the ones that moved most per side
        if (frameCount >= 60)
        {
            // Sort by movement and pick best left and right
            Transform bestLeftIndex = null, bestRightIndex = null;
            float bestLeftMove = 0, bestRightMove = 0;

            for (int i = 0; i < allIndexTips.Count && i < allPaths.Count; i++)
            {
                Transform t = allIndexTips[i];
                if (t == null) continue;
                float moved = totalMovement[t];
                string path = allPaths[i];

                if (path.Contains("Left"))
                {
                    if (moved > bestLeftMove) { bestLeftMove = moved; bestLeftIndex = t; }
                }
                else if (path.Contains("Right"))
                {
                    if (moved > bestRightMove) { bestRightMove = moved; bestRightIndex = t; }
                }
            }

            debugText = $"RESULTS after 60 frames:\n" +
                        $"Best L moved: {bestLeftMove:F4}\n" +
                        $"Best R moved: {bestRightMove:F4}\n";

            // If bones moved, use them; find matching thumb tips
            if (bestLeftIndex != null && bestRightIndex != null &&
                (bestLeftMove > 0.001f || bestRightMove > 0.001f))
            {
                leftIndexTip = bestLeftIndex;
                rightIndexTip = bestRightIndex;

                // Find thumb tips from same parent chain
                leftThumbTip = FindSiblingBone(bestLeftIndex, "XRHand_ThumbTip");
                rightThumbTip = FindSiblingBone(bestRightIndex, "XRHand_ThumbTip");

                if (leftThumbTip != null && rightThumbTip != null)
                {
                    handsFound = true;
                    debugText = "FOUND via movement!";
                }
                else
                {
                    debugText += "But couldn't find matching ThumbTips!";
                    // Reset and retry
                    allIndexTips.Clear();
                }
            }
            else
            {
                // Nothing moved! Try using wrist as reference instead
                debugText += "\nNO MOVEMENT detected!\nFalling back to wrist approach...";
                FallbackToWristApproach();
            }
        }
    }

    // Find a sibling bone by navigating up to the wrist and back down
    Transform FindSiblingBone(Transform indexTip, string targetName)
    {
        // Go up until we find XRHand_Wrist
        Transform current = indexTip.parent;
        int depth = 0;
        while (current != null && depth < 10)
        {
            if (current.name == "XRHand_Wrist")
            {
                // Search down from wrist for the target
                return FindChildRecursive(current, targetName);
            }
            current = current.parent;
            depth++;
        }

        // Didn't find wrist — just search among all objects with same path prefix
        return null;
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Fallback: use wrist bones (like old code) but add pinch detection
    void FallbackToWristApproach()
    {
        Transform leftWrist = null, rightWrist = null;

        GameObject[] all = FindObjectsOfType<GameObject>();
        foreach (var obj in all)
        {
            if (!obj.activeInHierarchy) continue;
            if (obj.name == "XRHand_Wrist")
            {
                string path = GetPath(obj.transform);
                if (path.Contains("Left") && leftWrist == null)
                    leftWrist = obj.transform;
                else if (path.Contains("Right") && rightWrist == null)
                    rightWrist = obj.transform;
            }
        }

        if (leftWrist != null && rightWrist != null)
        {
            // Find tips relative to these wrists
            leftIndexTip = FindChildRecursive(leftWrist, "XRHand_IndexTip");
            leftThumbTip = FindChildRecursive(leftWrist, "XRHand_ThumbTip");
            rightIndexTip = FindChildRecursive(rightWrist, "XRHand_IndexTip");
            rightThumbTip = FindChildRecursive(rightWrist, "XRHand_ThumbTip");

            if (leftIndexTip != null && leftThumbTip != null &&
                rightIndexTip != null && rightThumbTip != null)
            {
                handsFound = true;
                debugText = "FOUND via wrist children!\nTesting pinch...";
            }
            else
            {
                debugText = $"Wrist found but missing tips!\n" +
                            $"LI:{leftIndexTip != null} LT:{leftThumbTip != null}\n" +
                            $"RI:{rightIndexTip != null} RT:{rightThumbTip != null}";
                // Retry
                allIndexTips.Clear();
            }
        }
        else
        {
            debugText = "No wrists found either! Retrying...";
            allIndexTips.Clear();
        }
    }

    void UpdatePinchStates()
    {
        float leftDist = Vector3.Distance(leftThumbTip.position, leftIndexTip.position);
        float rightDist = Vector3.Distance(rightThumbTip.position, rightIndexTip.position);

        if (!leftPinching && leftDist < pinchThreshold)
            leftPinching = true;
        if (leftPinching && leftDist > pinchReleaseThreshold)
            leftPinching = false;

        if (!rightPinching && rightDist < pinchThreshold)
            rightPinching = true;
        if (rightPinching && rightDist > pinchReleaseThreshold)
            rightPinching = false;

        debugText = $"L: {leftDist:F3}m {(leftPinching ? "PINCH!" : "")}\n" +
                    $"R: {rightDist:F3}m {(rightPinching ? "PINCH!" : "")}\n" +
                    $"Zoom: {(zoomActive ? "ACTIVE" : "off")} {ZoomLevel:F1}x";
    }

    void UpdateZoom()
    {
        bool bothPinching = leftPinching && rightPinching;

        if (bothPinching && !zoomActive)
        {
            Vector3 leftMid = (leftThumbTip.position + leftIndexTip.position) * 0.5f;
            Vector3 rightMid = (rightThumbTip.position + rightIndexTip.position) * 0.5f;
            Vector3 specimenPos = transform.parent.position;

            float distL = Vector3.Distance(leftMid, specimenPos);
            float distR = Vector3.Distance(rightMid, specimenPos);

            if (distL < activationRadius && distR < activationRadius)
            {
                zoomActive = true;
                startPinchDistance = Vector3.Distance(leftMid, rightMid);
                startZoom = ZoomLevel;
            }
        }
        else if (!bothPinching && zoomActive)
        {
            zoomActive = false;
        }

        if (zoomActive && startPinchDistance > 0.001f)
        {
            Vector3 leftMid = (leftThumbTip.position + leftIndexTip.position) * 0.5f;
            Vector3 rightMid = (rightThumbTip.position + rightIndexTip.position) * 0.5f;
            float currentDist = Vector3.Distance(leftMid, rightMid);

            float delta = currentDist - startPinchDistance;

            if (Mathf.Abs(delta) < deadZone)
                delta = 0f;
            else
                delta = (delta - Mathf.Sign(delta) * deadZone);

            float ratio = 1f + (delta * zoomSensitivity / startPinchDistance);
            ratio = Mathf.Max(ratio, 0.01f);
            float targetZoom = Mathf.Clamp(startZoom * ratio, minZoom, maxZoom);

            smoothedZoom = Mathf.Lerp(smoothedZoom, targetZoom, 1f - smoothing);
            ZoomLevel = smoothedZoom;
        }
        else
        {
            smoothedZoom = ZoomLevel;
        }
    }

    void UpdateVisualScale()
    {
        float visualScale;
        if (ZoomLevel <= 50f)
            visualScale = Mathf.Lerp(minVisualScale, maxVisualScale,
                          Mathf.InverseLerp(1f, 50f, ZoomLevel));
        else
            visualScale = Mathf.Lerp(maxVisualScale, minVisualScale,
                          Mathf.InverseLerp(50f, 500f, ZoomLevel));

        transform.parent.localScale = Vector3.one * visualScale;
    }

    void UpdateDebugDisplay()
    {
        if (debugLabel != null)
            debugLabel.text = debugText;
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    string GetShortPath(Transform t, int levels)
    {
        string result = t.name;
        Transform p = t.parent;
        for (int i = 0; i < levels && p != null; i++)
        {
            result = p.name + "/" + result;
            p = p.parent;
        }
        return result;
    }
}