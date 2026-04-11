using UnityEngine;

/// <summary>
/// Attached to squad-select ghost pieces by <see cref="SquadButtonHover"/>.
/// Mirrors the billboard + z-depth logic from <see cref="Piece.BillboardAndSort"/>
/// so preview sprites stay upright and correctly positioned in isometric view.
///
/// Sorting order is also updated every frame using the same grid-position formula
/// as live pieces so ghosts never render behind tiles in isometric mode.
/// </summary>
public class GhostPieceBillboard : MonoBehaviour
{
    private const float IsometricZ = 0.4f;

    // Sorting constants mirrored from Piece.cs.
    private const float SortWeightX  =  1f;
    private const float SortWeightY  = -1f;
    private const int   SortScale    = 10;
    private const int   PieceSortBase = 100;

    /// <summary>The z position used when the camera is in top-down mode (render in front of tiles).</summary>
    private float _topDownZ;

    private SpriteRenderer _sr;

    /// <summary>Grid position used to compute the correct isometric sorting order each frame.</summary>
    private Vector2Int _gridPos;

    /// <summary>
    /// Stores the original flat z and the grid position so sorting can be kept correct at all times.
    /// </summary>
    public void Initialize(float topDownZ, Vector2Int gridPos)
    {
        _topDownZ = topDownZ;
        _gridPos  = gridPos;
        _sr       = GetComponent<SpriteRenderer>();
    }

    /// <summary>Legacy overload — grid position defaults to (0,0). Prefer the two-argument version.</summary>
    public void Initialize(float topDownZ)
    {
        Initialize(topDownZ, Vector2Int.zero);
    }

    private void LateUpdate()
    {
        CameraController cc = CameraController.Instance;
        if (cc == null) return;

        if (cc.IsIsometric)
        {
            // Counter the camera tilt so the sprite always faces the viewer.
            transform.rotation = Camera.main.transform.rotation;

            Vector3 pos = transform.position;
            pos.z = IsometricZ;
            transform.position = pos;
        }
        else
        {
            // Top-down: flat upright orientation.
            transform.rotation = Quaternion.identity;

            Vector3 pos = transform.position;
            pos.z = _topDownZ;
            transform.position = pos;
        }

        // Keep sorting order in sync with live pieces regardless of camera mode.
        if (_sr != null)
            _sr.sortingOrder = PieceSortBase + (int)((_gridPos.x * SortWeightX + _gridPos.y * SortWeightY) * SortScale);
    }
}
