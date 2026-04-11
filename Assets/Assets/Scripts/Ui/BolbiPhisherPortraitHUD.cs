using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Boss portrait panel shown during a Bolbi Phisher (Boss 2) fight.
///
/// Mirrors BossPortraitHUD but uses the light-blue colour scheme and
/// the blueboss.png sprite sheet. Only activates when the active squad
/// has a BolbiPhisherEffect component, so it never conflicts with the
/// red-boss HUD.
/// </summary>
[ExecuteAlways]
public class BolbiPhisherPortraitHUD : MonoBehaviour, IBossHudElement
{
    public enum BossAnim { Idle, Smile, Scream, Death }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Sprites — assign blueboss.png slices")]
    [Tooltip("blueboss_0, blueboss_4")]
    public Sprite[] idleSprites   = new Sprite[2];
    [Tooltip("blueboss_1, blueboss_2")]
    public Sprite[] smileSprites  = new Sprite[2];
    [Tooltip("blueboss_5, blueboss_6")]
    public Sprite[] screamSprites = new Sprite[2];
    [Tooltip("blueboss_3, blueboss_7")]
    public Sprite[] deathSprites  = new Sprite[2];

    [Header("Boss info")]
    public string bossName = "BOLBI PHISHER";

    [Header("References")]
    public GameController gameController;

    [Header("Visual")]
    public Color outlineColor    = new Color(0.45f, 0.80f, 1.00f, 1f);
    public Color nameColor       = new Color(0.45f, 0.80f, 1.00f, 1f);
    public Color portraitBgColor = Color.black;

    // ── Layout ────────────────────────────────────────────────────────────────

    [Header("Layout")]
    [SerializeField] private float   anchorX      = 0.82f;
    [SerializeField] private float   anchorY      = 0.55f;
    [SerializeField] private Vector2 pixelOffset  = Vector2.zero;
    [SerializeField] private float   panelWidth   = 250f;
    [SerializeField] private float   portraitSize = 250f;
    [SerializeField] private float   nameFontSize = 22f;
    [SerializeField] private float   nameHeight   = 60f;
    [SerializeField] private float   outlineWidth = 2f;

    // ── Animation timings ─────────────────────────────────────────────────────

    private const float IdleFrameTime   = 0.8f;
    private const float SmileFrameTime  = 0.25f;
    private const float ScreamFrameTime = 0.2f;
    private const float DeathFrameTime  = 0.55f;

    // ── Sprite sheet path ─────────────────────────────────────────────────────

    private const string BossSpriteSheetPath = "Assets/Assets/Art/boss/blueboss.png";

    // ── Runtime ───────────────────────────────────────────────────────────────

    private Image       _portraitImage;
    private CanvasGroup _canvasGroup;
    private Coroutine   _animCoroutine;
    private bool        _isBossFight;
    private BossAnim    _currentAnim = BossAnim.Idle;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        AutoLoadSprites();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable   = false;

        ApplyRectTransform();
        BuildUI();
        ResolvePortraitImage();

        _canvasGroup.alpha = Application.isPlaying ? 0f : 1f;
    }

    void OnValidate() => ApplyRectTransform();

    void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (gameController == null)
            gameController = Object.FindAnyObjectByType<GameController>();
        if (gameController == null) return;

        gameController.OnFightLoaded         += HandleFightLoaded;
        gameController.OnPlayerWin           += HandlePlayerWin;
        gameController.OnEnemyPieceCaptured  += HandleEnemyPieceCaptured;
        gameController.OnPlayerPieceCaptured += HandlePlayerPieceCaptured;
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        if (gameController == null) return;

        gameController.OnFightLoaded         -= HandleFightLoaded;
        gameController.OnPlayerWin           -= HandlePlayerWin;
        gameController.OnEnemyPieceCaptured  -= HandleEnemyPieceCaptured;
        gameController.OnPlayerPieceCaptured -= HandlePlayerPieceCaptured;
    }

    // ── Visibility ────────────────────────────────────────────────────────────

    private void Show() { _canvasGroup.alpha = 1f; _canvasGroup.blocksRaycasts = false; }
    private void Hide() { _canvasGroup.alpha = 0f; StopAnim(); }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Transitions the portrait to the given animation state.</summary>
    public void PlayAnim(BossAnim anim)
    {
        if (_currentAnim == BossAnim.Death && anim != BossAnim.Death) return;

        _currentAnim = anim;
        StopAnim();

        switch (anim)
        {
            case BossAnim.Idle:
                _animCoroutine = StartCoroutine(RunLoop(idleSprites, IdleFrameTime));
                break;
            case BossAnim.Smile:
                _animCoroutine = StartCoroutine(RunOnce(smileSprites, SmileFrameTime));
                break;
            case BossAnim.Scream:
                _animCoroutine = StartCoroutine(RunOnce(screamSprites, ScreamFrameTime));
                break;
            case BossAnim.Death:
                _animCoroutine = StartCoroutine(RunOnce(deathSprites, DeathFrameTime, returnToIdle: false));
                break;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void HandleFightLoaded(int fightNumber, bool isBoss)
    {
        // Only activate for fights whose squad carries a BolbiPhisherEffect.
        bool isBolbiFight = isBoss && IsBolbiFight();

        _isBossFight = isBolbiFight;
        _currentAnim = BossAnim.Idle;

        if (isBolbiFight) { Show(); PlayAnim(BossAnim.Idle); }
        else                Hide();
    }

    /// <summary>Returns true if the currently loaded enemy squad is a Bolbi Phisher fight.</summary>
    private bool IsBolbiFight()
    {
        if (gameController == null) return false;
        return gameController.enemySquad != null &&
               gameController.enemySquad.bossEffect is BolbiPhisherEffect;
    }

    private void HandlePlayerWin()
    {
        if (!_isBossFight) return;
        PlayAnim(BossAnim.Death);
    }

    private void HandleEnemyPieceCaptured()
    {
        if (!_isBossFight) return;
        PlayAnim(BossAnim.Scream);
    }

    private void HandlePlayerPieceCaptured()
    {
        if (!_isBossFight) return;
        PlayAnim(BossAnim.Smile);
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private void StopAnim()
    {
        if (_animCoroutine == null) return;
        StopCoroutine(_animCoroutine);
        _animCoroutine = null;
    }

    private IEnumerator RunLoop(Sprite[] frames, float frameTime)
    {
        if (frames == null || frames.Length == 0) yield break;
        int idx = 0;
        while (true)
        {
            SetSprite(frames[idx % frames.Length]);
            idx++;
            yield return new WaitForSeconds(frameTime);
        }
    }

    private IEnumerator RunOnce(Sprite[] frames, float frameTime, bool returnToIdle = true)
    {
        if (frames == null || frames.Length == 0)
        {
            if (returnToIdle) PlayAnim(BossAnim.Idle);
            yield break;
        }
        foreach (Sprite frame in frames)
        {
            SetSprite(frame);
            yield return new WaitForSeconds(frameTime);
        }
        _animCoroutine = null;
        if (returnToIdle) PlayAnim(BossAnim.Idle);
    }

    private void SetSprite(Sprite s)
    {
        if (_portraitImage != null && s != null)
            _portraitImage.sprite = s;
    }

    // ── IBossHudElement ───────────────────────────────────────────────────────

    public void ApplyBossTint(Color tint, System.Collections.Generic.Dictionary<int, Color> originalColors) { }
    public void RestoreTint(System.Collections.Generic.Dictionary<int, Color> originalColors) { }

    // ── Sprite auto-loading ───────────────────────────────────────────────────

    private void AutoLoadSprites()
    {
#if UNITY_EDITOR
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(BossSpriteSheetPath);

        Sprite Get(string n)
        {
            foreach (Object obj in all)
                if (obj is Sprite s && s.name == n) return s;
            return null;
        }

        if (NeedsLoad(idleSprites))   idleSprites   = new[] { Get("blueboss_0"), Get("blueboss_4") };
        if (NeedsLoad(smileSprites))  smileSprites  = new[] { Get("blueboss_1"), Get("blueboss_2") };
        if (NeedsLoad(screamSprites)) screamSprites = new[] { Get("blueboss_5"), Get("blueboss_6") };
        if (NeedsLoad(deathSprites))  deathSprites  = new[] { Get("blueboss_3"), Get("blueboss_7") };
#endif
    }

    private static bool NeedsLoad(Sprite[] arr) =>
        arr == null || arr.Length < 2 || arr[0] == null;

    // ── UI construction ───────────────────────────────────────────────────────

    private void ApplyRectTransform()
    {
        RectTransform rt = GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(anchorX, anchorY);
        rt.anchorMax        = new Vector2(anchorX, anchorY);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pixelOffset;
        rt.sizeDelta        = new Vector2(panelWidth, portraitSize + nameHeight + 8f);
    }

    private void ResolvePortraitImage()
    {
        Transform t = transform.Find("PortraitFrame/PortraitBg/Portrait");
        if (t != null) _portraitImage = t.GetComponent<Image>();
    }

    private void BuildUI()
    {
        if (transform.childCount > 0) return;

        VerticalLayoutGroup vlg    = gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.spacing                = 8f;
        vlg.padding                = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // ── Name label ────────────────────────────────────────────────────────
        GameObject nameGO   = new GameObject("BossName", typeof(RectTransform));
        nameGO.transform.SetParent(transform, worldPositionStays: false);

        TextMeshProUGUI nameTMP  = nameGO.AddComponent<TextMeshProUGUI>();
        nameTMP.text             = bossName;
        nameTMP.fontSize         = nameFontSize;
        nameTMP.fontStyle        = FontStyles.Bold;
        nameTMP.alignment        = TextAlignmentOptions.Center;
        nameTMP.color            = nameColor;
        nameTMP.raycastTarget    = false;
        nameTMP.textWrappingMode = TextWrappingModes.Normal;

        TMP_FontAsset pixelFont = ResolvePixelFont();
        if (pixelFont != null) nameTMP.font = pixelFont;

        LayoutElement nameLE   = nameGO.AddComponent<LayoutElement>();
        nameLE.preferredHeight = nameHeight;

        // ── Portrait frame (blue outline) ─────────────────────────────────────
        GameObject frameGO  = new GameObject("PortraitFrame", typeof(RectTransform));
        frameGO.transform.SetParent(transform, worldPositionStays: false);

        Image frameImg         = frameGO.AddComponent<Image>();
        frameImg.color         = outlineColor;
        frameImg.raycastTarget = false;

        LayoutElement frameLE   = frameGO.AddComponent<LayoutElement>();
        frameLE.preferredWidth  = portraitSize;
        frameLE.preferredHeight = portraitSize;

        // ── Inner black background ────────────────────────────────────────────
        GameObject bgGO = new GameObject("PortraitBg", typeof(RectTransform));
        bgGO.transform.SetParent(frameGO.transform, worldPositionStays: false);

        RectTransform bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2( outlineWidth,  outlineWidth);
        bgRt.offsetMax = new Vector2(-outlineWidth, -outlineWidth);

        Image bgImg         = bgGO.AddComponent<Image>();
        bgImg.color         = portraitBgColor;
        bgImg.raycastTarget = false;

        // ── Portrait sprite ───────────────────────────────────────────────────
        GameObject portraitGO = new GameObject("Portrait", typeof(RectTransform));
        portraitGO.transform.SetParent(bgGO.transform, worldPositionStays: false);

        RectTransform portraitRt = portraitGO.GetComponent<RectTransform>();
        portraitRt.anchorMin = Vector2.zero;
        portraitRt.anchorMax = Vector2.one;
        portraitRt.offsetMin = Vector2.zero;
        portraitRt.offsetMax = Vector2.zero;

        Image portraitImg          = portraitGO.AddComponent<Image>();
        portraitImg.preserveAspect = true;
        portraitImg.raycastTarget  = false;

        if (idleSprites != null && idleSprites.Length > 0 && idleSprites[0] != null)
            portraitImg.sprite = idleSprites[0];

        _portraitImage = portraitImg;
    }

    // ── Font helper ───────────────────────────────────────────────────────────

    private static TMP_FontAsset ResolvePixelFont()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset");
#else
        return Resources.Load<TMP_FontAsset>("Fonts & Materials/Electronic Highway Sign SDF");
#endif
    }
}
