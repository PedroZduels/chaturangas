using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Promotion picker shown when a player pawn reaches the last rank.
/// Displays 5 buttons — Queen, Rook, Bishop, Knight, King.
///
/// Usage:
///   1. Call SetPrefabs() to supply piece prefabs (safe while panel is inactive).
///   2. yield return StartCoroutine(ChoosePromotion()) — shows the panel and waits.
///   3. Read ChosenPrefab after the coroutine returns.
///
/// The component lives ON the root PromotionPanel GameObject (this == panel).
/// ChoosePromotion() uses gameObject.SetActive() directly — a separate "panel"
/// field was removed because it caused a re-entrant SetActive(false) freeze.
/// </summary>
public class PromotionUI : MonoBehaviour
{
    [Header("Piece buttons")]
    public Button queenButton;
    public Button rookButton;
    public Button bishopButton;
    public Button knightButton;
    public Button kingButton;

    /// <summary>The prefab chosen by the player — valid after ChoosePromotion completes.</summary>
    public GameObject ChosenPrefab { get; private set; }

    private bool       chosen;
    private GameObject queenPrefab;
    private GameObject rookPrefab;
    private GameObject bishopPrefab;
    private GameObject knightPrefab;
    private GameObject kingPrefab;

    // Awake runs the FIRST time this GameObject becomes active.
    // Never call gameObject.SetActive() here — doing so is what caused the freeze.
    void Awake()
    {
        if (queenButton  != null) queenButton .onClick.AddListener(() => Pick(queenPrefab));
        if (rookButton   != null) rookButton  .onClick.AddListener(() => Pick(rookPrefab));
        if (bishopButton != null) bishopButton.onClick.AddListener(() => Pick(bishopPrefab));
        if (knightButton != null) knightButton.onClick.AddListener(() => Pick(knightPrefab));
        if (kingButton   != null) kingButton  .onClick.AddListener(() => Pick(kingPrefab));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Supply prefabs and enable/disable buttons accordingly.
    /// Safe to call while this panel is inactive — serialized refs are always valid.
    /// </summary>
    public void SetPrefabs(GameObject queen, GameObject rook,
                           GameObject bishop, GameObject knight,
                           GameObject king)
    {
        queenPrefab  = queen;
        rookPrefab   = rook;
        bishopPrefab = bishop;
        knightPrefab = knight;
        kingPrefab   = king;

        SetInteractable(queenButton,  queen  != null);
        SetInteractable(rookButton,   rook   != null);
        SetInteractable(bishopButton, bishop != null);
        SetInteractable(knightButton, knight != null);
        SetInteractable(kingButton,   king   != null);
    }

    /// <summary>
    /// Activates this panel, suspends the caller until a piece is chosen, then hides.
    /// Must be yielded from a coroutine on another MonoBehaviour (GameController).
    /// </summary>
    public IEnumerator ChoosePromotion()
    {
        ChosenPrefab = null;
        chosen       = false;
        gameObject.SetActive(true);          // Awake() fires here on the first call
        yield return new WaitUntil(() => chosen);
        gameObject.SetActive(false);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void Pick(GameObject prefab)
    {
        if (prefab == null) return;
        ChosenPrefab = prefab;
        chosen       = true;
    }

    private static void SetInteractable(Button btn, bool state)
    {
        if (btn != null) btn.interactable = state;
    }
}
