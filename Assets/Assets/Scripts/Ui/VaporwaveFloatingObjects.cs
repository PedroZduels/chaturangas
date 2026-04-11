using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns 5 black and 5 white reflective chess pieces that orbit the main floating chess board.
/// Each piece follows a 3D elliptical orbit whose plane is randomly inclined.
/// Near-horizontal orbits sweep pieces from behind the board all the way past
/// the camera, while steeper orbits circle above and below the board.
/// Pieces use the ReflectivePiece shader with a subtle environment purple tint.
/// </summary>
public class VaporwaveFloatingObjects : MonoBehaviour
{
    // ── Piece prefabs ─────────────────────────────────────────────────────────

    [Header("Chess Piece Prefabs")]
    public GameObject pawnPrefab;
    public GameObject rookPrefab;
    public GameObject knightPrefab;
    public GameObject bishopPrefab;
    public GameObject queenPrefab;
    public GameObject kingPrefab;

    // ── Orbit ─────────────────────────────────────────────────────────────────

    [Header("Orbit")]
    [Tooltip("Assign ChessBoard3D here. Pieces orbit its world-space position.")]
    public Transform boardCenter;

    [Tooltip("Min / max orbital radius (world units). Large values bring pieces near the camera.")]
    public Vector2 orbitRadiusRange = new Vector2(3f, 13f);

    [Tooltip("Min / max orbit speed in degrees per second.")]
    public Vector2 orbitSpeedRange = new Vector2(10f, 32f);

    [Tooltip("Max ellipticity: 0 = circle, 1 = very stretched.")]
    public float maxEllipticity = 0.55f;

    // ── Piece colours ─────────────────────────────────────────────────────────

    [Header("Piece Colours")]
    [Tooltip("Base colour for black pieces — near-black with a purple environment tint.")]
    public Color blackBaseColor = new Color(0.10f, 0.07f, 0.18f, 1f);

    [Tooltip("Base colour for white pieces — near-white with a purple environment tint.")]
    public Color whiteBaseColor = new Color(0.82f, 0.78f, 0.92f, 1f);

    [Header("Shared Sky Colours (sync with scene)")]
    public Color skyTop    = new Color(0.04f, 0.00f, 0.14f, 1f);
    public Color skyMid    = new Color(0.44f, 0.04f, 0.60f, 1f);
    public Color skyBottom = new Color(0.78f, 0.06f, 0.50f, 1f);

    [Header("Shared Piece Properties")]
    public Color rimGlow        = new Color(0.5f, 0.1f, 0.8f, 1f);
    public Color occlusionColor = new Color(0.01f, 0.00f, 0.03f, 1f);

    // ── Piece animation ───────────────────────────────────────────────────────

    [Header("Piece Animation")]
    [Tooltip("Each piece self-rotates at this max speed (deg/s).")]
    public float tumbleSpeed = 22f;

    [Tooltip("Scale matching MainMenuScene3D: BoardBaseScale(0.139) x boardSizeScale(4.5).")]
    public float pieceScale = 0.626f;

    // ── Private ───────────────────────────────────────────────────────────────

    private const string ReflectiveShaderName = "Chaturanga/ReflectivePiece";
    private const int    BlackCount = 5;
    private const int    WhiteCount = 5;

    private Material _blackMat;
    private Material _whiteMat;

    private class OrbitEntry
    {
        public Transform mesh;
        public Vector3   axisA;
        public Vector3   axisB;
        public float     radiusA;
        public float     radiusB;
        public float     speed;
        public float     phase;
        public Vector3   tumbleAxis;
        public float     tumbleRate;
    }

    private readonly List<OrbitEntry> _entries = new();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        InitMaterials();
        SpawnPieces();
    }

    private void OnDestroy()
    {
        if (_blackMat != null) Destroy(_blackMat);
        if (_whiteMat != null) Destroy(_whiteMat);
    }

    // ── Material init ─────────────────────────────────────────────────────────

    /// <summary>Creates two runtime ReflectivePiece instances — one black, one white — both purple-tinted.</summary>
    private void InitMaterials()
    {
        Shader shader = Shader.Find(ReflectiveShaderName);
        if (shader == null)
        {
            Debug.LogWarning($"[VaporwaveFloatingObjects] Shader '{ReflectiveShaderName}' not found.");
            return;
        }

        _blackMat = CreatePieceMaterial(shader, blackBaseColor, "BlackPiece_RT");
        _whiteMat = CreatePieceMaterial(shader, whiteBaseColor, "WhitePiece_RT");
    }

    private Material CreatePieceMaterial(Shader shader, Color baseColor, string matName)
    {
        var mat = new Material(shader) { name = matName };
        mat.SetColor("_BaseColor",        baseColor);
        mat.SetColor("_SkyTop",           skyTop);
        mat.SetColor("_SkyMid",           skyMid);
        mat.SetColor("_SkyBottom",        skyBottom);
        mat.SetColor("_RimColor",         rimGlow);
        mat.SetColor("_OcclusionColor",   occlusionColor);
        mat.SetColor("_CloudColor",       new Color(0.60f, 0.14f, 0.82f, 1f));
        mat.SetColor("_CloudDark",        new Color(0.08f, 0.00f, 0.22f, 1f));
        mat.SetFloat("_Smoothness",       0.92f);
        mat.SetFloat("_ReflBlend",        0.70f);
        mat.SetFloat("_FresnelPow",       2.2f);
        mat.SetFloat("_OcclusionStrength",0.40f);
        mat.SetFloat("_RimPow",           4.0f);
        mat.SetFloat("_CloudDensity",     0.486f);
        mat.SetFloat("_CloudSpeed",       0.5f);
        mat.SetFloat("_CloudScale",       3.5f);
        mat.SetFloat("_SkyHorizon",       0.618f);
        return mat;
    }

    // ── Spawning ──────────────────────────────────────────────────────────────

    private void SpawnPieces()
    {
        List<GameObject> pool = BuildPool();
        if (pool.Count == 0) return;

        int totalCount = BlackCount + WhiteCount;

        for (int i = 0; i < totalCount; i++)
        {
            bool      isBlack = i < BlackCount;
            Material  mat     = isBlack ? _blackMat : _whiteMat;

            GameObject go = Instantiate(pool[i % pool.Count], transform);
            go.name = $"Orbit_{(isBlack ? "Black" : "White")}_{i}";
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale    = Vector3.one * pieceScale;

            foreach (Collider c in go.GetComponentsInChildren<Collider>())
                Destroy(c);

            if (mat != null)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = mat;

            // ── Orbit plane ──────────────────────────────────────────────────
            // pitch = 0  → orbit sweeps in XZ → piece moves toward/away from camera
            // pitch = 90 → orbit sweeps in XY → piece moves up/down
            // First third of pieces: low pitch → camera-approach sweeps guaranteed
            float yaw   = Random.Range(0f, 360f);
            float pitch = (i < totalCount / 3)
                ? Random.Range(-20f, 20f)
                : Random.Range(-70f, 70f);

            Quaternion planeRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3    axisA    = planeRot * Vector3.right;
            Vector3    axisB    = planeRot * Vector3.forward;

            float radius = Random.Range(orbitRadiusRange.x, orbitRadiusRange.y);
            float ellip  = Random.Range(0f, maxEllipticity);
            float speed  = Random.Range(orbitSpeedRange.x, orbitSpeedRange.y) * Mathf.Deg2Rad;
            if (Random.value > 0.5f) speed = -speed;

            _entries.Add(new OrbitEntry
            {
                mesh       = go.transform,
                axisA      = axisA,
                axisB      = axisB,
                radiusA    = radius,
                radiusB    = radius * (1f - ellip),
                speed      = speed,
                phase      = Random.Range(0f, Mathf.PI * 2f),
                tumbleAxis = Random.onUnitSphere,
                tumbleRate = Random.Range(-tumbleSpeed, tumbleSpeed),
            });
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        float   t      = Time.time;
        Vector3 center = boardCenter != null ? boardCenter.position : transform.position;

        foreach (OrbitEntry e in _entries)
        {
            if (e.mesh == null) continue;

            float   angle = t * e.speed + e.phase;
            Vector3 pos   = center
                          + e.axisA * (e.radiusA * Mathf.Cos(angle))
                          + e.axisB * (e.radiusB * Mathf.Sin(angle));

            e.mesh.position = pos;
            e.mesh.Rotate(e.tumbleAxis, e.tumbleRate * Time.deltaTime, Space.World);
        }
    }

    // ── Pool ──────────────────────────────────────────────────────────────────

    private List<GameObject> BuildPool()
    {
        var pool = new List<GameObject>();
        void Add(GameObject p, int n) { if (p != null) for (int i = 0; i < n; i++) pool.Add(p); }
        Add(queenPrefab,  2);
        Add(bishopPrefab, 3);
        Add(rookPrefab,   2);
        Add(knightPrefab, 2);
        Add(kingPrefab,   1);
        Add(pawnPrefab,   2);
        return pool;
    }
}
