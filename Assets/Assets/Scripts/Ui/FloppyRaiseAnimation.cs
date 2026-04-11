using System.Collections;
using UnityEngine;

/// <summary>
/// Smoothly raises and lowers a floppy disk UI card on hover.
/// Attach to the floppy Image/Button GameObject.
/// </summary>
public class FloppyRaiseAnimation : MonoBehaviour
{
    [Header("Anchor Y positions")]
    public float RestingY = -340f;
    public float RaisedY  = -70f;

    [Header("Timing")]
    public float Duration = 0.22f;
    public AnimationCurve Curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private RectTransform _rect;
    private Coroutine     _coroutine;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        // Snap to resting position on start
        var pos = _rect.anchoredPosition;
        pos.y = RestingY;
        _rect.anchoredPosition = pos;
    }

    /// <summary>Animates the floppy upward into view.</summary>
    public void Raise() => Play(RaisedY);

    /// <summary>Animates the floppy back to its resting (mostly-hidden) position.</summary>
    public void Lower() => Play(RestingY);

    private void Play(float targetY)
    {
        if (!gameObject.activeInHierarchy)
        {
            // Snap without a coroutine — the card is hidden, no animation needed.
            var pos = _rect.anchoredPosition;
            pos.y = targetY;
            _rect.anchoredPosition = pos;
            return;
        }

        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(Animate(targetY));
    }

    private IEnumerator Animate(float targetY)
    {
        float startY  = _rect.anchoredPosition.y;
        float elapsed = 0f;

        while (elapsed < Duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Curve.Evaluate(Mathf.Clamp01(elapsed / Duration));
            var pos = _rect.anchoredPosition;
            pos.y = Mathf.Lerp(startY, targetY, t);
            _rect.anchoredPosition = pos;
            yield return null;
        }

        var final = _rect.anchoredPosition;
        final.y = targetY;
        _rect.anchoredPosition = final;
    }
}
