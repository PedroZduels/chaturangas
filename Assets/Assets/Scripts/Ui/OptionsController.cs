using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the options panel: volume sliders (music and SFX) and resolution dropdown.
/// Resolution choice is saved to PlayerPrefs and applied immediately via Screen.SetResolution.
/// Works in both the Main Menu and the in-game pause panel.
/// </summary>
public class OptionsController : MonoBehaviour
{
    private const string ResolutionPrefKey = "SelectedResolution";

    [Header("Audio Mixer (optional)")]
    [Tooltip("Assign your project's AudioMixer to drive exposed parameters directly.")]
    public AudioMixer audioMixer;

    [Header("Sliders")]
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Resolution")]
    [Tooltip("Assign the ResolutionDropdown TMP_Dropdown here.")]
    public TMP_Dropdown resolutionDropdown;

    // ── Resolution presets ────────────────────────────────────────────────────

    private static readonly List<(int width, int height, string label)> Resolutions = new List<(int, int, string)>
    {
        (1920, 1080, "1920 × 1080"),
        (3840, 2160, "4K  (3840 × 2160)")
    };

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        BuildResolutionDropdown();
        SyncSliders();
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void RegisterListeners()
    {
        if (musicSlider       != null) musicSlider      .onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider         != null) sfxSlider        .onValueChanged.AddListener(OnSfxChanged);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void UnregisterListeners()
    {
        if (musicSlider       != null) musicSlider      .onValueChanged.RemoveListener(OnMusicChanged);
        if (sfxSlider         != null) sfxSlider        .onValueChanged.RemoveListener(OnSfxChanged);
        if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
    }

    // ── Resolution ────────────────────────────────────────────────────────────

    /// <summary>Populates the dropdown options and restores the saved choice.</summary>
    private void BuildResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        resolutionDropdown.ClearOptions();

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var res in Resolutions)
            options.Add(new TMP_Dropdown.OptionData(res.label));

        resolutionDropdown.AddOptions(options);

        int saved = PlayerPrefs.GetInt(ResolutionPrefKey, 0);
        saved = Mathf.Clamp(saved, 0, Resolutions.Count - 1);
        resolutionDropdown.SetValueWithoutNotify(saved);
    }

    /// <summary>Applies the chosen resolution and persists the index.</summary>
    private void OnResolutionChanged(int index)
    {
        index = Mathf.Clamp(index, 0, Resolutions.Count - 1);
        var res = Resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt(ResolutionPrefKey, index);
        PlayerPrefs.Save();
    }

    // ── Audio handlers ────────────────────────────────────────────────────────

    private void OnMusicChanged(float value)
    {
        AudioManager.SetMusicVolume(value);
        ApplyToMixer("VolMusic", value);
    }

    private void OnSfxChanged(float value)
    {
        AudioManager.SetSFXVolume(value);
        ApplyToMixer("VolSfx", value);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Snaps sliders to the values currently stored in AudioManager.</summary>
    private void SyncSliders()
    {
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioManager.GetMusicVolume());
        if (sfxSlider   != null) sfxSlider  .SetValueWithoutNotify(AudioManager.GetSFXVolume());
    }

    /// <summary>Converts a linear 0–1 value to dB and pushes it to the AudioMixer if assigned.</summary>
    private void ApplyToMixer(string parameter, float linearValue)
    {
        if (audioMixer == null) return;
        float db = linearValue > 0.0001f ? Mathf.Log10(linearValue) * 20f : -80f;
        audioMixer.SetFloat(parameter, db);
    }
}
