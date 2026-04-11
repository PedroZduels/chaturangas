using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current fight number next to the board in vertical orientation.
/// Shows "Level N" for normal fights and "Boss Fight" for boss encounters.
/// Subscribes to GameController.OnFightLoaded to stay in sync across fights.
/// Implements IBossHudElement so the Communist Grandmaster boss can recolor the cyan text.
/// </summary>
public class LevelCounter : MonoBehaviour, IBossHudElement
{
    [Header("Dependencies")]
    public GameController gameController;

    [Header("Display")]
    public TMP_Text label;

    private const string BossLabel   = "BOSS\nFIGHT";
    private const string LevelPrefix  = "LEVEL";
    private const string FontAssetPath = "Fonts & Materials/Electronic Highway Sign SDF";

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();

        ApplyFont();
    }

    void OnEnable()
    {
        if (gameController != null)
            gameController.OnFightLoaded += OnFightLoaded;
    }

    void OnDisable()
    {
        if (gameController != null)
            gameController.OnFightLoaded -= OnFightLoaded;
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>Loads the shared font asset at runtime so the material is always valid.</summary>
    private void ApplyFont()
    {
        if (label == null) return;

        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(FontAssetPath);
        if (font != null)
            label.font = font;
    }

    /// <summary>Updates the label whenever a new fight starts.</summary>
    private void OnFightLoaded(int fightNumber, bool isBoss)
    {
        if (label == null) return;
        label.text = isBoss ? BossLabel : BuildLevelText(fightNumber);
    }

    /// <summary>Returns the fight number spread vertically, one character per line.</summary>
    private static string BuildLevelText(int fightNumber)
    {
        string full = $"{LevelPrefix} {fightNumber}";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < full.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(full[i]);
        }
        return sb.ToString();
    }

    // ── IBossHudElement ───────────────────────────────────────────────────────

    /// <summary>Saves the current label color and applies the boss tint.</summary>
    public void ApplyBossTint(Color tint, Dictionary<int, Color> originalColors)
    {
        if (label == null) return;

        int key = GetInstanceID();
        if (!originalColors.ContainsKey(key))
            originalColors[key] = label.color;

        label.color = tint;
    }

    /// <summary>Restores the original label color.</summary>
    public void RestoreTint(Dictionary<int, Color> originalColors)
    {
        if (label == null) return;

        if (originalColors.TryGetValue(GetInstanceID(), out Color original))
            label.color = original;
    }
}
