#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using RG.Tweening;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RG.Tweening.Editor
{
    public class RTweenDebugger : EditorWindow
    {
        private Vector2 scrollPosition;
        private bool autoRefresh = true;
        private double lastRefreshTime;
        private const double REFRESH_INTERVAL = 0.1;

        private GUIStyle headerStyle;
        private GUIStyle itemStyle;
        private GUIStyle warningStyle;
        private static readonly Color primaryColor = new Color(0.25f, 0.6f, 1f);

        [MenuItem("Window/RTween/Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<RTweenDebugger>("RTween Debugger");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > REFRESH_INTERVAL)
            {
                lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void InitializeStyles()
        {
            if (headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 10, 10)
            };
            headerStyle.normal.textColor = primaryColor;

            itemStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 5, 5),
                margin = new RectOffset(5, 5, 2, 2)
            };

            warningStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            InitializeStyles();

            // Header
            EditorGUILayout.BeginVertical();
            GUILayout.Label("🔍 RTween Debugger", headerStyle);

            Rect lineRect = GUILayoutUtility.GetRect(1, 2);
            EditorGUI.DrawRect(lineRect, primaryColor);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to see active tweens", MessageType.Info);
                return;
            }

            // Controls
            EditorGUILayout.BeginHorizontal();
            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);

            if (GUILayout.Button("Refresh Now", GUILayout.Width(100)))
            {
                Repaint();
            }

            if (GUILayout.Button("Kill All Tweens", GUILayout.Width(120)))
            {
                if (EditorUtility.DisplayDialog("Kill All Tweens",
                    "Are you sure you want to kill all active tweens?", "Yes", "Cancel"))
                {
                    RTween.KillAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Get tween data using reflection
            var tweenData = GetActiveTweenData();
            int activeTweenCount = tweenData.Count;

            // Statistics
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("📊 Statistics", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Active Tweens: {activeTweenCount}");
            EditorGUILayout.LabelField($"Paused Tweens: {tweenData.Count(t => t.isPaused)}");

            // Warning for high tween count
            if (activeTweenCount > 50 && activeTweenCount <= 100)
            {
                EditorGUILayout.HelpBox($"⚠️ High tween count ({activeTweenCount})! This may impact performance.", MessageType.Warning);
            }
            else if (activeTweenCount > 100)
            {
                EditorGUILayout.HelpBox($"🔴 CRITICAL: {activeTweenCount} tweens active! Severe performance impact expected!", MessageType.Error);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Tween list
            EditorGUILayout.LabelField("🎬 Active Tweens", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (activeTweenCount == 0)
            {
                EditorGUILayout.HelpBox("No active tweens", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < tweenData.Count; i++)
                {
                    DrawTweenItem(tweenData[i], i);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTweenItem(TweenDebugInfo info, int index)
        {
            Color bgColor = info.isPaused ? new Color(1f, 0.7f, 0.3f, 0.3f) : new Color(0.2f, 0.2f, 0.2f, 0.5f);

            EditorGUILayout.BeginVertical(itemStyle);

            // Draw background
            if (Event.current.type == EventType.Repaint)
            {
                Rect rect = GUILayoutUtility.GetLastRect();
                EditorGUI.DrawRect(rect, bgColor);
            }

            // Header
            EditorGUILayout.BeginHorizontal();

            string statusIcon = info.isPaused ? "❚❚" : "▶";
            string repeatInfo = info.repeatCount == -1 ? "∞" :
                               info.repeatCount > 0 ? $"×{info.currentRepeat}/{info.repeatCount}" : "";

            EditorGUILayout.LabelField($"{statusIcon} {GetTypeIcon(info.typeName)} {info.typeName}", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(repeatInfo))
            {
                GUILayout.Label(repeatInfo, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();

            // Target info
            if (info.target != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Target:", GUILayout.Width(60));
                EditorGUILayout.ObjectField(info.target as UnityEngine.Object, typeof(UnityEngine.Object), true);
                EditorGUILayout.EndHorizontal();
            }

            // Progress bar
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Progress:", GUILayout.Width(60));

            float progress = info.duration > 0 ? Mathf.Clamp01(info.elapsed / info.duration) : 0;
            Rect progressRect = GUILayoutUtility.GetRect(100, 18, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, progress, $"{(progress * 100):F1}%");

            EditorGUILayout.LabelField($"{info.elapsed:F2}s / {info.duration:F2}s", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Details
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Ease: {info.ease}", GUILayout.Width(150));
            EditorGUILayout.LabelField($"Delay: {info.delay:F2}s", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            // Values
            if (info.startValue != null && info.endValue != null)
            {
                EditorGUILayout.LabelField($"From: {FormatValue(info.startValue)} → To: {FormatValue(info.endValue)}");
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private List<TweenDebugInfo> GetActiveTweenData()
        {
            var debugInfoList = new List<TweenDebugInfo>();

            // Find TweenEngine instance using reflection
            var engineType = System.Type.GetType("RGames.Tweening.TweenEngine, Assembly-CSharp");
            if (engineType == null) return debugInfoList;

            var instanceProperty = engineType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProperty == null) return debugInfoList;

            var engineInstance = instanceProperty.GetValue(null);
            if (engineInstance == null) return debugInfoList;

            // Get activeTweens dictionary
            var activeTweensField = engineType.GetField("activeTweens", BindingFlags.NonPublic | BindingFlags.Instance);
            if (activeTweensField == null) return debugInfoList;

            var activeTweens = activeTweensField.GetValue(engineInstance) as System.Collections.IDictionary;
            if (activeTweens == null) return debugInfoList;

            // Get TweenData type
            var tweenDataType = System.Type.GetType("RGames.Tweening.TweenData, Assembly-CSharp");
            if (tweenDataType == null) return debugInfoList;

            foreach (System.Collections.DictionaryEntry entry in activeTweens)
            {
                var data = entry.Value;
                var debugInfo = new TweenDebugInfo
                {
                    id = (int)entry.Key,
                    target = GetFieldValue(data, "target"),
                    typeName = GetFieldValue(data, "type")?.ToString() ?? "Unknown",
                    duration = GetFieldValue<float>(data, "duration"),
                    elapsed = GetFieldValue<float>(data, "elapsed"),
                    delay = GetFieldValue<float>(data, "delay"),
                    ease = GetFieldValue<EaseType>(data, "ease"),
                    repeatCount = GetFieldValue<int>(data, "repeatCount"),
                    currentRepeat = GetFieldValue<int>(data, "currentRepeat"),
                    repeatType = GetFieldValue<RepeatType>(data, "repeatType"),
                    isPaused = GetFieldValue<bool>(data, "isPaused"),
                    startValue = GetFieldValue(data, "startValue"),
                    endValue = GetFieldValue(data, "endValue")
                };

                debugInfoList.Add(debugInfo);
            }

            return debugInfoList;
        }

        private object GetFieldValue(object obj, string fieldName)
        {
            if (obj == null) return null;

            var field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(obj);
        }

        private T GetFieldValue<T>(object obj, string fieldName)
        {
            var value = GetFieldValue(obj, fieldName);
            if (value == null) return default(T);

            try
            {
                return (T)value;
            }
            catch
            {
                return default(T);
            }
        }

        private string GetTypeIcon(string typeName)
        {
            if (typeName.Contains("Move") || typeName.Contains("UIMove"))
                return "→";

            if (typeName.Contains("Rotation"))
                return "↻";

            if (typeName.Contains("Scale") || typeName.Contains("SizeDelta"))
                return "⤢";

            if (typeName.Contains("Jump"))
                return "⤊";

            if (typeName.Contains("Color"))
                return "🎨";

            if (typeName.Contains("Alpha"))
                return "◐";

            if (typeName.Contains("Sequence"))
                return "📜";

            if (typeName.Contains("CustomValue"))
                return "📊";

            if (typeName.Contains("DelayedCall"))
                return "⏰";

            return "◆";
        }

        private string FormatValue(object value)
        {
            if (value == null) return "null";

            if (value is Vector3 v3)
                return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";

            if (value is Vector2 v2)
                return $"({v2.x:F2}, {v2.y:F2})";

            if (value is Color c)
                return $"RGBA({c.r:F2}, {c.g:F2}, {c.b:F2}, {c.a:F2})";

            if (value is Quaternion q)
                return $"Quat({q.eulerAngles.x:F1}, {q.eulerAngles.y:F1}, {q.eulerAngles.z:F1})";

            if (value is float f)
                return f.ToString("F2");

            return value.ToString();
        }

        private class TweenDebugInfo
        {
            public int id;
            public object target;
            public string typeName;
            public float duration;
            public float elapsed;
            public float delay;
            public EaseType ease;
            public int repeatCount;
            public int currentRepeat;
            public RepeatType repeatType;
            public bool isPaused;
            public object startValue;
            public object endValue;
        }
    }
}
#endif