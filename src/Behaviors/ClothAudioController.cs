using System;
using System.Collections.Generic;
using UnityEngine;
using ClothTheWarriorCompanion.Config;

namespace ClothTheWarriorCompanion.Behaviors;

/// <summary>
/// Controlador de efectos de voz y sonido de Cloth.
/// Localiza y reproduce los clips de audio originales de Cloth presentes en el juego
/// (ataques, saltos, grito de batalla, daño, derrota y victoria).
/// </summary>
public sealed class ClothAudioController : MonoBehaviour {
    private AudioSource _audioSource = null!;

    private readonly List<AudioClip> _attackClips = new();
    private readonly List<AudioClip> _jumpClips = new();
    private readonly List<AudioClip> _battleCryClips = new();
    private readonly List<AudioClip> _deathClips = new();
    private readonly List<AudioClip> _hurtClips = new();
    private readonly List<AudioClip> _burstClips = new();
    private readonly List<AudioClip> _happyClips = new();

    private bool _initialized = false;

    private static ClothSettings Settings => ClothSettings.Current;

    private void Awake() {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.15f; // Sonido ligeramente espacializado, mayormente audible
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 5f;
        _audioSource.maxDistance = 45f;
    }

    private void Start() {
        InitializeClips();
    }

    /// <summary>
    /// Escanea los assets precargados de Hollow Knight para descubrir los clips originales de voz de Cloth.
    /// </summary>
    public void InitializeClips() {
        if (_initialized) return;

        var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        for (int i = 0; i < allClips.Length; i++) {
            var clip = allClips[i];
            if (clip == null) continue;

            string name = clip.name;
            if (string.IsNullOrEmpty(name)) continue;

            if (name.StartsWith("HK_Cloth_Combat_attack_", StringComparison.OrdinalIgnoreCase)) {
                _attackClips.Add(clip);
            } else if (name.StartsWith("HK_Cloth_Combat_jump_", StringComparison.OrdinalIgnoreCase)) {
                _jumpClips.Add(clip);
            } else if (name.Equals("HK_Cloth_Battle_Cry", StringComparison.OrdinalIgnoreCase)) {
                _battleCryClips.Add(clip);
            } else if (name.Equals("HK_Cloth_Death01", StringComparison.OrdinalIgnoreCase)) {
                _deathClips.Add(clip);
            } else if (name.StartsWith("HK_Cloth_Sad", StringComparison.OrdinalIgnoreCase)) {
                _hurtClips.Add(clip);
            } else if (name.StartsWith("HK_Cloth_Burst", StringComparison.OrdinalIgnoreCase)) {
                _burstClips.Add(clip);
            } else if (name.StartsWith("HK_Cloth_Happy", StringComparison.OrdinalIgnoreCase)) {
                _happyClips.Add(clip);
            }
        }

        // Si los clips de daño no están disponibles en la escena actual, los saltos sirven como quejidos
        if (_hurtClips.Count == 0 && _jumpClips.Count > 0) {
            _hurtClips.AddRange(_jumpClips);
        }

        ClothTheWarriorCompanionMod.Instance?.LogDebug(
            $"ClothAudioController inicializado: {_attackClips.Count} ataques, {_jumpClips.Count} saltos, " +
            $"{_battleCryClips.Count} gritos, {_deathClips.Count} muerte, {_hurtClips.Count} daño."
        );

        _initialized = true;
    }

    private void PlayRandomClip(List<AudioClip> clips, float pitchMin = 0.95f, float pitchMax = 1.05f) {
        if (!Settings.ClothSoundsEnabled || clips.Count == 0 || _audioSource == null)
            return;

        int index = UnityEngine.Random.Range(0, clips.Count);
        AudioClip clip = clips[index];
        if (clip == null) return;

        _audioSource.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
        _audioSource.PlayOneShot(clip, Mathf.Clamp01(Settings.ClothVoiceVolume));
    }

    public void PlayAttack() {
        PlayRandomClip(_attackClips, 0.96f, 1.04f);
    }

    public void PlayJump() {
        PlayRandomClip(_jumpClips, 0.94f, 1.06f);
    }

    public void PlayBattleCry() {
        if (_battleCryClips.Count > 0) {
            PlayRandomClip(_battleCryClips, 0.98f, 1.02f);
        } else {
            PlayAttack();
        }
    }

    public void PlayHurt() {
        PlayRandomClip(_hurtClips, 1.05f, 1.15f);
    }

    public void PlayDeath() {
        if (_deathClips.Count > 0) {
            PlayRandomClip(_deathClips, 0.98f, 1.02f);
        } else {
            PlayHurt();
        }
    }

    public void PlayBurst() {
        if (_burstClips.Count > 0) {
            PlayRandomClip(_burstClips, 0.95f, 1.05f);
        }
    }

    public void PlayHappy() {
        if (_happyClips.Count > 0) {
            PlayRandomClip(_happyClips, 0.97f, 1.03f);
        }
    }
}

