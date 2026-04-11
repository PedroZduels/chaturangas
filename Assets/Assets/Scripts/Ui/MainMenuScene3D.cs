using UnityEngine;

/// <summary>
/// Builds the main menu 3D chess scene at runtime using real prefabs.
/// Assign all piece prefabs and the board prefab in the Inspector.
/// The three hero piece slots (HeroLeft, HeroCenter, HeroRight) are
/// pre-placed GameObjects whose scale and rotation you can pose freely in the scene.
/// </summary>
public class MainMenuScene3D : MonoBehaviour
{
    // ── Prefabs ───────────────────────────────────────────────────────────────

    [Header("Board Prefab")]
    public GameObject boardPrefab;

  

    [Header("Settings")]
    [Tooltip("Slow Y-axis rotation speed in degrees per second.")]
    public float rotationSpeed = 5f;

    [Tooltip("Overall scale multiplier for the board and all pieces on it.")]
    public float boardSizeScale = 3.5f;

    [Header("Float")]
    [Tooltip("Vertical oscillation amplitude in world units.")]
    public float floatAmplitude = 0.4f;
    [Tooltip("Vertical oscillation frequency.")]
    public float floatSpeed = 0.45f;

    // The Chess Board.fbx is 57.6 units wide covering 8 squares → 7.2 units/square.
    // We scale everything so 1 world unit = 1 square, then multiply by boardSizeScale.
    private const float BoardBaseScale  = 0.139f;  // 1 / 7.2
    private const float SquareSize      = 1f;
    private const float BoardTopY       = 0.21f;   // board surface Y after scale
    private const float PieceBaseScale  = 0.139f;



    public float _startY;

    void Start()
    {
        _startY = 3f;
        SpawnBoard();
        
    }

    void Update()
    {
        // Spin around world Y — the X tilt set in the inspector stays fixed,
        // producing a tilted rotating board like a spinning coin angled toward camera
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        // Vertical float: oscillates from _startY to _startY + floatAmplitude (never below _startY)
        Vector3 pos = transform.position;
        float wave = Mathf.Sin(Time.time * floatSpeed) * 0.8f + 0.8f; // remapped 0..1
        pos.y = _startY + wave * floatAmplitude;
        transform.position = pos;
        
    }

    // ── Board ─────────────────────────────────────────────────────────────────

    private void SpawnBoard()
    {
        if (boardPrefab == null) return;

        GameObject board = Instantiate(boardPrefab, transform);
        board.name                    = "Board";
        board.transform.localPosition = Vector3.zero;
        board.transform.localRotation = Quaternion.identity;
        board.transform.localScale    = Vector3.one * (BoardBaseScale * boardSizeScale);
        Vector3 currentRotation = transform.localEulerAngles;

        // Set the X component to -60 degrees
        // Note: Working with Euler angles can sometimes lead to unexpected results 
        // due to gimbal lock or value wrapping, but for simply setting a value once it is a common approach.
        currentRotation.x = -60f;
        currentRotation.y = 110f;
        // Apply the new rotation
        transform.localEulerAngles = currentRotation;

    }

    // ── Piece placement ───────────────────────────────────────────────────────

    
    

    // ── Helpers ───────────────────────────────────────────────────────────────

    
}
