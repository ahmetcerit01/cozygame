using System;
using System.Collections;
using UnityEngine;

namespace CozyLab.Puzzle.Presentation
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Gentle overshoot (~4%) for a subtle elastic settle.</summary>
        public static float OutBackSoft(float t)
        {
            const float c1 = 0.9f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Decaying oscillation 0 -> +1 -> -x -> 0. Good for jelly squash.</summary>
        public static float Wobble(float t) => Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * (1f - t);
    }

    /// <summary>
    /// Minimal coroutine tween helper (no external tween library). Uses unscaled time.
    /// </summary>
    public static class Tween
    {
        public static Coroutine Run(MonoBehaviour host, float duration, Func<float, float> ease, Action<float> step,
            Action onComplete = null, float delay = 0f)
        {
            if (host == null || !host.isActiveAndEnabled)
            {
                step?.Invoke(1f);
                onComplete?.Invoke();
                return null;
            }
            return host.StartCoroutine(Routine(duration, ease ?? Ease.Linear, step, onComplete, delay));
        }

        public static void Stop(MonoBehaviour host, ref Coroutine routine)
        {
            if (routine != null && host != null) host.StopCoroutine(routine);
            routine = null;
        }

        private static IEnumerator Routine(float duration, Func<float, float> ease, Action<float> step, Action onComplete,
            float delay)
        {
            if (delay > 0f)
            {
                float waited = 0f;
                while (waited < delay)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                step?.Invoke(ease(Mathf.Clamp01(elapsed / duration)));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            step?.Invoke(ease(1f));
            onComplete?.Invoke();
        }
    }
}
