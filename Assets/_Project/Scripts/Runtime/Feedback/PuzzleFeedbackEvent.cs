namespace CozyLab.Puzzle.Feedback
{
    /// <summary>Moments that deserve sound and haptics (puzzle and meta screens). Theme-agnostic.</summary>
    public enum PuzzleFeedbackEvent
    {
        Pickup,
        Rotate,
        ValidPlacement,
        InvalidPlacement,
        Completion,

        // Meta / navigation (Home, Experiments, Lab)
        UiSelect,
        ResearchReward,
        EquipmentTap,
        EquipmentUpgrade,
        EquipmentUnlock,
    }
}
