using UnityEngine;

namespace RG.Tweening
{
    internal static class EaseFunctions
    {
        public static float Ease(float t, EaseType ease)
        {
            const float c1 = 1.70158f;
            const float c2 = c1 * 1.525f;
            const float c3 = c1 + 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            const float c5 = (2f * Mathf.PI) / 4.5f;

            switch (ease)
            {
                case EaseType.Linear: return t;

                case EaseType.InQuad: return t * t;
                case EaseType.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseType.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                case EaseType.InCubic: return t * t * t;
                case EaseType.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case EaseType.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

                case EaseType.InQuart: return t * t * t * t;
                case EaseType.OutQuart: return 1f - Mathf.Pow(1f - t, 4f);
                case EaseType.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;

                case EaseType.InQuint: return t * t * t * t * t;
                case EaseType.OutQuint: return 1f - Mathf.Pow(1f - t, 5f);
                case EaseType.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 5f) / 2f;

                case EaseType.InSine: return 1f - Mathf.Cos((t * Mathf.PI) / 2f);
                case EaseType.OutSine: return Mathf.Sin((t * Mathf.PI) / 2f);
                case EaseType.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

                case EaseType.InExpo: return t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
                case EaseType.OutExpo: return t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
                case EaseType.InOutExpo:
                    return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f ?
                        Mathf.Pow(2f, 20f * t - 10f) / 2f :
                        (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

                case EaseType.InCirc: return 1f - Mathf.Sqrt(1f - Mathf.Pow(t, 2f));
                case EaseType.OutCirc: return Mathf.Sqrt(1f - Mathf.Pow(t - 1f, 2f));
                case EaseType.InOutCirc:
                    return t < 0.5f ?
                        (1f - Mathf.Sqrt(1f - Mathf.Pow(2f * t, 2f))) / 2f :
                        (Mathf.Sqrt(1f - Mathf.Pow(-2f * t + 2f, 2f)) + 1f) / 2f;

                case EaseType.InBack: return c3 * t * t * t - c1 * t * t;
                case EaseType.OutBack: return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                case EaseType.InOutBack:
                    return t < 0.5f ?
                        (Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2)) / 2f :
                        (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;

                case EaseType.InElastic:
                    return t == 0f ? 0f : t == 1f ? 1f :
                        -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * c4);
                case EaseType.OutElastic:
                    return t == 0f ? 0f : t == 1f ? 1f :
                        Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
                case EaseType.InOutElastic:
                    return t == 0f ? 0f : t == 1f ? 1f : t < 0.5f ?
                        -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f :
                        (Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * c5)) / 2f + 1f;

                case EaseType.InBounce: return 1f - BounceOut(1f - t);
                case EaseType.OutBounce: return BounceOut(t);
                case EaseType.InOutBounce:
                    return t < 0.5f ?
                        (1f - BounceOut(1f - 2f * t)) / 2f :
                        (1f + BounceOut(2f * t - 1f)) / 2f;

                default: return t;
            }
        }

        private static float BounceOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }
            else if (t < 2f / d1)
            {
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            }
            else if (t < 2.5f / d1)
            {
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            }
            else
            {
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
            }
        }
    }
}