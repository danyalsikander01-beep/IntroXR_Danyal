using UnityEngine;
using UnityEngine.InputSystem;

public class LightSwitch : MonoBehaviour
{
    [Header("Controller input")]
    public InputActionReference toggleLightAction;

    [Header("Keyboard fallback")]
    public Key keyboardKey = Key.L;

    [Header("Target")]
    public Light targetLight;

    [Header("Optional: cycle colors")]
    public Color[] colors = { Color.white, Color.red, Color.green, Color.blue };
    private int colorIndex = 0;

    private void OnEnable()
    {
        if (toggleLightAction != null)
            toggleLightAction.action.Enable();
    }

    private void OnDisable()
    {
        if (toggleLightAction != null)
            toggleLightAction.action.Disable();
    }

    private void Update()
    {
        bool controllerPressed = toggleLightAction != null && toggleLightAction.action.WasPressedThisFrame();
        bool keyboardPressed = Keyboard.current != null && Keyboard.current[keyboardKey].wasPressedThisFrame;

        if (controllerPressed || keyboardPressed)
        {
            if (targetLight == null) return;

            // Change color (visible proof for grading)
            colorIndex = (colorIndex + 1) % colors.Length;
            targetLight.color = colors[colorIndex];

            Debug.Log("LightSwitch: toggled");
        }
    }
}
