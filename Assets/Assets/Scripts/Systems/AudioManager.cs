using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton audio manager. Persists across scenes via DontDestroyOnLoad.
/// Playlists (regular, boss, market) play sequentially in random order without
/// immediate repeats. Volumes are persisted via PlayerPrefs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── Context enum ──────────────────────────────────────────────────────────

    public enum MusicContext { Regular, Boss, Boss2, Market }

    // ── PlayerPrefs keys ──────────────────────────────────────────────────────

    public const string KeyMusic = "VolMusic";
    public const string KeySfx   = "VolSfx";

    // ── Music playlists ───────────────────────────────────────────────────────

    [Header("Music — Regular fights (playlist, shuffled)")]
    public List<AudioClip> regularPlaylist = new List<AudioClip>();

    [Header("Music — Boss fights (playlist, shuffled)")]
    public List<AudioClip> bossPlaylist = new List<AudioClip>();

    [Header("Music — Boss 2 (Bolbi Phisher) fights (playlist, shuffled)")]
    public List<AudioClip> boss2Playlist = new List<AudioClip>();

    [Header("Music — Market / Mainframe (single track or playlist)")]
    public List<AudioClip> marketPlaylist = new List<AudioClip>();

    [Range(0f, 1f)] public float musicVolume = 0.45f;

    // ── SFX clips ─────────────────────────────────────────────────────────────

    [Header("SFX — Piece movement")]
    public AudioClip moveClip;
    public AudioClip captureClip;

    [Header("SFX — Upgrade")]
    public AudioClip upgradeClip;

    [Header("SFX — Armor & stun")]
    public AudioClip armorSpawnClip;
    public AudioClip armorHitClip;
    public AudioClip stunClip;

    [Header("SFX — Sudden Death")]
    public AudioClip suddenDeathAnnouncementClip;
    public AudioClip redTileHighlightClip;
    public AudioClip tileCollapseClip;

    [Header("SFX — UI")]
    public AudioClip uiClickClip;

    [Header("SFX — Victory / Defeat")]
    public AudioClip victoryClip;
    public AudioClip defeatClip;

    [Header("SFX — Volumes")]
    [Range(0f, 1f)] public float sfxVolume = 0.85f;

    // ── Internal ──────────────────────────────────────────────────────────────

    private AudioSource         _musicSource;
    private AudioSource         _sfxSource;

    private MusicContext        _currentContext = MusicContext.Regular;
    private List<AudioClip>     _currentPlaylist;
    private readonly List<int>  _shuffledIndices = new List<int>();
    private int                 _playlistCursor;
    private int                 _lastPlayedIndex = -1;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicSource             = gameObject.AddComponent<AudioSource>();
        _musicSource.loop        = false;
        _musicSource.playOnAwake = false;

        _sfxSource               = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop          = false;
        _sfxSource.playOnAwake   = false;

        musicVolume = PlayerPrefs.GetFloat(KeyMusic, musicVolume);
        sfxVolume   = PlayerPrefs.GetFloat(KeySfx,   sfxVolume);

        _musicSource.volume = musicVolume;
        _sfxSource.volume   = sfxVolume;

        SwitchContext(MusicContext.Regular);
    }

    private void Update()
    {
        // Advance playlist when the current track finishes.
        if (_musicSource != null && !_musicSource.isPlaying && _currentPlaylist is { Count: > 0 })
            PlayNextInPlaylist();
    }

    // ── Public API — music ────────────────────────────────────────────────────

    /// <summary>
    /// Switches music context (Regular / Boss / Market) and starts the new playlist.
    /// If the requested context is already active and music is currently playing, does nothing.
    /// </summary>
    public static void SwitchContext(MusicContext context)
    {
        if (Instance == null) return;

        // Do not restart music if we are already in the requested context and a track is playing.
        if (Instance._currentContext == context && Instance._musicSource.isPlaying)
            return;

        Instance._currentContext = context;
        Instance._currentPlaylist = context switch
        {
            MusicContext.Boss   => Instance.bossPlaylist,
            MusicContext.Boss2  => Instance.boss2Playlist,
            MusicContext.Market => Instance.marketPlaylist,
            _                   => Instance.regularPlaylist,
        };
        Instance.ShufflePlaylist();
        Instance.PlayNextInPlaylist();
    }

    /// <summary>Stops music playback.</summary>
    public static void StopMusic() => Instance?._musicSource.Stop();

    /// <summary>Adjusts music volume (0–1) at runtime and persists the value.</summary>
    public static void SetMusicVolume(float volume)
    {
        if (Instance == null) return;
        volume = Mathf.Clamp01(volume);
        Instance.musicVolume         = volume;
        Instance._musicSource.volume = volume;
        PlayerPrefs.SetFloat(KeyMusic, volume);
    }

    /// <summary>Adjusts SFX volume (0–1) at runtime and persists the value.</summary>
    public static void SetSFXVolume(float volume)
    {
        if (Instance == null) return;
        volume = Mathf.Clamp01(volume);
        Instance.sfxVolume         = volume;
        Instance._sfxSource.volume = volume;
        PlayerPrefs.SetFloat(KeySfx, volume);
    }

    /// <summary>Returns the current music volume (0–1).</summary>
    public static float GetMusicVolume() => Instance != null ? Instance.musicVolume : 1f;

    /// <summary>Returns the current SFX volume (0–1).</summary>
    public static float GetSFXVolume() => Instance != null ? Instance.sfxVolume : 1f;

    // ── Public API — SFX ─────────────────────────────────────────────────────

    /// <summary>Plays a piece-move sound.</summary>
    public static void PlayMove()    => Play(Instance?.moveClip);

    /// <summary>Plays a capture sound.</summary>
    public static void PlayCapture() => Play(Instance?.captureClip);

    /// <summary>Plays the piece-upgraded sting.</summary>
    public static void PlayUpgrade() => Play(Instance?.upgradeClip);

    /// <summary>Plays the armor-spawn sound.</summary>
    public static void PlayArmorSpawn() => Play(Instance?.armorSpawnClip);

    /// <summary>Plays the armor-hit (absorption) sound.</summary>
    public static void PlayArmorHit() => Play(Instance?.armorHitClip);

    /// <summary>Plays the stun sound.</summary>
    public static void PlayStun() => Play(Instance?.stunClip);

    /// <summary>Plays the Sudden Death announcement sting.</summary>
    public static void PlaySuddenDeathAnnouncement() => Play(Instance?.suddenDeathAnnouncementClip);

    /// <summary>Plays the red-tile danger-highlight sound.</summary>
    public static void PlayRedTileHighlight() => Play(Instance?.redTileHighlightClip);

    /// <summary>Plays the tile-collapse sound.</summary>
    public static void PlayTileCollapse() => Play(Instance?.tileCollapseClip);

    /// <summary>Plays a UI button-click sound.</summary>
    public static void PlayUIClick() => Play(Instance?.uiClickClip);

    /// <summary>Plays the victory fanfare.</summary>
    public static void PlayVictory() => Play(Instance?.victoryClip);

    /// <summary>Plays the defeat sting.</summary>
    public static void PlayDefeat() => Play(Instance?.defeatClip);

    // ── Playlist internals ────────────────────────────────────────────────────

    private void ShufflePlaylist()
    {
        _shuffledIndices.Clear();
        if (_currentPlaylist == null) return;

        for (int i = 0; i < _currentPlaylist.Count; i++)
            _shuffledIndices.Add(i);

        // Fisher-Yates shuffle.
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }

        // Avoid replaying the last track immediately after a reshuffle.
        if (_shuffledIndices.Count > 1 && _shuffledIndices[0] == _lastPlayedIndex)
        {
            (_shuffledIndices[0], _shuffledIndices[1]) = (_shuffledIndices[1], _shuffledIndices[0]);
        }

        _playlistCursor = 0;
    }

    private void PlayNextInPlaylist()
    {
        if (_currentPlaylist == null || _currentPlaylist.Count == 0) return;

        if (_playlistCursor >= _shuffledIndices.Count)
            ShufflePlaylist();

        int index      = _shuffledIndices[_playlistCursor++];
        AudioClip clip = _currentPlaylist[index];
        if (clip == null) return;

        _lastPlayedIndex     = index;
        _musicSource.clip    = clip;
        _musicSource.volume  = musicVolume;
        _musicSource.Play();
    }

    // ── SFX internal ─────────────────────────────────────────────────────────

    private static void Play(AudioClip clip)
    {
        if (clip == null || Instance == null) return;
        Instance._sfxSource.PlayOneShot(clip, Instance._sfxSource.volume);
    }
}
