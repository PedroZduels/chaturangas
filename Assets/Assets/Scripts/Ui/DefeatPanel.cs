using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shown when the player loses all pieces.
/// GameController calls <see cref="Show"/> directly — panel must start INACTIVE.
/// </summary>
public class DefeatPanel : MonoBehaviour
{
    [Header("Labels")]
    public TMP_Text messageText;

    [Header("Buttons")]
    public Button newRunButton;
    public Button mainMenuButton;

    [Header("Scene names")]
    public string gameSceneName     = "SampleScene";
    public string mainMenuSceneName = "MainMenu";

    private const string DefeatMessage = "You could not beat this master";

    private bool initialized;

    void OnEnable()
    {
        if (initialized) return;
        initialized = true;

        if (newRunButton   != null) newRunButton  .onClick.AddListener(OnNewRun);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by GameController when the player's last piece is removed.</summary>
    public void Show()
    {
        if (messageText != null) messageText.text = DefeatMessage;
        AudioManager.PlayDefeat();
        gameObject.SetActive(true);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    /// <summary>Restarts the game scene for a fresh run.</summary>
    public void OnNewRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Returns to the main menu.</summary>
    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
