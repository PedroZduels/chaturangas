using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Overlay panel shown when the player activates the Ctrl+Z floppy.
/// Displays a confirmation prompt before reverting the board.
/// Wire Confirm and Cancel buttons in the Inspector.
/// </summary>
public class CtrlZConfirmPanel : MonoBehaviour
{
    [Tooltip("The button that commits the revert.")]
    public Button confirmButton;

    [Tooltip("The button that cancels without using the floppy.")]
    public Button cancelButton;

    private System.Action _onConfirm;
    private System.Action _onCancel;

    private void Awake()
    {
        confirmButton?.onClick.AddListener(OnConfirm);
        cancelButton?.onClick.AddListener(OnCancel);
        gameObject.SetActive(false);
    }

    /// <summary>Shows the panel and registers the confirm/cancel callbacks.</summary>
    public void Show(System.Action onConfirm, System.Action onCancel)
    {
        _onConfirm = onConfirm;
        _onCancel  = onCancel;
        gameObject.SetActive(true);
    }

    private void OnConfirm()
    {
        gameObject.SetActive(false);
        _onConfirm?.Invoke();
    }

    private void OnCancel()
    {
        gameObject.SetActive(false);
        _onCancel?.Invoke();
    }
}
