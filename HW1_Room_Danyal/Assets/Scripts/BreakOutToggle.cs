using UnityEngine;
using UnityEngine.InputSystem;

public class BreakOutToggle : MonoBehaviour
{
    [Header("XR Origin (XR Rig)")]
    public Transform xrOrigin; // drag XR Origin here

    [Header("View Points")]
    public Transform insideViewPoint;
    public Transform outsideViewPoint;

    [Header("Input (optional)")]
    public InputActionReference toggleAction; // e.g., LeftHand/PrimaryButton

    [Header("Keyboard fallback (for testing without headset)")]
    public Key keyboardKey = Key.B;

    private bool isOutside = false;

    private void OnEnable()
    {
        if (toggleAction != null) toggleAction.action.Enable();
    }

    private void OnDisable()
    {
        if (toggleAction != null) toggleAction.action.Disable();
    }

    private void Update()
    {
        bool pressed =
            (toggleAction != null && toggleAction.action.WasPressedThisFrame()) ||
            (Keyboard.current != null && Keyboard.current[keyboardKey].wasPressedThisFrame);

        if (pressed)
        {
            TogglePosition();
        }
    }

    private void TogglePosition()
    {
        if (xrOrigin == null || insideViewPoint == null || outsideViewPoint == null)
        {
            Debug.LogWarning("BreakOutToggle: Assign xrOrigin, insideViewPoint, outsideViewPoint in the Inspector.");
            return;
        }

        Transform target = isOutside ? insideViewPoint : outsideViewPoint;

        // Teleport XR Origin to the target position + rotation
        xrOrigin.SetPositionAndRotation(target.position, target.rotation);

        isOutside = !isOutside;
        Debug.Log("BreakOutToggle: Moved to " + (isOutside ? "Outside" : "Inside"));
    }
}
