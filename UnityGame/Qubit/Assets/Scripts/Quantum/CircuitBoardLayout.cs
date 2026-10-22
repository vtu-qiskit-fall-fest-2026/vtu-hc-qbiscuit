using UnityEngine;

/// <summary>
/// Spreads the circuit board's qubit rows over the board's current height.
/// Works for any number of rows: add rows and tag new elements with BoardAnchor.
/// </summary>
public class CircuitBoardLayout : MonoBehaviour
{
    [SerializeField] int rows = 5;
    [Tooltip("Space reserved for the column headers.")]
    [SerializeField] float headerHeight = 14f;
    [SerializeField] float bottomPadding = 4f;
    [Tooltip("Row spacing the gates were authored at.")]
    [SerializeField] float basePitch = 14f;
    [SerializeField] float minPitch = 8f;
    [SerializeField] float maxPitch = 30f;
    [SerializeField] float maxGateScale = 1.35f;

    BoardAnchor[] anchors;

    public int Rows => rows;

    public void Layout()
    {
        if (anchors == null)
        {
            anchors = GetComponentsInChildren<BoardAnchor>(true);
            foreach (var a in anchors) a.Capture();
        }

        float height = ((RectTransform)transform).rect.height;
        float available = height - headerHeight - bottomPadding;
        float pitch = Mathf.Clamp(available / rows, minPitch, maxPitch);
        // Centre the rows in the space left below the headers.
        float rowTop = headerHeight + Mathf.Max(0f, (available - pitch * rows) * 0.5f);
        float scale = Mathf.Clamp(pitch / basePitch, minPitch / basePitch, maxGateScale);

        foreach (var a in anchors) a.Place(rowTop, pitch, rows, scale);
    }
}
