using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Drives the main menu. Wire up all panels and buttons in the Inspector.
/// Assign a <see cref="MainMenuData"/> asset to control all displayed text
/// without editing this script.
/// The scene to load on "Start Run" is set via <see cref="gameSceneName"/>.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("ScriptableObject that holds all editable main menu text.")]
    public MainMenuData menuData;

    [Header("Root Panels")]
    public GameObject mainPanel;
    public GameObject infoPanel;
    public GameObject optionsPanel;

    [Header("Main Panel Text")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI startRunButtonText;
    public TextMeshProUGUI infoButtonText;
    public TextMeshProUGUI optionsButtonText;
    public TextMeshProUGUI exitButtonText;

    [Header("Info Panel Text")]
    public TextMeshProUGUI infoTitleText;
    public TextMeshProUGUI infoBackButtonText;

    [Header("Info Panel Builder")]
    [Tooltip("InfoPanelBuilder component on the ScrollView Content GameObject.")]
    public InfoPanelBuilder infoPanelBuilder;

    [Header("Options Panel Text")]
    public TextMeshProUGUI optionsBackButtonText;

    [Header("Main Buttons")]
    public Button startRunButton;
    public Button infoButton;
    public Button optionsButton;
    public Button exitButton;

    [Header("Back Buttons")]
    public Button infoBackButton;
    public Button optionsBackButton;

    [Header("Scene")]
    [Tooltip("Name of the game scene (must be added to Build Settings).")]
    public string gameSceneName = "SampleScene";

    void Awake()
    {
        ApplyMenuData();
        RegisterListeners();
        ShowMain();
    }

    // ── Data application ──────────────────────────────────────────────────────

    /// <summary>Pushes all values from the assigned <see cref="MainMenuData"/> into the UI.</summary>
    private void ApplyMenuData()
    {
        if (menuData == null)
            return;

        SetText(titleText,             menuData.gameTitle);
        SetText(startRunButtonText,    menuData.startRunLabel);
        SetText(infoButtonText,        menuData.infoLabel);
        SetText(optionsButtonText,     menuData.optionsLabel);
        SetText(exitButtonText,        menuData.exitLabel);
        SetText(infoTitleText,         menuData.infoTitle);
        SetText(infoBackButtonText,    menuData.infoBackLabel);
        if (infoPanelBuilder != null)  infoPanelBuilder.Build(menuData);
        SetText(optionsBackButtonText, menuData.optionsBackLabel);
    }

    private static void SetText(TextMeshProUGUI label, string value)
    {
        if (label != null)
            label.text = value;
    }

    // ── Listener registration ─────────────────────────────────────────────────

    private void RegisterListeners()
    {
        if (startRunButton    != null) startRunButton   .onClick.AddListener(OnStartRun);
        if (infoButton        != null) infoButton       .onClick.AddListener(OnInfo);
        if (optionsButton     != null) optionsButton    .onClick.AddListener(OnOptions);
        if (exitButton        != null) exitButton       .onClick.AddListener(OnExit);
        if (infoBackButton    != null) infoBackButton   .onClick.AddListener(ShowMain);
        if (optionsBackButton != null) optionsBackButton.onClick.AddListener(ShowMain);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    /// <summary>Loads the game scene and starts a new run.</summary>
    public void OnStartRun()
    {
        AudioManager.PlayUIClick();
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Shows the Info panel.</summary>
    public void OnInfo()
    {
        AudioManager.PlayUIClick();
        SetPanels(main: false, info: true, options: false);
    }

    /// <summary>Shows the Options panel.</summary>
    public void OnOptions()
    {
        AudioManager.PlayUIClick();
        SetPanels(main: false, info: false, options: true);
    }

    /// <summary>Quits the application (or stops play mode in the Editor).</summary>
    public void OnExit()
    {
        AudioManager.PlayUIClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Panel helpers ─────────────────────────────────────────────────────────

    private void ShowMain()
    {
        AudioManager.PlayUIClick();
        SetPanels(main: true, info: false, options: false);
    }

    private void SetPanels(bool main, bool info, bool options)
    {
        if (mainPanel    != null) mainPanel   .SetActive(main);
        if (infoPanel    != null) infoPanel   .SetActive(info);
        if (optionsPanel != null) optionsPanel.SetActive(options);
    }
}
