using UnityEngine;
using TMPro;

public class ScaleLabel : MonoBehaviour
{
    public TextMeshProUGUI labelText;
    public Transform specimenAnchor;

    void Update()
    {
        if (labelText == null) return;

        float zoom = PinchZoom.ZoomLevel;
        string unit, description;

        if (zoom < 2f) { unit = "~ 1 m"; description = "Human scale"; }
        else if (zoom < 20f) { unit = "~ 1 mm"; description = "Cell scale"; }
        else if (zoom < 200f) { unit = "~ 1 um"; description = "Molecular scale"; }
        else if (zoom < 1500f) { unit = "~ 1 nm"; description = "Atomic scale"; }
        else { unit = "~ 1 pm"; description = "Nuclear scale"; }

        labelText.text = $"{unit}\n{description}";
    }
}