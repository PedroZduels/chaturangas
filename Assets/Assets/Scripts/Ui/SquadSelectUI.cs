using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Dynamically spawns one FloppyCard per squad in the pool and distributes
/// them evenly across the bottom of the screen. Hides all cards after selection.
/// </summary>
public class SquadSelectUI : MonoBehaviour
{
    [Header("Dependencies")]
    public GameController gameController;
    public BoardManager   board;

    [Header("Squad Pool")]
    [Tooltip("Add any number of SquadDefinition assets here.")]
    public List<SquadDefinition> squads = new List<SquadDefinition>();

    [Header("Floppy Card Prefab")]
    [Tooltip("Assign Assets/Prefabs/FloppyCard.prefab here.")]
    public GameObject floppyCardPrefab;

    [Header("Layout")]
    [Tooltip("Parent RectTransform that cards are spawned into. Assign /Canvas.")]
    public RectTransform cardContainer;

    [Tooltip("Horizontal spacing between card centre-points in pixels.")]
    public float cardSpacing = 400f;

    // ── Runtime state ────────────────────────────────────────────────────────

    private readonly List<SquadButtonHover> _hovers = new List<SquadButtonHover>();
    private readonly List<GameObject>       _cards  = new List<GameObject>();

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (board == null)
            board = Object.FindAnyObjectByType<BoardManager>();
    }

    void Start()
    {
        SpawnCards();
    }

    // ── Private ──────────────────────────────────────────────────────────────

    /// <summary>Instantiates one FloppyCard per squad and spaces them evenly.</summary>
    private void SpawnCards()
    {
        if (floppyCardPrefab == null)
        {
            Debug.LogError("[SquadSelectUI] floppyCardPrefab is not assigned.", this);
            return;
        }

        int count = squads.Count;
        if (count == 0) return;

        // Total width occupied by all cards, centred on the container.
        float totalWidth = cardSpacing * (count - 1);
        float startX     = -totalWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            SquadDefinition squad = squads[i];
            if (squad == null) continue;

            GameObject card = Instantiate(floppyCardPrefab, cardContainer);
            _cards.Add(card);

            // Position
            RectTransform rt = card.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos = rt.anchoredPosition;
                pos.x = startX + cardSpacing * i;
                rt.anchoredPosition = pos;
            }

            // Populate labels
            TMP_Text nameText = card.transform.Find("TextName")?.GetComponent<TMP_Text>();
            TMP_Text descText = card.transform.Find("TextDesc")?.GetComponent<TMP_Text>();

            if (nameText != null)
            {
                nameText.text          = squad.squadName;
                nameText.raycastTarget = false;
            }
            if (descText != null)
            {
                descText.text          = string.Join("\n", squad.pieces.Select(p => p.piecePrefab.name));
                descText.raycastTarget = false;
            }

            // Wire button
            Button button = card.GetComponent<Button>();
            if (button != null)
            {
                SquadDefinition captured = squad;
                button.onClick.AddListener(() => OnSquadSelected(captured));
            }

            // Hover behaviour
            SquadButtonHover hover = card.AddComponent<SquadButtonHover>();
            hover.Initialize(board, squad);
            _hovers.Add(hover);
        }
    }

    /// <summary>Called when any floppy card is clicked.</summary>
    private void OnSquadSelected(SquadDefinition selected)
    {
        AudioManager.PlayUIClick();
        // Show the chosen card as selected, clear all others.
        int selectedIndex = squads.IndexOf(selected);
        for (int i = 0; i < _hovers.Count; i++)
        {
            if (i == selectedIndex)
                _hovers[i]?.ShowSelected();
            else
                _hovers[i]?.ClearHighlight();
        }

        // Clear remaining ghosts before hiding cards so Lower() runs while they are still active.
        foreach (SquadButtonHover hover in _hovers)
            hover?.ClearHighlight();

        // Hide all cards after clearing.
        foreach (GameObject card in _cards)
            card.SetActive(false);

        // Kick off the game.
        gameController.InitializePlayerSquad(selected);
    }
}
