using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Floppy-disc section of the Mainframe market.
/// Always shows 3 random floppies from the catalogue, restocked on every market visit.
/// Buying a floppy places it in the player's FloppyInventory (if space is available).
///
/// Cards are compact (icon only + name). Cost and description appear in a pixel-art
/// tooltip on mouse-over.
/// </summary>
public class MarketFloppyShop : MonoBehaviour
{
    [Header("References")]
    public GameController  gameController;
    public FloppyInventory floppyInventory;

    [Header("Slot root (HorizontalLayoutGroup parent)")]
    public Transform slotRoot;

    [Header("Confirm panel")]
    public GameObject confirmPanel;
    public TMP_Text   confirmLabel;
    public TMP_Text   notEnoughLabel;
    public TMP_Text   inventoryFullLabel;
    public Button     confirmButton;
    public Button     cancelButton;

    [Header("Floppy Catalogue")]
    [Tooltip("Leave empty — floppies are loaded automatically from Resources/Floppies/.")]
    public FloppyDefinition[] catalogue;

    [Header("Visual")]
    [Tooltip("Global floppy sprite used when a FloppyDefinition has no override icon.")]
    public Sprite floppySprite;

    [Tooltip("Z rotation applied to the floppy icon on market cards.")]
    [Range(-45f, 45f)]
    public float iconTiltDeg = -15f;

    // ── Card visual constants ─────────────────────────────────────────────────
    private static readonly Color CardColor   = new Color(0f,    0f,    0f,    1f);   // pure black
    private static readonly Color BorderColor = new Color(0f,    1f,    0.95f, 1f);   // solid cyan
    private static readonly Color NameColor   = new Color(0f,    1f,    0.95f, 1f);   // cyan
    private const float CardWidth  = 72f;
    private const float CardHeight = 90f;
    private const float IconSize   = 52f;

    private const int PoolSize = 3;

    // ── Runtime pool ─────────────────────────────────────────────────────────

    private class MarketSlot
    {
        public FloppyDefinition floppy;
        public bool             sold;
        public Button           button;
        public Image            icon;
        public TMP_Text         nameLabel;
        public TMP_Text         costLabel;  // unused now (tooltip only), kept for MarkSold
    }

    private readonly List<MarketSlot> _pool = new List<MarketSlot>();
    private MarketSlot          _pendingSlot;
    private bool                _initialized;
    private MarketFloppyTooltip _tooltip;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (floppyInventory == null)
            floppyInventory = Object.FindAnyObjectByType<FloppyInventory>();

        if (!_initialized)
        {
            _initialized = true;
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton  != null) cancelButton .onClick.AddListener(OnCancel);

            EnsureTooltip();
        }

        HideConfirm();

        // Always restock on every market visit.
        GeneratePool();
    }

    void OnDisable()
    {
        _tooltip?.Hide();
    }

    // ── Tooltip ───────────────────────────────────────────────────────────────

    private void EnsureTooltip()
    {
        if (_tooltip != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        _tooltip = MarketFloppyTooltip.GetOrCreate(canvas);
    }

    // ── Pool ─────────────────────────────────────────────────────────────────

    private void GeneratePool()
    {
        if (slotRoot != null)
            for (int i = slotRoot.childCount - 1; i >= 0; i--)
                Destroy(slotRoot.GetChild(i).gameObject);

        _pool.Clear();

        FloppyDefinition[] pool = Resources.LoadAll<FloppyDefinition>("Floppies");
        if (pool == null || pool.Length == 0 || slotRoot == null) return;

        List<FloppyDefinition> available = new List<FloppyDefinition>(pool);

        for (int i = 0; i < PoolSize && available.Count > 0; i++)
        {
            int idx = Random.Range(0, available.Count);
            FloppyDefinition def = available[idx];
            available.RemoveAt(idx);

            MarketSlot slot = new MarketSlot { floppy = def };
            BuildSlotUI(slot);
            _pool.Add(slot);
        }
    }

    private void BuildSlotUI(MarketSlot slot)
    {
        // ── Card root ─────────────────────────────────────────────────────────
        GameObject card = new GameObject("FloppySlot", typeof(RectTransform));
        card.transform.SetParent(slotRoot, worldPositionStays: false);

        LayoutElement le = card.AddComponent<LayoutElement>();
        le.preferredWidth  = CardWidth;
        le.preferredHeight = CardHeight;

        // Cyan border via outline image
        Image border = card.AddComponent<Image>();
        border.color = BorderColor;

        // Inner black background as a child panel
        GameObject innerGO = new GameObject("Bg", typeof(RectTransform));
        innerGO.transform.SetParent(card.transform, worldPositionStays: false);
        RectTransform innerRt = innerGO.GetComponent<RectTransform>();
        innerRt.anchorMin       = Vector2.zero;
        innerRt.anchorMax       = Vector2.one;
        innerRt.offsetMin       = new Vector2(2f, 2f);
        innerRt.offsetMax       = new Vector2(-2f, -2f);
        Image innerBg = innerGO.AddComponent<Image>();
        innerBg.color         = CardColor;
        innerBg.raycastTarget = false;

        Button btn = card.AddComponent<Button>();
        btn.targetGraphic = border;

        VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.LowerCenter;
        vlg.padding                = new RectOffset(4, 4, 4, 6);
        vlg.spacing                = 0f;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // ── Icon ──────────────────────────────────────────────────────────────
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(card.transform, worldPositionStays: false);
        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget  = false;
        iconImg.sprite = (slot.floppy.icon != null) ? slot.floppy.icon : floppySprite;
        // No tilt on market cards — always upright.
        iconImg.transform.localRotation = Quaternion.identity;
        LayoutElement iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredWidth  = IconSize;
        iconLE.preferredHeight = IconSize;

        // ── Name label (white, larger) ───────────────────────────────────────
        GameObject nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(card.transform, worldPositionStays: false);
        TextMeshProUGUI nameLbl = nameGO.AddComponent<TextMeshProUGUI>();
        nameLbl.text          = slot.floppy.displayName;
        nameLbl.fontSize      = 13f;
        nameLbl.fontStyle     = FontStyles.Bold;
        nameLbl.alignment     = TextAlignmentOptions.Center;
        nameLbl.color         = Color.white;
        nameLbl.raycastTarget = false;
        ApplyPixelFont(nameLbl);
        LayoutElement nameLE = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredHeight = 18f;

        slot.button    = btn;
        slot.icon      = iconImg;
        slot.nameLabel = nameLbl;

        // ── Hover tooltip ─────────────────────────────────────────────────────
        MarketSlot cap = slot;
        AddHoverHandlers(card, cap);

        btn.onClick.AddListener(() => OnSlotClicked(cap));
    }

    private void AddHoverHandlers(GameObject card, MarketSlot slot)
    {
        MarketFloppyHoverHandler hover = card.AddComponent<MarketFloppyHoverHandler>();
        hover.Initialize(
            onEnter: () =>
            {
                if (slot.sold || _tooltip == null) return;
                _tooltip.Show(
                    card.GetComponent<RectTransform>(),
                    slot.floppy.description ?? "");
            },
            onExit: () => _tooltip?.Hide()
        );
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnSlotClicked(MarketSlot slot)
    {
        if (slot.sold) return;
        _pendingSlot = slot;

        bool canAfford = gameController != null && gameController.Bits >= slot.floppy.marketCost;
        bool hasRoom   = floppyInventory != null && floppyInventory.HasRoom;

        if (confirmLabel      != null) confirmLabel     .text = $"Buy {slot.floppy.displayName}  —  {slot.floppy.marketCost} bits";
        if (notEnoughLabel    != null) notEnoughLabel   .gameObject.SetActive(!canAfford);
        if (inventoryFullLabel != null) inventoryFullLabel.gameObject.SetActive(canAfford && !hasRoom);
        if (confirmButton     != null) confirmButton    .interactable = canAfford && hasRoom;

        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    private void OnConfirm()
    {
        if (_pendingSlot == null || gameController == null || floppyInventory == null) return;

        gameController.AddBits(-_pendingSlot.floppy.marketCost);
        floppyInventory.TryAdd(_pendingSlot.floppy);
        MarkSold(_pendingSlot);
        _pendingSlot = null;
        HideConfirm();
    }

    private void OnCancel()
    {
        _pendingSlot = null;
        HideConfirm();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MarkSold(MarketSlot slot)
    {
        slot.sold = true;
        if (slot.button != null) slot.button.interactable = false;
        if (slot.icon   != null) slot.icon.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        if (slot.nameLabel != null) slot.nameLabel.text = "Sold";
    }

    private void HideConfirm()
    {
        if (confirmPanel      != null) confirmPanel     .SetActive(false);
        if (notEnoughLabel    != null) notEnoughLabel   .gameObject.SetActive(false);
        if (inventoryFullLabel != null) inventoryFullLabel.gameObject.SetActive(false);
    }

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
}

/// <summary>
/// Lightweight pointer-enter/exit handler for floppy market card hover tooltips.
/// Attached programmatically by <see cref="MarketFloppyShop.AddHoverHandlers"/>.
/// </summary>
public class MarketFloppyHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private System.Action _onEnter;
    private System.Action _onExit;

    /// <summary>Wire hover callbacks immediately after AddComponent.</summary>
    public void Initialize(System.Action onEnter, System.Action onExit)
    {
        _onEnter = onEnter;
        _onExit  = onExit;
    }

    public void OnPointerEnter(PointerEventData eventData) => _onEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData)  => _onExit?.Invoke();
}

