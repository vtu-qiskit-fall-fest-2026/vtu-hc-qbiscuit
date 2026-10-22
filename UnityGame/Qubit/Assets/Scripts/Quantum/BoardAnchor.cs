using UnityEngine;

/// <summary>
/// Ties a circuit-board element to a qubit row so CircuitBoardLayout can place it
/// for any board height. Elements keep their horizontal position (their column).
/// </summary>
public class BoardAnchor : MonoBehaviour
{
    public enum Mode
    {
        Center,      // gate, meter or label centred on a row
        Wire,        // 1 px horizontal wire on a row
        Span,        // vertical line from one row's centre to another's (e.g. a CNOT)
        FullHeight   // covers every row (e.g. the scan bar)
    }

    public Mode mode = Mode.Center;
    public int row;
    [Tooltip("Last row for Span mode.")]
    public int rowEnd;
    [Tooltip("Grow with the row spacing when the board is enlarged.")]
    public bool scales = true;

    bool captured;
    float centreX;

    public RectTransform Rect => (RectTransform)transform;

    /// <summary>Records the authored column, and centres the pivot so the element scales in place.</summary>
    public void Capture()
    {
        if (captured) return;
        captured = true;
        if (mode != Mode.Center) return;
        var rt = Rect;
        centreX = rt.anchoredPosition.x + rt.sizeDelta.x * (0.5f - rt.pivot.x);
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    public void Place(float rowTop, float pitch, int rows, float scale)
    {
        var rt = Rect;
        float centre = rowTop + pitch * (row + 0.5f);
        switch (mode)
        {
            case Mode.Center:
                rt.anchoredPosition = new Vector2(centreX, -centre);
                rt.localScale = scales ? new Vector3(scale, scale, 1f) : Vector3.one;
                break;
            case Mode.Wire:
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -centre);
                break;
            case Mode.Span:
                float end = rowTop + pitch * (rowEnd + 0.5f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -centre);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, end - centre);
                break;
            case Mode.FullHeight:
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -rowTop);
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, pitch * rows);
                break;
        }
    }
}
