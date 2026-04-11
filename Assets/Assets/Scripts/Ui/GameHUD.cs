using TMPro;
using UnityEngine;

/// <summary>
/// Keeps the timer and bits display in sync with GameController.
/// Attach to the HUD GameObject. Wire timerText → TimerValue, bitsText → BitsValue.
/// Use backgroundHeight to control the height of the HUD background strip from the Inspector.
/// </summary>
[ExecuteAlways]
public class GameHUD : MonoBehaviour
{
    [Header("Text fields")]
    public TMP_Text timerText;
    public TMP_Text bitsText;

    [Header("Layout")]
    [Tooltip("Height in pixels of the HUD background strip. Adjust this when content overflows.")]
    public float backgroundHeight = 70f;

    private RectTransform rectTransform;
    private GameController gameController;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif
        gameController = Object.FindAnyObjectByType<GameController>();
    }

    void OnEnable()
    {
        if (gameController != null)
            gameController.OnBitsChanged += OnBitsChanged;

        ApplyBackgroundHeight();
    }

    void OnDisable()
    {
        if (gameController != null)
            gameController.OnBitsChanged -= OnBitsChanged;
    }

    void Update()
    {
#if UNITY_EDITOR
        // Keep height in sync while editing in the Inspector.
        ApplyBackgroundHeight();
        if (!Application.isPlaying) return;
#endif
        if (gameController == null || timerText == null) return;

        float t   = Mathf.Max(0f, gameController.RemainingTime);
        int   min = (int)(t / 60f);
        int   sec = (int)(t % 60f);
        timerText.text = $"{min}:{sec:00}";
    }

    private void OnBitsChanged(int newBits)
    {
        if (bitsText != null)
            bitsText.text = newBits.ToString();
    }

    /// <summary>Writes the desired backgroundHeight into the RectTransform's sizeDelta.y.</summary>
    private void ApplyBackgroundHeight()
    {
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        if (rectTransform == null) return;

        Vector2 sd = rectTransform.sizeDelta;
        if (!Mathf.Approximately(sd.y, backgroundHeight))
        {
            sd.y = backgroundHeight;
            rectTransform.sizeDelta = sd;
        }
    }
}
