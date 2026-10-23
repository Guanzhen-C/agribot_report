using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RG.Tweening
{
    /// <summary>
    /// Main tween animation system for RGames. Provides high-performance tweening for transforms, UI, visuals, audio, and more.
    /// </summary>
    /// <remarks>
    /// RTween offers a complete tweening solution with zero garbage allocation during runtime.
    /// Features include 30+ easing functions, sequences, repeat modes, and special effects.
    /// </remarks>
    /// <example>
    /// Basic usage:
    /// <code>
    /// RTween.Move(transform, Vector3.up * 5, 1f)
    ///     .SetEase(EaseType.OutQuad)
    ///     .OnComplete(() => Debug.Log("Done!"));
    /// </code>
    /// </example>
    public static class RTween
    {
        // Transform tweens

        /// <summary>
        /// Animates a transform's world position to the target value.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target world position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween (ease, delay, callbacks, etc.)</returns>
        /// <example>
        /// <code>
        /// RTween.Move(transform, new Vector3(5, 0, 0), 1f)
        ///     .SetEase(EaseType.OutBack);
        /// </code>
        /// </example>
        public static TweenHandle Move(Transform target, Vector3 endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.Move);
        }

        /// <summary>
        /// Animates only the X component of a transform's world position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target X position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle MoveX(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(endValue, 0, 0), duration, TweenType.MoveX);
        }

        /// <summary>
        /// Animates only the Y component of a transform's world position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target Y position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle MoveY(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, endValue, 0), duration, TweenType.MoveY);
        }

        /// <summary>
        /// Animates only the Z component of a transform's world position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target Z position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle MoveZ(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, 0, endValue), duration, TweenType.MoveZ);
        }

        /// <summary>
        /// Animates a transform's local position to the target value.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle LocalMove(Transform target, Vector3 endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.LocalMove);
        }

        /// <summary>
        /// Animates only the X component of a transform's local position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local X position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle LocalMoveX(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(endValue, 0, 0), duration, TweenType.LocalMoveX);
        }

        /// <summary>
        /// Animates only the Y component of a transform's local position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local Y position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle LocalMoveY(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, endValue, 0), duration, TweenType.LocalMoveY);
        }

        /// <summary>
        /// Animates only the Z component of a transform's local position.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local Z position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle LocalMoveZ(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, 0, endValue), duration, TweenType.LocalMoveZ);
        }

        /// <summary>
        /// Animates a transform's world rotation to the target value using spherical interpolation.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target world rotation as a Quaternion.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// RTween.Rotation(transform, Quaternion.Euler(0, 180, 0), 1f);
        /// </code>
        /// </example>
        public static TweenHandle Rotation(Transform target, Quaternion endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.Rotation);
        }

        /// <summary>
        /// Animates a transform's local rotation to the target value using spherical interpolation.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local rotation as a Quaternion.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle LocalRotation(Transform target, Quaternion endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.LocalRotation);
        }

        /// <summary>
        /// Animates a transform's local scale to the target value.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target local scale.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// RTween.Scale(transform, Vector3.one * 2, 0.5f)
        ///     .SetEase(EaseType.OutBack);
        /// </code>
        /// </example>
        public static TweenHandle Scale(Transform target, Vector3 endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.Scale);
        }

        /// <summary>
        /// Animates only the X component of a transform's local scale.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target X scale.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle ScaleX(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(endValue, 0, 0), duration, TweenType.ScaleX);
        }

        /// <summary>
        /// Animates only the Y component of a transform's local scale.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target Y scale.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle ScaleY(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, endValue, 0), duration, TweenType.ScaleY);
        }

        /// <summary>
        /// Animates only the Z component of a transform's local scale.
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target Z scale.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle ScaleZ(Transform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector3(0, 0, endValue), duration, TweenType.ScaleZ);
        }

        /// <summary>
        /// Animates a transform's position in an arc motion (jumping effect).
        /// </summary>
        /// <param name="target">The transform to animate. Cannot be null.</param>
        /// <param name="endValue">The target world position.</param>
        /// <param name="jumpPower">The height of the jump arc. Higher values create higher jumps.</param>
        /// <param name="numJumps">The number of jumps to perform. Must be at least 1.</param>
        /// <param name="duration">Total animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// // Jump to position with 3 bounces
        /// RTween.Jump(transform, new Vector3(5, 0, 0), 2f, 3, 1.5f);
        /// </code>
        /// </example>
        public static TweenHandle Jump(Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration)
        {
            return TweenEngine.Instance.CreateJump(target, endValue, jumpPower, numJumps, duration);
        }

        // Material tweens

        /// <summary>
        /// Animates a material's main color to the target value.
        /// </summary>
        /// <param name="target">The material to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(Material target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.MaterialColor);
        }

        /// <summary>
        /// Animates a material's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The material to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Alpha(Material target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.MaterialAlpha);
        }

        // SpriteRenderer tweens

        /// <summary>
        /// Animates a SpriteRenderer's color to the target value.
        /// </summary>
        /// <param name="target">The SpriteRenderer to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(SpriteRenderer target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.SpriteColor);
        }

        /// <summary>
        /// Animates a SpriteRenderer's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The SpriteRenderer to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Alpha(SpriteRenderer target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.SpriteAlpha);
        }

        // Image tweens

        /// <summary>
        /// Animates a UI Image's color to the target value.
        /// </summary>
        /// <param name="target">The Image to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(Image target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.ImageColor);
        }

        /// <summary>
        /// Animates a UI Image's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The Image to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// // Fade out image
        /// RTween.Alpha(image, 0f, 0.5f);
        /// </code>
        /// </example>
        public static TweenHandle Alpha(Image target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.ImageAlpha);
        }

        // Text tweens

        /// <summary>
        /// Animates a UI Text's color to the target value.
        /// </summary>
        /// <param name="target">The Text to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(Text target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.TextColor);
        }

        /// <summary>
        /// Animates a UI Text's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The Text to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Alpha(Text target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.TextAlpha);
        }

        // TextMeshPro tweens

        /// <summary>
        /// Animates a TextMeshPro text's color to the target value.
        /// </summary>
        /// <param name="target">The TMP_Text to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(TMP_Text target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.TMPColor);
        }

        /// <summary>
        /// Animates a TextMeshPro text's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The TMP_Text to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Alpha(TMP_Text target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.TMPAlpha);
        }

        // CanvasGroup tweens

        /// <summary>
        /// Animates a CanvasGroup's alpha transparency to the target value.
        /// </summary>
        /// <param name="target">The CanvasGroup to animate. Cannot be null.</param>
        /// <param name="endValue">The target alpha (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <remarks>
        /// CanvasGroup alpha affects all child UI elements at once, making it ideal for fading entire UI panels.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Fade out entire panel
        /// RTween.Alpha(panelCanvasGroup, 0f, 0.5f)
        ///     .OnComplete(() => panel.SetActive(false));
        /// </code>
        /// </example>
        public static TweenHandle Alpha(CanvasGroup target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.CanvasAlpha);
        }

        // RectTransform tweens

        /// <summary>
        /// Animates a RectTransform's anchored position to the target value.
        /// </summary>
        /// <param name="target">The RectTransform to animate. Cannot be null.</param>
        /// <param name="endValue">The target anchored position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// RectTransform button = GetComponent<RectTransform>();
        /// RTween.UIMove(button, new Vector2(100, 50), 1f);
        /// </code>
        /// </example>
        public static TweenHandle UIMove(RectTransform target, Vector2 endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.UIMove);
        }

        /// <summary>
        /// Animates only the X component of a RectTransform's anchored position.
        /// </summary>
        /// <param name="target">The RectTransform to animate. Cannot be null.</param>
        /// <param name="endValue">The target X position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle UIMoveX(RectTransform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector2(endValue, 0), duration, TweenType.UIMoveX);
        }

        /// <summary>
        /// Animates only the Y component of a RectTransform's anchored position.
        /// </summary>
        /// <param name="target">The RectTransform to animate. Cannot be null.</param>
        /// <param name="endValue">The target Y position.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle UIMoveY(RectTransform target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, new Vector2(0, endValue), duration, TweenType.UIMoveY);
        }

        /// <summary>
        /// Animates a RectTransform's size delta to the target value.
        /// </summary>
        /// <param name="target">The RectTransform to animate. Cannot be null.</param>
        /// <param name="endValue">The target size delta.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <remarks>
        /// Size delta represents the size of the RectTransform relative to its anchors.
        /// </remarks>
        public static TweenHandle SizeDelta(RectTransform target, Vector2 endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.SizeDelta);
        }

        // Camera tweens

        /// <summary>
        /// Animates a camera's field of view to the target value.
        /// </summary>
        /// <param name="target">The Camera to animate. Cannot be null. Must be a perspective camera.</param>
        /// <param name="endValue">The target field of view in degrees (typically 1-179).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// // Zoom effect
        /// RTween.FieldOfView(Camera.main, 30f, 1f);
        /// </code>
        /// </example>
        public static TweenHandle FieldOfView(Camera target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.CameraFOV);
        }

        /// <summary>
        /// Animates a camera's orthographic size to the target value.
        /// </summary>
        /// <param name="target">The Camera to animate. Cannot be null. Must be an orthographic camera.</param>
        /// <param name="endValue">The target orthographic size.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <remarks>
        /// Orthographic size represents half the vertical size of the camera view.
        /// </remarks>
        public static TweenHandle OrthographicSize(Camera target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.CameraOrthoSize);
        }

        // AudioSource tweens

        /// <summary>
        /// Animates an AudioSource's volume to the target value.
        /// </summary>
        /// <param name="target">The AudioSource to animate. Cannot be null.</param>
        /// <param name="endValue">The target volume (0-1).</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <example>
        /// <code>
        /// // Fade out music
        /// RTween.Volume(musicSource, 0f, 2f)
        ///     .OnComplete(() => musicSource.Stop());
        /// </code>
        /// </example>
        public static TweenHandle Volume(AudioSource target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.AudioVolume);
        }

        /// <summary>
        /// Animates an AudioSource's pitch to the target value.
        /// </summary>
        /// <param name="target">The AudioSource to animate. Cannot be null.</param>
        /// <param name="endValue">The target pitch. 1 is normal, 0.5 is half speed, 2 is double speed.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Pitch(AudioSource target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.AudioPitch);
        }

        // Light tweens

        /// <summary>
        /// Animates a Light's intensity to the target value.
        /// </summary>
        /// <param name="target">The Light to animate. Cannot be null.</param>
        /// <param name="endValue">The target intensity.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Intensity(Light target, float endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.LightIntensity);
        }

        /// <summary>
        /// Animates a Light's color to the target value.
        /// </summary>
        /// <param name="target">The Light to animate. Cannot be null.</param>
        /// <param name="endValue">The target color.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        public static TweenHandle Color(Light target, Color endValue, float duration)
        {
            return TweenEngine.Instance.Create(target, endValue, duration, TweenType.LightColor);
        }

        // Custom value tween

        /// <summary>
        /// Animates a custom float value from start to end, invoking a callback with the current value each frame.
        /// </summary>
        /// <param name="startValue">The starting value.</param>
        /// <param name="endValue">The target value.</param>
        /// <param name="duration">Animation duration in seconds. Must be positive.</param>
        /// <param name="onUpdate">Callback invoked each frame with the current interpolated value.</param>
        /// <returns>A TweenHandle to control the tween.</returns>
        /// <remarks>
        /// Useful for animating custom properties or values not directly supported by RTween.
        /// </remarks>
        /// <example>
        /// <code>
        /// RTween.Value(0f, 100f, 2f, value => 
        /// {
        ///     customSlider.value = value;
        ///     scoreText.text = value.ToString("F0");
        /// });
        /// </code>
        /// </example>
        public static TweenHandle Value(float startValue, float endValue, float duration, Action<float> onUpdate)
        {
            return TweenEngine.Instance.CreateValue(startValue, endValue, duration, onUpdate);
        }

        // Delayed callback

        /// <summary>
        /// Executes a callback after a specified delay.
        /// </summary>
        /// <param name="delay">Delay in seconds before executing the callback. Must be positive.</param>
        /// <param name="callback">The callback to execute after the delay.</param>
        /// <returns>A TweenHandle to control or cancel the delayed call.</returns>
        /// <example>
        /// <code>
        /// RTween.DelayedCall(2f, () => Debug.Log("Executed after 2 seconds"));
        /// </code>
        /// </example>
        public static TweenHandle DelayedCall(float delay, Action callback)
        {
            return TweenEngine.Instance.CreateDelayedCall(delay, callback);
        }

        // Sequence creation

        /// <summary>
        /// Creates a new tween sequence for chaining multiple tweens with precise timing control.
        /// </summary>
        /// <returns>A new TweenSequence instance.</returns>
        /// <remarks>
        /// Sequences allow complex animation choreography by chaining, overlapping, or scheduling tweens at specific times.
        /// Call Play() on the sequence to start it.
        /// </remarks>
        /// <example>
        /// <code>
        /// RTween.Sequence()
        ///     .Add(RTween.Move(obj1, Vector3.up, 0.5f))
        ///     .Wait(0.2f)
        ///     .Add(RTween.Scale(obj2, Vector3.one * 2, 0.3f))
        ///     .Play();
        /// </code>
        /// </example>
        public static TweenSequence Sequence()
        {
            return new TweenSequence();
        }

        // Control methods

        /// <summary>
        /// Kills (stops and removes) a specific tween.
        /// </summary>
        /// <param name="handle">The tween handle to kill.</param>
        /// <param name="complete">If true, the tween jumps to its end value before being killed. If false, it stops immediately at current value.</param>
        /// <example>
        /// <code>
        /// TweenHandle handle = RTween.Move(transform, Vector3.up, 1f);
        /// // Later...
        /// RTween.Kill(handle, complete: true); // Jump to end and stop
        /// </code>
        /// </example>
        public static void Kill(TweenHandle handle, bool complete = false)
        {
            if (!TweenEngine.HasInstance) return;
            TweenEngine.Instance.Kill(handle, complete);
        }

        /// <summary>
        /// Kills all active tweens targeting a specific object.
        /// </summary>
        /// <param name="target">The target object whose tweens should be stopped.</param>
        /// <param name="complete">If true, the tweens jump to their end values before being killed.</param>
        public static void Kill(object target, bool complete = false)
        {
            if (!TweenEngine.HasInstance) return;
            TweenEngine.Instance.Kill(target, complete);
        }

        /// <summary>
        /// Kills (stops and removes) all active tweens immediately.
        /// </summary>
        /// <remarks>
        /// Use this with caution as it will affect all tweens in the entire game.
        /// Useful for scene transitions or cleanup.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Clean up all tweens when changing scenes
        /// RTween.KillAll();
        /// SceneManager.LoadScene("NextScene");
        /// </code>
        /// </example>
        public static void KillAll()
        {
            if (!TweenEngine.HasInstance) return;
            TweenEngine.Instance.KillAll();
        }

        /// <summary>
        /// Pauses a specific tween, stopping its progress until resumed.
        /// </summary>
        /// <param name="handle">The tween handle to pause.</param>
        /// <remarks>
        /// Paused tweens remain in memory and can be resumed with Resume().
        /// Time spent paused does not count toward the tween's duration.
        /// </remarks>
        public static void Pause(TweenHandle handle)
        {
            TweenEngine.Instance.Pause(handle);
        }

        /// <summary>
        /// Resumes a previously paused tween.
        /// </summary>
        /// <param name="handle">The tween handle to resume.</param>
        public static void Resume(TweenHandle handle)
        {
            TweenEngine.Instance.Resume(handle);
        }

        /// <summary>
        /// Pauses all active tweens in the entire game.
        /// </summary>
        /// <remarks>
        /// Useful for implementing a pause menu or time-stop mechanic.
        /// All tweens remain in memory and can be resumed with ResumeAll().
        /// </remarks>
        /// <example>
        /// <code>
        /// void OnPauseGame()
        /// {
        ///     RTween.PauseAll();
        ///     Time.timeScale = 0f;
        /// }
        /// </code>
        /// </example>
        public static void PauseAll()
        {
            TweenEngine.Instance.PauseAll();
        }

        /// <summary>
        /// Resumes all previously paused tweens.
        /// </summary>
        /// <example>
        /// <code>
        /// void OnResumeGame()
        /// {
        ///     RTween.ResumeAll();
        ///     Time.timeScale = 1f;
        /// }
        /// </code>
        /// </example>
        public static void ResumeAll()
        {
            TweenEngine.Instance.ResumeAll();
        }
    }
}
