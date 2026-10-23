using System.Collections.Generic;
using UnityEngine;

namespace RG.Tweening
{
    internal static class TweenDataPool
    {
        static readonly Stack<TweenData> pool = new Stack<TweenData>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPool()
        {
            pool.Clear();
        }

        public static TweenData Get()
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }
            return new TweenData();
        }

        public static void Release(TweenData data)
        {
            if (data == null) return;
            data.Reset();
            pool.Push(data);
        }
    }
}
