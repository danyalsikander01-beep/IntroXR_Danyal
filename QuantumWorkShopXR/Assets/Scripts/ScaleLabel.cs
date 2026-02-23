using UnityEngine;
using TMPro;

public class ScaleLabel : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public Transform specimenAnchor;

    void Update()
    {
        if (specimenAnchor == null || labelText == null) return;

        float scale = specimenAnchor.localScale.x;

        // As sphere grows bigger = zooming IN = smaller real world units
        // scale 1 = 1m, scale 5 = 1mm, scale 25 = 1um, scale 125 = 1nm, scale 625 = 1pm
        float realSize = 1f / scale; // inverse relationship

        string display;
        if (realSize >= 0.01f)
            display = $"Scale: {realSize:F2} m";
        else if (realSize >= 0.00001f)
            display = $"Scale: {realSize * 1000f:F2} mm";
        else if (realSize >= 0.000000001f)
            display = $"Scale: {realSize * 1e6f:F2} μm";
        else if (realSize >= 0.000000000001f)
            display = $"Scale: {realSize * 1e9f:F2} nm";
        else
            display = $"Scale: {realSize * 1e12f:F2} pm";

        labelText.text = display;
    }
}