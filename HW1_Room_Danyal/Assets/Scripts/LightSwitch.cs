using UnityEngine;

public class LightSwitch : MonoBehaviour
{
    private Light pointLight;

    void Start()
    {
        pointLight = GetComponent<Light>();
    }

    void Update()
    {
        Debug.Log("Update running");
        if (Input.GetKeyDown(KeyCode.L))
        {
            pointLight.enabled = !pointLight.enabled;
        }
    }
}
