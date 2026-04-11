using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight immutable-style board used by the AI search.
/// ApplyAction returns a new board — the original is never mutated.
/// Live bounds (set by SuddenDeathManager) shrink as columns/rows collapse,
/// so the AI never generates moves onto tiles that no longer exist.
/// </summary>
public class SimBoard
{
    private readonly int _width;
    private readonly int _height;

    private readonly ISimPiece[,] pieces;

    // Live board bounds — default to the full board dimensions.
    public int MinX { get; private set; }
    public int MaxX { get; private set; }
    public int MinY { get; private set; }
    public int MaxY { get; private set; }

    // Individual tiles locked out by Sudden Death (collapsed or pending collapse).
    // Stored separately from bounds so mid-board patterns are correctly blocked.
    private HashSet<Vector2Int> _collapsedTiles = null;

    /// <summary>
    /// Creates a SimBoard sized to match the live board dimensions.
    /// </summary>
    public SimBoard(int width, int height)
    {
        _width  = width;
        _height = height;
        pieces  = new ISimPiece[width, height];
        MinX = 0;
        MaxX = width  - 1;
        MinY = 0;
        MaxY = height - 1;
    }

    public SimBoard(SimBoard other)
    {
        _width  = other._width;
        _height = other._height;
        pieces  = new ISimPiece[_width, _height];
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                pieces[x, y] = other.pieces[x, y];
        MinX = other.MinX;
        MaxX = other.MaxX;
        MinY = other.MinY;
        MaxY = other.MaxY;
        _collapsedTiles = other._collapsedTiles; // immutable ref share is fine — never mutated after set
    }

    /// <summary>
    /// Sets the live bounds and the per-tile collapsed set the AI must avoid.
    /// Call once after constructing the snapshot from the live scene.
    /// </summary>
    public void SetLiveBounds(int minX, int maxX, int minY, int maxY,
                               HashSet<Vector2Int> collapsedTiles = null)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        _collapsedTiles = collapsedTiles;
    }

    /// <summary>Returns the current set of collapsed/blocked tiles (may be null).</summary>
    public HashSet<Vector2Int> GetCollapsedTiles() => _collapsedTiles;

    /// <summary>
    /// Merges additional blocked tiles (e.g. from the Wall floppy) into the collapsed set.
    /// Call after SetLiveBounds when extra tiles should be invisible to the AI.
    /// </summary>
    public void SetAdditionalBlockedTiles(HashSet<Vector2Int> extra)
    {
        if (extra == null || extra.Count == 0) return;
        if (_collapsedTiles == null)
            _collapsedTiles = new HashSet<Vector2Int>(extra);
        else
            foreach (Vector2Int t in extra) _collapsedTiles.Add(t);
    }

    public bool InBounds(Vector2Int pos)
    {
        if (pos.x < MinX || pos.x > MaxX || pos.y < MinY || pos.y > MaxY) return false;
        if (_collapsedTiles != null && _collapsedTiles.Contains(pos)) return false;
        return true;
    }

    public ISimPiece Get(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height) return null;
        return pieces[pos.x, pos.y];
    }

    public void Set(Vector2Int pos, ISimPiece piece)
    {
        if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height)
        {
            Debug.LogError($"[SimBoard] Set out of range: {pos}");
            return;
        }
        pieces[pos.x, pos.y] = piece;
    }

    public List<ISimPiece> GetAllPieces(bool isPlayer)
    {
        var list = new List<ISimPiece>();
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
                if (pieces[x, y] != null && pieces[x, y].IsPlayer == isPlayer)
                    list.Add(pieces[x, y]);
        return list;
    }

    public List<SimAction> GetAllActions(bool isPlayer)
    {
        var actions = new List<SimAction>();
        foreach (var p in GetAllPieces(isPlayer))
        {
            // TurnsLeft == 0 means the piece is stunned — skip it this turn.
            if (p.TurnsLeft == 0) continue;
            actions.AddRange(p.GetActions(this));
        }
        return actions;
    }

    /// <summary>
    /// Decrements the stun counter on all pieces belonging to the given side.
    /// Call this after each side completes its move so the stun expires correctly.
    /// </summary>
    public SimBoard TickStuns(bool isPlayer)
    {
        var next = new SimBoard(this);
        for (int x = 0; x < _width; x++)
            for (int y = 0; y < _height; y++)
            {
                ISimPiece p = next.pieces[x, y];
                if (p != null && p.IsPlayer == isPlayer && p.TurnsLeft == 0)
                    next.pieces[x, y] = p.TickStun();
            }
        return next;
    }

    /// <summary>Returns a new board with the action applied.</summary>
    public SimBoard ApplyAction(SimAction action)
    {
        var next = new SimBoard(this);

        switch (action)
        {
            case MoveAction m:
            {
                ISimPiece piece = next.Get(m.From);
                if (piece == null) break;
                next.Set(m.From, null);
                next.Set(m.To, piece.MovedTo(m.To));
                break;
            }

            case CaptureAction c:
            {
                ISimPiece attacker = next.Get(c.From);
                ISimPiece target   = next.Get(c.To);
                if (attacker == null) break;

                bool piercesArmor = attacker.Type == AIPieceType.Knight && attacker.IsUpgraded;

                if (target != null && target.HasArmor && !piercesArmor)
                {
                    // Armor absorbs — attacker bounces back, target loses 1 stack.
                    next.Set(c.To, target.WithoutArmor());
                }
                else
                {
                    next.Set(c.From, null);
                    next.Set(c.To, attacker.MovedTo(c.To));
                }
                break;
            }
        }

        return next;
    }

    /// <summary>
    /// Returns a copy of this board where the piece at <paramref name="hackedPos"/> has its
    /// <see cref="ISimPiece.IsPlayer"/> flag flipped to <c>false</c> (enemy side).
    /// Used by <see cref="BolbiPhisherEffect"/> so the AI evaluates moves for the hacked
    /// player piece as if it were an enemy — favouring squares that hurt the player most.
    /// </summary>
    public SimBoard BuildHackedView(Vector2Int hackedPos)
    {
        var view = new SimBoard(this);
        ISimPiece original = view.Get(hackedPos);
        if (original != null && original.IsPlayer)
            view.Set(hackedPos, original.WithSide(false));
        return view;
    }

}
