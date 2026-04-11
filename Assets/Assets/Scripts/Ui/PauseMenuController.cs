using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles the in-game pause menu triggered by the Escape key.
/// Attach to a persistent Canvas in the game scene. Wire the pause panel,
/// its buttons, the options panel, and the volume sliders in the Inspector.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Pause Panel")]
    [Tooltip("Root GameObject of the pause overlay. Starts inactive.")]
    public GameObject pausePanel;

    [Header("Buttons")]
    public Button continueButton;
    public Button optionsButton;
    public Button exitToMenuButton;

    [Header("Options Panel (child of pause card with the volume sliders)")]
    [Tooltip("GameObject that contains the music/SFX sliders. Toggled by the Options button.")]
    public GameObject optionsPanel;

    [Header("Volume Sliders")]
    [Tooltip("Slider controlling background music volume (0–1).")]
    public Slider musicSlider;
    [Tooltip("Slider controlling SFX volume (0–1).")]
    public Slider sfxSlider;

    [Header("Scene")]
    [Tooltip("Name of the main menu scene to load on exit.")]
    public string mainMenuSceneName = "MainMenu";

    private bool _isPaused;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (pausePanel   != null) pausePanel  .SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        if (continueButton   != null) continueButton  .onClick.AddListener(Continue);
        if (optionsButton    != null) optionsButton   .onClick.AddListener(ToggleOptions);
        if (exitToMenuButton != null) exitToMenuButton.onClick.AddListener(ExitToMenu);

        if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider   != null) sfxSlider  .onValueChanged.AddListener(OnSfxChanged);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Resumes the game and hides the pause panel.</summary>
    public void Continue()
    {
        AudioManager.PlayUIClick();
        SetPaused(false);
    }

    /// <summary>Toggles the options (volume sliders) sub-panel.</summary>
    public void ToggleOptions()
    {
        AudioManager.PlayUIClick();
        if (optionsPanel == null) return;
        bool next = !optionsPanel.activeSelf;
        optionsPanel.SetActive(next);
        if (next) SyncSliders();
    }

    /// <summary>Loads the main menu scene and restores time scale.</summary>
    public void ExitToMenu()
    {
        AudioManager.PlayUIClick();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ── Slider handlers ───────────────────────────────────────────────────────

    private void OnMusicChanged(float value) => AudioManager.SetMusicVolume(value);
    private void OnSfxChanged(float value)   => AudioManager.SetSFXVolume(value);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TogglePause() => SetPaused(!_isPaused);

    private void SetPaused(bool paused)
    {
        _isPaused      = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pausePanel   != null) pausePanel  .SetActive(paused);

        // Hide options sub-panel whenever we close the pause menu.
        if (!paused && optionsPanel != null) optionsPanel.SetActive(false);
    }

    private void SyncSliders()
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioManager.GetMusicVolume());
        if (sfxSlider   != null) sfxSlider  .SetValueWithoutNotify(AudioManager.GetSFXVolume());
    }
}
