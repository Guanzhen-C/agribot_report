using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace RG.Tweening
{
    public struct TweenHandle
    {
        internal int id;
        internal TweenEngine engine;

        public readonly bool IsActive
        {
            get
            {
                if (engine == null) return false;
                return engine.IsActive(id);
            }
        }

        public readonly TweenHandle SetEase(EaseType ease)
        {
            if (engine != null)
                engine.SetEase(id, ease);

            return this;
        }

        public readonly TweenHandle SetEase(AnimationCurve curve)
        {
            if (engine != null)
                engine.SetEase(id, curve);

            return this;
        }

        public readonly TweenHandle SetDelay(float delay)
        {
            if (engine != null)
                engine.SetDelay(id, delay);

            return this;
        }

        public readonly TweenHandle SetRepeat(int count, RepeatType repeatType = RepeatType.Restart)
        {
            if (engine != null)
                engine.SetRepeat(id, count, repeatType);

            return this;
        }

        public readonly TweenHandle OnComplete(Action callback)
        {
            if (engine != null)
                engine.SetOnComplete(id, callback);

            return this;
        }

        public readonly TweenHandle OnUpdate(Action callback)
        {
            if (engine != null)
                engine.SetOnUpdate(id, callback);

            return this;
        }

        public readonly TweenHandle OnStart(Action callback)
        {
            if (engine != null)
                engine.SetOnStart(id, callback);

            return this;
        }

        public readonly void Kill(bool complete = false)
        {
            if (engine != null)
                engine.Kill(this, complete);
        }

        public readonly void Pause()
        {
            if (engine != null)
                engine.Pause(this);
        }

        public readonly void Resume()
        {
            if (engine != null)
                engine.Resume(this);
        }

        public readonly TweenAwaiter GetAwaiter()
        {
            return new TweenAwaiter(this);
        }
    }

    public struct TweenAwaiter : INotifyCompletion
    {
        readonly TweenHandle handle;

        public TweenAwaiter(TweenHandle handle)
        {
            this.handle = handle;
        }

        public readonly bool IsCompleted => !handle.IsActive;

        public readonly void OnCompleted(Action continuation)
        {
            if (continuation == null) return;
            if (!handle.IsActive)
            {
                continuation();
            }
            else
            {
                handle.OnComplete(continuation);
            }
        }

        public readonly void GetResult() { }
    }
}
