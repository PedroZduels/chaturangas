using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardManager : MonoBehaviour
{
    // ── Grid Scale Presets ────────────────────────────────────────────────────

    /// <summary>
    /// Per-grid-size camera and tile settings applied when the board reaches a specific
    /// square size. The board transform is never scaled — visual resize is achieved
    /// entirely through <see cref="tileSize"/> (tile spacing) and the camera ortho size.
    /// Non-square boards match on the larger axis.
    /// </summary>
    [Serializable]
    public struct GridScalePreset
    {
        [Tooltip("Square grid size this preset applies to (e.g. 6 for a 6×6 grid).")]
        public int gridSize;

        [Tooltip("World-space size of each tile used in GridToWorld calculations.")]
        public float tileSize;

        [Tooltip("Camera orthographic size in Top-Down view.")]
        public float topDownOrthoSize;

        [Tooltip("Camera orthographic size in Isometric view.")]
        public float isoOrthoSize;

        [Tooltip("Camera orthographic size in Isometric 2 view.")]
        public float iso2OrthoSize;
    }

    [Header("Grid Scale Presets")]
    [Tooltip("Camera ortho size and tile-size per grid size. Sorted ascending by gridSize at runtime.")]
    public List<GridScalePreset> scalePresets = new List<GridScalePreset>
    {
        new GridScalePreset { gridSize = 5, tileSize = 0.9f,  topDownOrthoSize = 5f, isoOrthoSize = 5f, iso2OrthoSize = 5f },
        new GridScalePreset { gridSize = 6, tileSize = 0.75f, topDownOrthoSize = 6f, isoOrthoSize = 6f, iso2OrthoSize = 6f },
        new GridScalePreset { gridSize = 7, tileSize = 0.65f, topDownOrthoSize = 7f, isoOrthoSize = 7f, iso2OrthoSize = 7f },
        new GridScalePreset { gridSize = 8, tileSize = 0.55f, topDownOrthoSize = 8f, isoOrthoSize = 8f, iso2OrthoSize = 8f },
    };

    // ── Board ─────────────────────────────────────────────────────────────────

    public int width = 6;
    public int height = 6;

    public Tile[,] tiles;

    public float tileSize = 1f;

    public GameObject tilePrefab;

    /// <summary>
    /// Optional reference to the Sudden Death manager.
    /// When assigned, <see cref="IsInsideBoard"/> also checks live tile bounds
    /// so pieces cannot move onto collapsed tiles.
    /// </summary>
    [HideInInspector]
    public SuddenDeathManager suddenDeath;


    void Awake()
    {
        tiles = new Tile[width, height];

    }

    /// <summary>
    /// Returns true if <paramref name="pos"/> is within the original board dimensions
    /// AND within the current live Sudden Death shrink bounds (if active).
    /// Use this for non-capture movement.
    /// </summary>
    public bool IsInsideBoard(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return false;

        if (suddenDeath != null && !suddenDeath.IsLiveTile(pos))
            return false;

        return true;
    }

    /// <summary>
    /// Returns true if <paramref name="pos"/> is within the original board dimensions
    /// and is either a live tile or a pending-collapse (red-highlighted) tile.
    /// Use this in <see cref="Piece.GetLegalMoves"/> so players can voluntarily move
    /// onto a danger strip even though those tiles are not "live".
    /// </summary>
    public bool IsInsideBoardOrPending(Vector2Int pos)
    {
        if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height)
            return false;

        if (suddenDeath != null && !suddenDeath.IsLiveTile(pos))
            return suddenDeath.IsPendingTile(pos);

        return true;
    }

    /// <summary>
    /// Returns true if <paramref name="pos"/> is within the original board dimensions,
    /// ignoring Sudden Death shrink bounds.
    /// Use this when the destination is confirmed to hold an enemy piece —
    /// players may capture into a collapsing strip to win immediately.
    /// </summary>
    public bool IsInsideBoardForCapture(Vector2Int pos) =>
        pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;

    /// <summary>
    /// Converts a grid position to a world position, offset by this transform's position.
    /// </summary>
    public Vector3 GridToWorld(Vector2Int pos)
    {
        return transform.position + new Vector3(pos.x * tileSize, pos.y * tileSize, 0);
    }

    void Start()
    {
        tiles = new Tile[width, height];

        ApplyScalePreset(Mathf.Max(width, height));

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject tileObj = Instantiate(tilePrefab, GridToWorld(new Vector2Int(x, y)), Quaternion.identity, transform);

                Tile tile = tileObj.GetComponent<Tile>();
                tile.gridPos = new Vector2Int(x, y);
                bool isLight = (x + y) % 2 != 0;
                tile.InitTile(isLight);
                // Isometric depth sort: tiles closer to the camera (lower X, higher Y)
                // get higher sorting order so they render above farther tiles.
                // Pieces add 100 on top of this, so they always sit above their tile.
                SpriteRenderer tileSr = tileObj.GetComponent<SpriteRenderer>();
                if (tileSr != null)
                    tileSr.sortingOrder = -(x - y) * 10;

                tiles[x, y] = tile;
            }
        }
    }

    /// <summary>
    /// Expands the board to <paramref name="newWidth"/> × <paramref name="newHeight"/>.
    /// Only expansion is supported — the new size must be larger than the current.
    /// Existing tiles are preserved; new tiles are spawned for every previously
    /// empty cell. Called automatically after each boss victory.
    /// </summary>
    public void Expand(int newWidth, int newHeight)
    {
        if (newWidth <= width && newHeight <= height)
        {
            Debug.LogWarning($"[Board] Expand ignored: new size ({newWidth}×{newHeight}) " +
                             $"is not larger than current ({width}×{height}).");
            return;
        }

        int oldWidth  = width;
        int oldHeight = height;

        // Commit new dimensions before spawning so GridToWorld is correct.
        width  = newWidth;
        height = newHeight;

        // Build the new tile array and copy existing references across.
        Tile[,] newTiles = new Tile[newWidth, newHeight];
        for (int x = 0; x < oldWidth;  x++)
        for (int y = 0; y < oldHeight; y++)
            newTiles[x, y] = tiles[x, y];

        tiles = newTiles;

        // Spawn tiles for every new cell.
        for (int x = 0; x < newWidth; x++)
        for (int y = 0; y < newHeight; y++)
        {
            if (x < oldWidth && y < oldHeight) continue;  // already exists

            var pos = new Vector2Int(x, y);
            GameObject tileObj = Instantiate(tilePrefab, GridToWorld(pos),
                                             Quaternion.identity, transform);

            Tile tile    = tileObj.GetComponent<Tile>();
            tile.gridPos = pos;
            tile.InitTile((x + y) % 2 != 0);

            SpriteRenderer sr = tileObj.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = -(x - y) * 10;

            tiles[x, y] = tile;
        }

        Debug.Log($"[Board] Expanded from {oldWidth}×{oldHeight} to {newWidth}×{newHeight}.");

        // Apply scale preset for the new grid size before re-centering.
        ApplyScalePreset(Mathf.Max(newWidth, newHeight));

        // Re-center the board on screen for the new dimensions.
        CameraController.Instance?.Recenter();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the best matching <see cref="GridScalePreset"/> for <paramref name="size"/>
    /// and applies its scale and tileSize. Falls back to the largest preset when the
    /// grid exceeds all defined sizes.
    /// </summary>
    private void ApplyScalePreset(int size)
    {
        if (scalePresets == null || scalePresets.Count == 0) return;

        // Sort ascending so we can walk from smallest to largest.
        scalePresets.Sort((a, b) => a.gridSize.CompareTo(b.gridSize));

        // Find the preset whose gridSize matches exactly, or the largest one available.
        GridScalePreset chosen = scalePresets[scalePresets.Count - 1];
        foreach (GridScalePreset preset in scalePresets)
        {
            if (size <= preset.gridSize)
            {
                chosen = preset;
                break;
            }
        }

        tileSize = chosen.tileSize;
        // Do NOT scale the transform — scaling breaks piece billboard rotations.
        // Visual board size is controlled via tileSize + camera orthographic size.
        transform.localScale = Vector3.one;

        CameraController.Instance?.SetOrthoSizes(
            chosen.topDownOrthoSize,
            chosen.isoOrthoSize,
            chosen.iso2OrthoSize);

        Debug.Log($"[Board] Applied preset for {chosen.gridSize}: " +
                  $"tileSize={chosen.tileSize}, ortho=({chosen.topDownOrthoSize}, " +
                  $"{chosen.isoOrthoSize}, {chosen.iso2OrthoSize})");
    }
}
