using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Attached to each floppy card button at runtime by SquadSelectUI.
/// On hover  : raises the floppy and spawns board preview pieces.
/// On exit   : lowers the floppy and despawns preview (unless clicked).
/// On select : raises and locks until game starts.
/// </summary>
public class SquadButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private GameController        gameController;
    private SquadDefinition       squad;
    private readonly List<Piece>  previewPieces = new List<Piece>();
    private bool                  _selected     = false;
    private FloppyRaiseAnimation  _raiseAnim;

    // ── Initialisation ───────────────────────────────────────────────────────

    /// <summary>Called once by SquadSelectUI before any hover events.</summary>
    public void Initialize(BoardManager boardManager, SquadDefinition squadDefinition)
    {
        squad          = squadDefinition;
        gameController = Object.FindAnyObjectByType<GameController>();
        _raiseAnim     = GetComponent<FloppyRaiseAnimation>();
    }

    // ── Pointer events ───────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        _raiseAnim?.Raise();
        ShowPreview();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_selected)
        {
            _raiseAnim?.Lower();
            ClearHighlight();
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Locks the floppy raised so pointer-exit does not lower it.
    /// Called by SquadSelectUI when the player clicks this card.
    /// </summary>
    public void ShowSelected()
    {
        _selected = true;
        _raiseAnim?.Raise();
        ShowPreview();
    }

    /// <summary>Destroys all preview pieces, lowers floppy, and resets state.</summary>
    public void ClearHighlight()
    {
        _selected = false;
        _raiseAnim?.Lower();

        if (gameController != null)
            gameController.DespawnPreviewSquad(previewPieces);
        else
        {
            foreach (Piece p in previewPieces)
                if (p != null) Destroy(p.gameObject);
            previewPieces.Clear();
        }
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private void ShowPreview()
    {
        // Don't clear on enter — just append fresh preview
        if (gameController == null || squad == null) return;

        List<Piece> spawned = gameController.SpawnPreviewSquad(squad);
        previewPieces.AddRange(spawned);
    }
}
