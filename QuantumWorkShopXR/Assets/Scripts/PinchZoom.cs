using UnityEngine;

public class PinchZoom : MonoBehaviour
{
    public float minScale = 0.5f;
    public float maxScale = 500f;
    public float sensitivity = 1.5f;
    public float deadzone = 0.012f;

    private Transform leftWrist;
    private Transform rightWrist;
    private float prevDistance = 0f;

    void Update()
    {
        if (leftWrist == null || rightWrist == null)
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
            return;
        }

        float currentDistance = Vector3.Distance(leftWrist.position, rightWrist.position);
        float delta = currentDistance - prevDistance;

        if (Mathf.Abs(delta) > deadzone)
        {
            float newScale = Mathf.Clamp(
                specimenAnchor.localScale.x * (1f + delta * sensitivity),
                minScale, maxScale
            );
            specimenAnchor.localScale = Vector3.one * newScale;
        }

        prevDistance = currentDistance;
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
}