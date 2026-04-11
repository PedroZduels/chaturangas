using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shown when the player wins a fight.
/// GameController calls Show(remainingTime, alivePieces) directly — panel can safely start INACTIVE.
/// WinRewardButton and TimeRewardButton are individually clickable; each grants its reward once.
/// The alive-squad bonus (2 bits per surviving piece) is granted automatically on Show.
/// FloppyRewardButton is only shown on a 20% roll.
/// ForwardButton advances to the next fight or opens the Mainframe.
/// </summary>
public class VictoryPanel : MonoBehaviour
{
    [Header("References")]
    public GameController  gameController;
    public FloppyInventory floppyInventory;

    [Header("Reward buttons")]
    public Button   winRewardButton;
    public Button   timeRewardButton;

    [Header("Reward labels (child of each button)")]
    public TMP_Text winLabel;
    public TMP_Text timeLabel;

    [Header("Alive squad bonus label (auto-granted, no button needed)")]
    [Tooltip("Text element that shows the automatic alive-squad bits reward.")]
    public TMP_Text aliveLabel;

    [Header("Floppy reward button (shown on 20% roll, start INACTIVE)")]
    [Tooltip("Button styled like WinRewardButton. Shown only when a floppy drops. Start inactive.")]
    public Button   floppyRewardButton;
    [Tooltip("Icon image inside FloppyRewardButton — set to the rolled floppy's sprite at runtime.")]
    public Image    floppyRewardIcon;
    [Tooltip("Label inside FloppyRewardButton — set to the rolled floppy's name at runtime.")]
    public TMP_Text floppyRewardLabel;
    [Tooltip("Pool of floppies eligible to drop from fights.")]
    public FloppyDefinition[] floppyDropPool;
    [Tooltip("Fallback sprite when a FloppyDefinition has no icon assigned.")]
    public Sprite   floppyFallbackSprite;

    [Header("Title and Next Fight")]
    public TMP_Text titleText;
    public Button   forwardButton;

    [Header("Shown on mainframe-threshold fights instead of starting next fight")]
    public GameObject mainframePanel;

    private const int   WinBonusBits        = 5;
    private const int   AliveBitsPerPiece   = 2;
    private const float FloppyDropChance    = 0.20f;

    private float            timeAtWin;
    private bool             initialized;
    private FloppyDefinition rolledFloppy;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (floppyInventory == null)
            floppyInventory = Object.FindAnyObjectByType<FloppyInventory>();

        if (initialized) return;
        initialized = true;

        if (winRewardButton    != null) winRewardButton   .onClick.AddListener(OnWinReward);
        if (timeRewardButton   != null) timeRewardButton  .onClick.AddListener(OnTimeReward);
        if (floppyRewardButton != null) floppyRewardButton.onClick.AddListener(OnFloppyReward);
        if (forwardButton      != null) forwardButton     .onClick.AddListener(OnForward);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Called by GameController. Captures remaining time, auto-grants alive-squad bits,
    /// and shows the panel. <paramref name="alivePieceCount"/> must be passed before the
    /// squad is respawned so the count reflects pieces that survived the fight.
    /// </summary>
    public void Show(float remainingTime, int alivePieceCount)
    {
        timeAtWin = remainingTime;
        int timeBits  = Mathf.FloorToInt(timeAtWin / 60f);
        int aliveBits = alivePieceCount * AliveBitsPerPiece;

        // Auto-grant alive-squad bonus immediately.
        gameController?.AddBits(aliveBits);
        Debug.Log($"[Victory] Auto-granted {aliveBits} bits for {alivePieceCount} surviving piece(s).");

        if (titleText        != null) titleText.text  = "Victory!";
        if (winLabel         != null) winLabel.text   = $"Win bonus  +{WinBonusBits} bits";
        if (timeLabel        != null) timeLabel.text  = $"Time bonus  +{timeBits} bits";
        if (aliveLabel       != null) aliveLabel.text = $"Squad alive  +{aliveBits} bits";

        if (winRewardButton  != null) winRewardButton .interactable = true;
        if (timeRewardButton != null) timeRewardButton.interactable = true;
        if (forwardButton    != null) forwardButton   .interactable = true;

        // Roll floppy drop (20% chance, only if pool and inventory are ready).
        rolledFloppy = RollFloppyDrop();
        SetupFloppyButton(rolledFloppy);

        AudioManager.PlayVictory();

        gameObject.SetActive(true);
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnWinReward()
    {
        gameController?.AddBits(WinBonusBits);
        if (winRewardButton != null) winRewardButton.interactable = false;
    }

    private void OnTimeReward()
    {
        gameController?.AddBits(Mathf.FloorToInt(timeAtWin / 60f));
        if (timeRewardButton != null) timeRewardButton.interactable = false;
    }

    private void OnFloppyReward()
    {
        if (rolledFloppy == null || floppyInventory == null) return;
        if (floppyInventory.TryAdd(rolledFloppy))
        {
            if (floppyRewardButton != null) floppyRewardButton.interactable = false;
            Debug.Log($"[Victory] Floppy reward claimed: {rolledFloppy.displayName}");
        }
    }

    private void OnForward()
    {
        gameObject.SetActive(false);

        if (gameController != null
            && gameController.FightCount % GameController.FightsBeforeMainframe == 0
            && mainframePanel != null)
        {
            mainframePanel.SetActive(true);
        }
        else
        {
            gameController?.StartNextFight();
        }
    }

    // ── Floppy drop helpers ───────────────────────────────────────────────────

    /// <summary>Returns a randomly chosen FloppyDefinition on a 20% roll, or null.</summary>
    private FloppyDefinition RollFloppyDrop()
    {
        if (Random.value >= FloppyDropChance) return null;
        if (floppyDropPool == null || floppyDropPool.Length == 0) return null;
        if (floppyInventory == null || !floppyInventory.HasRoom) return null;

        return floppyDropPool[Random.Range(0, floppyDropPool.Length)];
    }

    /// <summary>Shows or hides FloppyRewardButton and populates its icon and label.</summary>
    private void SetupFloppyButton(FloppyDefinition def)
    {
        bool show = def != null;
        if (floppyRewardButton != null)
        {
            floppyRewardButton.gameObject.SetActive(show);
            floppyRewardButton.interactable = show;
        }

        if (!show) return;

        if (floppyRewardIcon  != null)
            floppyRewardIcon.sprite = (def.icon != null) ? def.icon : floppyFallbackSprite;

        if (floppyRewardLabel != null)
            floppyRewardLabel.text = def.displayName;
    }
}


