using CozyLab.Puzzle.Feedback;
using UnityEngine;

namespace CozyLab.Puzzle.Data
{
    /// <summary>
    /// Optional sound set for a theme. Every clip may be left empty; missing clips are simply skipped.
    /// </summary>
    [CreateAssetMenu(menuName = "CozyLab/Puzzle/Audio Set", fileName = "Audio_New")]
    public sealed class PuzzleAudioSet : ScriptableObject
    {
        public AudioClip pickup;
        public AudioClip rotate;
        public AudioClip validPlacement;
        public AudioClip invalidPlacement;
        public AudioClip completion;

        [Range(0f, 1f)] public float volume = 0.8f;
        [Tooltip("Random pitch variation (+/-) so repeated sounds don't feel mechanical.")]
        [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

        public AudioClip GetClip(PuzzleFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case PuzzleFeedbackEvent.Pickup: return pickup;
                case PuzzleFeedbackEvent.Rotate: return rotate;
                case PuzzleFeedbackEvent.ValidPlacement: return validPlacement;
                case PuzzleFeedbackEvent.InvalidPlacement: return invalidPlacement;
                case PuzzleFeedbackEvent.Completion: return completion;
                default: return null;
            }
        }
    }
}
