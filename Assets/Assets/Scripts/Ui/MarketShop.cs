using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Buy-a-piece section of the Mainframe market.
///
/// Flow:
///   1. Panel opens → GeneratePool() picks 2 random piece types.
///      Each has a 10% chance to be pre-upgraded (costs +5 bits).
///   2. Player clicks a slot → ConfirmBuyPanel opens with price + not-enough-bits feedback.
///   3. Confirm → bits deducted, GameController enters placement mode.
///   4. Player clicks a valid tile on the player's half → piece spawns there.
///   5. Panel refreshes (bought slot grays out).
///   6. Reroll button costs RerollCost bits and regenerates the entire pool.
/// </summary>
public class MarketShop : MonoBehaviour
{
    [Header("References")]
    public GameController  gameController;
    public MainframeMenu   mainframeMenu;   // kept in sync so bits label refreshes

    [Header("Shop Slot Roots — 3 slots, assigned in Inspector")]
    public Transform slotRoot;             // parent with HorizontalLayoutGroup

    [Header("Reroll")]
    [Tooltip("Button that rerolls the shop pool. Wire from Inspector.")]
    public Button   rerollButton;
    [Tooltip("Label on the reroll button — updated at runtime to show cost.")]
    public TMP_Text rerollCostLabel;
    public int      rerollCost = 5;

    [Header("Slot Style — match disc row")]
    [Tooltip("Assign the blacktile.png sprite used by disc/floppy slots.")]
    public Sprite slotBackground;

    [Header("Confirm Panel")]
    public GameObject confirmBuyPanel;
    public TMP_Text   confirmTitleLabel;   // "Buy [PieceName]  —  X bits"
    public TMP_Text   notEnoughLabel;      // "Not enough bits" — hidden normally
    public Button     confirmBuyButton;
    public Button     cancelBuyButton;

    [Header("Placement")]
    [Tooltip("Root panel to hide while the player is placing a purchased piece (e.g. MainframePanel).")]
    public GameObject marketPanelRoot;
    public GameObject placementHintPanel;  // "Click your half of the board to place"

    // ── Piece catalogue ───────────────────────────────────────────────────────

    [System.Serializable]
    public class ShopEntry
    {
        [Tooltip("C# type name e.g. 'Pawn'")]
        public string  typeName;
        public int     baseCost;
        public GameObject prefab;
        /// <summary>Resolved at runtime.</summary>
        public System.Type PieceType =>
            System.Type.GetType(typeName)
            ?? System.Type.GetType($"{typeName}, Assembly-CSharp");
    }

    [Header("Catalogue — one entry per buyable piece type")]
    public ShopEntry[] catalogue;

    private const float UpgradeChance    = 0.10f;
    private const int   UpgradeExtraCost = 5;
    private const int   PoolSize         = 3;

    [Header("Slot Visual — tweak here, preview updates live")]
    public float slotWidth     = 140f;
    public float slotHeight    = 190f;
    public float spriteSize    = 105f;
    public float slotSpacing   = 8f;
    public float labelFontSize = 13f;
    [Tooltip("Slot card padding: left, right, top, bottom")]
    public int slotPadLeft   = 8;
    public int slotPadRight  = 8;
    public int slotPadTop    = 16;
    public int slotPadBottom = 12;
    public Color slotColor          = new Color(0.08f, 0.18f, 0.22f, 1f);
    public Color slotPurchasedColor = new Color(0.4f,  0.4f,  0.4f,  0.5f);

    // Piece sprite tint — resolved live from PieceTintConfig
    private Color PlayerNormalTint   => GameController.TintConfig != null ? GameController.TintConfig.playerNormal   : new Color(0f, 1f, 0.95f, 1f);
    private Color PlayerUpgradedTint => GameController.TintConfig != null ? GameController.TintConfig.playerUpgraded : new Color(1f, 0.84f, 0.10f, 1f);

    /// <summary>Applies the four serialized pad fields onto an existing RectOffset in-place.</summary>
    public void ApplySlotPadding(RectOffset target)
    {
        target.left   = slotPadLeft;
        target.right  = slotPadRight;
        target.top    = slotPadTop;
        target.bottom = slotPadBottom;
    }
    // ── Runtime pool ─────────────────────────────────────────────────────────

    private class PoolSlot
    {
        public ShopEntry entry;
        public bool      isUpgraded;
        public int       finalCost;
        public bool      purchased;
        public int       siblingIndex; // position in slotRoot — preserved across rerolls
        public Button    button;
        public Image     spriteImage;
        public TMP_Text  costLabel;
    }

    private readonly List<PoolSlot> pool = new List<PoolSlot>();
    private PoolSlot pendingSlot;

    private bool initialized;

    /// <summary>
    /// When true the pool was consumed in a previous market visit and must be
    /// regenerated the next time the shop opens. Set by InvalidateShopPool().
    /// </summary>
    private bool _poolDirty = true;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();

        if (!initialized)
        {
            initialized = true;
            if (confirmBuyButton != null) confirmBuyButton.onClick.AddListener(OnConfirmBuy);
            if (cancelBuyButton  != null) cancelBuyButton .onClick.AddListener(OnCancelBuy);
            if (rerollButton     != null) rerollButton    .onClick.AddListener(OnReroll);

            // Invalidate the pool whenever the player wins a fight so the next
            // market visit gets a fresh selection. OnPlayerWin fires in LoadNextFight.
            if (gameController != null)
                gameController.OnPlayerWin += InvalidateShopPool;
        }

        if (rerollCostLabel != null)
            rerollCostLabel.text = $"Reroll  {rerollCost} bits";

        HideConfirmPanel();
        if (placementHintPanel != null) placementHintPanel.SetActive(false);

        // Only regenerate the pool when entering a fresh market session.
        // Re-opening sub-menus within the same visit preserves existing slots.
        if (_poolDirty)
        {
            GeneratePool();
            _poolDirty = false;
        }
        else
        {
            RefreshSlotStates();
        }

        RefreshRerollButton();
    }

    void OnDestroy()
    {
        if (gameController != null)
            gameController.OnPlayerWin -= InvalidateShopPool;
    }

    /// <summary>
    /// Marks the current pool as stale so it is regenerated on the next market visit.
    /// Call this from GameController when a new fight begins (i.e. when the player leaves
    /// the market).
    /// </summary>
    public void InvalidateShopPool()
    {
        _poolDirty = true;
    }

    void OnDisable()
    {
        // Safety: cancel placement if the panel is closed mid-flow.
        gameController?.ExitPlacementMode();
    }

    // ── Pool generation ───────────────────────────────────────────────────────

    /// <summary>Picks PoolSize random catalogue entries (no duplicates) and builds UI slots.</summary>
    private void GeneratePool()
    {
        // Destroy all existing slot cards directly from the hierarchy so layout updates
        // immediately — Destroy() is deferred and would leave stale children visible.
        if (slotRoot != null)
        {
            for (int i = slotRoot.childCount - 1; i >= 0; i--)
                Destroy(slotRoot.GetChild(i).gameObject);
        }
        pool.Clear();

        if (catalogue == null || catalogue.Length == 0 || slotRoot == null) return;

        List<ShopEntry> available = new List<ShopEntry>(catalogue);

        for (int i = 0; i < PoolSize && available.Count > 0; i++)
        {
            int        idx   = Random.Range(0, available.Count);
            ShopEntry  entry = available[idx];
            available.RemoveAt(idx);

            bool upgraded  = Random.value < UpgradeChance;
            int  cost      = entry.baseCost + (upgraded ? UpgradeExtraCost : 0);

            PoolSlot slot = new PoolSlot
            {
                entry        = entry,
                isUpgraded   = upgraded,
                finalCost    = cost,
                purchased    = false,
                siblingIndex = i
            };

            BuildSlotUI(slot);
            pool.Add(slot);
        }
    }

    /// <summary>
    /// Rerolls only unpurchased slots. Purchased slots keep their "Sold" state and card.
    /// Excludes catalogue entries already shown in unpurchased slots to avoid immediate duplicates.
    /// </summary>
    private void RerollUnpurchasedSlots()
    {
        if (catalogue == null || catalogue.Length == 0) return;

        // Build exclusion list: types already in purchased slots (keep variety among new ones).
        HashSet<string> exclude = new HashSet<string>();
        foreach (PoolSlot slot in pool)
        {
            if (slot.purchased)
                exclude.Add(slot.entry.typeName);
        }

        List<ShopEntry> available = new List<ShopEntry>();
        foreach (ShopEntry e in catalogue)
        {
            if (!exclude.Contains(e.typeName))
                available.Add(e);
        }

        // Fallback: if exclusions wiped the whole catalogue, allow everything.
        if (available.Count == 0)
            available = new List<ShopEntry>(catalogue);

        foreach (PoolSlot slot in pool)
        {
            if (slot.purchased) continue;
            if (available.Count == 0) break;

            // Destroy the old card.
            if (slot.button != null)
                Destroy(slot.button.gameObject);

            // Pick a new entry.
            int       idx   = Random.Range(0, available.Count);
            ShopEntry entry = available[idx];
            available.RemoveAt(idx);

            bool upgraded = Random.value < UpgradeChance;
            int  cost     = entry.baseCost + (upgraded ? UpgradeExtraCost : 0);

            slot.entry       = entry;
            slot.isUpgraded  = upgraded;
            slot.finalCost   = cost;
            slot.purchased   = false;
            slot.button      = null;
            slot.spriteImage = null;
            slot.costLabel   = null;

            BuildSlotUI(slot, slot.siblingIndex);
        }
    }

    /// <summary>
    /// Re-applies purchased / interactable state to existing slot cards without
    /// rebuilding the pool. Called when the panel is reopened mid-market-session.
    /// </summary>
    private void RefreshSlotStates()
    {
        foreach (PoolSlot slot in pool)
        {
            if (slot.purchased) MarkSlotPurchased(slot);
        }
    }

    private void BuildSlotUI(PoolSlot slot, int insertAtSibling = -1)
    {
        // Card root — styled like DiscRow slots (blacktile.png background, cyan tint)
        GameObject card = new GameObject("ShopSlot", typeof(RectTransform));
        card.transform.SetParent(slotRoot, worldPositionStays: false);

        // Insert at the correct sibling position when rerolling so order is preserved.
        if (insertAtSibling >= 0)
            card.transform.SetSiblingIndex(insertAtSibling);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.preferredWidth  = slotWidth;
        le.preferredHeight = slotHeight;

        Image cardBg = card.AddComponent<Image>();
        if (slotBackground != null) cardBg.sprite = slotBackground;
        cardBg.color = slotColor;
        cardBg.type  = Image.Type.Simple;
        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = cardBg;

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.MiddleCenter;
        ApplySlotPadding(vlg.padding);
        vlg.spacing                = slotSpacing;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Piece sprite — larger to make better use of the taller slot
        GameObject imgGO = new GameObject("Sprite", typeof(RectTransform));
        imgGO.transform.SetParent(card.transform, worldPositionStays: false);
        Image spriteImg = imgGO.AddComponent<Image>();
        spriteImg.preserveAspect = true;
        spriteImg.raycastTarget  = false;
        LayoutElement imgLE = imgGO.AddComponent<LayoutElement>();
        imgLE.preferredWidth  = spriteSize;
        imgLE.preferredHeight = spriteSize;

        // Resolve sprite from prefab
        Sprite pieceSprite = GetSpriteFromPrefab(slot.entry.prefab);
        if (pieceSprite != null)
            spriteImg.sprite = pieceSprite;

        // Tint: cyan for normal player pieces, gold for upgraded
        spriteImg.color = slot.isUpgraded ? PlayerUpgradedTint : PlayerNormalTint;

        // Cost label
        GameObject costGO = new GameObject("CostLabel", typeof(RectTransform));
        costGO.transform.SetParent(card.transform, worldPositionStays: false);
        TextMeshProUGUI costLbl = costGO.AddComponent<TextMeshProUGUI>();
        costLbl.text          = $"{slot.finalCost} bits";
        costLbl.fontSize      = labelFontSize;
        costLbl.fontStyle     = FontStyles.Bold;
        costLbl.alignment     = TextAlignmentOptions.Center;
        costLbl.color         = Color.white;
        costLbl.raycastTarget = false;
        LayoutElement costLE = costGO.AddComponent<LayoutElement>();
        costLE.preferredHeight = 20f;

        slot.button      = btn;
        slot.spriteImage = spriteImg;
        slot.costLabel   = costLbl;

        PoolSlot capturedSlot = slot;
        btn.onClick.AddListener(() => OnSlotClicked(capturedSlot));
    }

    // ── Slot interaction ──────────────────────────────────────────────────────

    private void OnSlotClicked(PoolSlot slot)
    {
        if (slot.purchased) return;
        pendingSlot = slot;

        string upgLabel = slot.isUpgraded ? " [Upgraded]" : "";
        if (confirmTitleLabel != null)
            confirmTitleLabel.text = $"Buy {slot.entry.typeName}{upgLabel}  —  {slot.finalCost} bits";

        bool canAfford = gameController != null && gameController.Bits >= slot.finalCost;
        if (notEnoughLabel  != null) notEnoughLabel .gameObject.SetActive(!canAfford);
        if (confirmBuyButton != null) confirmBuyButton.interactable = canAfford;

        if (confirmBuyPanel != null) confirmBuyPanel.SetActive(true);
    }

    private void OnConfirmBuy()
    {
        if (pendingSlot == null || gameController == null) return;

        gameController.AddBits(-pendingSlot.finalCost);
        MarkSlotPurchased(pendingSlot);
        mainframeMenu?.RefreshBitsLabel(gameController.Bits);
        RefreshRerollButton();

        bool upgraded     = pendingSlot.isUpgraded;
        GameObject prefab = pendingSlot.entry.prefab;
        pendingSlot = null;

        HideConfirmPanel();

        // Hide the entire market panel so only the board is visible during placement.
        if (marketPanelRoot != null) marketPanelRoot.SetActive(false);

        // Show the hint overlay (must be on Canvas level, not a child of marketPanelRoot).
        if (placementHintPanel != null) placementHintPanel.SetActive(true);

        // Enter placement mode — GameController will handle tile clicks.
        gameController.EnterPlacementMode(prefab, upgraded, OnPiecePlaced);
    }

    private void OnCancelBuy()
    {
        pendingSlot = null;
        HideConfirmPanel();
    }

    /// <summary>Deducts the reroll cost and regenerates only unpurchased shop slots.</summary>
    private void OnReroll()
    {
        if (gameController == null || gameController.Bits < rerollCost) return;

        gameController.AddBits(-rerollCost);
        mainframeMenu?.RefreshBitsLabel(gameController.Bits);

        RerollUnpurchasedSlots();
        RefreshRerollButton();

        Debug.Log($"[Market] Unpurchased slots rerolled for {rerollCost} bits. Remaining: {gameController.Bits}");
    }

    /// <summary>Disables the reroll button when the player can't afford it.</summary>
    private void RefreshRerollButton()
    {
        if (rerollButton == null) return;
        rerollButton.interactable = gameController != null && gameController.Bits >= rerollCost;
    }

    /// <summary>Called by GameController when the player successfully places the piece.</summary>
    private void OnPiecePlaced()
    {
        if (placementHintPanel != null) placementHintPanel.SetActive(false);

        // Restore the market panel now that placement is done.
        if (marketPanelRoot != null) marketPanelRoot.SetActive(true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MarkSlotPurchased(PoolSlot slot)
    {
        slot.purchased = true;
        if (slot.button      != null) slot.button.interactable = false;
        if (slot.spriteImage != null) slot.spriteImage.color   = slotPurchasedColor;
        if (slot.costLabel   != null) slot.costLabel.text      = "Sold";
    }

    private void HideConfirmPanel()
    {
        if (confirmBuyPanel != null) confirmBuyPanel.SetActive(false);
        if (notEnoughLabel  != null) notEnoughLabel .gameObject.SetActive(false);
    }

    /// <summary>Extracts the sprite from the first SpriteRenderer found in the prefab.</summary>
    private static Sprite GetSpriteFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        return sr != null ? sr.sprite : null;
    }
}
