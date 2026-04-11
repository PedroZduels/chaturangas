using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores the full physical state of the board at a single point in time so that
/// Ctrl+Z can restore the scene to a previous position.
/// </summary>
public class BoardSnapshot
{
    public readonly List<PieceState> playerStates = new List<PieceState>();
    public readonly List<PieceState> enemyStates  = new List<PieceState>();

    public class PieceState
    {
        /// <summary>The prefab to re-instantiate from. Captured at snapshot time.</summary>
        public GameObject  sourcePrefab;
        public Vector2Int  position;
        public Vector3     worldPos;
        public bool        isUpgraded;
        public bool        isGlitched;
        public bool        isStunned;
        public bool        isHacked;
        public bool        isEthereal;
        public int         armorCount;
        public System.Type type;        // used to detect if a morph must be unwound first
    }

    /// <summary>Captures the current board state from the live scene.</summary>
    public static BoardSnapshot Capture(List<Piece> playerPieces, List<Piece> enemyPieces,
                                         BoardManager board)
    {
        var snap = new BoardSnapshot();
        foreach (Piece p in playerPieces)
            if (p != null && p.gameObject != null)
                snap.playerStates.Add(Capture(p, board));
        foreach (Piece p in enemyPieces)
            if (p != null && p.gameObject != null)
                snap.enemyStates.Add(Capture(p, board));
        return snap;
    }

    private static PieceState Capture(Piece p, BoardManager board) => new PieceState
    {
        sourcePrefab = p.sourcePrefab,
        position     = p.position,
        worldPos     = board.GridToWorld(p.position),
        isUpgraded   = p.isUpgraded,
        isGlitched   = p.isGlitched,
        isStunned    = p.isStunned,
        isHacked     = p.isHacked,
        isEthereal   = p.isEthereal,
        armorCount   = p.armorCount,
        type         = p.GetType(),
    };
}
