using System;
using System.Collections.Generic;
using UnityEngine;

namespace RG.Tweening
{
    /// <summary>
    /// Sequence for chaining multiple tweens
    /// </summary>
    public class TweenSequence
    {
        private List<SequenceItem> items = new List<SequenceItem>();
        private float currentTime;

        public TweenSequence Add(TweenHandle tween)
        {
            float tweenDuration = tween.engine.GetTweenDuration(tween.id);
            items.Add(new SequenceItem
            {
                handle = tween,
                startTime = currentTime,
                duration = tweenDuration,
                type = SequenceType.Add
            });
            currentTime += tweenDuration;
            return this;
        }

        public TweenSequence Wait(float interval)
        {
            currentTime += interval;
            return this;
        }

        public TweenSequence Call(Action callback)
        {
            items.Add(new SequenceItem
            {
                callback = callback,
                startTime = currentTime,
                type = SequenceType.Callback
            });
            return this;
        }

        public TweenSequence With(TweenHandle tween)
        {
            float previousTime = currentTime;
            if (items.Count > 0)
            {
                previousTime = items[items.Count - 1].startTime;
            }

            float tweenDuration = tween.engine.GetTweenDuration(tween.id);
            items.Add(new SequenceItem
            {
                handle = tween,
                startTime = previousTime,
                duration = tweenDuration,
                type = SequenceType.With
            });
            currentTime = Mathf.Max(currentTime, previousTime + tweenDuration);
            return this;
        }

        public TweenSequence At(float time, TweenHandle tween)
        {
            float tweenDuration = tween.engine.GetTweenDuration(tween.id);
            items.Add(new SequenceItem
            {
                handle = tween,
                startTime = time,
                duration = tweenDuration,
                type = SequenceType.At
            });
            currentTime = Mathf.Max(currentTime, time + tweenDuration);
            return this;
        }

        public TweenHandle Play()
        {
            return TweenEngine.Instance.CreateSequence(this, currentTime);
        }

        internal List<SequenceItem> GetItems()
        {
            return items;
        }

        internal struct SequenceItem
        {
            public TweenHandle handle;
            public Action callback;
            public float startTime;
            public float duration;
            public SequenceType type;
            public bool hasStarted;
        }

        internal enum SequenceType
        {
            Add,
            With,
            At,
            Callback
        }
    }
}