using System;
using System.Collections.Generic;
using UnityEngine;

namespace RG.Tweening
{
    internal class TweenData
    {
        public object target;
        public UnityEngine.Object unityTarget;
        public bool isUnityObject;

        // Concrete fields to completely avoid boxing of value types (Vector3, Vector2, float, Quaternion, Color)
        public float startFloat;
        public float endFloat;

        public Vector3 startVector3;
        public Vector3 endVector3;

        public Vector2 startVector2;
        public Vector2 endVector2;

        public Quaternion startQuaternion;
        public Quaternion endQuaternion;

        public Color startColor;
        public Color endColor;

        public float duration;
        public float elapsed;
        public float delay;
        public TweenType type;
        public EaseType ease = EaseType.OutQuad;
        public AnimationCurve customEaseCurve; // Custom curve easing support

        public int repeatCount;
        public int currentRepeat;
        public RepeatType repeatType;
        public bool isPaused;
        public Action onComplete;
        public Action onUpdate;
        public Action onStart;
        public Action<float> valueUpdateCallback;
        public List<TweenSequence.SequenceItem> sequenceItems;

        // Jump tween specific
        public float jumpPower;
        public int numJumps;

        // Performance and safety tracking
        public float creationTime;

        public void Reset()
        {
            target = null;
            unityTarget = null;
            isUnityObject = false;

            startFloat = 0f;
            endFloat = 0f;

            startVector3 = Vector3.zero;
            endVector3 = Vector3.zero;

            startVector2 = Vector2.zero;
            endVector2 = Vector2.zero;

            startQuaternion = Quaternion.identity;
            endQuaternion = Quaternion.identity;

            startColor = Color.clear;
            endColor = Color.clear;

            duration = 0f;
            elapsed = 0f;
            delay = 0f;
            type = default;
            ease = EaseType.OutQuad;
            customEaseCurve = null;

            repeatCount = 0;
            currentRepeat = 0;
            repeatType = RepeatType.Restart;
            isPaused = false;
            onComplete = null;
            onUpdate = null;
            onStart = null;
            valueUpdateCallback = null;
            sequenceItems = null;

            jumpPower = 0f;
            numJumps = 0;
            creationTime = 0f;
        }
    }
}