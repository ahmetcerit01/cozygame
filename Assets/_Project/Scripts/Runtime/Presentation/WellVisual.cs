using CozyLab.Puzzle.Data;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation
{
    /// <summary>
    /// Draws a warm translucent culture recess, with an inner shadow facing the authored
    /// sunlight and a fine lower bevel. The dish texture remains visible through the floor.
    /// </summary>
    public static class WellVisual
    {
        public static void Draw(string name, Transform parent, Vector2 center, float size, PuzzleTheme theme)
        {
            // A translucent recessed surface lets the photographed dish remain visible.
            // Its upper/left inner shadow and lower bevel share the plate's light direction.
            UIFactory.CreateSpriteImage(name, parent, ProceduralSprites.InsetWell,
                new Vector2(size, size), Color.white, center);
        }
    }
}
