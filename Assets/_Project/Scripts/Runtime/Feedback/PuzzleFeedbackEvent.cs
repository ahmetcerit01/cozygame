namespace CozyLab.Puzzle.Feedback
{
    /// <summary>Moments in the puzzle that deserve sound and haptics. Theme-agnostic.</summary>
    public enum PuzzleFeedbackEvent
    {
        Pickup,
        Rotate,
        ValidPlacement,
        InvalidPlacement,
        Completion,
    }
}
