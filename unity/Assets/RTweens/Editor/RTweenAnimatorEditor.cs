#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using RG.Tweening;
using System.Collections.Generic;

namespace RG.Tweening.Editor
{
    [CustomEditor(typeof(RTweenAnimator))]
    public class RTweenAnimatorEditor : UnityEditor.Editor
    {
        private SerializedProperty animationsProperty;
        private SerializedProperty useSequenceProperty;
        private SerializedProperty autoPlayProperty;

        private static readonly Dictionary<string, Dictionary<int, bool>> foldoutStatesPerObject = new Dictionary<string, Dictionary<int, bool>>();
        private Dictionary<int, bool> foldoutStates;

        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle boxStyle;
        private GUIStyle buttonStyle;
        private GUIStyle miniButtonStyle;
        private GUIStyle foldoutStyle;
        
        private static readonly Color primaryColor = new Color(0.12f, 0.58f, 0.95f); // Sleek modern blue
        private Color bgColor;
        private Color lightBgColor;

        [InitializeOnLoadMethod]
        private static void MonitorPlayMode()
        {
            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                {
                    foldoutStatesPerObject.Clear();
                }
            };
        }

        private void OnEnable()
        {
            animationsProperty = serializedObject.FindProperty("animations");
            useSequenceProperty = serializedObject.FindProperty("useSequence");
            autoPlayProperty = serializedObject.FindProperty("autoPlay");

            string objectKey = target.GetInstanceID().ToString();

            if (!foldoutStatesPerObject.ContainsKey(objectKey))
            {
                foldoutStatesPerObject[objectKey] = new Dictionary<int, bool>();
            }
            foldoutStates = foldoutStatesPerObject[objectKey];
        }

        private void InitializeStyles()
        {
            bgColor = EditorGUIUtility.isProSkin ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.92f, 0.92f, 0.92f);
            lightBgColor = EditorGUIUtility.isProSkin ? new Color(0.24f, 0.24f, 0.24f) : new Color(0.96f, 0.96f, 0.96f);

            if (headerStyle != null) return;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = Color.white;

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                padding = new RectOffset(2, 2, 2, 2)
            };
            subHeaderStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.85f, 0.85f) : new Color(0.15f, 0.15f, 0.15f);

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 4, 4)
            };

            // Modern flat look for custom box
            Texture2D boxBg = new Texture2D(1, 1);
            boxBg.SetPixel(0, 0, EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.88f, 0.88f, 0.88f));
            boxBg.Apply();
            boxStyle.normal.background = boxBg;
            boxStyle.border = new RectOffset(1, 1, 1, 1);

            buttonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                fixedHeight = 22
            };

            miniButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontStyle = FontStyle.Normal,
                fontSize = 10,
                fixedHeight = 18
            };

            foldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            InitializeStyles();

            DrawCustomHeader();
            EditorGUILayout.Space(5);
            DrawGeneralSettings();
            EditorGUILayout.Space(5);
            DrawAnimationsList();

            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                DrawRuntimeControls();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCustomHeader()
        {
            Rect headerRect = GUILayoutUtility.GetRect(1, 40);
            EditorGUI.DrawRect(headerRect, new Color(0.12f, 0.12f, 0.12f)); // Dark background
            
            // Left primary accent bar
            Rect borderRect = new Rect(headerRect.x, headerRect.y, 4, headerRect.height);
            EditorGUI.DrawRect(borderRect, primaryColor);

            // Title
            Rect titleRect = new Rect(headerRect.x + 12, headerRect.y + 6, 200, 18);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = Color.white;
            GUI.Label(titleRect, "⚡ RTWEEN ANIMATOR", titleStyle);

            // Subtitle
            Rect subtitleRect = new Rect(headerRect.x + 12, headerRect.y + 22, 250, 14);
            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft
            };
            subtitleStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(subtitleRect, "HIGH PERFORMANCE TWEENING SYSTEM", subtitleStyle);

            // Version tag at the right
            Rect versionRect = new Rect(headerRect.xMax - 65, headerRect.y + 12, 55, 16);
            GUIStyle versionStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 8,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            versionStyle.normal.textColor = primaryColor;
            
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
            GUI.Box(versionRect, "v2.0.1", versionStyle);
            GUI.backgroundColor = originalBg;
        }

        private void DrawGeneralSettings()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.LabelField("⚙ General Settings", subHeaderStyle);
            EditorGUILayout.Space(4);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(autoPlayProperty, new GUIContent("Auto Play"));
            EditorGUILayout.PropertyField(useSequenceProperty, new GUIContent("Use Sequence", "Play animations sequentially"));
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationsList()
        {
            EditorGUILayout.BeginVertical(boxStyle);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"🎬 Animations ({animationsProperty.arraySize})", subHeaderStyle);
            GUILayout.FlexibleSpace();

            GUI.color = new Color(0.35f, 0.85f, 0.35f);
            if (GUILayout.Button("+ Add Animation", miniButtonStyle, GUILayout.Width(110)))
            {
                AddNewAnimation();
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            if (animationsProperty.arraySize == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("No animations yet. Click '+ Add Animation' to create one!", MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space(5);
                for (int i = 0; i < animationsProperty.arraySize; i++)
                {
                    DrawAnimationElement(i);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawAnimationElement(int index)
        {
            SerializedProperty animProp = animationsProperty.GetArrayElementAtIndex(index);
            SerializedProperty enabledProp = animProp.FindPropertyRelative("enabled");
            SerializedProperty nameProp = animProp.FindPropertyRelative("name");
            SerializedProperty animTypeProp = animProp.FindPropertyRelative("animationType");

            if (!foldoutStates.ContainsKey(index))
            {
                foldoutStates[index] = false;
            }

            var animType = (RTweenAnimator.AnimationType)animTypeProp.enumValueIndex;
            Color backgroundColor = index % 2 == 0 ? bgColor : lightBgColor;
            Color categoryColor = GetAnimationCategoryColor(animType);

            Rect cardRect = EditorGUILayout.BeginVertical();
            
            // Draw card background, accent side-border and separator border
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(cardRect, backgroundColor);
                
                // Color-coded category side accent border
                Rect leftAccentRect = new Rect(cardRect.x, cardRect.y, 4, cardRect.height);
                EditorGUI.DrawRect(leftAccentRect, categoryColor);
                
                Rect bottomLine = new Rect(cardRect.x, cardRect.yMax - 1, cardRect.width, 1);
                EditorGUI.DrawRect(bottomLine, EditorGUIUtility.isProSkin ? new Color(0.15f, 0.15f, 0.15f) : new Color(0.8f, 0.8f, 0.8f));
            }

            EditorGUILayout.Space(4);

            // Header Row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);

            // Enable Toggle
            enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(16));

            // Foldout header
            string displayName = animTypeProp.enumDisplayNames[animTypeProp.enumValueIndex];
            string icon = GetAnimationIcon(animType);
            string cardTitle = string.IsNullOrEmpty(nameProp.stringValue) ? $"{icon} {displayName}" : $"{icon} {nameProp.stringValue} ({displayName})";
            
            foldoutStates[index] = EditorGUILayout.Foldout(foldoutStates[index], cardTitle, true, foldoutStyle);

            GUILayout.FlexibleSpace();

            // Play button in play mode
            if (Application.isPlaying && enabledProp.boolValue)
            {
                GUI.color = new Color(0.35f, 0.85f, 0.35f);
                if (GUILayout.Button("▶ Play", miniButtonStyle, GUILayout.Width(45)))
                {
                    ((RTweenAnimator)target).PlayAnimation(index);
                }
                GUI.color = Color.white;
                GUILayout.Space(2);
            }

            // Duplicate button
            if (GUILayout.Button("❐ Dup", miniButtonStyle, GUILayout.Width(45)))
            {
                DuplicateAnimation(index);
            }
            GUILayout.Space(2);

            // Delete button
            GUI.color = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("🗑 Remove", miniButtonStyle, GUILayout.Width(65)))
            {
                if (EditorUtility.DisplayDialog("Delete Animation", "Delete this animation?", "Delete", "Cancel"))
                {
                    animationsProperty.DeleteArrayElementAtIndex(index);
                    foldoutStates.Remove(index);
                    serializedObject.ApplyModifiedProperties();
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    return;
                }
            }
            GUI.color = Color.white;
            GUILayout.Space(5);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4);

            // Content inside foldout
            if (foldoutStates[index])
            {
                EditorGUILayout.Space(2);
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginVertical(boxStyle);

                // Quick Name field
                EditorGUILayout.PropertyField(nameProp, new GUIContent("Animation Name"));

                // Animation Type & Targets
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(animTypeProp, new GUIContent("Type"));
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("playMode"), new GUIContent("Play On"));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                DrawSeparator("⏱ Timing & Easing");

                // Timing side-by-side
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("duration"));
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("delay"));
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("easeType"), new GUIContent("Ease"));

                EditorGUILayout.Space(5);
                DrawSeparator("🔁 Repeat Settings");

                // Repeat side-by-side
                EditorGUILayout.BeginHorizontal();
                SerializedProperty repeatCountProp = animProp.FindPropertyRelative("repeatCount");
                int newRepeatCount = EditorGUILayout.IntField(new GUIContent("Repeat Count", "0 = no repeat, -1 = infinite"), repeatCountProp.intValue);
                if (newRepeatCount < -1) newRepeatCount = -1;
                repeatCountProp.intValue = newRepeatCount;

                if (repeatCountProp.intValue != 0)
                {
                    EditorGUILayout.PropertyField(animProp.FindPropertyRelative("repeatType"), new GUIContent("Type"));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);
                DrawSeparator("🎯 Target Settings");
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("customTarget"),
                    new GUIContent("Custom Target", "Leave empty to use this GameObject"));

                EditorGUILayout.Space(5);
                DrawSeparator("📊 Value Settings");
                SerializedProperty useFromValueProp = animProp.FindPropertyRelative("useFromValue");
                
                EditorGUILayout.BeginHorizontal();
                useFromValueProp.boolValue = EditorGUILayout.ToggleLeft("Use Start/From value manually", useFromValueProp.boolValue);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(2);

                DrawValueFields(animType, animProp, useFromValueProp.boolValue);

                EditorGUILayout.Space(5);
                DrawSeparator("⚡ Unity Events");
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("onStart"));
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("onUpdate"));
                EditorGUILayout.PropertyField(animProp.FindPropertyRelative("onComplete"));

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        private void DrawValueFields(RTweenAnimator.AnimationType animType, SerializedProperty animProp, bool useFromValue)
        {
            switch (animType)
            {
                case RTweenAnimator.AnimationType.Move:
                case RTweenAnimator.AnimationType.LocalMove:
                case RTweenAnimator.AnimationType.Scale:
                case RTweenAnimator.AnimationType.Jump:
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromVector3Value"), new GUIContent("From"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("vector3Value"), new GUIContent("To"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("vector3Value"), new GUIContent("To"));
                    }

                    if (animType == RTweenAnimator.AnimationType.Jump)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("jumpPower"), new GUIContent("Jump Power"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("numJumps"), new GUIContent("Num Jumps"));
                        EditorGUILayout.EndHorizontal();
                    }
                    break;

                case RTweenAnimator.AnimationType.UIMove:
                case RTweenAnimator.AnimationType.SizeDelta:
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromVector2Value"), new GUIContent("From"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("vector2Value"), new GUIContent("To"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("vector2Value"), new GUIContent("To"));
                    }
                    break;

                case RTweenAnimator.AnimationType.MoveX:
                case RTweenAnimator.AnimationType.MoveY:
                case RTweenAnimator.AnimationType.MoveZ:
                case RTweenAnimator.AnimationType.LocalMoveX:
                case RTweenAnimator.AnimationType.LocalMoveY:
                case RTweenAnimator.AnimationType.LocalMoveZ:
                case RTweenAnimator.AnimationType.ScaleX:
                case RTweenAnimator.AnimationType.ScaleY:
                case RTweenAnimator.AnimationType.ScaleZ:
                case RTweenAnimator.AnimationType.UIMoveX:
                case RTweenAnimator.AnimationType.UIMoveY:
                case RTweenAnimator.AnimationType.CanvasGroupAlpha:
                case RTweenAnimator.AnimationType.CameraFOV:
                case RTweenAnimator.AnimationType.CameraOrthoSize:
                case RTweenAnimator.AnimationType.AudioVolume:
                case RTweenAnimator.AnimationType.AudioPitch:
                case RTweenAnimator.AnimationType.LightIntensity:
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromFloatValue"), new GUIContent("From"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("floatValue"), new GUIContent("To"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("floatValue"), new GUIContent("To"));
                    }
                    break;

                case RTweenAnimator.AnimationType.Color:
                case RTweenAnimator.AnimationType.MaterialColor:
                case RTweenAnimator.AnimationType.LightColor:
                    EditorGUILayout.HelpBox("Auto-detects: Image, Text, TextMeshPro, SpriteRenderer, Material", MessageType.None);
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromColorValue"), new GUIContent("From"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("colorValue"), new GUIContent("To"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("colorValue"), new GUIContent("To"));
                    }
                    break;

                case RTweenAnimator.AnimationType.Alpha:
                case RTweenAnimator.AnimationType.MaterialAlpha:
                    EditorGUILayout.HelpBox("Auto-detects: Image, Text, TextMeshPro, SpriteRenderer, Material, CanvasGroup", MessageType.None);
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromFloatValue"), new GUIContent("From"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("floatValue"), new GUIContent("To"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("floatValue"), new GUIContent("To"));
                    }
                    break;

                case RTweenAnimator.AnimationType.Rotation:
                case RTweenAnimator.AnimationType.LocalRotation:
                case RTweenAnimator.AnimationType.RotationEuler:
                case RTweenAnimator.AnimationType.LocalRotationEuler:
                    if (useFromValue)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("fromRotationValue"), new GUIContent("From (Euler)"));
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("rotationValue"), new GUIContent("To (Euler)"));
                        EditorGUILayout.EndHorizontal();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(animProp.FindPropertyRelative("rotationValue"), new GUIContent("To (Euler)"));
                    }
                    break;
            }
        }

        private void DrawRuntimeControls()
        {
            EditorGUILayout.BeginVertical(boxStyle);
            EditorGUILayout.LabelField("🎮 Runtime Controls", subHeaderStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            GUI.color = new Color(0.4f, 0.9f, 0.4f);
            if (GUILayout.Button("▶ Play All", buttonStyle))
            {
                ((RTweenAnimator)target).Play();
            }

            GUI.color = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("■ Stop All", buttonStyle))
            {
                ((RTweenAnimator)target).Stop();
            }

            GUI.color = new Color(1f, 0.75f, 0.2f);
            if (GUILayout.Button("❚❚ Pause All", buttonStyle))
            {
                ((RTweenAnimator)target).Pause();
            }

            GUI.color = new Color(0.35f, 0.75f, 1f);
            if (GUILayout.Button("▶ Resume All", buttonStyle))
            {
                ((RTweenAnimator)target).Resume();
            }

            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void AddNewAnimation()
        {
            animationsProperty.arraySize++;
            SerializedProperty newAnim = animationsProperty.GetArrayElementAtIndex(animationsProperty.arraySize - 1);

            newAnim.FindPropertyRelative("enabled").boolValue = true;
            newAnim.FindPropertyRelative("animationType").enumValueIndex = 0;
            newAnim.FindPropertyRelative("playMode").enumValueIndex = 0;
            newAnim.FindPropertyRelative("duration").floatValue = 1f;
            newAnim.FindPropertyRelative("delay").floatValue = 0f;
            newAnim.FindPropertyRelative("easeType").enumValueIndex = 5;
            newAnim.FindPropertyRelative("repeatCount").intValue = 0;
            newAnim.FindPropertyRelative("repeatType").enumValueIndex = 0;
            newAnim.FindPropertyRelative("useFromValue").boolValue = false;
            newAnim.FindPropertyRelative("jumpPower").floatValue = 1f;
            newAnim.FindPropertyRelative("numJumps").intValue = 1;
            newAnim.FindPropertyRelative("customTarget").objectReferenceValue = null;

            foldoutStates[animationsProperty.arraySize - 1] = true;
            serializedObject.ApplyModifiedProperties();
        }

        private void DuplicateAnimation(int index)
        {
            animationsProperty.InsertArrayElementAtIndex(index);

            Dictionary<int, bool> newFoldoutStates = new Dictionary<int, bool>();
            foreach (var kvp in foldoutStates)
            {
                if (kvp.Key > index)
                    newFoldoutStates[kvp.Key + 1] = kvp.Value;
                else
                    newFoldoutStates[kvp.Key] = kvp.Value;
            }
            newFoldoutStates[index + 1] = true;
            foldoutStates.Clear();
            foreach (var kvp in newFoldoutStates)
            {
                foldoutStates[kvp.Key] = kvp.Value;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSeparator(string label)
        {
            EditorGUILayout.Space(2);
            Rect rect = GUILayoutUtility.GetRect(1, 18);
            
            // Draw label
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 16), label, EditorStyles.boldLabel);
            
            // Underline
            Rect lineRect = new Rect(rect.x, rect.y + 16, rect.width, 1);
            EditorGUI.DrawRect(lineRect, EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.75f, 0.75f, 0.75f));
            EditorGUILayout.Space(2);
        }

        private Color GetAnimationCategoryColor(RTweenAnimator.AnimationType type)
        {
            switch (type)
            {
                case RTweenAnimator.AnimationType.Move:
                case RTweenAnimator.AnimationType.MoveX:
                case RTweenAnimator.AnimationType.MoveY:
                case RTweenAnimator.AnimationType.MoveZ:
                case RTweenAnimator.AnimationType.LocalMove:
                case RTweenAnimator.AnimationType.LocalMoveX:
                case RTweenAnimator.AnimationType.LocalMoveY:
                case RTweenAnimator.AnimationType.LocalMoveZ:
                case RTweenAnimator.AnimationType.Rotation:
                case RTweenAnimator.AnimationType.LocalRotation:
                case RTweenAnimator.AnimationType.RotationEuler:
                case RTweenAnimator.AnimationType.LocalRotationEuler:
                case RTweenAnimator.AnimationType.Scale:
                case RTweenAnimator.AnimationType.ScaleX:
                case RTweenAnimator.AnimationType.ScaleY:
                case RTweenAnimator.AnimationType.ScaleZ:
                case RTweenAnimator.AnimationType.Jump:
                    return new Color(0.13f, 0.59f, 0.95f); // Beautiful Blue

                case RTweenAnimator.AnimationType.UIMove:
                case RTweenAnimator.AnimationType.UIMoveX:
                case RTweenAnimator.AnimationType.UIMoveY:
                case RTweenAnimator.AnimationType.SizeDelta:
                case RTweenAnimator.AnimationType.CanvasGroupAlpha:
                    return new Color(0f, 0.59f, 0.53f); // Cool Teal

                case RTweenAnimator.AnimationType.Color:
                case RTweenAnimator.AnimationType.Alpha:
                case RTweenAnimator.AnimationType.MaterialColor:
                case RTweenAnimator.AnimationType.MaterialAlpha:
                    return new Color(0.61f, 0.15f, 0.69f); // Elegant Purple

                case RTweenAnimator.AnimationType.CameraFOV:
                case RTweenAnimator.AnimationType.CameraOrthoSize:
                case RTweenAnimator.AnimationType.AudioVolume:
                case RTweenAnimator.AnimationType.AudioPitch:
                case RTweenAnimator.AnimationType.LightIntensity:
                case RTweenAnimator.AnimationType.LightColor:
                    return new Color(1f, 0.6f, 0f); // Warm Orange

                default:
                    return Color.grey;
            }
        }

        private string GetAnimationIcon(RTweenAnimator.AnimationType type)
        {
            return type switch
            {
                RTweenAnimator.AnimationType.Move or RTweenAnimator.AnimationType.MoveX or RTweenAnimator.AnimationType.MoveY or RTweenAnimator.AnimationType.MoveZ or RTweenAnimator.AnimationType.LocalMove or RTweenAnimator.AnimationType.LocalMoveX or RTweenAnimator.AnimationType.LocalMoveY or RTweenAnimator.AnimationType.LocalMoveZ or RTweenAnimator.AnimationType.UIMove or RTweenAnimator.AnimationType.UIMoveX or RTweenAnimator.AnimationType.UIMoveY => "→",
                RTweenAnimator.AnimationType.Rotation or RTweenAnimator.AnimationType.LocalRotation or RTweenAnimator.AnimationType.RotationEuler or RTweenAnimator.AnimationType.LocalRotationEuler => "↻",
                RTweenAnimator.AnimationType.Scale or RTweenAnimator.AnimationType.ScaleX or RTweenAnimator.AnimationType.ScaleY or RTweenAnimator.AnimationType.ScaleZ or RTweenAnimator.AnimationType.SizeDelta => "⤢",
                RTweenAnimator.AnimationType.Jump => "⤊",
                RTweenAnimator.AnimationType.Color or RTweenAnimator.AnimationType.MaterialColor or RTweenAnimator.AnimationType.LightColor => "🎨",
                RTweenAnimator.AnimationType.Alpha or RTweenAnimator.AnimationType.MaterialAlpha or RTweenAnimator.AnimationType.CanvasGroupAlpha => "◐",
                RTweenAnimator.AnimationType.CameraFOV or RTweenAnimator.AnimationType.CameraOrthoSize => "📷",
                RTweenAnimator.AnimationType.AudioVolume or RTweenAnimator.AnimationType.AudioPitch => "🔊",
                RTweenAnimator.AnimationType.LightIntensity => "💡",
                _ => "◆",
            };
        }
    }
}
#endif
