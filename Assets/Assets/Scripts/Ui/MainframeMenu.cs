using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mainframe upgrade market — shown every FightsBeforeMainframe wins.
/// VictoryPanel activates this panel; it hides itself only on Enter Fight.
///
/// Flow:
///   1. Panel activates → PieceBar populated with one button per unique player piece type.
///   2. Player clicks a piece → ConfirmPanel opens.
///   3. Confirm → 10 bits deducted, piece type upgraded permanently, bar refreshes.
///   4. Cancel → ConfirmPanel closes.
///   5. Enter Fight → panel hides, tier advances, next fight loads.
///
/// DiscRow / FloppyRow slots are intentionally untouched — reserved for future items.
///
/// Flow:
///   1. Panel activates → PieceBar populated with one button per unique player piece type.
///   2. Player clicks a piece → ConfirmPanel opens.
///   3. Confirm → 10 bits deducted, piece type upgraded permanently, bar refreshes.
///   4. Cancel → ConfirmPanel closes.
///   5. Enter Fight → panel hides, tier advances, next fight loads.
///
/// DiscRow / FloppyRow slots are intentionally untouched — reserved for future items.
/// </summary>
public class MainframeMenu : MonoBehaviour
{
    [Header("References")]
    public GameController gameController;
    public GameObject     levelCounter;          // disabled while the market is open
    public GameObject[]   hudElementsToHide;     // BoardNudge, AutoWinButton, TurnIndicatorPanel, ViewToggleButton

    [Header("Top-bar")]
    public TMP_Text bitsLabel;

    [Header("Piece Bar — populated at runtime from the player's squad")]
    public Transform pieceBar;

    [Header("Confirm Dialog")]
    public GameObject confirmPanel;
    public TMP_Text   confirmPieceLabel;
    public TMP_Text   confirmDescLabel;
    public Button     confirmButton;
    public Button     cancelButton;

    [Header("Enter Fight")]
    public Button enterFightButton;

    [Header("Upgrade Definitions — matched by piece type name, used for descriptions")]
    public UpgradeCardEntry[] upgradeCards;

    [System.Serializable]
    public class UpgradeCardEntry
    {
        [Tooltip("C# class name, e.g. 'Queen', 'Knight'.")]
        public string pieceTypeName;
        public string pieceName;
        [TextArea(1, 3)]
        public string description;

        /// <summary>Resolved at runtime from pieceTypeName.</summary>
        public System.Type PieceType =>
            System.Type.GetType(pieceTypeName)
            ?? System.Type.GetType($"{pieceTypeName}, Assembly-CSharp");
    }

    private const int UpgradeCost = 10;

    private readonly List<PieceBarEntry> pieceBarEntries = new List<PieceBarEntry>();

    // Badge colours
    private static readonly Color UpgradedCardColor = new Color(0.50f, 0.40f, 0.08f, 1f);  // dark gold bg
    private static readonly Color ArmoredCardColor  = new Color(0.10f, 0.28f, 0.45f, 1f);  // dark steel bg
    private static readonly Color BothCardColor     = new Color(0.40f, 0.32f, 0.08f, 1f);  // gold + steel blend
    private static readonly Color DefaultCardColor  = new Color(0.06f, 0.14f, 0.18f, 1f);  // dark cyan-tinted bg

    private static readonly Color ArmoredSpriteColor  = new Color(0.60f, 0.85f, 1.00f, 1f); // icy blue tint

    // ── Sprite tint helpers — read from PieceTintConfig when available ────────
    private static Color PlayerNormalColor   => GameController.TintConfig != null ? GameController.TintConfig.playerNormal   : new Color(0f, 1f, 0.95f, 1f);
    private static Color PlayerUpgradedColor => GameController.TintConfig != null ? GameController.TintConfig.playerUpgraded : new Color(1f, 0.84f, 0.10f, 1f);

    private class PieceBarEntry
    {
        public System.Type PieceType;
        public Button      Button;
        public TMP_Text    StatusLabel;
        public Image       CardBg;
        public Image       SpriteImg;
        public Piece       Piece;        // direct reference for live state reads
    }

    private Piece       pendingPiece;
    private bool        initialized;
    private bool        _fightEntered;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();

        if (!initialized)
        {
            initialized = true;
            if (confirmButton    != null) confirmButton   .onClick.AddListener(OnConfirm);
            if (cancelButton     != null) cancelButton    .onClick.AddListener(OnCancel);
            if (enterFightButton != null) enterFightButton.onClick.AddListener(OnEnterFight);
        }

        if (confirmPanel != null) confirmPanel.SetActive(false);

        _fightEntered = false;
        gameController.OnBitsChanged += OnBitsChanged;
        RefreshBitsLabel(gameController.Bits);
        PopulatePieceBar();

        AudioManager.SwitchContext(AudioManager.MusicContext.Market);

        if (levelCounter != null) levelCounter.SetActive(false);
        foreach (GameObject go in hudElementsToHide)
            if (go != null) go.SetActive(false);
    }

    void OnDisable()
    {
        if (gameController != null)
            gameController.OnBitsChanged -= OnBitsChanged;

        if (levelCounter != null) levelCounter.SetActive(true);
        foreach (GameObject go in hudElementsToHide)
            if (go != null) go.SetActive(true);
    }

    // ── Piece Bar ────────────────────────────────────────────────────────────

    /// <summary>
    /// Destroys existing piece bar cards and rebuilds one card per live player piece,
    /// showing its sprite and highlighting armor / upgrade state.
    /// </summary>
    private void PopulatePieceBar()
    {
        if (pieceBar == null || gameController == null) return;

        foreach (Transform child in pieceBar)
            Destroy(child.gameObject);
        pieceBarEntries.Clear();

        foreach (Piece piece in gameController.playerPieces)
        {
            if (piece == null) continue;

            // Pieces that are already upgraded have nothing to offer in the bar — skip them.
            if (piece.isUpgraded) continue;

            System.Type      type        = piece.GetType();
            UpgradeCardEntry card        = FindCard(type);
            string           displayName = card != null ? card.pieceName : type.Name;
            string           description = card != null ? card.description : "No description available.";
            Sprite           sprite      = GetSpriteFromPiece(piece);

            // ── Card root ──────────────────────────────────────────────────
            GameObject btnGO = new GameObject(type.Name, typeof(RectTransform));
            btnGO.transform.SetParent(pieceBar, worldPositionStays: false);

            Image cardBg = btnGO.AddComponent<Image>();
            Button btn   = btnGO.AddComponent<Button>();
            btn.targetGraphic = cardBg;

            LayoutElement le = btnGO.AddComponent<LayoutElement>();
            le.preferredWidth  = 90f;
            le.preferredHeight = 105f;

            VerticalLayoutGroup vlg = btnGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.LowerCenter;
            vlg.padding                = new RectOffset(4, 4, 4, 8);
            vlg.spacing                = 0f;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            // ── Piece sprite ──────────────────────────────────────────────
            GameObject spriteGO = new GameObject("Sprite", typeof(RectTransform));
            spriteGO.transform.SetParent(btnGO.transform, worldPositionStays: false);
            Image spriteImg = spriteGO.AddComponent<Image>();
            spriteImg.preserveAspect = true;
            spriteImg.raycastTarget  = false;
            if (sprite != null) spriteImg.sprite = sprite;
            LayoutElement spriteLE = spriteGO.AddComponent<LayoutElement>();
            spriteLE.preferredWidth  = 60f;
            spriteLE.preferredHeight = 60f;

            // ── Status label ──────────────────────────────────────────────
            TMP_Text statusLbl = CreateLabel(btnGO.transform, "", 11, FontStyles.Normal);

            // ── Wire upgrade click ────────────────────────────────────────
            Piece  capturedPiece = piece;
            string capturedName  = displayName;
            string capturedDesc  = description;
            btn.onClick.AddListener(() => OnPieceClicked(capturedPiece, capturedName, capturedDesc));

            var entry = new PieceBarEntry
            {
                PieceType  = type,
                Button     = btn,
                StatusLabel = statusLbl,
                CardBg     = cardBg,
                SpriteImg  = spriteImg,
                Piece      = piece
            };
            pieceBarEntries.Add(entry);
            RefreshEntry(entry);
        }
    }

    /// <summary>Updates a single card's colours, label and interactable state.</summary>
    private void RefreshEntry(PieceBarEntry entry)
    {
        bool upgraded  = entry.Piece != null && entry.Piece.isUpgraded;
        bool armored   = entry.Piece != null && entry.Piece.armorCount > 0;
        bool canAfford = gameController.Bits >= UpgradeCost;

        // Card background — transparent; state is communicated via sprite tint only
        if (entry.CardBg != null)
            entry.CardBg.color = Color.clear;

        // Sprite tint — cyan for normal player pieces, configured upgraded color for upgraded, icy blue for armored
        if (entry.SpriteImg != null)
        {
            if (upgraded && armored)
            {
                // Blend upgraded tint with armor hint
                Color upgCol = PlayerUpgradedColor;
                entry.SpriteImg.color = new Color(upgCol.r * 0.85f, upgCol.g * 0.85f, upgCol.b * 0.95f, 1f);
            }
            else if (upgraded)
                entry.SpriteImg.color = PlayerUpgradedColor;
            else if (armored)
                entry.SpriteImg.color = ArmoredSpriteColor;
            else
                entry.SpriteImg.color = PlayerNormalColor;
        }

        // Status label
        string armorStr   = armored  ? $"🛡 {entry.Piece.armorCount}" : "";
        string upgradeStr = upgraded ? "★" : "";
        string separator  = (armored && upgraded) ? " " : "";
        string badges     = $"{upgradeStr}{separator}{armorStr}".Trim();

        if (entry.StatusLabel != null)
            entry.StatusLabel.text = badges.Length > 0 ? badges
                                   : upgraded           ? "Upgraded"
                                   : $"{UpgradeCost} bits";

        // Interactable: can still upgrade if not yet upgraded and can afford
        entry.Button.interactable = !upgraded && canAfford;
    }

    private void RefreshAllEntries()
    {
        foreach (PieceBarEntry e in pieceBarEntries)
            RefreshEntry(e);
    }

    /// <summary>Returns the sprite from the given live piece's SpriteRenderer.</summary>
    private static Sprite GetSpriteFromPiece(Piece piece)
    {
        if (piece == null) return null;
        SpriteRenderer sr = piece.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        return sr != null ? sr.sprite : null;
    }

    /// <summary>Returns the sprite from any live player piece of the given type (fallback).</summary>
    private Sprite GetSpriteForType(System.Type type)
    {
        foreach (Piece p in gameController.playerPieces)
        {
            if (p == null || p.GetType() != type) continue;
            return GetSpriteFromPiece(p);
        }
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private TMP_Text CreateLabel(Transform parent, string text, float fontSize, FontStyles style)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);

        TextMeshProUGUI lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text          = text;
        lbl.fontSize      = fontSize;
        lbl.fontStyle     = style;
        lbl.alignment     = TextAlignmentOptions.Center;
        lbl.color         = Color.white;
        lbl.raycastTarget = false;
        ApplyPixelFont(lbl);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 6f;

        return lbl;
    }

    /// <summary>Applies the Electronic Highway Sign pixel font, matching the rest of the market UI.</summary>
    private static void ApplyPixelFont(TMP_Text tmp)
    {
#if UNITY_EDITOR
        TMP_FontAsset pixelFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset");
        if (pixelFont != null) tmp.font = pixelFont;
#else
        TMP_FontAsset pixelFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
        if (pixelFont != null) tmp.font = pixelFont;
#endif
    }

    private UpgradeCardEntry FindCard(System.Type type)
    {
        if (upgradeCards == null) return null;
        foreach (UpgradeCardEntry card in upgradeCards)
            if (card.PieceType == type) return card;
        return null;
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    private void OnBitsChanged(int bits)
    {
        RefreshBitsLabel(bits);
        RefreshAllEntries();
    }

    public void RefreshBitsLabel(int bits)
    {
        if (bitsLabel != null) bitsLabel.text = $"Bits: {bits}";
    }

    private void OnPieceClicked(Piece piece, string pieceName, string description)
    {
        pendingPiece = piece;

        if (confirmPieceLabel != null)
            confirmPieceLabel.text = $"Upgrade {pieceName}  —  {UpgradeCost} bits";
        if (confirmDescLabel != null)
            confirmDescLabel.text = description;
        if (confirmPanel != null)
            confirmPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (pendingPiece == null || gameController == null) return;
        gameController.UpgradePiece(pendingPiece, UpgradeCost);
        pendingPiece = null;
        if (confirmPanel != null) confirmPanel.SetActive(false);
        RefreshAllEntries();
    }

    private void OnCancel()
    {
        pendingPiece = null;
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    /// <summary>Hides the panel, advances the enemy tier, and starts the next fight.</summary>
    private void OnEnterFight()
    {
        if (_fightEntered) return;
        _fightEntered = true;

        gameObject.SetActive(false);
        gameController.AdvanceTier();
        gameController.StartNextFight();
    }
}
