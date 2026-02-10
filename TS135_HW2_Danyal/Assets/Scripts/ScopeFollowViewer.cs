using UnityEngine;

public class ScopeFollowViewer : MonoBehaviour
{
    public Camera viewerCamera;      // XR Main Camera
    public Transform lensAnchor;     // MagnifyingGlass_V2/Model/LensAnchor
    public float backOffset = 0.02f; // 2 cm behind lens

    void LateUpdate()
    {
        if (!viewerCamera || !lensAnchor) return;

        transform.position = lensAnchor.position - viewerCamera.transform.forward * backOffset;
        transform.rotation = viewerCamera.transform.rotation;
    }
}