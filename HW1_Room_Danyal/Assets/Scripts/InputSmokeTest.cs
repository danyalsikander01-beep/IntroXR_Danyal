using UnityEngine;
using UnityEngine.InputSystem;

public class InputSmokeTest : MonoBehaviour
{
    public InputActionReference testAction;

    private void OnEnable()
    {
        if (testAction != null)
            testAction.action.Enable();
    }

    private void OnDisable()
    {
        if (testAction != null)
            testAction.action.Disable();
    }

    private void Update()
    {
        if (testAction != null && testAction.action.WasPressedThisFrame())
        {
            Debug.Log("TestButton pressed");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
