using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    [Header("Board")]
    public BoardManager board;

    [Header("Piece Tints")]
    [Tooltip("ScriptableObject that controls piece colours. Create via Assets → Chaturanga → Piece Tint Config.")]
    public PieceTintConfig pieceTintConfig;

    /// <summary>Global accessor so Piece and UI can read tints without a per-object reference.</summary>
    public static PieceTintConfig TintConfig { get; private set; }

    [Header("Enemy")]
    public SquadDefinition enemySquad;

    [Header("Boss Fight")]
    [Tooltip("Squad used for the first boss encounter (Voris). Spawns on fight 7 (after 6 wins).")]
    public SquadDefinition bossFightSquad;

    [Tooltip("Squad used for the second boss encounter (Bobli). Spawns on fight 13 (after 12 wins).")]
    public SquadDefinition boss2FightSquad;

    /// <summary>Fight count at which the first boss (Voris) is triggered.</summary>
    private const int Boss1FightCount = 6;

    /// <summary>Fight count at which the second boss (Bobli) is triggered.</summary>
    private const int Boss2FightCount = 12;

    // Flags set to true once each boss fight has been loaded so it never repeats.
    private bool _boss1Played = false;
    private bool _boss2Played = false;

    /// <summary>The active boss effect for the current fight, or null in normal fights.</summary>
    private BossEffect _activeBossEffect;

    [Header("Board Expansion")]
    [Tooltip("Board grows by this many tiles in each dimension after every boss fight.")]
    public int boardGrowthPerBoss = 1;

    /// <summary>How many boss fights have been completed this run.</summary>
    private int _bossesDefeated = 0;

    [Header("Enemy Pool")]
    [Tooltip("One entry per difficulty tier. Tier 0 = pre-first-market, Tier 1 = after market 1, etc.")]
    public EnemyTier[] enemyTiers;

    /// <summary>Which tier is currently active. Advances each time the Mainframe is visited.</summary>
    public int CurrentTier { get; private set; } = 0;

    [Tooltip("Search depth for the AI. 1 = greedy (easy), 3 = strong. Increase per level.")]
    [Range(1, 5)]
    public int aiSearchDepth = 1;

    [Tooltip("How often the AI makes suboptimal moves. 0 = always plays the best move. " +
             "0.5 = misses captures and blunders occasionally. 1 = nearly random.")]
    [Range(0f, 1f)]
    public float aiRandomness = 0.45f;

    // ── Game state ────────────────────────────────────────────────────────────

    public const float StartingTime = 300f;  // 5 minutes
    private const float CaptureTimeBonus = 15f;
    private const float PieceMoveDuration = 0.18f; // seconds for a single movement tween leg

    public float RemainingTime { get; private set; } = StartingTime;
    public int Bits { get; private set; } = 0;

    /// <summary>
    /// Multiplier applied to the base win-bits reward (5).
    /// Relics that grant bonus gold % should increase this value.
    /// </summary>
    public float WinBitsMultiplier { get; private set; } = 1f;

    /// <summary>Total fights the player has won this run. Used to gate the Mainframe.</summary>
    public int FightCount { get; private set; } = 0;

    /// <summary>Mainframe opens after every N wins.</summary>
    public const int FightsBeforeMainframe = 3;

    public bool gameOver = false;
    private bool gameStarted = false;

    private SquadDefinition playerSquad;

    /// <summary>
    /// Piece types the player has permanently upgraded this run.
    /// Persists across fights — used to re-apply upgrades after respawn.
    /// </summary>
    private readonly HashSet<System.Type> upgradedTypes = new HashSet<System.Type>();

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<int> OnBitsChanged;
    public event Action OnTimeExpired;
    /// <summary>Fired when the player eliminates all enemy pieces.</summary>
    public event Action OnPlayerWin;
    /// <summary>Fired when the AI has no legal moves and passes its turn.</summary>
    public event Action OnAISkippedTurn;
    /// <summary>Fired when the player has no legal moves and the turn is auto-skipped.</summary>
    public event Action OnPlayerSkippedTurn;
    /// <summary>Fired when it becomes the player's turn.</summary>
    public event Action OnPlayerTurnStarted;
    /// <summary>Fired when it becomes the AI's turn.</summary>
    public event Action OnAITurnStarted;
    /// <summary>Fired whenever a new fight begins (including the first). Passes the current fight number (1-based) and whether it is the boss fight.</summary>
    public event Action<int, bool> OnFightLoaded;

    /// <summary>Fired when the player captures an enemy piece during a boss fight.</summary>
    public event Action OnEnemyPieceCaptured;
    /// <summary>Fired when the AI captures a player piece during a boss fight.</summary>
    public event Action OnPlayerPieceCaptured;

    // ── Internal ──────────────────────────────────────────────────────────────

    private bool isBusy = false;
    public List<Piece> playerPieces = new List<Piece>();
    public List<Piece> enemyPieces = new List<Piece>();

    private bool isPlayerTurn = true;

    private Piece selectedPiece;
    private List<Vector2Int> currentMoves = new List<Vector2Int>();

    // ── Promotion ─────────────────────────────────────────────────────────────

    [Header("Victory")]
    [Tooltip("Assign the VictoryPanel GameObject from the Canvas.")]
    public VictoryPanel victoryPanel;

    [Header("Defeat")]
    [Tooltip("Assign the DefeatPanel GameObject from the Canvas.")]
    public DefeatPanel defeatPanel;

    [Header("Sudden Death")]
    [Tooltip("Manages board shrinking when neither side can win.")]
    public SuddenDeathManager suddenDeath;

    [Header("Promotion")]
    [Tooltip("UI controller for the player promotion picker.")]
    public PromotionUI promotionUI;

    [Tooltip("Prefab used when the enemy auto-promotes (always Queen).")]
    public GameObject enemyQueenPrefab;

    [Tooltip("Player promotion piece prefabs — shown in the picker when a pawn reaches the last rank.")]
    public GameObject promoteQueenPrefab;
    public GameObject promoteRookPrefab;
    public GameObject promoteBishopPrefab;
    public GameObject promoteKnightPrefab;
    public GameObject promoteKingPrefab;

    /// <summary>
    /// Pieces spawned via promotion. Fight-temporary — destroyed at fight end
    /// and never shown in the market or carried to the next fight.
    /// </summary>
    private readonly List<Piece> promotedPieces = new List<Piece>();

    // ── Placement mode (market buy) ───────────────────────────────────────────

    private bool          inPlacementMode  = false;
    private GameObject    placementPrefab  = null;
    private bool          placementUpgraded = false;
    private Action        onPlacementDone  = null;

    /// <summary>Player's half: y = 0 .. ceil(height/2) - 1.</summary>
    private int PlayerHalfMaxY => Mathf.CeilToInt(board.height / 2f) - 1;

    // ── Floppy activation mode ────────────────────────────────────────────────

    /// <summary>Floppy inventory — wired in Inspector or found at runtime.</summary>
    public FloppyInventory floppyInventory;

    [Header("Floppy — Morph")]
    [Tooltip("Queen prefab used when a player piece is temporarily morphed. Must have a Queen component.")]
    public GameObject morphQueenPrefab;

    [Header("Floppy — Ctrl+Z")]
    [Tooltip("Confirm panel shown before the board revert executes.")]
    public CtrlZConfirmPanel ctrlZConfirmPanel;

    private bool           _inFloppyMode   = false;
    private IFloppyEffect  _pendingEffect  = null;
    private int            _pendingSlotIdx = -1;

    /// <summary>Fired when floppy mode starts or ends so the HUD can update cursors.</summary>
    public event Action<bool> OnFloppyModeChanged;

    // ── Board history (Ctrl+Z) ────────────────────────────────────────────────

    /// <summary>
    /// Ring buffer of the last 3 snapshots.
    /// Index 0 = two player turns ago, 1 = last AI turn end, 2 = last player turn end.
    /// </summary>
    private readonly BoardSnapshot[] _history = new BoardSnapshot[3];
    private int _historyCount = 0;

    // ── Wall floppy state ─────────────────────────────────────────────────────

    private const int WallMaxTiles = 3;
    private bool                 _inWallMode          = false;
    private int                  _wallSlotIdx         = -1;
    private readonly List<Vector2Int> _wallSelected   = new List<Vector2Int>();

    /// <summary>Tiles the AI cannot move to on its next turn (set by Wall floppy).</summary>
    public readonly HashSet<Vector2Int> WallBlockedTiles = new HashSet<Vector2Int>();

    // ── Morph tracking ────────────────────────────────────────────────────────

    /// <summary>Piece that was morphed this turn — restored at the start of the next player turn.</summary>
    private Piece _morphedQueenPiece = null;

    /// <summary>Original piece that was replaced by the morph Queen — stored for restore.</summary>
    private (GameObject go, Vector2Int pos, bool upgraded, bool glitched, int armor) _morphedOriginal;

    void Start()
    {
        TintConfig = pieceTintConfig;

        // Link the SuddenDeathManager into the BoardManager so IsInsideBoard
        // respects collapsed tiles automatically for all piece move generation.
        if (board != null && suddenDeath != null)
            board.suddenDeath = suddenDeath;

        SpawnEnemySquad();

        // Activate any boss effect assigned to the opening squad (e.g. fight 1 is already a boss fight).
        _activeBossEffect = enemySquad?.bossEffect;
        _activeBossEffect?.Activate(this);

        // Notify subscribers that fight 1 has started.
        bool isBoss = enemySquad == bossFightSquad || enemySquad == boss2FightSquad;
        OnFightLoaded?.Invoke(FightCount + 1, isBoss);

        // Capture the initial board state so Ctrl+Z works from move one.
        ResetHistory();
        PushSnapshot();
    }
    void SyncBoardState()
    {
        // Clear all tiles
        foreach (Tile tile in board.tiles)
        {
            tile.occupiedPiece = null;
        }

        // Reassign player pieces
        foreach (Piece p in playerPieces)
        {
            if (p == null) continue;

            board.tiles[p.position.x, p.position.y].occupiedPiece = p;
        }

        // Reassign enemy pieces
        foreach (Piece p in enemyPieces)
        {
            if (p == null) continue;

            board.tiles[p.position.x, p.position.y].occupiedPiece = p;
        }
    }
    void CleanupPieces()
    {
        playerPieces.RemoveAll(p => p == null || p.gameObject == null);
        enemyPieces.RemoveAll(p => p == null || p.gameObject == null);
    }
    /// <summary>Spawns all enemy pieces defined in the enemySquad ScriptableObject.</summary>
    void SpawnEnemySquad()
    {
        if (enemySquad == null)
        {
            Debug.LogError("GameController: enemySquad is not assigned!");
            return;
        }

        // Randomise positions across the enemy's back two rows before spawning.
        EnemySquadPlacer.Randomise(enemySquad.pieces, board.width, board.height);

        foreach (PieceSpawnData spawnData in enemySquad.pieces)
        {
            if (!IsSpawnPositionValid(spawnData.startPosition, spawnData.piecePrefab?.name ?? "?"))
                continue;

            Vector3 worldPos = board.GridToWorld(spawnData.startPosition);
            GameObject obj = Instantiate(spawnData.piecePrefab, worldPos, Quaternion.identity, board.transform);

            Piece piece = obj.GetComponent<Piece>();
            if (piece == null)
            {
                Debug.LogError($"Enemy prefab '{spawnData.piecePrefab.name}' is missing a Piece component.");
                continue;
            }

            piece.position = spawnData.startPosition;
            piece.PlaceAt(worldPos);
            piece.isPlayer = false;
            piece.board = board;
            piece.sourcePrefab = spawnData.piecePrefab;

            if (spawnData.isUpgraded)
                piece.SetUpgraded();
            else
                piece.ApplyTint();

            if (spawnData.startingArmor > 0)
                piece.GainArmor(spawnData.startingArmor);

            board.tiles[spawnData.startPosition.x, spawnData.startPosition.y].occupiedPiece = piece;
            enemyPieces.Add(piece);
        }

        Debug.Log($"Enemy squad '{enemySquad.squadName}' spawned with {enemyPieces.Count} piece(s).");
    }
    void Update()
    {
        if (!gameStarted || !isPlayerTurn || isBusy || RemainingTime <= 0f || gameOver) return;

        RemainingTime -= Time.deltaTime;

        if (RemainingTime <= 0f)
        {
            RemainingTime = 0f;
            Debug.Log("[Timer] Time's up!");
            OnTimeExpired?.Invoke();
        }
    }

    // 🔹 PREVIEW — spawn real pieces for the hover preview, no game-state side-effects
    /// <summary>
    /// Spawns the squad as fully-functional player pieces for the hover preview.
    /// Does NOT modify gameStarted, playerSquad, board.tiles, or any game state —
    /// safe to call repeatedly as the player hovers over squad cards.
    /// Returns the spawned pieces so the caller can destroy them on pointer-exit.
    /// </summary>
    public List<Piece> SpawnPreviewSquad(SquadDefinition squad)
    {
        var previews = new List<Piece>();
        foreach (PieceSpawnData spawnData in squad.pieces)
        {
            if (!IsSpawnPositionValid(spawnData.startPosition, spawnData.piecePrefab?.name ?? "?"))
                continue;

            Vector3 worldPos = board.GridToWorld(spawnData.startPosition);
            GameObject obj = Instantiate(spawnData.piecePrefab, worldPos, Quaternion.identity, board.transform);
            obj.name = $"Preview_{spawnData.piecePrefab.name}";

            Piece piece = obj.GetComponent<Piece>();
            if (piece == null) { Destroy(obj); continue; }

            piece.position = spawnData.startPosition;
            piece.PlaceAt(board.GridToWorld(spawnData.startPosition));
            piece.isPlayer = true;
            piece.board = board;

            if (spawnData.isUpgraded)
                piece.SetUpgraded();
            else
                piece.ApplyTint();

            if (spawnData.startingArmor > 0)
                piece.GainArmor(spawnData.startingArmor);

            previews.Add(piece);
        }
        return previews;
    }

    /// <summary>Destroys all pieces returned by SpawnPreviewSquad.</summary>
    public void DespawnPreviewSquad(List<Piece> previews)
    {
        foreach (Piece p in previews)
            if (p != null) Destroy(p.gameObject);
        previews.Clear();
    }

    // 🔹 SQUAD INITIALIZATION — called by SquadSelectUI on squad pick
    public void InitializePlayerSquad(SquadDefinition squad)
    {
        foreach (PieceSpawnData spawnData in squad.pieces)
        {
            if (!IsSpawnPositionValid(spawnData.startPosition, spawnData.piecePrefab?.name ?? "?"))
                continue;

            Vector3 worldPos = board.GridToWorld(spawnData.startPosition);
            GameObject obj = Instantiate(spawnData.piecePrefab, worldPos, Quaternion.identity, board.transform);

            Piece piece = obj.GetComponent<Piece>();
            if (piece == null)
            {
                Debug.LogError($"Prefab '{spawnData.piecePrefab.name}' is missing a Piece component.");
                continue;
            }

            piece.position = spawnData.startPosition;
            piece.PlaceAt(board.GridToWorld(spawnData.startPosition));
            piece.isPlayer = true;
            piece.board = board;
            piece.sourcePrefab = spawnData.piecePrefab;

            if (spawnData.isUpgraded)
                piece.SetUpgraded();
            else
                piece.ApplyTint();

            if (spawnData.startingArmor > 0)
                piece.GainArmor(spawnData.startingArmor);

            board.tiles[spawnData.startPosition.x, spawnData.startPosition.y].occupiedPiece = piece;
            playerPieces.Add(piece);
        }

        Debug.Log($"Squad '{squad.squadName}' initialized with {playerPieces.Count} piece(s). Game started — 5:00 on the clock.");

        // Store a runtime clone so the original ScriptableObject asset is never mutated.
        // The clone is discarded automatically when the scene reloads on defeat or quit.
        playerSquad = squad.Clone();
        gameStarted = true;

        // Initialize Sudden Death bounds for the first fight.
        if (suddenDeath != null)
        {
            board.suddenDeath = suddenDeath;
            suddenDeath.ResetForFight();
        }

        // Upgraded kings grant armor to the 3 pieces directly in front of them.
        GrantStartingArmor();

        // Capture the initial board state so Ctrl+Z can undo the very first move.
        ResetHistory();
        PushSnapshot();
    }

    /// <summary>
    /// Called once after both squads are fully placed.
    /// Any upgraded king (player or enemy) grants 1 armor to its three front pieces.
    /// </summary>
    private void GrantStartingArmor()
    {
        foreach (Piece p in playerPieces)
            if (p is King king && king.isUpgraded)
                king.GrantFrontArmor(board);

        foreach (Piece p in enemyPieces)
            if (p is King king && king.isUpgraded)
                king.GrantFrontArmor(board);
    }

    // 🔹 TILE CLICK ENTRY POINT
    public void OnTileClicked(Tile tile)
    {
        // Placement mode overrides all other input.
        if (inPlacementMode)
        {
            HandlePlacementClick(tile);
            return;
        }

        // Wall multi-tile mode.
        if (_inWallMode)
        {
            HandleWallTileClick(tile);
            return;
        }

        // Floppy mode: wait for the player to pick a valid target piece.
        if (_inFloppyMode)
        {
            HandleFloppyTargetClick(tile);
            return;
        }

        if (isBusy || !isPlayerTurn) return;

        // ── Normal selection / move flow ──────────────────────────────────────

        // Select a friendly piece.
        if (tile.occupiedPiece != null && tile.occupiedPiece.isPlayer)
        {
            if (tile.occupiedPiece.isStunned)
            {
                Debug.Log($"[Stun] {tile.occupiedPiece.GetType().Name} is stunned and cannot move this turn.");
                return;
            }
            SelectPiece(tile.occupiedPiece);
            return;
        }

        if (selectedPiece == null) return;

        // Standard move to any highlighted square.
        if (currentMoves.Contains(tile.gridPos))
        {
            Piece piece = selectedPiece;
            Vector2Int target = tile.gridPos;
            ClearSelection();
            StartCoroutine(PlayerMoveRoutine(piece, target));
        }
    }

    /// <summary>
    /// Executes a standard piece move with a smooth tween, then ends the player turn.
    /// Handles queen two-step animation (attack square → retreat square) automatically.
    /// If the piece has a pendingDoubleJump it enters a second move selection before ending.
    /// </summary>
    private IEnumerator PlayerMoveRoutine(Piece piece, Vector2Int target)
    {
        isBusy = true;

        Vector3 startWorldPos   = piece.transform.position;
        Vector3 captureWorldPos = board.GridToWorld(target);

        TryMove(piece, target);

        Vector3 endWorldPos = board.GridToWorld(piece.position);

        piece.transform.position = startWorldPos;

        bool queenRetreated = piece != null && piece.isUpgraded && piece is Queen && piece.position != target;
        if (queenRetreated)
        {
            yield return StartCoroutine(piece.TweenMoveTo(captureWorldPos, PieceMoveDuration));
            if (piece != null) yield return StartCoroutine(piece.TweenMoveTo(endWorldPos, PieceMoveDuration));
        }
        else
        {
            if (piece != null) yield return StartCoroutine(piece.TweenMoveTo(endWorldPos, PieceMoveDuration));
        }

        if (gameOver)
        {
            isBusy = false;
            ShowEndScreen();
            yield break;
        }

        if (piece != null && piece.gameObject != null &&
            piece is Pawn pawn && IsPromotionRank(pawn))
        {
            yield return StartCoroutine(PlayerPromotionRoutine(pawn));
        }

        // ── Double Jump floppy ────────────────────────────────────────────────
        if (piece != null && piece.pendingDoubleJump)
        {
            piece.pendingDoubleJump = false;
            isBusy = false;

            Debug.Log($"[Floppy:DoubleJump] {piece.GetType().Name} gets a second move.");

            // Re-select the same piece so the player can pick the second destination.
            SelectPiece(piece);

            // Wait until the player makes their second move (isBusy goes true again).
            yield return new WaitUntil(() => !isPlayerTurn || isBusy);
            yield break;   // EndTurn will be called by the second PlayerMoveRoutine.
        }

        isBusy = false;
        EndTurn();
    }

    /// <summary>
    /// Executes the upgraded bishop color-change combo with a two-leg tween:
    /// piece origin → cardinal square → final diagonal square.
    /// </summary>
    // 🔹 SELECT PIECE
    void SelectPiece(Piece piece)
    {
        ClearSelection();

        selectedPiece = piece;
        currentMoves = piece.GetLegalMoves(board);

        Debug.Log($"Selected {piece.GetType().Name} at {ChessNotation.ToNotation(piece.position)}" +
                  $" — {currentMoves.Count} move(s) available.");

        HighlightMoves();
    }

    // 🔹 HIGHLIGHT VALID MOVES
    void HighlightMoves()
    {
        foreach (var move in currentMoves)
        {
            Tile t = board.tiles[move.x, move.y];
            // Show capture overlay for occupied enemy tiles, move overlay for empty ones.
            if (t.occupiedPiece != null && !t.occupiedPiece.isPlayer)
                t.ShowCaptureHighlight(true);
            else
                t.ShowMoveHighlight(true);
        }
    }




    // 🔹 MOVE LOGIC
    public void TryMove(Piece piece, Vector2Int target)
    {
        if (piece == null) return;

        // Guard: reject out-of-range coordinates before they crash the array access.
        if (target.x < 0 || target.x >= board.width ||
            target.y < 0 || target.y >= board.height)
        {
            Debug.LogError($"[TryMove] Out-of-range target {target} for {piece.GetType().Name} " +
                           $"at {piece.position}. Board is {board.width}x{board.height}. Move skipped.");
            return;
        }

        Vector2Int from = piece.position;

        // Guard: reject out-of-range source position.
        if (from.x < 0 || from.x >= board.width ||
            from.y < 0 || from.y >= board.height)
        {
            Debug.LogError($"[TryMove] Out-of-range source {from} for {piece.GetType().Name}. Move skipped.");
            return;
        }

        Tile fromTile = board.tiles[from.x, from.y];
        Tile targetTile = board.tiles[target.x, target.y];
        string pieceName = piece.GetType().Name;

        if (fromTile.occupiedPiece == piece)
            fromTile.occupiedPiece = null;

        // Handle capture
        string capturedName = null;
        if (targetTile.occupiedPiece != null)
        {
            Piece captured = targetTile.occupiedPiece;
            bool piercesArmor = piece is Knight knight && knight.isUpgraded;

            // Armor absorption: attacker bounces back unless it's a knight.
            if (captured.HasArmor && !piercesArmor)
            {
                captured.ConsumeArmor();

                // Restore attacker's tile and abort the move — turn still ends.
                fromTile.occupiedPiece = piece;
                AudioManager.PlayArmorHit();
                Debug.Log($"[Armor] {captured.GetType().Name} absorbed a hit! " +
                          $"Armor remaining: {captured.armorCount}. " +
                          $"{piece.GetType().Name} bounces back.");
                return;
            }

            capturedName = captured.GetType().Name;

            // Player captures enemy → award Bits and bonus time.
            // Boss-spawned pieces (e.g. Communist Grandmaster pawns) grant no bits.
            if (piece.isPlayer && !captured.isPlayer && !captured.isBossSpawned)
            {
                int earnedBits = GetBitValue(captured);
                Bits += earnedBits;
                RemainingTime += CaptureTimeBonus;
                OnBitsChanged?.Invoke(Bits);

                Debug.Log($"[Reward] +{earnedBits} Bits  +{(int)CaptureTimeBonus}s  |  " +
                          $"Total: {Bits} Bits  {FormatTime(RemainingTime)} left");
            }
            else if (piece.isPlayer && !captured.isPlayer && captured.isBossSpawned)
            {
                Debug.Log($"[Reward] Boss-spawned {captured.GetType().Name} captured — no bits awarded.");
            }

            if (captured.isPlayer) playerPieces.Remove(captured);
            else enemyPieces.Remove(captured);

            // Boss portrait events — only fire during the boss fight.
            if (_activeBossEffect != null)
            {
                if (!captured.isPlayer) OnEnemyPieceCaptured?.Invoke();   // player killed an enemy piece
                else                    OnPlayerPieceCaptured?.Invoke();  // AI killed a player piece
            }

            AudioManager.PlayCapture();
            captured.FadeOutAndDestroy();
            CheckWinCondition();

            // If the last enemy was just captured, CheckWinCondition destroyed and
            // respawned all player pieces (including `piece`). Stop here.
            if (gameOver) return;
        }

        targetTile.occupiedPiece = piece;
        piece.position = target;
        piece.transform.position = board.GridToWorld(target);

        // Play move sound only for non-capture moves (captures already played their sound).
        if (capturedName == null) AudioManager.PlayMove();

        // Upgrade — Queen compound strike: retreat along the same ray after a capture
        if (piece.isUpgraded && piece is Queen queen && capturedName != null)
        {
            Vector2Int? retreat = queen.GetRetreatSquare(from, target, board);
            if (retreat.HasValue)
            {
                Vector2Int retreatPos = retreat.Value;
                targetTile.occupiedPiece = null;
                board.tiles[retreatPos.x, retreatPos.y].occupiedPiece = piece;
                piece.position = retreatPos;
                piece.transform.position = board.GridToWorld(retreatPos);
                Debug.Log($"[Queen] Compound Strike — retreated to {ChessNotation.ToNotation(retreatPos)}");
            }
        }

        string side = piece.isPlayer ? "Player" : "AI";
        Debug.Log($"[{side}] {ChessNotation.FormatMove(pieceName, from, target, capturedName)}");
    }

    /// <summary>
    /// Returns true when the given pawn has reached the opposite end of the board
    /// (row board.height-1 for player pawns, row 0 for enemy pawns).
    /// </summary>
    private bool IsPromotionRank(Pawn pawn) =>
        pawn.isPlayer ? pawn.position.y == board.height - 1
                       : pawn.position.y == 0;

    /// <summary>
    /// Handles player pawn promotion: shows the PromotionUI, waits for a pick,
    /// then replaces the pawn with the chosen piece.
    /// </summary>
    private IEnumerator PlayerPromotionRoutine(Pawn pawn)
    {
        if (promotionUI == null)
        {
            Debug.LogError("[Promotion] PromotionUI is not assigned on GameController.");
            yield break;
        }

        promotionUI.SetPrefabs(promoteQueenPrefab, promoteRookPrefab,
                               promoteBishopPrefab, promoteKnightPrefab,
                               promoteKingPrefab);

        isBusy = true;
        yield return StartCoroutine(promotionUI.ChoosePromotion());
        isBusy = false;

        GameObject chosenPrefab = promotionUI.ChosenPrefab;
        if (chosenPrefab == null) yield break;

        SpawnPromotedPiece(pawn, chosenPrefab, isPlayer: true);
    }

    /// <summary>
    /// Enemy pawn auto-promotes to Queen (upgraded if the pawn was upgraded).
    /// </summary>
    private void EnemyAutoPromote(Pawn pawn)
    {
        if (enemyQueenPrefab == null)
        {
            Debug.LogError("[Promotion] enemyQueenPrefab is not assigned on GameController.");
            return;
        }

        SpawnPromotedPiece(pawn, enemyQueenPrefab, isPlayer: false);
    }

    /// <summary>
    /// Removes the pawn from the board and spawns a promoted piece in its place.
    /// The promoted piece is fight-temporary: tracked in <see cref="promotedPieces"/>
    /// and destroyed at fight end without touching the squad or market.
    /// </summary>
    private void SpawnPromotedPiece(Pawn pawn, GameObject prefab, bool isPlayer)
    {
        Vector2Int pos = pawn.position;
        bool upgraded = pawn.isUpgraded;

        // Remove the pawn.
        board.tiles[pos.x, pos.y].occupiedPiece = null;
        if (isPlayer) playerPieces.Remove(pawn);
        else enemyPieces.Remove(pawn);
        Destroy(pawn.gameObject);

        // Spawn the promoted piece.
        Vector3 worldPos = board.GridToWorld(pos);
        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, board.transform);
        Piece promoted = obj.GetComponent<Piece>();

        if (promoted == null)
        {
            Debug.LogError($"[Promotion] Prefab '{prefab.name}' has no Piece component.");
            return;
        }

        promoted.position = pos;
        promoted.PlaceAt(board.GridToWorld(pos));
        promoted.isPlayer = isPlayer;
        promoted.board = board;
        promoted.sourcePrefab = prefab;

        if (upgraded) promoted.SetUpgraded();
        else promoted.ApplyTint();

        // Re-apply the active boss tint so the promoted piece isn't unexpectedly cyan mid-fight.
        ApplyActiveBossTintToPiece(promoted);

        board.tiles[pos.x, pos.y].occupiedPiece = promoted;

        if (isPlayer) playerPieces.Add(promoted);
        else enemyPieces.Add(promoted);

        // Track as fight-temporary so it is cleaned up before the market.
        promotedPieces.Add(promoted);

        string side = isPlayer ? "Player" : "AI";
        Debug.Log($"[Promotion] {side} pawn promoted to {promoted.GetType().Name} at " +
                  $"{ChessNotation.ToNotation(pos)}{(upgraded ? " (upgraded)" : "")}.");
    }

    /// <summary>
    /// Called when an upgraded rook moves. Destroys every enemy pawn it phased
    /// through between 'from' (exclusive) and 'target' (exclusive) along the
    /// cardinal ray. Armor is respected: armored pawns lose one stack and survive.
    /// </summary>
    private void SweepRookPhasedPawns(Piece rook, Vector2Int from, Vector2Int target)
    {
        Vector2Int dir = new Vector2Int(
            Math.Sign(target.x - from.x),
            Math.Sign(target.y - from.y));

        Vector2Int cur = from + dir;
        while (cur != target)
        {
            if (!board.IsInsideBoard(cur)) break;

            Piece occupant = board.tiles[cur.x, cur.y].occupiedPiece;
            if (occupant != null && occupant is Pawn && occupant.isPlayer != rook.isPlayer)
            {
                if (occupant.HasArmor)
                {
                    occupant.ConsumeArmor();
                    Debug.Log($"[Rook] Phased through armored {occupant.GetType().Name} at " +
                              $"{ChessNotation.ToNotation(cur)} — armor reduced to {occupant.armorCount}.");
                }
                else
                {
                    board.tiles[cur.x, cur.y].occupiedPiece = null;

                    if (occupant.isPlayer) playerPieces.Remove(occupant);
                    else enemyPieces.Remove(occupant);

                    Destroy(occupant.gameObject);
                    Debug.Log($"[Rook] Phased through and killed {occupant.GetType().Name} at {ChessNotation.ToNotation(cur)}.");
                    CheckWinCondition();
                }
            }

            cur += dir;
        }
    }

    /// <summary>Removes a piece from the board without moving an attacker (used by AOE / direct removal).</summary>
    private void TryCapture(Piece target)
    {
        board.tiles[target.position.x, target.position.y].occupiedPiece = null;

        if (target.isPlayer) playerPieces.Remove(target);
        else enemyPieces.Remove(target);

        Destroy(target.gameObject);
        Debug.Log($"[Remove] {target.GetType().Name} at {ChessNotation.ToNotation(target.position)} removed.");

        CheckWinCondition();
    }

    /// <summary>Fires OnPlayerWin when every enemy piece has been eliminated.</summary>
    private void CheckWinCondition()
    {
        CheckAndResolveWin();
    }

    /// <summary>
    /// Checks both win and loss conditions and resolves them.
    /// Returns true if the game ended (used by SuddenDeathManager to abort collapse early).
    /// </summary>
    public bool CheckAndResolveWin()
    {
        if (gameOver) return true;

        CleanupPieces();

        if (enemyPieces.Count == 0)
        {
            gameOver     = true;
            isPlayerTurn = false;
            FightCount++;
            Debug.Log($"[Victory] All enemies eliminated — player wins! (Fight #{FightCount})");

            bool wasBossFight = _activeBossEffect != null;

            _activeBossEffect?.Deactivate(this);
            _activeBossEffect = null;

            // Expand the board after every boss victory.
            if (wasBossFight)
            {
                _bossesDefeated++;
                GrowBoardAfterBoss();
            }

            OnPlayerWin?.Invoke();
            return true;
        }

        if (playerPieces.Count == 0)
        {
            gameOver     = true;
            isPlayerTurn = false;
            Debug.Log("[Defeat] All player pieces lost.");

            _activeBossEffect?.Deactivate(this);
            _activeBossEffect = null;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Displays the victory or defeat panel. Call this only after all move
    /// animations have finished so the player sees the final board state.
    /// </summary>
    private void ShowEndScreen()
    {
        if (!gameOver) return;

        if (enemyPieces.Count == 0)
        {
            // Capture alive count before RespawnSquadForMarket clears and rebuilds the list.
            int alivePieces = playerPieces.Count;
            RespawnSquadForMarket();
            victoryPanel?.Show(RemainingTime, alivePieces);
        }
        else
        {
            defeatPanel?.Show();
        }
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantly destroys all enemy pieces and triggers the normal victory path.
    /// </summary>
    public void AutoWin()
    {
        if (gameOver) return;

        isBusy       = true;
        isPlayerTurn = false;

        foreach (Piece p in new List<Piece>(enemyPieces))
        {
            if (p == null) continue;
            board.tiles[p.position.x, p.position.y].occupiedPiece = null;
            Destroy(p.gameObject);
        }
        enemyPieces.Clear();

        isBusy = false;
        CheckWinCondition();
        ShowEndScreen();
    }

    /// <summary>
    /// Debug shortcut — immediately loads the specified boss fight without
    /// going through the normal fight-count gate.
    /// boss = 1 → Voris (Boss 1), boss = 2 → Bobli (Boss 2).
    /// </summary>
    public void LoadBoss(int boss)
    {
        SquadDefinition squad = boss == 2 ? boss2FightSquad : bossFightSquad;
        if (squad == null)
        {
            Debug.LogWarning($"[DebugHUD] Boss {boss} squad is not assigned on GameController.");
            return;
        }

        // Reset so the fight can be re-entered cleanly from any state.
        gameOver     = false;
        isPlayerTurn = true;
        isBusy       = false;

        // Reset the played flag so the boss can trigger again through normal progression after testing.
        if (boss == 1) _boss1Played = false;
        else           _boss2Played = false;

        LoadNextFight(squad);
    }

    /// <summary>
    /// Expands the board by <see cref="boardGrowthPerBoss"/> tiles in each dimension.
    /// Called after each boss fight victory, before the next fight loads.
    /// </summary>
    private void GrowBoardAfterBoss()
    {
        if (board == null || boardGrowthPerBoss <= 0) return;

        int newW = board.width  + boardGrowthPerBoss;
        int newH = board.height + boardGrowthPerBoss;

        board.Expand(newW, newH);

        // Refresh Sudden Death bounds so it knows the board is larger.
        if (suddenDeath != null)
            suddenDeath.ResetForFight();

        Debug.Log($"[Board] Post-boss expansion to {newW}×{newH} (bosses defeated: {_bossesDefeated}).");
    }

    /// <summary>
    /// Destroys surviving player pieces and re-spawns the entire squad at their
    /// original positions, re-applying any upgrades earned so far.
    /// Enemy pieces and board state are left untouched — this is market-prep only.
    /// </summary>
    public void RespawnSquadForMarket()
    {
        if (playerSquad == null) return;

        // Destroy all fight-temporary promoted pieces first (player and enemy).
        foreach (Piece p in promotedPieces)
        {
            if (p == null) continue;
            board.tiles[p.position.x, p.position.y].occupiedPiece = null;
            playerPieces.Remove(p);
            enemyPieces.Remove(p);
            Destroy(p.gameObject);
        }
        promotedPieces.Clear();

        // Remove survivors from the board.
        foreach (Piece p in playerPieces)
        {
            if (p == null) continue;
            board.tiles[p.position.x, p.position.y].occupiedPiece = null;
            Destroy(p.gameObject);
        }
        playerPieces.Clear();

        // Respawn the full squad at starting positions with upgrades re-applied.
        RespawnPlayerPieces(playerSquad);

        // Reset Sudden Death so red tile tints and broken tiles are cleared before the market.
        if (suddenDeath != null)
            suddenDeath.ResetForFight();

        Debug.Log($"[Market] Player squad respawned — {playerPieces.Count} piece(s) available for upgrade.");
    }

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>Adds bits to the player's total and fires OnBitsChanged.</summary>
    public void AddBits(int amount)
    {
        Bits += amount;
        OnBitsChanged?.Invoke(Bits);
    }

    /// <summary>
    /// Permanently upgrades a piece type for the rest of the run.
    /// Applies the gold tint immediately and records the type so future spawns inherit it.
    /// </summary>
    public void UpgradePiece(Piece piece)
    {
        upgradedTypes.Add(piece.GetType());
        piece.SetUpgraded();
        AudioManager.PlayUpgrade();
    }

    /// <summary>
    /// Increases the win-bits multiplier. Called by relics that grant bonus gold %.
    /// For example, a relic granting +50% → ApplyWinBitsMultiplier(1.5f).
    /// </summary>
    public void ApplyWinBitsMultiplier(float multiplier)
    {
        WinBitsMultiplier *= multiplier;
    }

    /// <summary>Returns true if the given individual piece instance is upgraded.</summary>
    public bool IsPieceUpgraded(Piece piece) => piece != null && piece.isUpgraded;

    /// <summary>
    /// Upgrades a single piece instance permanently for this run.
    /// Deducts cost from Bits. Returns false if funds are insufficient or the piece is already upgraded.
    /// The upgrade is written into the runtime squad clone so it survives fight respawns,
    /// but the original ScriptableObject asset on disk is never touched.
    /// </summary>
    public bool UpgradePiece(Piece piece, int cost)
    {
        if (piece == null || piece.isUpgraded || Bits < cost) return false;

        AddBits(-cost);
        piece.SetUpgraded();
        AudioManager.PlayUpgrade();

        // Write the flag into the runtime clone so respawns inherit it.
        if (playerSquad != null)
        {
            foreach (PieceSpawnData data in playerSquad.pieces)
            {
                if (data.startPosition == piece.position)
                {
                    data.isUpgraded = true;
                    break;
                }
            }
        }

        return true;
    }

    // ── Placement mode ────────────────────────────────────────────────────────

    /// <summary>
    /// Begins placement mode: the next valid player-half tile click
    /// will spawn <paramref name="prefab"/> there and call <paramref name="onDone"/>.
    /// Highlights every unoccupied tile in the player's half.
    /// </summary>
    public void EnterPlacementMode(GameObject prefab, bool upgraded, Action onDone)
    {
        placementPrefab   = prefab;
        placementUpgraded = upgraded;
        onPlacementDone   = onDone;
        inPlacementMode   = true;

        // Highlight valid placement tiles with the dedicated placement overlay.
        for (int x = 0; x < board.width; x++)
            for (int y = 0; y <= PlayerHalfMaxY; y++)
                if (board.tiles[x, y].occupiedPiece == null)
                    board.tiles[x, y].ShowPlacementHighlight(true);
    }

    /// <summary>Cancels placement mode and clears tile highlights.</summary>
    public void ExitPlacementMode()
    {
        if (!inPlacementMode) return;
        inPlacementMode = false;
        placementPrefab = null;
        onPlacementDone = null;

        // Clear placement overlays.
        if (board?.tiles != null)
            foreach (Tile t in board.tiles)
                t.ShowPlacementHighlight(false);
    }

    /// <summary>
    /// Called from <see cref="OnTileClicked"/> when in placement mode.
    /// Spawns the piece on the chosen tile, adds it to the player squad's
    /// PieceSpawnData so it persists across fights, and exits placement mode.
    /// </summary>
    private void HandlePlacementClick(Tile tile)
    {
        // Validate: must be player half and unoccupied.
        if (tile.gridPos.y > PlayerHalfMaxY || tile.occupiedPiece != null)
        {
            Debug.Log("[Market] Invalid placement tile — must be on your half and empty.");
            return;
        }

        // Spawn piece.
        Vector3    worldPos = board.GridToWorld(tile.gridPos);
        GameObject obj      = Instantiate(placementPrefab, worldPos, Quaternion.identity, board.transform);
        Piece      piece    = obj.GetComponent<Piece>();

        if (piece == null)
        {
            Debug.LogError("[Market] Purchased prefab has no Piece component.");
            ExitPlacementMode();
            return;
        }

        piece.position = tile.gridPos;
        piece.PlaceAt(board.GridToWorld(tile.gridPos));
        piece.isPlayer = true;
        piece.board = board;

        if (placementUpgraded)
            piece.SetUpgraded();
        else
            piece.ApplyTint();

        board.tiles[tile.gridPos.x, tile.gridPos.y].occupiedPiece = piece;
        playerPieces.Add(piece);

        // Persist into the player squad so the piece survives respawn / market refresh.
        if (playerSquad != null)
        {
            playerSquad.pieces.Add(new PieceSpawnData
            {
                piecePrefab    = placementPrefab,
                startPosition  = tile.gridPos,
                isUpgraded     = placementUpgraded,
                startingArmor  = 0
            });
        }

        Debug.Log($"[Market] Placed {piece.GetType().Name} at {ChessNotation.ToNotation(tile.gridPos)}.");

        Action callback = onPlacementDone;
        ExitPlacementMode();
        callback?.Invoke();
    }

    // ── Floppy activation ─────────────────────────────────────────────────────

    /// <summary>
    /// Activates a floppy from the player's inventory at the given slot.
    /// If the effect requires a target the game enters floppy-selection mode;
    /// the floppy is only consumed once a valid target is clicked.
    /// </summary>
    public void ActivateFloppy(int slotIndex)
    {
        if (floppyInventory == null || _inFloppyMode || _inWallMode || isBusy || !isPlayerTurn) return;

        FloppyDefinition floppy = floppyInventory.PeekAt(slotIndex);
        if (floppy == null) return;

        IFloppyEffect effect = FloppyEffectFactory.Create(floppy.effectType);
        if (effect == null)
        {
            Debug.LogWarning($"[Floppy] No effect implemented for {floppy.effectType}.");
            return;
        }

        // Ctrl+Z: show confirm panel before consuming the disc.
        if (floppy.effectType == FloppyEffectType.CtrlZ)
        {
            if (ctrlZConfirmPanel == null)
            {
                Debug.LogWarning("[Floppy:CtrlZ] No CtrlZConfirmPanel assigned — executing immediately.");
                floppyInventory.RemoveAt(slotIndex);
                effect.Execute(null, this);
            }
            else
            {
                ctrlZConfirmPanel.Show(
                    onConfirm: () => { floppyInventory.RemoveAt(slotIndex); effect.Execute(null, this); },
                    onCancel:  () => Debug.Log("[Floppy:CtrlZ] Cancelled.")
                );
            }
            return;
        }

        // Wall: enter multi-tile selection mode.
        if (floppy.effectType == FloppyEffectType.Wall)
        {
            _inWallMode  = true;
            _wallSlotIdx = slotIndex;
            _wallSelected.Clear();
            OnFloppyModeChanged?.Invoke(true);
            Debug.Log("[Floppy:Wall] Select up to 3 connected empty tiles. Right-click or click occupied tile to cancel.");
            return;
        }

        if (effect.RequiresTarget)
        {
            // Enter target-selection mode; consume on valid click.
            _inFloppyMode   = true;
            _pendingEffect  = effect;
            _pendingSlotIdx = slotIndex;
            OnFloppyModeChanged?.Invoke(true);
            Debug.Log($"[Floppy] Activated '{floppy.displayName}' — click a valid piece.");
        }
        else
        {
            // Immediate effect — consume and execute right away.
            floppyInventory.RemoveAt(slotIndex);
            effect.Execute(null, this);
        }
    }

    /// <summary>Cancels any active floppy selection without consuming the disc.</summary>
    public void CancelFloppyMode()
    {
        if (_inFloppyMode)
        {
            _inFloppyMode   = false;
            _pendingEffect  = null;
            _pendingSlotIdx = -1;
            OnFloppyModeChanged?.Invoke(false);
            Debug.Log("[Floppy] Cancelled.");
        }

        if (_inWallMode)
        {
            ClearWallHighlights();
            _inWallMode  = false;
            _wallSlotIdx = -1;
            _wallSelected.Clear();
            OnFloppyModeChanged?.Invoke(false);
            Debug.Log("[Floppy:Wall] Cancelled.");
        }
    }

    private void HandleFloppyTargetClick(Tile tile)
    {
        // Clicking an empty tile cancels.
        if (tile.occupiedPiece == null)
        {
            CancelFloppyMode();
            return;
        }

        Piece piece = tile.occupiedPiece;

        if (!_pendingEffect.IsValidTarget(piece, this))
        {
            Debug.Log($"[Floppy] {piece.GetType().Name} is not a valid target for this effect.");
            CancelFloppyMode();
            return;
        }

        // Valid target — consume and execute.
        floppyInventory.RemoveAt(_pendingSlotIdx);
        _pendingEffect.Execute(piece, this);

        _inFloppyMode   = false;
        _pendingEffect  = null;
        _pendingSlotIdx = -1;
        OnFloppyModeChanged?.Invoke(false);
    }

    // ── Wall floppy ───────────────────────────────────────────────────────────

    private void HandleWallTileClick(Tile tile)
    {
        // Only accept empty, in-bounds tiles.
        if (tile.occupiedPiece != null)
        {
            CancelFloppyMode();
            return;
        }

        Vector2Int pos = tile.gridPos;

        // Deselect if clicked again.
        if (_wallSelected.Contains(pos))
        {
            _wallSelected.Remove(pos);
            tile.ShowPlacementHighlight(false);
            Debug.Log($"[Floppy:Wall] Deselected {pos}. ({_wallSelected.Count}/{WallMaxTiles})");
            return;
        }

        // Must be adjacent to an already-selected tile (or the first selection).
        if (_wallSelected.Count > 0 && !IsAdjacentToAny(pos, _wallSelected))
        {
            Debug.Log($"[Floppy:Wall] {pos} is not connected to the current selection — pick an adjacent tile.");
            return;
        }

        _wallSelected.Add(pos);
        tile.ShowPlacementHighlight(true);
        Debug.Log($"[Floppy:Wall] Selected {pos}. ({_wallSelected.Count}/{WallMaxTiles})");

        if (_wallSelected.Count >= WallMaxTiles)
            CommitWall();
    }

    /// <summary>Commits the Wall selection and blocks the chosen tiles for the next AI turn.</summary>
    public void CommitWall()
    {
        if (_wallSelected.Count == 0) { CancelFloppyMode(); return; }

        floppyInventory.RemoveAt(_wallSlotIdx);

        ClearWallHighlights();

        foreach (Vector2Int p in _wallSelected)
            WallBlockedTiles.Add(p);

        // Show danger highlight to make the wall visible to the player.
        foreach (Vector2Int p in WallBlockedTiles)
            board.tiles[p.x, p.y].ShowDangerHighlight(true);

        _inWallMode  = false;
        _wallSlotIdx = -1;
        _wallSelected.Clear();
        OnFloppyModeChanged?.Invoke(false);

        Debug.Log($"[Floppy:Wall] {WallBlockedTiles.Count} tile(s) walled. AI cannot move there next turn.");
    }

    private void ClearWallHighlights()
    {
        foreach (Vector2Int p in _wallSelected)
            if (board.IsInsideBoard(p))
                board.tiles[p.x, p.y].ShowPlacementHighlight(false);
    }

    private static bool IsAdjacentToAny(Vector2Int pos, List<Vector2Int> list)
    {
        foreach (Vector2Int other in list)
        {
            Vector2Int delta = pos - other;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                return true;
        }
        return false;
    }

    // ── Morph floppy ──────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces <paramref name="target"/> with a Queen for the current turn.
    /// The original piece is hidden; RestoreMorph swaps it back at the start of the next player turn.
    /// </summary>
    public void MorphPiece(Piece target)
    {
        if (morphQueenPrefab == null)
        {
            Debug.LogWarning("[Floppy:Morph] morphQueenPrefab not assigned.");
            return;
        }

        Vector2Int pos       = target.position;
        bool       upgraded  = target.isUpgraded;
        bool       glitched  = target.isGlitched;
        int        armor     = target.armorCount;

        // Hide original piece visually but keep it in the list for restore.
        target.gameObject.SetActive(false);
        _morphedOriginal = (target.gameObject, pos, upgraded, glitched, armor);

        // Spawn queen in its place.
        Vector3    worldPos  = board.GridToWorld(pos);
        GameObject queenGo   = Instantiate(morphQueenPrefab, worldPos, Quaternion.identity, board.transform);
        Piece      queenPiece = queenGo.GetComponent<Piece>();

        queenPiece.position = pos;
        queenPiece.PlaceAt(worldPos);
        queenPiece.isPlayer = true;
        queenPiece.board    = board;
        if (upgraded) queenPiece.SetUpgraded();
        else          queenPiece.ApplyTint();
        if (armor > 0) queenPiece.GainArmor(armor);

        // Re-apply the active boss tint so the morph queen isn't unexpectedly cyan mid-fight.
        ApplyActiveBossTintToPiece(queenPiece);

        board.tiles[pos.x, pos.y].occupiedPiece = queenPiece;

        // Replace in player list.
        playerPieces.Remove(target);
        playerPieces.Add(queenPiece);
        promotedPieces.Add(queenPiece);   // tracked so it's cleaned up normally
        _morphedQueenPiece = queenPiece;

        Debug.Log($"[Floppy:Morph] Spawned Queen at {pos} replacing {target.GetType().Name}.");
    }

    /// <summary>
    /// Called at the start of the player's next turn to undo the Morph.
    /// Destroys the Queen and restores the original piece.
    /// </summary>
    private void RestoreMorph()
    {
        if (_morphedQueenPiece == null) return;

        Vector2Int pos = _morphedQueenPiece.position;

        // Remove and destroy the morph Queen.
        playerPieces.Remove(_morphedQueenPiece);
        promotedPieces.Remove(_morphedQueenPiece);
        board.tiles[pos.x, pos.y].occupiedPiece = null;
        Destroy(_morphedQueenPiece.gameObject);
        _morphedQueenPiece = null;

        // Restore original piece.
        GameObject origGo = _morphedOriginal.go;
        if (origGo != null)
        {
            origGo.SetActive(true);
            Piece origPiece = origGo.GetComponent<Piece>();
            origPiece.position = pos;
            origPiece.PlaceAt(board.GridToWorld(pos));
            board.tiles[pos.x, pos.y].occupiedPiece = origPiece;
            playerPieces.Add(origPiece);
            Debug.Log($"[Floppy:Morph] Restored {origPiece.GetType().Name} at {pos}.");
        }

        _morphedOriginal = default;
    }

    // ── Dismantle floppy ──────────────────────────────────────────────────────

    /// <summary>Removes a player piece from the board and awards bits. Called by DismantleFloppyEffect.</summary>
    public void DismantlePiece(Piece target)
    {
        if (target == null) return;
        board.tiles[target.position.x, target.position.y].occupiedPiece = null;
        playerPieces.Remove(target);
        promotedPieces.Remove(target);
        Destroy(target.gameObject);
        Debug.Log($"[Floppy:Dismantle] Removed {target.GetType().Name}.");

        // If the player dismantled their last piece, treat it as a defeat.
        if (playerPieces.Count == 0)
        {
            gameOver = true;
            defeatPanel?.Show();
            return;
        }

        CheckWinCondition();
    }

    // ── Ctrl+Z floppy ─────────────────────────────────────────────────────────

    /// <summary>
    /// Records the current board state into the rolling history buffer.
    /// Called once at fight start (initial state), once at the end of each player turn
    /// (before AI moves), and once after the AI turn ends.
    /// </summary>
    private void PushSnapshot()
    {
        if (_historyCount < _history.Length)
            _historyCount++;

        // Shift history back by one slot.
        for (int i = _history.Length - 1; i > 0; i--)
            _history[i] = _history[i - 1];

        _history[0] = BoardSnapshot.Capture(playerPieces, enemyPieces, board);
    }

    /// <summary>Clears all history slots. Call at the start of every new fight.</summary>
    private void ResetHistory()
    {
        for (int i = 0; i < _history.Length; i++)
            _history[i] = null;
        _historyCount = 0;
    }

    /// <summary>
    /// Reverts the board to the state before the player's last move, undoing both
    /// the player's move and the AI's response.
    /// Snapshot layout when Ctrl+Z is used (during player turn, after AI moved):
    ///   _history[0] = after AI moved   (most recent)
    ///   _history[1] = after player moved
    ///   _history[2] = BEFORE player moved  ← restore target
    /// Called by CtrlZFloppyEffect after the player confirms.
    /// </summary>
    public void RevertBoard()
    {
        // Need 3 snapshots: initial/prev-AI, after-player, after-AI.
        if (_historyCount < 3)
        {
            Debug.Log("[Floppy:CtrlZ] Not enough history to revert — need at least one full turn played.");
            return;
        }

        BoardSnapshot snap = _history[2];   // state before the player moved

        // Clear current board state.
        foreach (Tile t in board.tiles) t.occupiedPiece = null;

        // Destroy all current pieces.
        foreach (Piece p in playerPieces) if (p != null) Destroy(p.gameObject);
        foreach (Piece p in enemyPieces)  if (p != null) Destroy(p.gameObject);
        playerPieces.Clear();
        enemyPieces.Clear();
        promotedPieces.Clear();
        _morphedQueenPiece = null;

        // Restore snapshotted pieces.
        RestorePieces(snap.playerStates, isPlayer: true);
        RestorePieces(snap.enemyStates,  isPlayer: false);

        // The restored state becomes the new baseline — keep it as _history[0]
        // and discard the two now-invalid entries above it.
        _history[0] = snap;
        _history[1] = null;
        _history[2] = null;
        _historyCount = 1;

        isPlayerTurn = true;
        gameOver     = false;
        isBusy       = false;

        Debug.Log("[Floppy:CtrlZ] Board reverted — player and AI moves undone.");
    }

    private void RestorePieces(List<BoardSnapshot.PieceState> states, bool isPlayer)
    {
        foreach (BoardSnapshot.PieceState s in states)
        {
            if (s.sourcePrefab == null)
            {
                Debug.LogWarning("[Floppy:CtrlZ] PieceState has no sourcePrefab — skipping.");
                continue;
            }

            // Re-instantiate a fresh piece from the original prefab.
            GameObject obj = Instantiate(s.sourcePrefab, s.worldPos, Quaternion.identity, board.transform);
            Piece piece = obj.GetComponent<Piece>();
            if (piece == null)
            {
                Debug.LogError($"[Floppy:CtrlZ] Restored prefab '{s.sourcePrefab.name}' has no Piece component.");
                Destroy(obj);
                continue;
            }

            piece.sourcePrefab = s.sourcePrefab;
            piece.position     = s.position;
            piece.isPlayer     = isPlayer;
            piece.isUpgraded   = s.isUpgraded;
            piece.isGlitched   = s.isGlitched;
            piece.isStunned    = false;
            piece.isHacked     = false;
            piece.isEthereal   = false;
            piece.StopHackFlicker();
            piece.armorCount   = s.armorCount;
            piece.board        = board;
            piece.PlaceAt(s.worldPos);
            piece.ApplyTint();

            // Re-apply the active boss tint — restored pieces are fresh prefab
            // instances with no override tint, so without this the boss red is lost.
            ApplyActiveBossTintToPiece(piece);

            board.tiles[s.position.x, s.position.y].occupiedPiece = piece;
            if (isPlayer) playerPieces.Add(piece);
            else          enemyPieces.Add(piece);
        }
    }

    /// <summary>
    /// Starts the next fight. Called by VictoryPanel and MainframeMenu.
    /// MainframeMenu should call AdvanceTier() first before calling this.
    /// </summary>
    public void StartNextFight() => AdvanceToNextFight();

    /// <summary>
    /// Picks a random squad from the current tier and starts the next fight.
    /// Boss 1 (Voris) triggers at fight 7 (after 6 wins), Boss 2 (Bobli) at fight 13 (after 12 wins).
    /// Returns false if the tier pool is empty or not configured.
    /// </summary>
    public bool AdvanceToNextFight()
    {
        if (bossFightSquad != null && !_boss1Played && FightCount == Boss1FightCount)
        {
            _boss1Played = true;
            Debug.Log($"[Fight] Triggering Boss 1 (Voris) '{bossFightSquad.squadName}' after {FightCount} wins.");
            LoadNextFight(bossFightSquad);
            return true;
        }

        if (boss2FightSquad != null && !_boss2Played && FightCount == Boss2FightCount)
        {
            _boss2Played = true;
            Debug.Log($"[Fight] Triggering Boss 2 (Bobli) '{boss2FightSquad.squadName}' after {FightCount} wins.");
            LoadNextFight(boss2FightSquad);
            return true;
        }

        if (enemyTiers == null || enemyTiers.Length == 0)
        {
            Debug.LogWarning("[Fight] enemyTiers is empty — assign tiers in the Inspector.");
            return false;
        }

        // Select the highest-index tier whose unlockAfterFight is <= current FightCount.
        // This means tiers unlock automatically based on fight number — no manual AdvanceTier() needed.
        int tierIndex = 0;
        for (int i = 0; i < enemyTiers.Length; i++)
        {
            if (FightCount >= enemyTiers[i].unlockAfterFight)
                tierIndex = i;
        }

        CurrentTier = tierIndex;
        EnemyTier tier = enemyTiers[tierIndex];

        if (tier.pool == null || tier.pool.Count == 0)
        {
            Debug.LogWarning($"[Fight] Tier {tierIndex} ('{tier.tierName}') pool is empty.");
            return false;
        }

        SquadDefinition next = PickRandomEnemy(tier);
        if (next == null) return false;
        LoadNextFight(next);
        return true;
    }

    /// <summary>
    /// Legacy method — tier selection is now automatic based on each tier's
    /// unlockAfterFight value compared to FightCount. This method is kept for
    /// any external callers but no longer needs to be called manually.
    /// </summary>
    public void AdvanceTier()
    {
        if (enemyTiers == null) return;
        Debug.Log("[Fight] AdvanceTier called — tier is now driven by unlockAfterFight, no manual advance needed.");
    }

    private SquadDefinition PickRandomEnemy(EnemyTier tier)
    {
        // Filter out null entries defensively.
        List<SquadDefinition> valid = new List<SquadDefinition>();
        foreach (SquadDefinition s in tier.pool)
            if (s != null) valid.Add(s);

        if (valid.Count == 0)
        {
            Debug.LogError($"[Fight] Tier '{tier.tierName}' has no valid (non-null) squads in its pool.");
            return null;
        }

        if (valid.Count == 1)
            return valid[0];

        // Avoid repeating the previous squad when possible.
        List<SquadDefinition> candidates = new List<SquadDefinition>();
        foreach (SquadDefinition s in valid)
            if (s != enemySquad) candidates.Add(s);

        if (candidates.Count == 0) candidates = valid;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Resets the board for the next fight: clears surviving enemy pieces,
    /// moves all player pieces back to their starting positions, spawns nextSquad as enemies,
    /// and resets fight state. Player pieces are already fully respawned from the market phase.
    /// </summary>
    public void LoadNextFight(SquadDefinition nextSquad)
    {
        if (nextSquad == null) return;

        // Clear any lingering promoted pieces from the previous fight.
        promotedPieces.Clear();

        // Destroy all surviving enemy pieces.
        foreach (Piece p in enemyPieces)
            if (p != null) Destroy(p.gameObject);
        enemyPieces.Clear();

        // Destroy market-respawned player pieces so we can re-place them fresh.
        foreach (Piece p in playerPieces)
            if (p != null) Destroy(p.gameObject);
        playerPieces.Clear();

        // Clear every tile occupants.
        foreach (Tile tile in board.tiles)
            tile.occupiedPiece = null;

        // Respawn the player squad at starting positions with upgrades applied.
        if (playerSquad != null)
            RespawnPlayerPieces(playerSquad);

        // Spawn the new enemy squad.
        enemySquad = nextSquad;
        SpawnEnemySquad();

        // Grant starting armor for any upgraded king in either squad.
        GrantStartingArmor();

        // Reset fight state.
        gameOver = false;
        isPlayerTurn = true;
        isBusy = false;
        RemainingTime = StartingTime;

        // Reset Sudden Death for the fresh fight.
        if (suddenDeath != null)
        {
            board.suddenDeath = suddenDeath;
            suddenDeath.ResetForFight();
        }

        Debug.Log($"[Fight] Started fight vs '{nextSquad.squadName}'.");

        // Switch music context: both boss squads use Boss music; otherwise defer to the tier flag.
        bool isBoss = nextSquad == bossFightSquad || nextSquad == boss2FightSquad;
        if (!isBoss && enemyTiers != null && enemyTiers.Length > 0)
        {
            int tierIndex = 0;
            for (int i = 0; i < enemyTiers.Length; i++)
                if (FightCount >= enemyTiers[i].unlockAfterFight)
                    tierIndex = i;
            isBoss = enemyTiers[tierIndex].isBossTier;
        }
        AudioManager.SwitchContext(isBoss ? AudioManager.MusicContext.Boss : AudioManager.MusicContext.Regular);

        // Activate the boss effect if this squad has one.
        _activeBossEffect?.Deactivate(this);
        _activeBossEffect = nextSquad.bossEffect;
        _activeBossEffect?.Activate(this);

        // FightCount has already been incremented after the previous win; the next fight number is FightCount + 1.
        OnFightLoaded?.Invoke(FightCount + 1, isBoss);

        // Capture the initial board state for this fight so Ctrl+Z can undo the very first move.
        ResetHistory();
        PushSnapshot();
    }

    private void RespawnPlayerPieces(SquadDefinition squad)
    {
        foreach (PieceSpawnData spawnData in squad.pieces)
        {
            if (!IsSpawnPositionValid(spawnData.startPosition, spawnData.piecePrefab?.name ?? "?"))
                continue;

            Vector3 worldPos = board.GridToWorld(spawnData.startPosition);
            GameObject obj = Instantiate(spawnData.piecePrefab, worldPos, Quaternion.identity, board.transform);
            Piece piece = obj.GetComponent<Piece>();

            if (piece == null)
            {
                Debug.LogError($"Prefab '{spawnData.piecePrefab.name}' is missing a Piece component.");
                continue;
            }

            piece.position = spawnData.startPosition;
            piece.PlaceAt(worldPos);
            piece.isPlayer = true;
            piece.board = board;
            piece.sourcePrefab = spawnData.piecePrefab;

            // Re-apply the per-piece upgrade flag persisted from a previous fight.
            if (spawnData.isUpgraded)
                piece.SetUpgraded();
            else
                piece.ApplyTint();

            // Re-apply squad-defined starting armor.
            if (spawnData.startingArmor > 0)
                piece.GainArmor(spawnData.startingArmor);

            board.tiles[spawnData.startPosition.x, spawnData.startPosition.y].occupiedPiece = piece;
            playerPieces.Add(piece);
        }
    }

    // ── Boss effect helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Allows ScriptableObject-based BossEffects to run coroutines through this MonoBehaviour.
    /// </summary>
    public Coroutine StartEffectCoroutine(IEnumerator routine) => StartCoroutine(routine);

    /// <summary>
    /// Re-applies the active boss effect's override tint to a piece if warranted.
    /// Call this after spawning or morphing any piece mid-fight so newly created pieces
    /// respect the current boss tint (e.g. the Communist Grandmaster's red infection).
    /// Upgraded pieces are intentionally excluded — they keep their gold/purple tint.
    /// </summary>
    private void ApplyActiveBossTintToPiece(Piece piece)
    {
        if (_activeBossEffect == null || piece == null || piece.isUpgraded) return;
        _activeBossEffect.ApplyTintToPiece(piece);
    }

    // ── Spawn helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="pos"/> fits inside the current board dimensions.
    /// Logs a warning so it is easy to spot misconfigured squads when the board is resized.
    /// </summary>
    private bool IsSpawnPositionValid(Vector2Int pos, string pieceName)
    {
        if (pos.x >= 0 && pos.x < board.width && pos.y >= 0 && pos.y < board.height)
            return true;

        Debug.LogWarning($"[Spawn] Skipping '{pieceName}' — position {pos} is outside the " +
                         $"{board.width}×{board.height} board. Resize squad positions to match.");
        return false;
    }

    // ── Bit values for player captures ───────────────────────────────────────

    private static int GetBitValue(Piece piece)
    {
        // Every enemy kill rewards a flat 1 bit, regardless of piece type.
        return 1;
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return $"{m}:{s:00}";
    }


    // 🔹 CLEAR SELECTION
    void ClearSelection()
    {
        foreach (var tile in board.tiles)
        {
            tile.ResetColor();
            tile.ClearMoveHighlights();
        }

        selectedPiece = null;
        currentMoves.Clear();
    }
    // 🔹 TURN SYSTEM
    public void EndTurn()
    {
        // Snapshot the board before the AI moves (slot 1 = end of player turn).
        PushSnapshot();

        // Clear per-turn flags on player pieces.
        foreach (Piece p in playerPieces)
        {
            p.isStunned        = false;
            p.pendingDoubleJump = false;
            p.isEthereal       = false;
            p.ResetTilt();
        }

        // Wall blocks are cleared by EnemyTurn after the AI has moved,
        // so the highlights and AI avoidance data remain valid for the full AI turn.

        isPlayerTurn = false;
        OnAITurnStarted?.Invoke();
        StartCoroutine(PostPlayerTurnRoutine());
    }

    /// <summary>
    /// Runs between the player's move and the AI's turn.
    /// • Checks for stalemate / first SD activation.
    /// • If SD is active: collapses the highlighted pattern immediately (step 5),
    ///   then tells SD to select the next pattern so the AI can plan around it (step 1).
    /// </summary>
    private IEnumerator PostPlayerTurnRoutine()
    {
        yield return null; // let move animations settle

        if (!gameOver && suddenDeath != null)
        {
            // Step 5 / activation: stalemate check + collapse pending tiles.
            Coroutine afterPlayer = suddenDeath.OnAfterPlayerTurn();
            if (afterPlayer != null)
                yield return afterPlayer;
        }

        if (gameOver)
        {
            // An AI piece may have been crushed by a collapsing tile — show the end screen.
            ShowEndScreen();
            yield break;
        }

        if (suddenDeath != null)
        {
            // Step 1: select the next pattern — AI will avoid those tiles.
            suddenDeath.OnBeforeAITurn();
        }

        StartCoroutine(EnemyTurn());
    }

    // 🔹 AI ENEMY TURN
    IEnumerator EnemyTurn()
    {
        isBusy = true;
        OnAITurnStarted?.Invoke();

        // ── Boss pre-move (e.g. Bolbi moves a hacked player piece first) ──────
        if (_activeBossEffect != null)
        {
            IEnumerator bossPreMove = _activeBossEffect.OnEnemyTurnStarted(this);
            if (bossPreMove != null)
                yield return StartCoroutine(bossPreMove);

            if (gameOver) { isBusy = false; ShowEndScreen(); yield break; }
        }

        // Randomised "thinking" pause — feels more natural than an instant response.
        float thinkDelay = UnityEngine.Random.Range(1f, 3f);
        yield return new WaitForSeconds(thinkDelay);

        CleanupPieces();

        // Identify stunned pieces — they skip their move this turn.
        // The flag is cleared AFTER the move so the snapshot and AI exclude them correctly.
        var stunnedThisTurn = new System.Collections.Generic.List<Piece>();
        foreach (Piece p in enemyPieces)
        {
            if (p.isStunned)
            {
                stunnedThisTurn.Add(p);
                Debug.Log($"[Stun] {p.GetType().Name} @ {p.position} is stunned — skipping its turn.");
            }
        }

        // Check for a hacked piece — force only that piece to move.
        // If the hacked piece is also stunned it cannot move, so the AI loses its turn.
        Piece hackedPieceAny = enemyPieces.Find(p => p.isHacked);
        if (hackedPieceAny != null && hackedPieceAny.isStunned)
        {
            Debug.Log($"[AI:Hack] {hackedPieceAny.GetType().Name} is hacked but stunned — AI loses its turn.");
            yield return StartCoroutine(AISkippedTurnRoutine());

            hackedPieceAny.isStunned = false;
            hackedPieceAny.isHacked  = false;
            hackedPieceAny.StopHackFlicker();

            goto EndEnemyTurn;
        }

        Piece hackedPiece = hackedPieceAny; // guaranteed non-stunned at this point

        Debug.Log($"[AI] Thinking… ({enemyPieces.Count} enemy piece(s) vs {playerPieces.Count} player piece(s))" +
                  (hackedPiece != null ? $"  HACKED: {hackedPiece.GetType().Name}" : ""));

        foreach (Piece p in enemyPieces)
            Debug.Log($"[AI]   {p.GetType().Name} @ {p.position}  stunned={p.isStunned}  hacked={p.isHacked}  armor={p.armorCount}");

        // Combine Wall blocked tiles with Sudden Death danger tiles for AI planning.
        HashSet<Vector2Int> dangerTiles = suddenDeath?.GetDangerTiles() ?? new HashSet<Vector2Int>();
        foreach (Vector2Int wt in WallBlockedTiles) dangerTiles.Add(wt);

        SimBoard snapshot = ChessAI.SnapshotBoard(board, playerPieces, enemyPieces, suddenDeath);

        // ── Apply Wall: mark walled tiles as collapsed so the AI won't move there ──
        if (WallBlockedTiles.Count > 0)
        {
            var wallSet = new HashSet<Vector2Int>(snapshot.GetCollapsedTiles() ?? new HashSet<Vector2Int>());
            foreach (Vector2Int wt in WallBlockedTiles) wallSet.Add(wt);
            snapshot.SetAdditionalBlockedTiles(wallSet);
        }

        SimAction bestAction;

        if (hackedPiece != null)
        {
            // Build a restricted snapshot that only includes the hacked piece.
            bestAction = ChessAI.GetBestMoveForPiece(snapshot, hackedPiece.position, aiSearchDepth, aiRandomness, dangerTiles);
            if (bestAction == null)
            {
                Debug.Log($"[AI:Hack] {hackedPiece.GetType().Name} has no legal moves — AI loses its turn.");
                yield return StartCoroutine(AISkippedTurnRoutine());
                bestAction = null;
            }
        }
        else
        {
            bestAction = ChessAI.GetBestEnemyMove(snapshot, aiSearchDepth, aiRandomness, dangerTiles);
        }

        if (bestAction != null)
        {
            Debug.Log($"[AI] Executing {bestAction.GetType().Name}");
            yield return StartCoroutine(ExecuteEnemyAction(bestAction));
        }
        else if (hackedPiece == null)
        {
            Debug.Log("[AI] No legal moves — passing turn.");
            yield return StartCoroutine(AISkippedTurnRoutine());
        }

        EndEnemyTurn:
        // Clear stuns now that stunned pieces have been skipped for this turn.
        foreach (Piece p in stunnedThisTurn)
        {
            p.isStunned = false;
            p.ResetTilt();
        }

        // Clear hacked flags.
        foreach (Piece p in enemyPieces)
        {
            p.isHacked = false;
            p.StopHackFlicker();
        }

        // Clear Wall tile danger highlights now that the AI turn is over.
        foreach (Vector2Int wt in WallBlockedTiles)
            if (board.IsInsideBoard(wt)) board.tiles[wt.x, wt.y].ShowDangerHighlight(false);
        WallBlockedTiles.Clear();

        // Restore morph (must happen before next player turn snapshot).
        RestoreMorph();

        // Snapshot after AI turn for Ctrl+Z history.
        PushSnapshot();

        // Step 3: show the pre-selected danger tiles to the player as a red warning.
        if (!gameOver && suddenDeath != null)
        {
            Coroutine highlight = suddenDeath.OnAfterAITurn();
            if (highlight != null)
                yield return highlight;
        }

        isBusy = false;

        if (!gameOver)
        {
            isPlayerTurn = true;
            OnPlayerTurnStarted?.Invoke();
            _activeBossEffect?.OnPlayerTurnStarted(this);

            // If every player piece is blocked, auto-skip the turn.
            if (!HasAnyPlayerLegalMove())
            {
                isPlayerTurn = false;
                isBusy       = true;
                Debug.Log("[Turn] Player has no legal moves — skipping turn.");
                StartCoroutine(PlayerSkippedTurnRoutine());
            }
        }
    }

    /// <summary>
    /// Fires OnAISkippedTurn and waits for the notification UI to finish
    /// displaying before resuming the turn sequence.
    /// </summary>
    private IEnumerator AISkippedTurnRoutine()
    {
        OnAISkippedTurn?.Invoke();
        // Wait long enough for the notification to be visible (matches TurnNotificationUI.displayDuration).
        yield return new WaitForSeconds(2f);
    }

    /// <summary>
    /// Returns true when at least one player piece has at least one legal move.
    /// </summary>
    private bool HasAnyPlayerLegalMove()
    {
        foreach (Piece p in playerPieces)
        {
            if (p != null && p.GetLegalMoves(board).Count > 0)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Fires OnPlayerSkippedTurn, waits for the alert to display,
    /// then resumes the turn sequence from the AI side.
    /// </summary>
    private IEnumerator PlayerSkippedTurnRoutine()
    {
        OnPlayerSkippedTurn?.Invoke();
        yield return new WaitForSeconds(2f);
        // Hand off to the AI turn as if the player moved.
        StartCoroutine(PostPlayerTurnRoutine());
    }

    /// <summary>
    /// Translates a SimAction chosen by the AI into actual Unity piece operations,
    /// playing a movement tween for each affected piece.
    /// </summary>
    private IEnumerator ExecuteEnemyAction(SimAction action)
    {
        switch (action)
        {
            case MoveAction m:
                {
                    Piece piece = FindEnemyAt(m.From);
                    Debug.Log($"[AI-Execute] MoveAction From={m.From} To={m.To} piece={piece?.GetType().Name ?? "NULL"}");
                    if (piece != null)
                    {
                        Vector3 startPos = piece.transform.position;
                        TryMove(piece, m.To);
                        Vector3 endPos = board.GridToWorld(piece.position);
                        piece.transform.position = startPos;
                        yield return StartCoroutine(piece.TweenMoveTo(endPos, PieceMoveDuration));

                        if (gameOver) { ShowEndScreen(); yield break; }

                        // Enemy pawn promotion.
                        if (piece != null && piece.gameObject != null &&
                            piece is Pawn pawnM && IsPromotionRank(pawnM))
                            EnemyAutoPromote(pawnM);
                    }
                    break;
                }
            case CaptureAction c:
                {
                    Piece piece = FindEnemyAt(c.From);
                    Debug.Log($"[AI-Execute] CaptureAction From={c.From} To={c.To} piece={piece?.GetType().Name ?? "NULL"}");
                    if (piece != null)
                    {
                        Vector3 startPos = piece.transform.position;
                        TryMove(piece, c.To);
                        Vector3 endPos = board.GridToWorld(piece.position);
                        piece.transform.position = startPos;
                        yield return StartCoroutine(piece.TweenMoveTo(endPos, PieceMoveDuration));

                        if (gameOver) { ShowEndScreen(); yield break; }

                        // Enemy pawn promotion.
                        if (piece != null && piece.gameObject != null &&
                            piece is Pawn pawnC && IsPromotionRank(pawnC))
                            EnemyAutoPromote(pawnC);
                    }
                    break;
                }
            case CompoundAction q:
                {
                    Piece piece = FindEnemyAt(q.From);
                    if (piece != null)
                    {
                        Vector3 startPos      = piece.transform.position;
                        Vector3 captureWorld  = board.GridToWorld(q.CaptureAt);
                        TryMove(piece, q.CaptureAt);
                        piece.transform.position = startPos;
                        yield return StartCoroutine(piece.TweenMoveTo(captureWorld, PieceMoveDuration));

                        if (gameOver) { ShowEndScreen(); yield break; }

                        // Retreat: move piece from capture square back to retreat square.
                        Vector3 retreatStart = piece.transform.position;
                        Vector3 retreatWorld = board.GridToWorld(q.RetreatTo);
                        TryMove(piece, q.RetreatTo);
                        piece.transform.position = retreatStart;
                        yield return StartCoroutine(piece.TweenMoveTo(retreatWorld, PieceMoveDuration));

                        if (gameOver) { ShowEndScreen(); yield break; }
                    }
                    break;
                }
            case AOEAction aoe:
                {
                    foreach (Vector2Int t in aoe.Targets)
                    {
                        Tile tile = board.tiles[t.x, t.y];
                        if (tile.occupiedPiece != null && tile.occupiedPiece.isPlayer)
                            TryCapture(tile.occupiedPiece);
                    }
                    break;
                }
            case BoardShiftAction shift:
                Debug.Log($"[AI] BoardShift row={shift.IsRow} index={shift.Index} dir={shift.Direction} (not yet implemented in gameplay)");
                break;

            default:
                Debug.LogWarning($"[AI] Unknown SimAction type: {action.GetType().Name}");
                break;
        }

        // Ensure the coroutine always has at least one yield even for non-animated cases.
        yield return null;
    }

    private Piece FindEnemyAt(Vector2Int pos)
    {
        // Primary: check the enemy list by logical position.
        Piece found = enemyPieces.Find(p => p != null && p.gameObject != null && p.position == pos);
        if (found != null) return found;

        // Fallback: check the tile's occupiedPiece directly — covers edge cases where
        // the list position and tile registration have drifted after a capture tween.
        if (board.IsInsideBoardOrPending(pos))
        {
            Piece onTile = board.tiles[pos.x, pos.y].occupiedPiece;
            if (onTile != null && !onTile.isPlayer)
            {
                Debug.LogWarning($"[AI] FindEnemyAt({pos}) — list miss, recovered from tile. " +
                                 $"Piece logical pos was {onTile.position}. Correcting.");
                onTile.position = pos;
                return onTile;
            }
        }

        Debug.LogWarning($"[AI] FindEnemyAt({pos}) — no enemy piece found at that position. " +
                         $"Enemy positions: [{string.Join(", ", enemyPieces.ConvertAll(p => p != null ? p.position.ToString() : "null"))}]");
        return null;
    }
}