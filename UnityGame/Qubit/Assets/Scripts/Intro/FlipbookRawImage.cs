using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a frame grid (left-to-right, top-to-bottom) on a RawImage by moving its uvRect.
/// All flipbooks read the same clock, so separate regions stay in sync.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class FlipbookRawImage : MonoBehaviour
{
    [SerializeField] int columns = 8;
    [SerializeField] int rows = 6;
    [SerializeField] int frameCount = 48;
    [SerializeField] float framesPerSecond = 12f;

    RawImage image;
    int lastFrame = -1;

    void Awake()
    {
        image = GetComponent<RawImage>();
    }

    void Update()
    {
        int frame = Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % frameCount;
        if (frame == lastFrame) return;
        lastFrame = frame;

        int col = frame % columns;
        int row = frame / columns;
        float w = 1f / columns;
        float h = 1f / rows;
        // Row 0 is the top of the texture, which is v = 1 in UV space.
        image.uvRect = new Rect(col * w, 1f - (row + 1) * h, w, h);
    }
}
