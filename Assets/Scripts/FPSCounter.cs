using UnityEngine;

/// <summary>
/// Simple FPS counter displayed in the top-right corner of the screen.
/// Automatically attaches to any GameObject in the scene.
/// </summary>
public class FPSCounter : MonoBehaviour
{
    private float deltaTime = 0.0f;
    private GUIStyle style;

    void Awake()
    {
        // Setup text style
        style = new GUIStyle();
        style.alignment = TextAnchor.UpperRight;
        style.fontSize = 24;
        style.normal.textColor = Color.white;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void OnGUI()
    {
        int w = Screen.width;
        int h = 30;

        Rect rect = new Rect(w - 100, 10, 90, h);

        float msec = deltaTime * 1000.0f;
        float fps = 1.0f / deltaTime;
        string text = string.Format("{0:0.} FPS", fps);

        // Draw shadow for better visibility
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);

        // Draw main text
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        // Auto-create FPS counter on play
        if (FindObjectOfType<FPSCounter>() == null)
        {
            GameObject fpsObj = new GameObject("FPS Counter");
            fpsObj.AddComponent<FPSCounter>();
            DontDestroyOnLoad(fpsObj);
            Debug.Log("FPS Counter created automatically");
        }
    }
}
