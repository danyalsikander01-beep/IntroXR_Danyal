using UnityEngine;
using TMPro;

public class ScaleLabel : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public Transform specimenAnchor;

    void Update()
    {
        float scale = specimenAnchor.localScale.x;

        string display;
        if (scale > 0.5f) display = $"Scale: {scale:F2} m";
        else if (scale > 0.001f) display = $"Scale: {scale * 1000f:F1} mm";
        else if (scale > 0.000001f) display = $"Scale: {scale * 1000000f:F1} μm";
        else if (scale > 0.000000001f) display = $"Scale: {scale * 1e9f:F1} nm";
        else display = $"Scale: {scale * 1e12f:F1} pm";

        labelText.text = display;
    }
}