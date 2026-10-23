using UnityEngine;

namespace RG.Tweening
{
    /// <summary>
    /// Shake and Punch effects for RTween
    /// </summary>
    public static class RTweenEffects
    {
        // ========== SHAKE EFFECTS ==========

        /// <summary>
        /// Shake a transform's position
        /// </summary>
        public static TweenHandle ShakePosition(Transform target, float strength, float duration, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            Vector3 originalPos = target.position;
            float elapsed = 0f;

            return RTween.Value(0f, duration, duration, (t) =>
            {
                elapsed = t;
                float dampening = fadeOut ? 1f - (elapsed / duration) : 1f;

                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening
                );

                target.position = originalPos + offset;
            }).OnComplete(() => target.position = originalPos);
        }

        /// <summary>
        /// Shake a transform's local position
        /// </summary>
        public static TweenHandle ShakeLocalPosition(Transform target, float strength, float duration, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            Vector3 originalPos = target.localPosition;
            float elapsed = 0f;

            return RTween.Value(0f, duration, duration, (t) =>
            {
                elapsed = t;
                float dampening = fadeOut ? 1f - (elapsed / duration) : 1f;

                Vector3 offset = new Vector3(
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening
                );

                target.localPosition = originalPos + offset;
            }).OnComplete(() => target.localPosition = originalPos);
        }

        /// <summary>
        /// Shake a transform's rotation
        /// </summary>
        public static TweenHandle ShakeRotation(Transform target, float strength, float duration, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            Quaternion originalRot = target.rotation;
            float elapsed = 0f;

            return RTween.Value(0f, duration, duration, (t) =>
            {
                elapsed = t;
                float dampening = fadeOut ? 1f - (elapsed / duration) : 1f;

                Vector3 rotationOffset = new Vector3(
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening
                );

                target.rotation = originalRot * Quaternion.Euler(rotationOffset);
            }).OnComplete(() => target.rotation = originalRot);
        }

        /// <summary>
        /// Shake a transform's scale
        /// </summary>
        public static TweenHandle ShakeScale(Transform target, float strength, float duration, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            Vector3 originalScale = target.localScale;
            float elapsed = 0f;

            return RTween.Value(0f, duration, duration, (t) =>
            {
                elapsed = t;
                float dampening = fadeOut ? 1f - (elapsed / duration) : 1f;

                Vector3 scaleOffset = new Vector3(
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening
                );

                target.localScale = originalScale + scaleOffset;
            }).OnComplete(() => target.localScale = originalScale);
        }

        /// <summary>
        /// Camera shake effect
        /// </summary>
        public static TweenHandle ShakeCamera(Camera camera, float strength, float duration, int vibrato = 10, bool fadeOut = true)
        {
            return ShakeLocalPosition(camera.transform, strength, duration, vibrato, 90f, fadeOut);
        }

        /// <summary>
        /// Shake UI RectTransform anchored position
        /// </summary>
        public static TweenHandle ShakeUIPosition(RectTransform target, float strength, float duration, int vibrato = 10, float randomness = 90f, bool fadeOut = true)
        {
            if (target == null)
                return default;

            Vector2 originalPos = target.anchoredPosition;
            float elapsed = 0f;

            return RTween.Value(0f, duration, duration, (t) =>
            {
                if (target == null)
                    return;

                elapsed = t;
                float dampening = fadeOut ? 1f - (elapsed / duration) : 1f;

                Vector2 offset = new Vector2(
                    Random.Range(-1f, 1f) * strength * dampening,
                    Random.Range(-1f, 1f) * strength * dampening
                );

                target.anchoredPosition = originalPos + offset;
            }).OnComplete(() =>
            {
                if (target != null)
                    target.anchoredPosition = originalPos;
            });
        }

        // ========== PUNCH EFFECTS ==========

        /// <summary>
        /// Punch a transform's position (sharp impact that returns)
        /// </summary>
        public static TweenHandle PunchPosition(Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
        {
            Vector3 originalPos = target.position;

            return RTween.Value(0f, 1f, duration, (t) =>
            {
                float strength = EvaluatePunchCurve(t, vibrato, elasticity);
                target.position = originalPos + punch * strength;
            }).SetEase(EaseType.Linear)
              .OnComplete(() => target.position = originalPos);
        }

        /// <summary>
        /// Punch a transform's local position
        /// </summary>
        public static TweenHandle PunchLocalPosition(Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
        {
            Vector3 originalPos = target.localPosition;

            return RTween.Value(0f, 1f, duration, (t) =>
            {
                float strength = EvaluatePunchCurve(t, vibrato, elasticity);
                target.localPosition = originalPos + punch * strength;
            }).SetEase(EaseType.Linear)
              .OnComplete(() => target.localPosition = originalPos);
        }

        /// <summary>
        /// Punch a transform's rotation
        /// </summary>
        public static TweenHandle PunchRotation(Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
        {
            Quaternion originalRot = target.rotation;

            return RTween.Value(0f, 1f, duration, (t) =>
            {
                float strength = EvaluatePunchCurve(t, vibrato, elasticity);
                target.rotation = originalRot * Quaternion.Euler(punch * strength);
            }).SetEase(EaseType.Linear)
              .OnComplete(() => target.rotation = originalRot);
        }

        /// <summary>
        /// Punch a transform's scale
        /// </summary>
        public static TweenHandle PunchScale(Transform target, Vector3 punch, float duration, int vibrato = 10, float elasticity = 1f)
        {
            Vector3 originalScale = target.localScale;

            return RTween.Value(0f, 1f, duration, (t) =>
            {
                float strength = EvaluatePunchCurve(t, vibrato, elasticity);
                target.localScale = originalScale + punch * strength;
            }).SetEase(EaseType.Linear)
              .OnComplete(() => target.localScale = originalScale);
        }

        /// <summary>
        /// Punch UI RectTransform anchored position
        /// </summary>
        public static TweenHandle PunchUIPosition(RectTransform target, Vector2 punch, float duration, int vibrato = 10, float elasticity = 1f)
        {
            Vector2 originalPos = target.anchoredPosition;

            return RTween.Value(0f, 1f, duration, (t) =>
            {
                float strength = EvaluatePunchCurve(t, vibrato, elasticity);
                target.anchoredPosition = originalPos + punch * strength;
            }).SetEase(EaseType.Linear)
              .OnComplete(() => target.anchoredPosition = originalPos);
        }

        // ========== HELPER METHODS ==========

        private static float EvaluatePunchCurve(float t, int vibrato, float elasticity)
        {
            // Create a dampened oscillation
            float frequency = vibrato * Mathf.PI * 2f;
            float decay = Mathf.Pow(1f - t, elasticity);
            return Mathf.Sin(t * frequency) * decay;
        }

        // ========== CONVENIENCE METHODS ==========

        /// <summary>
        /// Quick camera shake for impacts
        /// </summary>
        public static TweenHandle QuickCameraShake(Camera camera, float intensity = 0.3f)
        {
            return ShakeCamera(camera, intensity, 0.3f, 20, true);
        }

        /// <summary>
        /// Quick punch scale for button press effect
        /// </summary>
        public static TweenHandle QuickPunchScale(Transform target, float intensity = 0.2f)
        {
            return PunchScale(target, Vector3.one * intensity, 0.3f, 10, 1f);
        }

        /// <summary>
        /// Quick shake for UI element
        /// </summary>
        public static TweenHandle QuickUIShake(RectTransform target, float intensity = 10f)
        {
            return ShakeUIPosition(target, intensity, 0.3f, 15, 90f, true);
        }

        /// <summary>
        /// Impact shake (short, sharp shake)
        /// </summary>
        public static TweenHandle ImpactShake(Transform target, float strength = 0.5f)
        {
            return ShakeLocalPosition(target, strength, 0.2f, 20, 90f, true);
        }

        /// <summary>
        /// Continuous shake (for earthquakes, explosions, etc.)
        /// </summary>
        public static TweenHandle ContinuousShake(Transform target, float strength, float duration)
        {
            return ShakeLocalPosition(target, strength, duration, 30, 90f, false);
        }

        /// <summary>
        /// Hit punch effect (for taking damage)
        /// </summary>
        public static TweenHandle HitPunch(Transform target, Vector3 direction, float force = 0.5f)
        {
            return PunchLocalPosition(target, direction * force, 0.4f, 8, 0.8f);
        }

        /// <summary>
        /// Pop effect (scale punch)
        /// </summary>
        public static TweenHandle PopEffect(Transform target, float intensity = 0.3f)
        {
            Vector3 originalScale = target.localScale;
            target.localScale = originalScale * (1f - intensity);

            return RTween.Scale(target, originalScale, 0.3f)
                .SetEase(EaseType.OutBack);
        }

        /// <summary>
        /// Wobble effect (rotation shake)
        /// </summary>
        public static TweenHandle WobbleEffect(Transform target, float strength = 15f, float duration = 0.5f)
        {
            return ShakeRotation(target, strength, duration, 8, 90f, true);
        }
    }
}
