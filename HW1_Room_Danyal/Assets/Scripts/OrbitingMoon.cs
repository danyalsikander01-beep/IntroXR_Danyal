using UnityEngine;

public class OrbitingMoon : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 30f; // degrees per second

    void Update()
    {
        // Rotate around Y axis using deltaTime (frame-rate independent)
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
    }
}
