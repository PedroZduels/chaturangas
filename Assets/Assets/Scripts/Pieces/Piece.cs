using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Base class for all physical chess pieces in the scene.</summary>
public abstract class Piece : MonoBehaviour
{
    public Vector2Int position;
    public bool isPlayer;
    public bool isUpgraded;
    public bool isGlitched;

    /// <summary>
    /// The prefab this piece was instantiated from.
    /// Set immediately after Instantiate() at every spawn site so that
    /// the Ctrl+Z snapshot system can re-instantiate pieces correctly.
    /// </summary>
    [HideInInspector] public GameObject sourcePrefab;

    public int  armorCount = 0;
    public bool HasArmor   => armorCount > 0;

    /// <summary>Piece cannot act for one full turn after armor is fully depleted.</summary>
    public bool isStunned = false;

    /// <summary>
    /// Set by the Double Jump floppy. When true, GameController will ask the piece
    /// to move a second time immediately after its first move this turn.
    /// Cleared automatically at the start of the next player turn.
    /// </summary>
    public bool pendingDoubleJump = false;

    /// <summary>
    /// Set by the Ethereal floppy. When true, GetLegalMoves ignores friendly blocking pieces.
    /// Cleared at the start of the next player turn.
    /// </summary>
    public bool isEthereal = false;

    /// <summary>
    /// Set by the Hack floppy. When true the enemy AI must move this piece and only this piece.
    /// Cleared after the forced move resolves.
    /// </summary>
    public bool isHacked = false;

    /// <summary>
    /// True for enemy pieces spawned mid-fight by a boss effect (e.g. Communist Grandmaster pawns).
    /// Killing a boss-spawned piece grants no bits reward.
    /// </summary>
    public bool isBossSpawned = false;

    /// <summary>
    /// Set by the Morph floppy. Holds the original prefab type so it can be restored.
    /// Cleared after the AI turn ends.
    /// </summary>
    public System.Type morphOriginalType = null;

    [Header("Config")]
    public PieceUpgradeConfig upgradeConfig;

    /// <summary>
    /// Sprite to use when this piece is spawned as an enemy.
    /// If null, the prefab's default sprite is used unchanged.
    /// </summary>
    [SerializeField] private Sprite enemySprite;

    private static readonly Color GoldTint   = new Color(1.00f, 0.84f, 0.10f, 1f);
    private static readonly Color RedTint    = new Color(0.90f, 0.20f, 0.20f, 1f);
    private static readonly Color PurpleTint = new Color(0.65f, 0.10f, 0.90f, 1f);

    // Resolved at runtime from GameController.TintConfig; fallbacks keep old behaviour
    // if no config is assigned.
    private static Color ResolvePlayerNormal   => GameController.TintConfig != null ? GameController.TintConfig.playerNormal   : Color.white;
    private static Color ResolvePlayerUpgraded => GameController.TintConfig != null ? GameController.TintConfig.playerUpgraded : GoldTint;
    private static Color ResolveEnemyNormal    => GameController.TintConfig != null ? GameController.TintConfig.enemyNormal    : RedTint;
    private static Color ResolveEnemyUpgraded  => GameController.TintConfig != null ? GameController.TintConfig.enemyUpgraded  : PurpleTint;
    /// <summary>Returns the tint that matches the piece's current state (team + upgrade).</summary>
    private Color ResolveCurrentTint() => isPlayer
        ? (isUpgraded ? ResolvePlayerUpgraded : ResolvePlayerNormal)
        : (isUpgraded ? ResolveEnemyUpgraded  : ResolveEnemyNormal);

    /// <summary>Returns the effective visible tint — override tint when active, otherwise team/upgrade tint.</summary>
    private Color ResolveEffectiveTint() => _overrideTint.HasValue ? _overrideTint.Value : ResolveCurrentTint();

    /// <summary>Degrees to tilt when an armor stack is lost.</summary>
    private const float ArmorLossTiltDeg  = 15f;
    private const float ArmorLossTiltTime = 0.35f;

    /// <summary>Y offset applied to the visual position in isometric view.</summary>
    private const float IsoYOffset = 0.4f;

    // Current tilt offset (degrees, Z-axis) applied on top of billboard rotation.
    private float _tiltAngle = 0f;

    /// <summary>Reference to the board this piece lives on. Set by GameController after spawn.</summary>
    [HideInInspector]
    public BoardManager board;

    // Logical world position from GridToWorld — the board-space source of truth.
    // BillboardAndSort snaps transform.position to VisualPos(_logicalWorldPos) only
    // when idle; during a tween the coroutine owns the position.
    private Vector3 _logicalWorldPos;
    private bool    _isTweening;
// Isometric sorting: pieces further from the iso camera appear behind closer ones.
    // Depth weight per board axis, derived from camera forward (35,45,60 euler).
    // camera forward ≈ (0.579, -0.574, 0.579) → higher X = deeper, lower Y = deeper.
    private const float SortWeightX =  1f;
    private const float SortWeightY = -1f;
    private const int   SortScale   = 10;

    private SpriteRenderer     sr;
    private List<GameObject>   _armorIndicators = new List<GameObject>();
    private Coroutine          _tiltCoroutine;
    private Coroutine          _hackFlickerCoroutine;
    private PieceParticleEffect _particleEffect;
    private Color?             _overrideTint;

    private const float HackFlickerPeriod    = 0.12f;   // seconds per on/off cycle
    private const float HackFlickerMinAlpha  = 0.15f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        sr              = GetComponentInChildren<SpriteRenderer>();
        _particleEffect = GetComponent<PieceParticleEffect>();
    }

    private void LateUpdate()
    {
        BillboardAndSort();
    }

    public abstract List<Vector2Int> GetLegalMoves(BoardManager board);
    public abstract ISimPiece ToSimPiece();

    /// <summary>
    /// Snaps the piece to a logical world position (as returned by BoardManager.GridToWorld)
    /// and applies the isometric Y offset when in iso view. Always use this instead of
    /// setting transform.position directly so BillboardAndSort stays drift-free.
    /// </summary>
    public void PlaceAt(Vector3 logicalWorldPos)
    {
        _logicalWorldPos   = logicalWorldPos;
        transform.position = VisualPos(logicalWorldPos);
    }

    /// <summary>Smooth ease-in/out tween. Yield this in a coroutine.</summary>
    public IEnumerator TweenMoveTo(Vector3 logicalDestination, float duration)
    {
        _logicalWorldPos = logicalDestination;
        Vector3 visualDestination = VisualPos(logicalDestination);

        _isTweening     = true;
        Vector3 origin  = transform.position;
        float   elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed           += Time.deltaTime;
            float t            = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(origin, visualDestination, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = visualDestination;
        _isTweening = false;
    }

    /// <summary>
    /// Fades the piece to white then destroys it. Starts a fire-and-forget coroutine
    /// so the caller does not need to yield — the piece will destroy itself after
    /// the capturing piece's tween has begun.
    /// </summary>
    public void FadeOutAndDestroy(float duration = 0.35f)
    {
        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        Color startColor = sr != null ? sr.color : Color.white;
        Color endColor   = Color.white;
        endColor.a       = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            if (sr != null) sr.color = Color.Lerp(startColor, endColor, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        Destroy(gameObject);
    }

    /// <summary>Grants armor stacks and refreshes the visual indicator.</summary>
    public void GainArmor(int amount = 1)
    {
        armorCount += amount;
        RefreshArmorIndicator();
        AudioManager.PlayArmorSpawn();
    }

    /// <summary>
    /// Consumes one armor stack. When armor reaches zero the piece is stunned for one turn.
    /// Returns true if armor remains after consumption.
    /// </summary>
    public bool ConsumeArmor()
    {
        if (armorCount <= 0) return false;
        armorCount--;
        RefreshArmorIndicator();
        TriggerArmorLossTilt();
        
            isStunned = true;
            AudioManager.PlayStun();
        
        return armorCount > 0;
    }

    /// <summary>Applies the correct tint for team + upgrade state, and swaps to the enemy sprite when not a player piece.</summary>
    public void ApplyTint()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        if (!isPlayer && enemySprite != null)
            sr.sprite = enemySprite;

        Color tint = ResolveEffectiveTint();
        sr.color   = tint;

        // Keep all armor indicator colours in sync.
        foreach (GameObject indicator in _armorIndicators)
        {
            if (indicator == null) continue;
            SpriteRenderer indicatorSr = indicator.GetComponent<SpriteRenderer>();
            if (indicatorSr != null) indicatorSr.color = tint;
        }
    }

    /// <summary>
    /// Forces a specific tint on this piece, bypassing the normal team/upgrade resolution.
    /// Call <see cref="ClearOverrideTint"/> to restore normal tinting.
    /// </summary>
    public void ApplyOverrideTint(Color tint)
    {
        _overrideTint = tint;
        ApplyTint();
    }

    /// <summary>Removes the override tint and restores the piece's normal team/upgrade color.</summary>
    public void ClearOverrideTint()
    {
        _overrideTint = null;
        ApplyTint();
    }

    /// <summary>Marks this piece as upgraded and refreshes its tint and particle effect.
    /// Clears any boss override tint so the upgrade colour always wins.</summary>
    public void SetUpgraded() { isUpgraded = true; ClearOverrideTint(); RefreshArmorIndicator(); _particleEffect?.Refresh(); }

    /// <summary>Marks this piece as glitched and refreshes its particle effect.</summary>
    public void SetGlitched() { isGlitched = true; _particleEffect?.Refresh(); }

    private const int ArmorIndicatorSortingOffset = 50;

    /// <summary>Recalculates sprite sorting order from board position.</summary>
    public void UpdateSortingOrder()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        // Tile sorting order is -(x - y) * 10, which reaches a maximum of MaxY * 10
        // when x = 0 (top-left corner). Using a base of 200 keeps pieces above tiles
        // on boards up to 20×20 and leaves headroom for the armor indicator offset.
        sr.sortingOrder = 200 + (int)((position.x * SortWeightX + position.y * SortWeightY) * SortScale);

        int armorOrder = sr.sortingOrder + ArmorIndicatorSortingOffset;
        foreach (GameObject indicator in _armorIndicators)
        {
            if (indicator == null) continue;
            SpriteRenderer indicatorSr = indicator.GetComponent<SpriteRenderer>();
            if (indicatorSr != null) indicatorSr.sortingOrder = armorOrder;
        }
    }

    private void RefreshArmorIndicator()
    {
        // Destroy all existing indicators and rebuild to match armorCount.
        foreach (GameObject indicator in _armorIndicators)
            if (indicator != null) Destroy(indicator);
        _armorIndicators.Clear();

        for (int i = 0; i < armorCount; i++)
            _armorIndicators.Add(CreateArmorIndicator(i, armorCount));
    }
    private void BillboardAndSort()
    {
        if (sr == null) return;

        CameraController cc = CameraController.Instance;
        if (cc == null) return;

        // Keep _logicalWorldPos in sync with the board's current world position
        // so pieces follow when BoardManager is moved.
        if (!_isTweening && board != null)
            _logicalWorldPos = board.GridToWorld(position);

        if (cc.IsIsometric)
        {
            Quaternion billboard = Camera.main.transform.rotation;
            Quaternion tilt      = Quaternion.AngleAxis(_tiltAngle, Camera.main.transform.forward);
            transform.rotation   = tilt * billboard;

            if (!_isTweening)
                transform.position = VisualPos(_logicalWorldPos);
        }
        else
        {
            transform.rotation = Quaternion.AngleAxis(_tiltAngle, Vector3.back);

            if (!_isTweening)
                transform.position = VisualPos(_logicalWorldPos);
        }

        UpdateSortingOrder();
    }

    // Converts a logical GridToWorld position to the visual transform position.
    // Applies CameraController.ActivePieceOffset in all views so pieces sit centered on their tile.
    private static Vector3 VisualPos(Vector3 logical)
    {
        CameraController cc = CameraController.Instance;
        if (cc == null) return logical;

        Vector2 offset = cc.ActivePieceOffset;
        float z = cc.IsIsometric ? 0.4f : logical.z;
        return new Vector3(logical.x + offset.x, logical.y + offset.y, z);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>Snaps the piece to the armor-loss tilt immediately. Stays until <see cref="ResetTilt"/> is called.</summary>
    private void TriggerArmorLossTilt()
    {
        if (_tiltCoroutine != null)
        {
            StopCoroutine(_tiltCoroutine);
            _tiltCoroutine = null;
        }
        _tiltAngle = ArmorLossTiltDeg;
    }

    /// <summary>Visually tilts the piece to indicate it is stunned. Stays until <see cref="ResetTilt"/> is called.</summary>
    public void ApplyStunTilt()
    {
        if (_tiltCoroutine != null)
        {
            StopCoroutine(_tiltCoroutine);
            _tiltCoroutine = null;
        }
        _tiltAngle = -ArmorLossTiltDeg;
    }

    /// <summary>
    /// Smoothly eases the tilt back to 0°. Call this at the start of the piece's next turn.
    /// </summary>
    public void ResetTilt()
    {
        if (_tiltAngle == 0f) return;
        if (_tiltCoroutine != null) StopCoroutine(_tiltCoroutine);
        _tiltCoroutine = StartCoroutine(EaseToUpright());
    }

    /// <summary>Starts a repeating alpha flicker to indicate the piece is hacked.</summary>
    public void StartHackFlicker()
    {
        if (_hackFlickerCoroutine != null) StopCoroutine(_hackFlickerCoroutine);
        _hackFlickerCoroutine = StartCoroutine(HackFlickerRoutine());
    }

    /// <summary>Stops the hack flicker and restores the piece's normal tint.</summary>
    public void StopHackFlicker()
    {
        if (_hackFlickerCoroutine == null) return;
        StopCoroutine(_hackFlickerCoroutine);
        _hackFlickerCoroutine = null;

        // Restore normal alpha through the tint system.
        if (sr != null)
        {
            Color c = sr.color;
            c.a      = 1f;
            sr.color = c;
        }
    }

    private IEnumerator HackFlickerRoutine()
    {
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        while (true)
        {
            // Dim phase.
            if (sr != null)
            {
                Color c = sr.color;
                c.a      = HackFlickerMinAlpha;
                sr.color = c;
            }
            yield return new WaitForSeconds(HackFlickerPeriod);

            // Full alpha phase.
            if (sr != null)
            {
                Color c = sr.color;
                c.a      = 1f;
                sr.color = c;
            }
            yield return new WaitForSeconds(HackFlickerPeriod);
        }
    }

    private IEnumerator EaseToUpright()
    {
        float start   = _tiltAngle;
        float elapsed = 0f;
        while (elapsed < ArmorLossTiltTime)
        {
            elapsed    += Time.deltaTime;
            float t     = Mathf.Clamp01(elapsed / ArmorLossTiltTime);
            _tiltAngle  = Mathf.Lerp(start, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        _tiltAngle     = 0f;
        _tiltCoroutine = null;
    }

    [Header("Armor Indicator")]
    [SerializeField] private Vector3 armorIndicatorOffset = new Vector3(0f, 0.45f, 0f);
    [SerializeField] private float   armorIndicatorScale  = 0.5f;

    /// <summary>
    /// Sprite displayed above the piece when it has armor. Assign any icon in the Inspector.
    /// Falls back to loading Assets/Art/armor.png if left empty.
    /// </summary>
    [SerializeField] private Sprite armorIndicatorSprite;

    // Horizontal spacing between stacked armor icons.
    private const float ArmorIconSpacing = 0.12f;

    private GameObject CreateArmorIndicator(int index, int total)
    {
        // Centre the row of icons around armorIndicatorOffset.
        float totalWidth = (total - 1) * ArmorIconSpacing;
        float xOffset    = -totalWidth * 0.5f + index * ArmorIconSpacing;

        var go = new GameObject($"ArmorIndicator_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = armorIndicatorOffset + new Vector3(xOffset, 0f, 0f);
        go.transform.localScale    = new Vector3(armorIndicatorScale, armorIndicatorScale, 1f);

        SpriteRenderer indicatorSr         = go.AddComponent<SpriteRenderer>();
        indicatorSr.sprite                 = ResolveArmorSprite();
        indicatorSr.color                  = ResolveEffectiveTint();
        indicatorSr.sortingLayerName       = "Game";
        indicatorSr.sortingOrder           = (sr != null ? sr.sortingOrder : 100) + ArmorIndicatorSortingOffset;
        return go;
    }

    /// <summary>Returns the sprite used for the armor indicator. Used by the squad preview.</summary>
    public Sprite GetArmorIndicatorSprite() => ResolveArmorSprite();

    /// <summary>
    /// Returns the sprite to use for the armor indicator.
    /// Uses the Inspector-assigned sprite when available, otherwise falls back to the asset path.
    /// </summary>
    private Sprite ResolveArmorSprite()
    {
        if (armorIndicatorSprite != null)
            return armorIndicatorSprite;

#if UNITY_EDITOR
        Sprite fromAssets = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Assets/Art/armor.png");
        if (fromAssets != null) return fromAssets;
#endif

        // Last-resort fallback: 1×1 white pixel so the indicator is always visible at runtime.
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}