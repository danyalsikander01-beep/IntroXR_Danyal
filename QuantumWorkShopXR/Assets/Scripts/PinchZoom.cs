using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class PinchZoom : MonoBehaviour
{
    [Header("Zoom Settings")]
    public float minZoom = 1f;
    public float maxZoom = 2500f;
    public float maxVisualScale = 1.2f;
    public float minVisualScale = 0.15f;

    [Header("Pinch Settings")]
    public float pinchThreshold = 0.025f;
    public float pinchReleaseThreshold = 0.045f;
    public float activationRadius = 0.5f;

    [Header("Smoothing")]
    public float deadZone = 0.015f;
    [Range(0f, 0.95f)]
    public float smoothing = 0.93f;
    public float zoomSensitivity = 1.0f;

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

    // Bone finding
    private List<Transform> allIndexTips = new List<Transform>();
    private Dictionary<Transform, Vector3> prevPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, float> totalMovement = new Dictionary<Transform, float>();
    private List<string> allPaths = new List<string>();
    private int frameCount = 0;

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
            UpdateVisualScale();
            return;
        }

        if (leftIndexTip == null || rightIndexTip == null)
        {
            handsFound = false;
            allIndexTips.Clear();
            prevPositions.Clear();
            totalMovement.Clear();
            UpdateVisualScale();
            return;
        }

        UpdatePinchStates();
        UpdateZoom();
        UpdateVisualScale();
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
        // Simple logarithmic scale: sphere grows consistently as you zoom in
        // log maps the huge range (1-2000) to a manageable visual range
        float t = Mathf.Log(ZoomLevel, maxZoom); // 0 at zoom=1, 1 at maxZoom
        t = Mathf.Clamp01(t);
        float visualScale = Mathf.Lerp(minVisualScale, maxVisualScale, t);
        transform.parent.localScale = Vector3.one * visualScale;
    }

    // =============================================
    // BONE FINDING
    // =============================================
    void FindAndTrackBones()
    {
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

            if (allIndexTips.Count == 0) return;
            frameCount = 0;
            return;
        }

        foreach (var t in allIndexTips)
        {
            if (t == null) continue;
            float moved = Vector3.Distance(t.position, prevPositions[t]);
            totalMovement[t] += moved;
            prevPositions[t] = t.position;
        }

        if (frameCount >= 60)
        {
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

            if (bestLeftIndex != null && bestRightIndex != null &&
                (bestLeftMove > 0.001f || bestRightMove > 0.001f))
            {
                leftIndexTip = bestLeftIndex;
                rightIndexTip = bestRightIndex;
                leftThumbTip = FindSiblingBone(bestLeftIndex, "XRHand_ThumbTip");
                rightThumbTip = FindSiblingBone(bestRightIndex, "XRHand_ThumbTip");

                if (leftThumbTip != null && rightThumbTip != null)
                    handsFound = true;
                else
                    allIndexTips.Clear();
            }
            else
            {
                FallbackToWristApproach();
            }
        }
    }

    Transform FindSiblingBone(Transform indexTip, string targetName)
    {
        Transform current = indexTip.parent;
        int depth = 0;
        while (current != null && depth < 10)
        {
            if (current.name == "XRHand_Wrist")
                return FindChildRecursive(current, targetName);
            current = current.parent;
            depth++;
        }
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
            leftIndexTip = FindChildRecursive(leftWrist, "XRHand_IndexTip");
            leftThumbTip = FindChildRecursive(leftWrist, "XRHand_ThumbTip");
            rightIndexTip = FindChildRecursive(rightWrist, "XRHand_IndexTip");
            rightThumbTip = FindChildRecursive(rightWrist, "XRHand_ThumbTip");

            if (leftIndexTip != null && leftThumbTip != null &&
                rightIndexTip != null && rightThumbTip != null)
                handsFound = true;
            else
                allIndexTips.Clear();
        }
        else
        {
            allIndexTips.Clear();
        }
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}