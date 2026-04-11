using UnityEngine;

/// <summary>
/// Exposes four directional nudge methods wired to HUD arrow buttons.
/// Each press shifts the board position for the active camera view by <see cref="nudgeStep"/>.
/// </summary>
public class BoardNudgeUI : MonoBehaviour
{
    private const float DefaultNudgeStep = 0.05f;

    [Tooltip("World-unit distance moved per button press.")]
    public float nudgeStep = DefaultNudgeStep;

    private CameraController cameraController;

    private void Awake()
    {
        cameraController = CameraController.Instance != null
            ? CameraController.Instance
            : FindFirstObjectByType<CameraController>();
    }

    /// <summary>Nudges the board up (+Y).</summary>
    public void NudgeUp()    => Nudge(Vector2.up);

    /// <summary>Nudges the board down (−Y).</summary>
    public void NudgeDown()  => Nudge(Vector2.down);

    /// <summary>Nudges the board left (−X).</summary>
    public void NudgeLeft()  => Nudge(Vector2.left);

    /// <summary>Nudges the board right (+X).</summary>
    public void NudgeRight() => Nudge(Vector2.right);

    private void Nudge(Vector2 direction)
    {
        if (cameraController == null) return;
        cameraController.ActiveBoardPosition += direction * nudgeStep;
    }
}
