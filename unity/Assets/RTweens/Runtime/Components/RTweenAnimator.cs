using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RG.Tweening
{
    /// <summary>
    /// Component for creating tween animations directly in the Inspector
    /// </summary>
    public class RTweenAnimator : MonoBehaviour
    {
        [Serializable]
        public class TweenAnimation
        {
            public string name = "New Animation";
            public bool enabled = true;
            public AnimationType animationType = AnimationType.Move;
            public PlayMode playMode = PlayMode.OnEnable;

            [Header("Timing")]
            public float duration = 1f;
            public float delay = 0f;
            public EaseType easeType = EaseType.OutQuad;

            [Header("Repeat")]
            public int repeatCount = 0;
            public RepeatType repeatType = RepeatType.Restart;

            [Header("Target")]
            public Component customTarget;

            [Header("Vector3 Values")]
            public Vector3 vector3Value;
            public bool useFromValue = false;
            public Vector3 fromVector3Value;

            [Header("Vector2 Values")]
            public Vector2 vector2Value;
            public Vector2 fromVector2Value;

            [Header("Float Values")]
            public float floatValue;
            public float fromFloatValue;

            [Header("Color/Alpha Values (Auto-detects component)")]
            public Color colorValue = Color.white;
            public Color fromColorValue = Color.white;

            [Header("Rotation Values")]
            public Vector3 rotationValue;
            public Vector3 fromRotationValue;

            [Header("Jump Settings")]
            public float jumpPower = 1f;
            public int numJumps = 1;

            [Header("Events")]
            public UnityEngine.Events.UnityEvent onStart;
            public UnityEngine.Events.UnityEvent onUpdate;
            public UnityEngine.Events.UnityEvent onComplete;

            [HideInInspector]
            public TweenHandle handle;
        }

        public enum AnimationType
        {
            Move, MoveX, MoveY, MoveZ,
            LocalMove, LocalMoveX, LocalMoveY, LocalMoveZ,
            Rotation, LocalRotation,
            RotationEuler, LocalRotationEuler,
            Scale, ScaleX, ScaleY, ScaleZ,
            Jump,
            UIMove, UIMoveX, UIMoveY,
            SizeDelta,
            CanvasGroupAlpha,
            Color, Alpha,
            MaterialColor, MaterialAlpha,
            CameraFOV, CameraOrthoSize,
            AudioVolume, AudioPitch,
            LightIntensity, LightColor
        }

        public enum PlayMode
        {
            OnEnable,
            OnStart,
            Manual,
            OnTriggerEnter,
            OnCollisionEnter,
            OnMouseDown
        }

        [Header("Animations")]
        public List<TweenAnimation> animations = new List<TweenAnimation>();

        [Header("Sequence Settings")]
        public bool useSequence = false;
        public bool autoPlay = true;

        private TweenSequence sequence;
        private bool hasPlayedOnEnable = false;

        private void OnEnable()
        {
            KillAllActiveHandles();

            foreach (var anim in animations)
            {
                anim.handle = default;
            }

            hasPlayedOnEnable = false;

            if (autoPlay)
            {
                PlayAnimations(PlayMode.OnEnable);
                hasPlayedOnEnable = true;
            }
        }

        private void OnDisable()
        {
            KillAllActiveHandles();
        }

        private void Start()
        {
            if (autoPlay && !hasPlayedOnEnable)
            {
                PlayAnimations(PlayMode.OnStart);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayAnimations(PlayMode.OnTriggerEnter);
        }

        private void OnCollisionEnter(Collision collision)
        {
            PlayAnimations(PlayMode.OnCollisionEnter);
        }

        private void OnMouseDown()
        {
            PlayAnimations(PlayMode.OnMouseDown);
        }

        /// <summary>
        /// Play ALL enabled animations regardless of PlayMode
        /// </summary>
        public void Play()
        {
            if (useSequence)
            {
                sequence = RTween.Sequence();
                foreach (var anim in animations)
                {
                    if (anim.enabled)
                    {
                        var handle = CreateTween(anim, false);
                        if (handle.IsActive)
                        {
                            sequence.Add(handle);
                        }
                    }
                }
                sequence.Play();
            }
            else
            {
                foreach (var anim in animations)
                {
                    if (anim.enabled)
                    {
                        CreateTween(anim);
                    }
                }
            }
        }

        public void PlayAnimation(int index)
        {
            if (index >= 0 && index < animations.Count && animations[index].enabled)
            {
                // Stop existing tween for this animation
                if (animations[index].handle.IsActive)
                {
                    animations[index].handle.Kill();
                }

                CreateTween(animations[index]);
            }
        }

        public void PlayAnimation(string animationName)
        {
            var anim = animations.Find(a => a.name == animationName);
            if (anim != null && anim.enabled)
            {
                if (anim.handle.IsActive)
                {
                    anim.handle.Kill();
                }

                CreateTween(anim);
            }
        }

        public void Stop()
        {
            KillAllActiveHandles();
        }

        public void StopAnimation(int index)
        {
            if (index >= 0 && index < animations.Count && animations[index].handle.IsActive)
            {
                animations[index].handle.Kill();
            }
        }

        public void Pause()
        {
            foreach (var anim in animations)
            {
                if (anim.handle.IsActive)
                {
                    anim.handle.Pause();
                }
            }
        }

        public void Resume()
        {
            foreach (var anim in animations)
            {
                if (anim.handle.IsActive)
                {
                    anim.handle.Resume();
                }
            }
        }

        private void KillAllActiveHandles()
        {
            foreach (var anim in animations)
            {
                if (anim.handle.IsActive)
                {
                    anim.handle.Kill();
                }
            }
        }

        private void PlayAnimations(PlayMode mode)
        {
            if (useSequence)
            {
                sequence = RTween.Sequence();
                foreach (var anim in animations)
                {
                    if (anim.enabled && anim.playMode == mode)
                    {
                        var handle = CreateTween(anim, false);
                        if (handle.IsActive)
                        {
                            sequence.Add(handle);
                        }
                    }
                }
                sequence.Play();
            }
            else
            {
                foreach (var anim in animations)
                {
                    if (anim.enabled && anim.playMode == mode)
                    {
                        CreateTween(anim);
                    }
                }
            }
        }

        private TweenHandle CreateTween(TweenAnimation anim, bool autoStart = true)
        {
            Component target = anim.customTarget != null ? anim.customTarget : GetTargetComponent(anim.animationType);

            if (target == null)
            {
                Debug.LogWarning($"RTweenAnimator: No valid target for {anim.animationType} on {gameObject.name}");
                return default;
            }

            TweenHandle handle = default;

            // Always set from value when starting a new tween to ensure correct starting position
            if (anim.useFromValue)
            {
                SetFromValue(target, anim);
            }

            switch (anim.animationType)
            {
                case AnimationType.Move:
                    handle = RTween.Move((Transform)target, anim.vector3Value, anim.duration);
                    break;
                case AnimationType.MoveX:
                    handle = RTween.MoveX((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.MoveY:
                    handle = RTween.MoveY((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.MoveZ:
                    handle = RTween.MoveZ((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.LocalMove:
                    handle = RTween.LocalMove((Transform)target, anim.vector3Value, anim.duration);
                    break;
                case AnimationType.LocalMoveX:
                    handle = RTween.LocalMoveX((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.LocalMoveY:
                    handle = RTween.LocalMoveY((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.LocalMoveZ:
                    handle = RTween.LocalMoveZ((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.Rotation:
                case AnimationType.RotationEuler:
                    handle = RTween.Rotation((Transform)target, Quaternion.Euler(anim.rotationValue), anim.duration);
                    break;
                case AnimationType.LocalRotation:
                case AnimationType.LocalRotationEuler:
                    handle = RTween.LocalRotation((Transform)target, Quaternion.Euler(anim.rotationValue), anim.duration);
                    break;
                case AnimationType.Scale:
                    handle = RTween.Scale((Transform)target, anim.vector3Value, anim.duration);
                    break;
                case AnimationType.ScaleX:
                    handle = RTween.ScaleX((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.ScaleY:
                    handle = RTween.ScaleY((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.ScaleZ:
                    handle = RTween.ScaleZ((Transform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.Jump:
                    handle = RTween.Jump((Transform)target, anim.vector3Value, anim.jumpPower, anim.numJumps, anim.duration);
                    break;
                case AnimationType.UIMove:
                    handle = RTween.UIMove((RectTransform)target, anim.vector2Value, anim.duration);
                    break;
                case AnimationType.UIMoveX:
                    handle = RTween.UIMoveX((RectTransform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.UIMoveY:
                    handle = RTween.UIMoveY((RectTransform)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.SizeDelta:
                    handle = RTween.SizeDelta((RectTransform)target, anim.vector2Value, anim.duration);
                    break;
                case AnimationType.CanvasGroupAlpha:
                    handle = RTween.Alpha((CanvasGroup)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.Color:
                    handle = CreateColorTween(target, anim.colorValue, anim.duration);
                    break;
                case AnimationType.Alpha:
                    handle = CreateAlphaTween(target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.MaterialColor:
                    handle = RTween.Color(((Renderer)target).material, anim.colorValue, anim.duration);
                    break;
                case AnimationType.MaterialAlpha:
                    handle = RTween.Alpha(((Renderer)target).material, anim.floatValue, anim.duration);
                    break;
                case AnimationType.CameraFOV:
                    handle = RTween.FieldOfView((Camera)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.CameraOrthoSize:
                    handle = RTween.OrthographicSize((Camera)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.AudioVolume:
                    handle = RTween.Volume((AudioSource)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.AudioPitch:
                    handle = RTween.Pitch((AudioSource)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.LightIntensity:
                    handle = RTween.Intensity((Light)target, anim.floatValue, anim.duration);
                    break;
                case AnimationType.LightColor:
                    handle = RTween.Color((Light)target, anim.colorValue, anim.duration);
                    break;
            }

            if (handle.IsActive)
            {
                handle.SetEase(anim.easeType)
                      .SetDelay(anim.delay)
                      .SetRepeat(anim.repeatCount, anim.repeatType);

                if (anim.onStart != null && anim.onStart.GetPersistentEventCount() > 0)
                    handle.OnStart(() => anim.onStart.Invoke());

                if (anim.onUpdate != null && anim.onUpdate.GetPersistentEventCount() > 0)
                    handle.OnUpdate(() => anim.onUpdate.Invoke());

                if (anim.onComplete != null && anim.onComplete.GetPersistentEventCount() > 0)
                    handle.OnComplete(() => anim.onComplete.Invoke());

                anim.handle = handle;
            }

            return handle;
        }

        private TweenHandle CreateColorTween(Component target, Color endColor, float duration)
        {
            if (target is Image img)
                return RTween.Color(img, endColor, duration);
            if (target is Text txt)
                return RTween.Color(txt, endColor, duration);
            if (target is TMP_Text tmp)
                return RTween.Color(tmp, endColor, duration);
            if (target is SpriteRenderer sprite)
                return RTween.Color(sprite, endColor, duration);
            if (target is Renderer renderer)
                return RTween.Color(renderer.material, endColor, duration);

            Debug.LogWarning($"RTweenAnimator: No color component found on {target.name}");
            return default;
        }

        private TweenHandle CreateAlphaTween(Component target, float endAlpha, float duration)
        {
            if (target is Image img)
                return RTween.Alpha(img, endAlpha, duration);
            if (target is Text txt)
                return RTween.Alpha(txt, endAlpha, duration);
            if (target is TMP_Text tmp)
                return RTween.Alpha(tmp, endAlpha, duration);
            if (target is SpriteRenderer sprite)
                return RTween.Alpha(sprite, endAlpha, duration);
            if (target is Renderer renderer)
                return RTween.Alpha(renderer.material, endAlpha, duration);
            if (target is CanvasGroup canvas)
                return RTween.Alpha(canvas, endAlpha, duration);

            Debug.LogWarning($"RTweenAnimator: No alpha component found on {target.name}");
            return default;
        }

        private void SetFromValue(Component target, TweenAnimation anim)
        {
            switch (anim.animationType)
            {
                case AnimationType.Move:
                    ((Transform)target).position = anim.fromVector3Value;
                    break;
                case AnimationType.MoveX:
                    var pos = ((Transform)target).position;
                    pos.x = anim.fromFloatValue;
                    ((Transform)target).position = pos;
                    break;
                case AnimationType.MoveY:
                    pos = ((Transform)target).position;
                    pos.y = anim.fromFloatValue;
                    ((Transform)target).position = pos;
                    break;
                case AnimationType.MoveZ:
                    pos = ((Transform)target).position;
                    pos.z = anim.fromFloatValue;
                    ((Transform)target).position = pos;
                    break;
                case AnimationType.LocalMove:
                    ((Transform)target).localPosition = anim.fromVector3Value;
                    break;
                case AnimationType.LocalMoveX:
                    var localPos = ((Transform)target).localPosition;
                    localPos.x = anim.fromFloatValue;
                    ((Transform)target).localPosition = localPos;
                    break;
                case AnimationType.LocalMoveY:
                    localPos = ((Transform)target).localPosition;
                    localPos.y = anim.fromFloatValue;
                    ((Transform)target).localPosition = localPos;
                    break;
                case AnimationType.LocalMoveZ:
                    localPos = ((Transform)target).localPosition;
                    localPos.z = anim.fromFloatValue;
                    ((Transform)target).localPosition = localPos;
                    break;
                case AnimationType.Scale:
                    ((Transform)target).localScale = anim.fromVector3Value;
                    break;
                case AnimationType.ScaleX:
                    var scale = ((Transform)target).localScale;
                    scale.x = anim.fromFloatValue;
                    ((Transform)target).localScale = scale;
                    break;
                case AnimationType.ScaleY:
                    scale = ((Transform)target).localScale;
                    scale.y = anim.fromFloatValue;
                    ((Transform)target).localScale = scale;
                    break;
                case AnimationType.ScaleZ:
                    scale = ((Transform)target).localScale;
                    scale.z = anim.fromFloatValue;
                    ((Transform)target).localScale = scale;
                    break;
                case AnimationType.Rotation:
                case AnimationType.RotationEuler:
                    ((Transform)target).rotation = Quaternion.Euler(anim.fromRotationValue);
                    break;
                case AnimationType.LocalRotation:
                case AnimationType.LocalRotationEuler:
                    ((Transform)target).localRotation = Quaternion.Euler(anim.fromRotationValue);
                    break;
                case AnimationType.UIMove:
                    ((RectTransform)target).anchoredPosition = anim.fromVector2Value;
                    break;
                case AnimationType.UIMoveX:
                    var anchoredPos = ((RectTransform)target).anchoredPosition;
                    anchoredPos.x = anim.fromFloatValue;
                    ((RectTransform)target).anchoredPosition = anchoredPos;
                    break;
                case AnimationType.UIMoveY:
                    anchoredPos = ((RectTransform)target).anchoredPosition;
                    anchoredPos.y = anim.fromFloatValue;
                    ((RectTransform)target).anchoredPosition = anchoredPos;
                    break;
                case AnimationType.SizeDelta:
                    ((RectTransform)target).sizeDelta = anim.fromVector2Value;
                    break;
                case AnimationType.Color:
                    SetFromColor(target, anim.fromColorValue);
                    break;
                case AnimationType.Alpha:
                case AnimationType.CanvasGroupAlpha:
                    SetFromAlpha(target, anim.fromFloatValue);
                    break;
            }
        }

        private void SetFromColor(Component target, Color color)
        {
            if (target is Image img)
                img.color = color;
            else if (target is Text txt)
                txt.color = color;
            else if (target is TMP_Text tmp)
                tmp.color = color;
            else if (target is SpriteRenderer sprite)
                sprite.color = color;
            else if (target is Renderer renderer)
                renderer.material.color = color;
        }

        private void SetFromAlpha(Component target, float alpha)
        {
            if (target is Image img)
            {
                var c = img.color;
                c.a = alpha;
                img.color = c;
            }
            else if (target is Text txt)
            {
                var c = txt.color;
                c.a = alpha;
                txt.color = c;
            }
            else if (target is TMP_Text tmp)
            {
                var c = tmp.color;
                c.a = alpha;
                tmp.color = c;
            }
            else if (target is SpriteRenderer sprite)
            {
                var c = sprite.color;
                c.a = alpha;
                sprite.color = c;
            }
            else if (target is Renderer renderer)
            {
                var c = renderer.material.color;
                c.a = alpha;
                renderer.material.color = c;
            }
            else if (target is CanvasGroup canvas)
            {
                canvas.alpha = alpha;
            }
        }

        private Component GetTargetComponent(AnimationType type)
        {
            switch (type)
            {
                case AnimationType.Move:
                case AnimationType.MoveX:
                case AnimationType.MoveY:
                case AnimationType.MoveZ:
                case AnimationType.LocalMove:
                case AnimationType.LocalMoveX:
                case AnimationType.LocalMoveY:
                case AnimationType.LocalMoveZ:
                case AnimationType.Rotation:
                case AnimationType.LocalRotation:
                case AnimationType.RotationEuler:
                case AnimationType.LocalRotationEuler:
                case AnimationType.Scale:
                case AnimationType.ScaleX:
                case AnimationType.ScaleY:
                case AnimationType.ScaleZ:
                case AnimationType.Jump:
                    return transform;

                case AnimationType.UIMove:
                case AnimationType.UIMoveX:
                case AnimationType.UIMoveY:
                case AnimationType.SizeDelta:
                    return GetComponent<RectTransform>();

                case AnimationType.CanvasGroupAlpha:
                    return GetComponent<CanvasGroup>();

                case AnimationType.Color:
                case AnimationType.Alpha:
                    Component c = GetComponent<Image>();
                    if (c == null) c = GetComponent<Text>();
                    if (c == null) c = GetComponent<TMP_Text>();
                    if (c == null) c = GetComponent<SpriteRenderer>();
                    if (c == null) c = GetComponent<Renderer>();
                    if (c == null) c = GetComponent<CanvasGroup>();
                    return c;

                case AnimationType.MaterialColor:
                case AnimationType.MaterialAlpha:
                    return GetComponent<Renderer>();

                case AnimationType.CameraFOV:
                case AnimationType.CameraOrthoSize:
                    return GetComponent<Camera>();

                case AnimationType.AudioVolume:
                case AnimationType.AudioPitch:
                    return GetComponent<AudioSource>();

                case AnimationType.LightIntensity:
                case AnimationType.LightColor:
                    return GetComponent<Light>();

                default:
                    return null;
            }
        }
    }
}