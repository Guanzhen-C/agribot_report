using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RG.Tweening
{
    internal class TweenEngine : MonoBehaviour
    {
        static TweenEngine instance;

        const int MAX_TWEENS_WARNING = 50;
        const int MAX_TWEENS_CRITICAL = 100;
        const int MAX_TWEENS_LIMIT = 500;
        bool hasShownWarning = false;
        bool hasShownCritical = false;

        public static TweenEngine Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<TweenEngine>();

                    if (instance == null)
                    {
                        GameObject go = new GameObject("[RTween Engine]");
                        instance = go.AddComponent<TweenEngine>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        Dictionary<int, TweenData> activeTweens = new Dictionary<int, TweenData>();
        List<int> toRemove = new List<int>();
        List<int> tweenIds = new List<int>();
        List<Action> pendingCallbacks = new List<Action>();
        int nextId = 1;
        float lastMemoryCheckTime = 0f;
        const float MEMORY_CHECK_INTERVAL = 5f;

        void OnApplicationQuit()
        {
            // Explicitly release the persistent engine before Unity closes the
            // scene, preventing the DontDestroyOnLoad helper from being reported
            // as an uncleaned scene object in the Editor console.
            KillAll();
        }

        void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
            KillAll();
        }

        void Update()
        {
            UpdateTweens(Time.deltaTime);

            if (Time.time - lastMemoryCheckTime > MEMORY_CHECK_INTERVAL)
            {
                CheckForMemoryLeaks();
                lastMemoryCheckTime = Time.time;
            }
        }

        public TweenHandle Create(object target, Vector3 endValue, float duration, TweenType type)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endVector3 = endValue;
            data.duration = duration;
            data.type = type;
            data.startVector3 = GetCurrentVector3(target, type);
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle Create(object target, Vector2 endValue, float duration, TweenType type)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endVector2 = endValue;
            data.duration = duration;
            data.type = type;
            data.startVector2 = GetCurrentVector2(target, type);
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle Create(object target, float endValue, float duration, TweenType type)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endFloat = endValue;
            data.duration = duration;
            data.type = type;
            data.startFloat = GetCurrentFloat(target, type);
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle Create(object target, Color endValue, float duration, TweenType type)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endColor = endValue;
            data.duration = duration;
            data.type = type;
            data.startColor = GetCurrentColor(target, type);
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle Create(object target, Quaternion endValue, float duration, TweenType type)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endQuaternion = endValue;
            data.duration = duration;
            data.type = type;
            data.startQuaternion = GetCurrentQuaternion(target, type);
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle CreateJump(Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.target = target;
            data.endVector3 = endValue;
            data.duration = duration;
            data.type = TweenType.Jump;
            data.startVector3 = target.position;
            data.jumpPower = jumpPower;
            data.numJumps = numJumps;
            data.creationTime = Time.time;

            if (target is UnityEngine.Object uObj)
            {
                data.unityTarget = uObj;
                data.isUnityObject = true;
            }

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle CreateValue(float start, float end, float duration, Action<float> onUpdate)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.startFloat = start;
            data.endFloat = end;
            data.duration = duration;
            data.type = TweenType.CustomValue;
            data.valueUpdateCallback = onUpdate;
            data.creationTime = Time.time;

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle CreateDelayedCall(float delay, Action callback)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.duration = delay;
            data.type = TweenType.DelayedCall;
            data.onComplete = callback;
            data.creationTime = Time.time;

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public TweenHandle CreateSequence(TweenSequence sequence, float totalDuration)
        {
            if (!CheckTweenLimit()) return default;

            TweenData data = TweenDataPool.Get();
            data.duration = totalDuration;
            data.type = TweenType.Sequence;
            data.sequenceItems = new List<TweenSequence.SequenceItem>(sequence.GetItems());
            data.creationTime = Time.time;

            int id = nextId++;
            activeTweens[id] = data;

            return new TweenHandle { id = id, engine = this };
        }

        public float GetTweenDuration(int id)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                return data.duration + data.delay;
            }
            return 0f;
        }

        bool CheckTweenLimit()
        {
            int count = activeTweens.Count;

            if (count >= MAX_TWEENS_LIMIT)
            {
                return false;
            }

            if (count >= MAX_TWEENS_CRITICAL && !hasShownCritical)
            {
                Debug.LogWarning($"RTween: CRITICAL - {count} active tweens!");
                hasShownCritical = true;
            }
            else if (count >= MAX_TWEENS_WARNING && !hasShownWarning)
            {
                Debug.LogWarning($"RTween: {count} active tweens detected.");
                hasShownWarning = true;
            }

            if (count < MAX_TWEENS_WARNING)
            {
                hasShownWarning = false;
                hasShownCritical = false;
            }
            else if (count < MAX_TWEENS_CRITICAL)
            {
                hasShownCritical = false;
            }

            return true;
        }

        void CheckForMemoryLeaks()
        {
            const float LEAK_THRESHOLD = 300f;
            int suspiciousTweens = 0;

            tweenIds.Clear();
            foreach (var pair in activeTweens)
            {
                tweenIds.Add(pair.Key);
            }

            for (int i = 0; i < tweenIds.Count; i++)
            {
                if (activeTweens.TryGetValue(tweenIds[i], out TweenData data))
                {
                    float lifetime = Time.time - data.creationTime;
                    if (data.repeatCount != -1 && lifetime > LEAK_THRESHOLD)
                    {
                        suspiciousTweens++;
                    }
                }
            }

            if (suspiciousTweens > 0)
            {
                Debug.LogWarning($"RTween: Detected {suspiciousTweens} tween(s) active for over 5 minutes.");
            }
        }

        void UpdateTweens(float deltaTime)
        {
            toRemove.Clear();
            pendingCallbacks.Clear();
            tweenIds.Clear();

            // Struct enumerator iteration prevents GC allocations
            foreach (var pair in activeTweens)
            {
                tweenIds.Add(pair.Key);
            }

            for (int i = 0; i < tweenIds.Count; i++)
            {
                int id = tweenIds[i];

                if (!activeTweens.TryGetValue(id, out TweenData data))
                {
                    continue;
                }

                // Unity object destruction check is optimized by avoiding native bridge crossing if the cached check is true
                if (data.isUnityObject && data.unityTarget == null)
                {
                    toRemove.Add(id);
                    continue;
                }

                if (data.isPaused)
                {
                    continue;
                }

                // Handle delay
                if (data.delay > 0)
                {
                    data.delay -= deltaTime;
                    if (data.delay > 0)
                    {
                        continue;
                    }

                    if (data.onStart != null)
                    {
                        Action callback = data.onStart;
                        pendingCallbacks.Add(callback);
                    }
                }

                // Handle sequence
                if (data.type == TweenType.Sequence)
                {
                    UpdateSequence(data, deltaTime);
                    if (data.elapsed >= data.duration)
                    {
                        HandleRepeatOrComplete(id, data, toRemove);
                    }
                    continue;
                }

                data.elapsed += deltaTime;
                float t = Mathf.Clamp01(data.elapsed / data.duration);
                float easedT = data.customEaseCurve != null 
                    ? data.customEaseCurve.Evaluate(t) 
                    : EaseFunctions.Ease(t, data.ease);

                ApplyTween(data, easedT);

                if (data.onUpdate != null)
                {
                    Action callback = data.onUpdate;
                    pendingCallbacks.Add(callback);
                }

                if (t >= 1f)
                {
                    HandleRepeatOrComplete(id, data, toRemove);
                }
            }

            // Remove completed tweens and return to pool
            for (int i = 0; i < toRemove.Count; i++)
            {
                int id = toRemove[i];
                if (activeTweens.TryGetValue(id, out TweenData data))
                {
                    activeTweens.Remove(id);
                    TweenDataPool.Release(data);
                }
            }

            // Execute all callbacks after dictionary operations are complete
            for (int i = 0; i < pendingCallbacks.Count; i++)
            {
                try
                {
                    pendingCallbacks[i]?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"RTween callback error: {e.Message}");
                }
            }
        }

        void UpdateSequence(TweenData data, float deltaTime)
        {
            data.elapsed += deltaTime;

            if (data.sequenceItems == null) return;

            for (int i = 0; i < data.sequenceItems.Count; i++)
            {
                TweenSequence.SequenceItem item = data.sequenceItems[i];
                if (data.elapsed >= item.startTime && !item.hasStarted)
                {
                    item.hasStarted = true;
                    data.sequenceItems[i] = item;

                    if (item.type == TweenSequence.SequenceType.Callback)
                    {
                        if (item.callback != null)
                        {
                            Action callback = item.callback;
                            pendingCallbacks.Add(callback);
                        }
                    }
                    else if (item.handle.IsActive)
                    {
                        Resume(item.handle);
                    }
                }
            }
        }

        void HandleRepeatOrComplete(int id, TweenData data, List<int> toRemove)
        {
            if (data.repeatCount != 0)
            {
                data.currentRepeat++;

                if (data.repeatCount > 0 && data.currentRepeat >= data.repeatCount)
                {
                    if (data.onComplete != null)
                    {
                        Action callback = data.onComplete;
                        pendingCallbacks.Add(callback);
                    }
                    toRemove.Add(id);
                }
                else
                {
                    data.elapsed = 0;

                    if (data.repeatType == RepeatType.Yoyo)
                    {
                        if (IsAxisSpecificTween(data.type))
                        {
                            float startFloat = data.startFloat;
                            float endFloat;

                            if (data.type == TweenType.UIMoveX || data.type == TweenType.UIMoveY)
                            {
                                endFloat = GetAxisValue(data.endVector2, data.type);
                                data.startFloat = endFloat;
                                data.endVector2 = SetAxisValue(data.endVector2, startFloat, data.type);
                            }
                            else
                            {
                                endFloat = GetAxisValue(data.endVector3, data.type);
                                data.startFloat = endFloat;
                                data.endVector3 = SetAxisValue(data.endVector3, startFloat, data.type);
                            }
                        }
                        else
                        {
                            if (data.type == TweenType.Move || data.type == TweenType.LocalMove || data.type == TweenType.Scale || data.type == TweenType.Jump)
                            {
                                Vector3 temp = data.startVector3;
                                data.startVector3 = data.endVector3;
                                data.endVector3 = temp;
                            }
                            else if (data.type == TweenType.UIMove || data.type == TweenType.SizeDelta)
                            {
                                Vector2 temp = data.startVector2;
                                data.startVector2 = data.endVector2;
                                data.endVector2 = temp;
                            }
                            else if (data.type == TweenType.Rotation || data.type == TweenType.LocalRotation)
                            {
                                Quaternion temp = data.startQuaternion;
                                data.startQuaternion = data.endQuaternion;
                                data.endQuaternion = temp;
                            }
                            else if (data.type == TweenType.MaterialColor || data.type == TweenType.SpriteColor || data.type == TweenType.ImageColor || data.type == TweenType.TextColor || data.type == TweenType.TMPColor || data.type == TweenType.LightColor)
                            {
                                Color temp = data.startColor;
                                data.startColor = data.endColor;
                                data.endColor = temp;
                            }
                            else
                            {
                                float temp = data.startFloat;
                                data.startFloat = data.endFloat;
                                data.endFloat = temp;
                            }
                        }
                    }
                    else if (data.repeatType == RepeatType.Incremental)
                    {
                        if (IsAxisSpecificTween(data.type))
                        {
                            if (data.type == TweenType.UIMoveX || data.type == TweenType.UIMoveY)
                            {
                                float start = data.startFloat;
                                float end = GetAxisValue(data.endVector2, data.type);
                                float increment = end - start;
                                data.startFloat = end;
                                data.endVector2 = SetAxisValue(data.endVector2, end + increment, data.type);
                            }
                            else
                            {
                                float start = data.startFloat;
                                float end = GetAxisValue(data.endVector3, data.type);
                                float increment = end - start;
                                data.startFloat = end;
                                data.endVector3 = SetAxisValue(data.endVector3, end + increment, data.type);
                            }
                        }
                        else if (data.type == TweenType.Move || data.type == TweenType.LocalMove || data.type == TweenType.Scale || data.type == TweenType.Jump)
                        {
                            Vector3 start = data.startVector3;
                            Vector3 increment = data.endVector3 - start;
                            data.startVector3 = data.endVector3;
                            data.endVector3 = data.endVector3 + increment;
                        }
                        else if (data.type == TweenType.UIMove || data.type == TweenType.SizeDelta)
                        {
                            Vector2 start = data.startVector2;
                            Vector2 increment = data.endVector2 - start;
                            data.startVector2 = data.endVector2;
                            data.endVector2 = data.endVector2 + increment;
                        }
                        else if (data.type == TweenType.MaterialColor || data.type == TweenType.SpriteColor || data.type == TweenType.ImageColor || data.type == TweenType.TextColor || data.type == TweenType.TMPColor || data.type == TweenType.LightColor)
                        {
                            Color start = data.startColor;
                            Color increment = data.endColor - start;
                            data.startColor = data.endColor;
                            data.endColor = data.endColor + increment;
                        }
                        else
                        {
                            float start = data.startFloat;
                            float increment = data.endFloat - start;
                            data.startFloat = data.endFloat;
                            data.endFloat = data.endFloat + increment;
                        }
                    }
                    else if (data.repeatType == RepeatType.Restart)
                    {
                        if (data.type == TweenType.Move || data.type == TweenType.LocalMove || data.type == TweenType.Scale || data.type == TweenType.Jump)
                            data.startVector3 = GetCurrentVector3(data.target, data.type);
                        else if (data.type == TweenType.UIMove || data.type == TweenType.SizeDelta)
                            data.startVector2 = GetCurrentVector2(data.target, data.type);
                        else if (data.type == TweenType.Rotation || data.type == TweenType.LocalRotation)
                            data.startQuaternion = GetCurrentQuaternion(data.target, data.type);
                        else if (data.type == TweenType.MaterialColor || data.type == TweenType.SpriteColor || data.type == TweenType.ImageColor || data.type == TweenType.TextColor || data.type == TweenType.TMPColor || data.type == TweenType.LightColor)
                            data.startColor = GetCurrentColor(data.target, data.type);
                        else
                            data.startFloat = GetCurrentFloat(data.target, data.type);
                    }

                    if (data.sequenceItems != null)
                    {
                        for (int i = 0; i < data.sequenceItems.Count; i++)
                        {
                            TweenSequence.SequenceItem item = data.sequenceItems[i];
                            item.hasStarted = false;
                            data.sequenceItems[i] = item;
                        }
                    }
                }
            }
            else
            {
                if (data.onComplete != null)
                {
                    Action callback = data.onComplete;
                    pendingCallbacks.Add(callback);
                }
                toRemove.Add(id);
            }
        }

        void ApplyTween(TweenData data, float t)
        {
            if (data.isUnityObject && data.unityTarget == null)
            {
                return;
            }

            switch (data.type)
            {
                case TweenType.Move:
                    ((Transform)data.target).position = Vector3.LerpUnclamped(data.startVector3, data.endVector3, t);
                    break;
                case TweenType.MoveX:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.position;
                        pos.x = Mathf.LerpUnclamped(data.startFloat, data.endVector3.x, t);
                        trans.position = pos;
                        break;
                    }
                case TweenType.MoveY:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.position;
                        pos.y = Mathf.LerpUnclamped(data.startFloat, data.endVector3.y, t);
                        trans.position = pos;
                        break;
                    }
                case TweenType.MoveZ:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.position;
                        pos.z = Mathf.LerpUnclamped(data.startFloat, data.endVector3.z, t);
                        trans.position = pos;
                        break;
                    }
                case TweenType.LocalMove:
                    ((Transform)data.target).localPosition = Vector3.LerpUnclamped(data.startVector3, data.endVector3, t);
                    break;
                case TweenType.LocalMoveX:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.localPosition;
                        pos.x = Mathf.LerpUnclamped(data.startFloat, data.endVector3.x, t);
                        trans.localPosition = pos;
                        break;
                    }
                case TweenType.LocalMoveY:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.localPosition;
                        pos.y = Mathf.LerpUnclamped(data.startFloat, data.endVector3.y, t);
                        trans.localPosition = pos;
                        break;
                    }
                case TweenType.LocalMoveZ:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 pos = trans.localPosition;
                        pos.z = Mathf.LerpUnclamped(data.startFloat, data.endVector3.z, t);
                        trans.localPosition = pos;
                        break;
                    }
                case TweenType.Rotation:
                    ((Transform)data.target).rotation = Quaternion.SlerpUnclamped(data.startQuaternion, data.endQuaternion, t);
                    break;
                case TweenType.LocalRotation:
                    ((Transform)data.target).localRotation = Quaternion.SlerpUnclamped(data.startQuaternion, data.endQuaternion, t);
                    break;
                case TweenType.Scale:
                    ((Transform)data.target).localScale = Vector3.LerpUnclamped(data.startVector3, data.endVector3, t);
                    break;
                case TweenType.ScaleX:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 scale = trans.localScale;
                        scale.x = Mathf.LerpUnclamped(data.startFloat, data.endVector3.x, t);
                        trans.localScale = scale;
                        break;
                    }
                case TweenType.ScaleY:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 scale = trans.localScale;
                        scale.y = Mathf.LerpUnclamped(data.startFloat, data.endVector3.y, t);
                        trans.localScale = scale;
                        break;
                    }
                case TweenType.ScaleZ:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 scale = trans.localScale;
                        scale.z = Mathf.LerpUnclamped(data.startFloat, data.endVector3.z, t);
                        trans.localScale = scale;
                        break;
                    }
                case TweenType.Jump:
                    {
                        Transform trans = (Transform)data.target;
                        Vector3 horizontalPos = Vector3.LerpUnclamped(data.startVector3, data.endVector3, t);
                        float jumpOffset = data.jumpPower * Mathf.Sin(Mathf.PI * t * data.numJumps);
                        trans.position = new Vector3(horizontalPos.x, horizontalPos.y + jumpOffset, horizontalPos.z);
                        break;
                    }
                case TweenType.MaterialColor:
                    ((Material)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.MaterialAlpha:
                    {
                        Material mat = (Material)data.target;
                        Color col = mat.color;
                        col.a = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        mat.color = col;
                        break;
                    }
                case TweenType.SpriteColor:
                    ((SpriteRenderer)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.SpriteAlpha:
                    {
                        SpriteRenderer sprite = (SpriteRenderer)data.target;
                        Color col = sprite.color;
                        col.a = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        sprite.color = col;
                        break;
                    }
                case TweenType.ImageColor:
                    ((Image)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.ImageAlpha:
                    {
                        Image image = (Image)data.target;
                        Color col = image.color;
                        col.a = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        image.color = col;
                        break;
                    }
                case TweenType.TextColor:
                    ((Text)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.TextAlpha:
                    {
                        Text text = (Text)data.target;
                        Color col = text.color;
                        col.a = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        text.color = col;
                        break;
                    }
                case TweenType.TMPColor:
                    ((TMP_Text)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.TMPAlpha:
                    {
                        TMP_Text tmp = (TMP_Text)data.target;
                        Color col = tmp.color;
                        col.a = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        tmp.color = col;
                        break;
                    }
                case TweenType.CanvasAlpha:
                    ((CanvasGroup)data.target).alpha = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.UIMove:
                    ((RectTransform)data.target).anchoredPosition = Vector2.LerpUnclamped(data.startVector2, data.endVector2, t);
                    break;
                case TweenType.UIMoveX:
                    {
                        RectTransform rect = (RectTransform)data.target;
                        Vector2 pos = rect.anchoredPosition;
                        pos.x = Mathf.LerpUnclamped(data.startFloat, data.endVector2.x, t);
                        rect.anchoredPosition = pos;
                        break;
                    }
                case TweenType.UIMoveY:
                    {
                        RectTransform rect = (RectTransform)data.target;
                        Vector2 pos = rect.anchoredPosition;
                        pos.y = Mathf.LerpUnclamped(data.startFloat, data.endVector2.y, t);
                        rect.anchoredPosition = pos;
                        break;
                    }
                case TweenType.SizeDelta:
                    ((RectTransform)data.target).sizeDelta = Vector2.LerpUnclamped(data.startVector2, data.endVector2, t);
                    break;
                case TweenType.CameraFOV:
                    ((Camera)data.target).fieldOfView = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.CameraOrthoSize:
                    ((Camera)data.target).orthographicSize = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.AudioVolume:
                    ((AudioSource)data.target).volume = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.AudioPitch:
                    ((AudioSource)data.target).pitch = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.LightIntensity:
                    ((Light)data.target).intensity = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                    break;
                case TweenType.LightColor:
                    ((Light)data.target).color = Color.LerpUnclamped(data.startColor, data.endColor, t);
                    break;
                case TweenType.CustomValue:
                    {
                        float value = Mathf.LerpUnclamped(data.startFloat, data.endFloat, t);
                        data.valueUpdateCallback?.Invoke(value);
                        break;
                    }
            }
        }

        bool IsAxisSpecificTween(TweenType type)
        {
            return type == TweenType.MoveX || type == TweenType.MoveY || type == TweenType.MoveZ ||
                   type == TweenType.LocalMoveX || type == TweenType.LocalMoveY || type == TweenType.LocalMoveZ ||
                   type == TweenType.ScaleX || type == TweenType.ScaleY || type == TweenType.ScaleZ ||
                   type == TweenType.UIMoveX || type == TweenType.UIMoveY;
        }

        float GetAxisValue(Vector3 vec, TweenType type)
        {
            switch (type)
            {
                case TweenType.MoveX:
                case TweenType.LocalMoveX:
                case TweenType.ScaleX:
                    return vec.x;
                case TweenType.MoveY:
                case TweenType.LocalMoveY:
                case TweenType.ScaleY:
                    return vec.y;
                case TweenType.MoveZ:
                case TweenType.LocalMoveZ:
                case TweenType.ScaleZ:
                    return vec.z;
                default:
                    return 0f;
            }
        }

        float GetAxisValue(Vector2 vec, TweenType type)
        {
            switch (type)
            {
                case TweenType.UIMoveX:
                    return vec.x;
                case TweenType.UIMoveY:
                    return vec.y;
                default:
                    return 0f;
            }
        }

        Vector3 SetAxisValue(Vector3 vec, float value, TweenType type)
        {
            switch (type)
            {
                case TweenType.MoveX:
                case TweenType.LocalMoveX:
                case TweenType.ScaleX:
                    return new Vector3(value, vec.y, vec.z);
                case TweenType.MoveY:
                case TweenType.LocalMoveY:
                case TweenType.ScaleY:
                    return new Vector3(vec.x, value, vec.z);
                case TweenType.MoveZ:
                case TweenType.LocalMoveZ:
                case TweenType.ScaleZ:
                    return new Vector3(vec.x, vec.y, value);
                default:
                    return vec;
            }
        }

        Vector2 SetAxisValue(Vector2 vec, float value, TweenType type)
        {
            switch (type)
            {
                case TweenType.UIMoveX:
                    return new Vector2(value, vec.y);
                case TweenType.UIMoveY:
                    return new Vector2(vec.x, value);
                default:
                    return vec;
            }
        }

        Vector3 GetCurrentVector3(object target, TweenType type)
        {
            switch (type)
            {
                case TweenType.Move: return ((Transform)target).position;
                case TweenType.LocalMove: return ((Transform)target).localPosition;
                case TweenType.Scale: return ((Transform)target).localScale;
                case TweenType.Jump: return ((Transform)target).position;
                default: return Vector3.zero;
            }
        }

        Vector2 GetCurrentVector2(object target, TweenType type)
        {
            switch (type)
            {
                case TweenType.UIMove: return ((RectTransform)target).anchoredPosition;
                case TweenType.SizeDelta: return ((RectTransform)target).sizeDelta;
                default: return Vector2.zero;
            }
        }

        float GetCurrentFloat(object target, TweenType type)
        {
            switch (type)
            {
                case TweenType.MoveX: return ((Transform)target).position.x;
                case TweenType.MoveY: return ((Transform)target).position.y;
                case TweenType.MoveZ: return ((Transform)target).position.z;
                case TweenType.LocalMoveX: return ((Transform)target).localPosition.x;
                case TweenType.LocalMoveY: return ((Transform)target).localPosition.y;
                case TweenType.LocalMoveZ: return ((Transform)target).localPosition.z;
                case TweenType.ScaleX: return ((Transform)target).localScale.x;
                case TweenType.ScaleY: return ((Transform)target).localScale.y;
                case TweenType.ScaleZ: return ((Transform)target).localScale.z;
                case TweenType.MaterialAlpha: return ((Material)target).color.a;
                case TweenType.SpriteAlpha: return ((SpriteRenderer)target).color.a;
                case TweenType.ImageAlpha: return ((Image)target).color.a;
                case TweenType.TextAlpha: return ((Text)target).color.a;
                case TweenType.TMPAlpha: return ((TMP_Text)target).color.a;
                case TweenType.CanvasAlpha: return ((CanvasGroup)target).alpha;
                case TweenType.UIMoveX: return ((RectTransform)target).anchoredPosition.x;
                case TweenType.UIMoveY: return ((RectTransform)target).anchoredPosition.y;
                case TweenType.CameraFOV: return ((Camera)target).fieldOfView;
                case TweenType.CameraOrthoSize: return ((Camera)target).orthographicSize;
                case TweenType.AudioVolume: return ((AudioSource)target).volume;
                case TweenType.AudioPitch: return ((AudioSource)target).pitch;
                case TweenType.LightIntensity: return ((Light)target).intensity;
                default: return 0f;
            }
        }

        Color GetCurrentColor(object target, TweenType type)
        {
            switch (type)
            {
                case TweenType.MaterialColor: return ((Material)target).color;
                case TweenType.SpriteColor: return ((SpriteRenderer)target).color;
                case TweenType.ImageColor: return ((Image)target).color;
                case TweenType.TextColor: return ((Text)target).color;
                case TweenType.TMPColor: return ((TMP_Text)target).color;
                case TweenType.LightColor: return ((Light)target).color;
                default: return Color.white;
            }
        }

        Quaternion GetCurrentQuaternion(object target, TweenType type)
        {
            switch (type)
            {
                case TweenType.Rotation: return ((Transform)target).rotation;
                case TweenType.LocalRotation: return ((Transform)target).localRotation;
                default: return Quaternion.identity;
            }
        }

        public bool IsActive(int id)
        {
            return activeTweens.ContainsKey(id);
        }

        public void SetEase(int id, EaseType ease)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.ease = ease;
            }
        }

        public void SetEase(int id, AnimationCurve curve)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.customEaseCurve = curve;
            }
        }

        public void SetDelay(int id, float delay)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.delay = delay;
            }
        }

        public void SetRepeat(int id, int count, RepeatType type)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.repeatCount = count;
                data.repeatType = type;
            }
        }

        public void SetOnComplete(int id, Action callback)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.onComplete = callback;
            }
        }

        public void SetOnUpdate(int id, Action callback)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.onUpdate = callback;
            }
        }

        public void SetOnStart(int id, Action callback)
        {
            if (activeTweens.TryGetValue(id, out TweenData data))
            {
                data.onStart = callback;
            }
        }

        public void Kill(TweenHandle handle, bool complete)
        {
            if (activeTweens.TryGetValue(handle.id, out TweenData data))
            {
                if (complete)
                {
                    ApplyTween(data, 1f);
                    data.onComplete?.Invoke();
                }
                activeTweens.Remove(handle.id);
                TweenDataPool.Release(data);
            }
        }

        public void Kill(object target, bool complete)
        {
            if (target == null) return;

            tweenIds.Clear();
            foreach (var pair in activeTweens)
            {
                if (pair.Value.target == target)
                {
                    tweenIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < tweenIds.Count; i++)
            {
                int id = tweenIds[i];
                if (activeTweens.TryGetValue(id, out TweenData data))
                {
                    if (complete)
                    {
                        ApplyTween(data, 1f);
                        data.onComplete?.Invoke();
                    }
                    activeTweens.Remove(id);
                    TweenDataPool.Release(data);
                }
            }
        }

        public void KillAll()
        {
            foreach (var pair in activeTweens)
            {
                TweenDataPool.Release(pair.Value);
            }
            activeTweens.Clear();
        }

        public void Pause(TweenHandle handle)
        {
            if (activeTweens.TryGetValue(handle.id, out TweenData data))
            {
                data.isPaused = true;
            }
        }

        public void Resume(TweenHandle handle)
        {
            if (activeTweens.TryGetValue(handle.id, out TweenData data))
            {
                data.isPaused = false;
            }
        }

        public void PauseAll()
        {
            tweenIds.Clear();
            foreach (var pair in activeTweens)
            {
                tweenIds.Add(pair.Key);
            }

            for (int i = 0; i < tweenIds.Count; i++)
            {
                if (activeTweens.TryGetValue(tweenIds[i], out TweenData data))
                {
                    data.isPaused = true;
                }
            }
        }

        public void ResumeAll()
        {
            tweenIds.Clear();
            foreach (var pair in activeTweens)
            {
                tweenIds.Add(pair.Key);
            }

            for (int i = 0; i < tweenIds.Count; i++)
            {
                if (activeTweens.TryGetValue(tweenIds[i], out TweenData data))
                {
                    data.isPaused = false;
                }
            }
        }

        public int GetActiveTweenCount()
        {
            return activeTweens.Count;
        }
    }
}
