using UnityEngine;

public class PinchZoom : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform specimenAnchor;

    [Header("Scale Settings")]
    public float minScale = 0.05f;
    public float maxScale = 5f;
    public float sensitivity = 2f;
    public float pinchThreshold = 0.7f;

    private bool wasPinching = false;
    private float prevDistance = 0f;

    void Update()
    {
        float leftPinch = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,
                                        OVRInput.Controller.LHand);
        float rightPinch = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger,
                                         OVRInput.Controller.RHand);

        bool bothPinching = leftPinch > pinchThreshold && rightPinch > pinchThreshold;

        Vector3 leftPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LHand);
        Vector3 rightPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RHand);

        if (bothPinching)
        {
            float currentDistance = Vector3.Distance(leftPos, rightPos);

            if (wasPinching)
            {
                float delta = currentDistance - prevDistance;
                float currentScale = specimenAnchor.localScale.x;
                float newScale = Mathf.Clamp(
                    currentScale * (1f + delta * sensitivity),
                    minScale, maxScale
                );
                specimenAnchor.localScale = Vector3.one * newScale;
            }

            prevDistance = currentDistance;
            wasPinching = true;
        }
        else
        {
            wasPinching = false;
        }
    }
}