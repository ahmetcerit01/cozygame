using System;
using CozyLab.Puzzle.Data;
using UnityEngine;

namespace CozyLab.Puzzle.Feedback
{
    /// <summary>
    /// Single entry point for non-visual feedback (audio + haptics). Works with no audio set or empty clips.
    /// Other systems (analytics, music ducking...) can listen to <see cref="Emitted"/>.
    /// </summary>
    public sealed class PuzzleFeedback : MonoBehaviour
    {
        private PuzzleAudioSet _audioSet;
        private AudioSource _source;

        public event Action<PuzzleFeedbackEvent> Emitted;

        public bool HapticsEnabled { get; set; } = true;

        public void Initialize(PuzzleAudioSet audioSet)
        {
            _audioSet = audioSet;
        }

        public void Emit(PuzzleFeedbackEvent feedbackEvent)
        {
            PlaySound(feedbackEvent);
            if (HapticsEnabled) PlayHaptic(feedbackEvent);
            Emitted?.Invoke(feedbackEvent);
        }

        private void PlaySound(PuzzleFeedbackEvent feedbackEvent)
        {
            if (_audioSet == null) return;
            var clip = _audioSet.GetClip(feedbackEvent);
            if (clip == null) return;

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            _source.pitch = 1f + UnityEngine.Random.Range(-_audioSet.pitchJitter, _audioSet.pitchJitter);
            _source.PlayOneShot(clip, _audioSet.volume);
        }

        private static void PlayHaptic(PuzzleFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case PuzzleFeedbackEvent.Pickup:
                case PuzzleFeedbackEvent.Rotate:
                    Haptics.Selection();
                    break;
                case PuzzleFeedbackEvent.ValidPlacement:
                    Haptics.Play(Haptics.Impact.Light);
                    break;
                case PuzzleFeedbackEvent.InvalidPlacement:
                    Haptics.Play(Haptics.Impact.Soft);
                    break;
                case PuzzleFeedbackEvent.Completion:
                    Haptics.Play(Haptics.Notification.Success);
                    break;
            }
        }
    }
}
