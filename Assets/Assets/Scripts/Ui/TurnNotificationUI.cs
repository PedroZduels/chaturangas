using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles two UI responsibilities:
///   1. Persistent turn indicator — shows "YOUR TURN" or "AI TURN" whenever the active side changes.
///   2. "I CAN'T MOVE" alert — a timed overlay shown when the AI has no legal moves.
///
/// Requires two separate panel GameObjects wired in the Inspector:
///   • turnIndicatorPanel  — always-visible label that switches text each turn.
///   • alertPanel          — short-lived overlay shown for alertDuration seconds.
///
/// The panel background and optional Outline component are also driven per turn:
///   • Player turn — uses playerTurnColor for background, outline, and text.
///   • AI turn     — black background, cyan outline and cyan text.
///
/// Implements IBossHudElement so boss effects can recolor the cyan accent.
/// </summary>
public class TurnNotificationUI : MonoBehaviour, IBossHudElement
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Turn Indicator (persistent)")]
    [Tooltip("Panel that is always visible and shows whose turn it is.")]
    public GameObject turnIndicatorPanel;

    [Tooltip("TMP label inside turnIndicatorPanel.")]
    public TMP_Text turnIndicatorText;

    [Tooltip("Color used when it is the player's turn (panel background). Text will be black, no outline.")]
    public Color playerTurnColor = new Color(0f, 1f, 1f, 1f);

    [Tooltip("Color used when it is the AI's turn (text and outline). Background is always black for AI.")]
    public Color aiTurnColor = new Color(0f, 1f, 1f, 1f);

    [Header("Alert (timed overlay)")]
    [Tooltip("Panel shown briefly when the AI has no legal moves.")]
    public GameObject alertPanel;

    [Tooltip("TMP label inside alertPanel.")]
    public TMP_Text alertText;

    [Tooltip("How many seconds the alert stays on screen.")]
    public float alertDuration = 2f;

    // ── Constants ──────────────────────────────────────────────────────────────

    private const string PlayerTurnLabel   = "YOUR TURN";
    private const string AITurnLabel       = "AI TURN";
    private const string AISkipMessage     = "I CAN'T MOVE\nYOUR TURN";
    private const string PlayerSkipMessage = "YOU CAN'T MOVE\nSKIPPING TURN";

    private static readonly Color AIBackgroundColor     = Color.black;
    private static readonly Color PlayerTextColor       = Color.black;

    // ── Private ────────────────────────────────────────────────────────────────

    private GameController gameController;
    private Coroutine      alertRoutine;
    private Image          panelImage;
    private Outline        panelOutline;

    // ── Unity ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (turnIndicatorPanel != null)
        {
            panelImage   = turnIndicatorPanel.GetComponent<Image>();
            panelOutline = turnIndicatorPanel.GetComponent<Outline>();
        }

        gameController = Object.FindAnyObjectByType<GameController>();
        HideAlert();
        SetIndicator(PlayerTurnLabel, isAI: false);
    }

    void OnEnable()
    {
        if (gameController == null) return;
        gameController.OnPlayerTurnStarted += HandlePlayerTurn;
        gameController.OnAITurnStarted     += HandleAITurn;
        gameController.OnAISkippedTurn     += HandleAISkipped;
        gameController.OnPlayerSkippedTurn += HandlePlayerSkipped;
    }

    void OnDisable()
    {
        if (gameController == null) return;
        gameController.OnPlayerTurnStarted -= HandlePlayerTurn;
        gameController.OnAITurnStarted     -= HandleAITurn;
        gameController.OnAISkippedTurn     -= HandleAISkipped;
        gameController.OnPlayerSkippedTurn -= HandlePlayerSkipped;
    }

    // ── Handlers ───────────────────────────────────────────────────────────────

    private void HandlePlayerTurn()
    {
        SetIndicator(PlayerTurnLabel, isAI: false);
    }

    private void HandleAITurn()
    {
        SetIndicator(AITurnLabel, isAI: true);
    }

    private void HandleAISkipped()
    {
        SetIndicator(PlayerTurnLabel, isAI: false);
        ShowAlert(AISkipMessage);
    }

    private void HandlePlayerSkipped()
    {
        // Keep the turn indicator showing "YOUR TURN" while the alert is visible.
        SetIndicator(PlayerTurnLabel, isAI: false);
        ShowAlert(PlayerSkipMessage);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    /// <summary>Updates the persistent turn indicator: text, panel background, and outline.</summary>
    private void SetIndicator(string label, bool isAI)
    {
        if (turnIndicatorPanel != null)
            turnIndicatorPanel.SetActive(true);

        if (isAI)
        {
            // AI turn: black background, cyan outline, cyan text.
            if (panelImage   != null) panelImage.color        = AIBackgroundColor;
            if (panelOutline != null) panelOutline.enabled    = true;
            if (panelOutline != null) panelOutline.effectColor = aiTurnColor;
            if (turnIndicatorText != null)
            {
                turnIndicatorText.text  = label;
                turnIndicatorText.color = aiTurnColor;
            }
        }
        else
        {
            // Player turn: cyan background, no outline, black text.
            if (panelImage   != null) panelImage.color     = playerTurnColor;
            if (panelOutline != null) panelOutline.enabled = false;
            if (turnIndicatorText != null)
            {
                turnIndicatorText.text  = label;
                turnIndicatorText.color = PlayerTextColor;
            }
        }
    }

    /// <summary>Shows the timed alert panel with the given message.</summary>
    private void ShowAlert(string message)
    {
        if (alertPanel == null || alertText == null) return;

        if (alertRoutine != null)
            StopCoroutine(alertRoutine);

        alertText.text = message;
        alertPanel.SetActive(true);
        alertRoutine = StartCoroutine(HideAlertAfterDelay());
    }

    private IEnumerator HideAlertAfterDelay()
    {
        yield return new WaitForSeconds(alertDuration);
        HideAlert();
    }

    private void HideAlert()
    {
        if (alertPanel != null)
            alertPanel.SetActive(false);
    }

    // ── IBossHudElement ────────────────────────────────────────────────────────

    /// <summary>Stores the current cyan accent colors and replaces them with the boss tint.</summary>
    public void ApplyBossTint(Color tint, Dictionary<int, Color> originalColors)
    {
        StoreAndReplace(ref playerTurnColor, tint, originalColors, GetInstanceID());
        StoreAndReplace(ref aiTurnColor,     tint, originalColors, GetInstanceID() + 1);

        // Refresh whichever indicator is currently showing.
        bool isAI = panelImage != null && panelImage.color == AIBackgroundColor;
        SetIndicator(isAI ? AITurnLabel : PlayerTurnLabel, isAI);
    }

    /// <summary>Restores the original cyan accent colors.</summary>
    public void RestoreTint(Dictionary<int, Color> originalColors)
    {
        if (originalColors.TryGetValue(GetInstanceID(),     out Color c0)) playerTurnColor = c0;
        if (originalColors.TryGetValue(GetInstanceID() + 1, out Color c1)) aiTurnColor     = c1;

        bool isAI = panelImage != null && panelImage.color == AIBackgroundColor;
        SetIndicator(isAI ? AITurnLabel : PlayerTurnLabel, isAI);
    }

    private static void StoreAndReplace(ref Color field, Color tint,
                                        Dictionary<int, Color> store, int key)
    {
        if (!store.ContainsKey(key)) store[key] = field;
        field = tint;
    }
}
