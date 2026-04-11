using UnityEngine;

/// <summary>
/// Manages upgrade and glitch particle effects on a piece.
/// Assign playerParticlePrefab and enemyParticlePrefab in the Inspector.
/// The correct prefab is instantiated based on the piece's team.
/// Colour is baked into the prefab assets and is NEVER modified at runtime.
/// Call Refresh() whenever isUpgraded or isGlitched changes.
/// </summary>
[RequireComponent(typeof(Piece))]
public class PieceParticleEffect : MonoBehaviour
{
    [Header("Particle Prefabs")]
    [Tooltip("Particle prefab for player upgraded pieces. Colour baked in — never changed at runtime.")]
    [SerializeField] private GameObject playerParticlePrefab;

    [Tooltip("Particle prefab for enemy upgraded pieces. Colour baked in — never changed at runtime.")]
    [SerializeField] private GameObject enemyParticlePrefab;

    [Tooltip("Optional particle prefab used when the piece is glitched (both teams).")]
    [SerializeField] private GameObject glitchedParticlePrefab;

    [Tooltip("Drift speed (world units/sec) matching what is baked into the prefabs.")]
    [SerializeField] private float particleSpeed = 0.25f;

    private Piece          _piece;
    private ParticleSystem _ps;
    private GameObject     _psInstance;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _piece = GetComponent<Piece>();
    }

    private void Start()
    {
        ApplyScreenUpVelocity(Camera.main != null ? Camera.main.transform.up : Vector3.up);
    }

    private void OnEnable()  => CameraController.OnViewChanged += OnViewChanged;
    private void OnDisable() => CameraController.OnViewChanged -= OnViewChanged;

    // ── Public ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Spawns or stops the correct particle prefab for the piece's current state.
    /// Must be called after isPlayer, isUpgraded, and isGlitched are fully set.
    /// </summary>
    public void Refresh()
    {
        bool shouldPlay = _piece.isUpgraded || _piece.isGlitched;

        if (!shouldPlay)
        {
            StopAndDestroyInstance();
            return;
        }

        GameObject prefab = ResolvePrefab();
        if (prefab == null)
        {
            Debug.LogWarning($"[PieceParticleEffect] {gameObject.name} (isPlayer={_piece.isPlayer}): prefab is null — no particle effect will play.");
            StopAndDestroyInstance();
            return;
        }

        // Correct prefab already running — nothing to do.
        if (_psInstance != null && _psInstance.name == prefab.name + "(Clone)")
            return;

        StopAndDestroyInstance();

        _psInstance                         = Instantiate(prefab, transform);
        _psInstance.transform.localPosition = Vector3.zero;

        // Prefer the PS on the root; fall back to a child.
        _ps = _psInstance.GetComponent<ParticleSystem>()
           ?? _psInstance.GetComponentInChildren<ParticleSystem>(true);

        if (_ps != null)
        {
            var rend = _ps.GetComponent<ParticleSystemRenderer>();
            if (rend != null)
            {
                rend.sortingLayerName = "Default";
                rend.sortingOrder     = 300;
            }

            _ps.gameObject.SetActive(true);
            if (!_ps.isPlaying) _ps.Play();

            Debug.Log($"[PieceParticleEffect] {gameObject.name} (isPlayer={_piece.isPlayer}): spawned '{prefab.name}' — isPlaying={_ps.isPlaying}, worldPos={_psInstance.transform.position}, mat={rend?.material?.name}");
        }
        else
        {
            Debug.LogWarning($"[PieceParticleEffect] {gameObject.name}: no ParticleSystem found on prefab '{prefab.name}'.");
        }

        ApplyScreenUpVelocity(Camera.main != null ? Camera.main.transform.up : Vector3.up);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private GameObject ResolvePrefab()
    {
        if (_piece.isGlitched && glitchedParticlePrefab != null)
            return glitchedParticlePrefab;

        return _piece.isPlayer ? playerParticlePrefab : enemyParticlePrefab;
    }

    private void StopAndDestroyInstance()
    {
        if (_psInstance != null)
        {
            if (_ps != null && _ps.isPlaying)
                _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(_psInstance);
        }
        _psInstance = null;
        _ps         = null;
    }

    private void OnViewChanged(Vector3 cameraUp) => ApplyScreenUpVelocity(cameraUp);

    /// <summary>Repoints particle velocity toward the top of screen on camera rotation.</summary>
    private void ApplyScreenUpVelocity(Vector3 cameraUp)
    {
        if (_ps == null) return;

        Vector3 dir = cameraUp.normalized * particleSpeed;
        var vel = _ps.velocityOverLifetime;
        if (!vel.enabled) return;

        vel.x = new ParticleSystem.MinMaxCurve(dir.x);
        vel.y = new ParticleSystem.MinMaxCurve(dir.y);
        vel.z = new ParticleSystem.MinMaxCurve(dir.z);
    }
}
