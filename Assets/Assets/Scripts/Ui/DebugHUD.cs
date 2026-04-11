using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug controls overlaid on the game HUD. Works in both Editor and builds.
/// </summary>
public class DebugHUD : MonoBehaviour
{
    public GameController gameController;
    public Button         autoWinButton;

    [Header("Boss Jump Buttons")]
    public Button boss1Button;
    public Button boss2Button;

    void Awake()
    {
        if (autoWinButton != null)
            autoWinButton.onClick.AddListener(OnAutoWin);

        if (boss1Button != null)
            boss1Button.onClick.AddListener(() => gameController?.LoadBoss(1));

        if (boss2Button != null)
            boss2Button.onClick.AddListener(() => gameController?.LoadBoss(2));
    }

    private void OnAutoWin() => gameController?.AutoWin();
}
