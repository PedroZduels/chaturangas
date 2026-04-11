using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the three floppy-disc slots in the player HUD.
/// Handles selection tinting (only the clicked slot is tinted),
/// hover tooltip display, and floppy-mode highlight.
/// Tooltip uses the shared <see cref="MarketFloppyTooltip"/> (pixel-art, cyan + black).
/// Only the selected floppy slot icon is tilted; unselected slots are upright.
/// </summary>
public class FloppyHUD : MonoBehaviour
{
    [Header("References")]
    public GameController gameController;
    public FloppyInventory inventory;

    [Header("Floppy Slots (assign 3 root GameObjects in Inspector)")]
    public Button[]   slotButtons;    // length 3
    public Image[]    slotImages;     // length 3 — the floppy icon
    public TMP_Text[] slotLabels;     // length 3 — effect name

    [Header("Visual")]
    [Tooltip("Global floppy sprite (used when a FloppyDefinition has no override icon).")]
    public Sprite floppySprite;

    [Tooltip("Z rotation applied to the selected floppy icon only.")]
    [Range(-45f, 45f)]
    public float iconTiltDeg = -15f;

    [Header("State colors")]
    public Color activeSlotColor     = Color.white;
    public Color emptySlotColor      = new Color(1f, 1f, 1f, 0.2f);
    public Color selectedSlotColor   = new Color(0f, 1f, 0.95f, 1f);   // cyan to match tooltip
    public Color floppyModeHighlight = new Color(0f, 1f, 0.95f, 1f);

    private bool _initialized;
    private int  _selectedSlotIndex = -1;
    private MarketFloppyTooltip _tooltip;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (inventory == null)
            inventory = Object.FindAnyObjectByType<FloppyInventory>();

        if (!_initialized)
        {
            _initialized = true;

            for (int i = 0; i < FloppyInventory.SlotCount; i++)
            {
                int captured = i;

                if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                {
                    slotButtons[i].onClick.AddListener(() => OnSlotClicked(captured));
                    AddHoverTriggers(slotButtons[i].gameObject, captured);
                }
            }

            if (inventory      != null) inventory     .OnChanged           += RefreshSlots;
            if (gameController != null) gameController.OnFloppyModeChanged += OnFloppyModeChanged;

            Canvas canvas = GetComponentInParent<Canvas>();
            _tooltip = MarketFloppyTooltip.GetOrCreate(canvas);
        }

        RefreshSlots();
    }

    void OnDisable()
    {
        if (inventory      != null) inventory     .OnChanged           -= RefreshSlots;
        if (gameController != null) gameController.OnFloppyModeChanged -= OnFloppyModeChanged;
        _tooltip?.Hide();
    }

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>Re-reads the inventory and refreshes all slot visuals.</summary>
    public void RefreshSlots()
    {
        for (int i = 0; i < FloppyInventory.SlotCount; i++)
        {
            FloppyDefinition floppy = inventory != null ? inventory.PeekAt(i) : null;
            bool filled    = floppy != null;
            bool selected  = i == _selectedSlotIndex;

            if (slotImages != null && i < slotImages.Length && slotImages[i] != null)
            {
                if (filled)
                {
                    slotImages[i].sprite  = floppy.icon != null ? floppy.icon : floppySprite;
                    slotImages[i].color   = selected ? selectedSlotColor : activeSlotColor;
                    slotImages[i].enabled = true;
                }
                else
                {
                    slotImages[i].enabled = false;
                }

                // Tilt only the selected slot; unselected slots are upright.
                slotImages[i].transform.localRotation =
                    Quaternion.Euler(0f, 0f, selected ? iconTiltDeg : 0f);
            }

            if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
                slotLabels[i].text = filled ? floppy.displayName : "";

            if (slotButtons != null && i < slotButtons.Length && slotButtons[i] != null)
                slotButtons[i].interactable = filled;
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnSlotClicked(int slotIndex)
    {
        AudioManager.PlayUIClick();
        _selectedSlotIndex = (_selectedSlotIndex == slotIndex) ? -1 : slotIndex;
        RefreshSlots();
        gameController?.ActivateFloppy(slotIndex);
    }

    private void OnSlotHoverEnter(int slotIndex)
    {
        if (_tooltip == null) return;
        FloppyDefinition floppy = inventory != null ? inventory.PeekAt(slotIndex) : null;
        if (floppy == null) return;

        RectTransform slotRect = slotButtons[slotIndex].GetComponent<RectTransform>();
        _tooltip.Show(slotRect, floppy.description);
    }

    private void OnSlotHoverExit(int slotIndex) => _tooltip?.Hide();

    private void OnFloppyModeChanged(bool active)
    {
        if (!active)
        {
            _selectedSlotIndex = -1;
            RefreshSlots();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddHoverTriggers(GameObject go, int slotIndex)
    {
        FloppySlotHover hover = go.GetComponent<FloppySlotHover>();
        if (hover == null)
            hover = go.AddComponent<FloppySlotHover>();

        hover.Initialize(slotIndex, OnSlotHoverEnter, OnSlotHoverExit);
    }
}
