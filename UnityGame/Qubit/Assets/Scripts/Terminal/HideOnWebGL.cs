using UnityEngine;

/// <summary>
/// Switches its GameObject off in WebGL builds, for options that cannot work in a browser
/// such as quitting the application. Runs before menus collect their items.
/// </summary>
[DefaultExecutionOrder(-100)]
public class HideOnWebGL : MonoBehaviour
{
    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        gameObject.SetActive(false);
#endif
    }
}
