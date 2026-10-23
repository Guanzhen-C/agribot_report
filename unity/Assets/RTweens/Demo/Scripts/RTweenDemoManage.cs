using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RG.Tweening
{
    public class RTweenDemoManager : MonoBehaviour
    {
        [Header("Demo Objects")]
        [SerializeField] List<Transform> demoObjects = new List<Transform>();

        [Header("Demo Settings")]
        [SerializeField] Color[] objectColors = new Color[] { Color.red, Color.green, Color.blue, Color.yellow };

        [Header("UI")]
        [SerializeField] Canvas demoCanvas;
        [SerializeField] Font uiFont;

        Camera mainCamera;
        List<Material> allMaterials = new List<Material>();
        List<Vector3> initialPos = new();

        void Start()
        {
            mainCamera = Camera.main;
            CacheMaterials();
            CreateUIButtons();
        }

        void CacheMaterials()
        {
            allMaterials.Clear();

            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] == null) continue;

                Renderer renderer = demoObjects[i].GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    allMaterials.Add(renderer.material);
                }

                initialPos.Add(demoObjects[i].position);
            }
        }

        void CreateUIButtons()
        {
            int buttonWidth = 140;
            int buttonHeight = 35;
            int buttonSpacing = 10;
            int buttonsPerRow = 4;
            int currentRow = 0;
            int currentCol = 0;

            // Shake effects
            CreateSectionLabel("SHAKE EFFECTS", ref currentRow, ref currentCol);
            CreateButton("Shake Position", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_ShakePosition);
            CreateButton("Camera Shake", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_CameraShake);
            CreateButton("Shake Rotation", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_ShakeRotation);
            CreateButton("Shake Scale", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_ShakeScale);
            CreateButton("Impact Shake", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_ImpactShake);
            CreateButton("Wobble", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Wobble);

            // Punch effects
            CreateSectionLabel("PUNCH EFFECTS", ref currentRow, ref currentCol);
            CreateButton("Punch Position", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_PunchPosition);
            CreateButton("Punch Scale", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_PunchScale);
            CreateButton("Punch Rotation", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_PunchRotation);
            CreateButton("Hit Punch", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_HitPunch);
            CreateButton("Pop Effect", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_PopEffect);

            // Basic tweens
            CreateSectionLabel("BASIC TWEENS", ref currentRow, ref currentCol);
            CreateButton("Move", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Move);
            CreateButton("Scale", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Scale);
            CreateButton("Rotate", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Rotate);
            CreateButton("Color", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Color);
            CreateButton("Fade", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Fade);
            CreateButton("Yoyo Move", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_YoyoMove);

            // Sequences
            CreateSectionLabel("SEQUENCES", ref currentRow, ref currentCol);
            CreateButton("Basic Sequence", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Sequence);
            CreateButton("Parallel", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Parallel);
            CreateButton("Complex", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Complex);

            // Game examples
            CreateSectionLabel("GAME EXAMPLES", ref currentRow, ref currentCol);
            CreateButton("Take Damage", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_TakeDamage);
            CreateButton("Explosion", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Explosion);
            CreateButton("Button Press", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_ButtonPress);
            CreateButton("Pickup", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Pickup);
            CreateButton("Power Up", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_PowerUp);
            CreateButton("Landing", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, Demo_Landing);
        }

        void CreateSectionLabel(string text, ref int currentRow, ref int currentCol)
        {
            currentRow++;
            currentCol = 0;

            GameObject textObj = new GameObject(text);
            textObj.transform.SetParent(demoCanvas.transform, false);

            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 20;
            txt.color = Color.black;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontStyle = FontStyle.Bold;

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, -20 - (currentRow * 45));
            rt.sizeDelta = new Vector2(600, 30);

            currentRow++;
        }

        void CreateButton(string text, int width, int height, int spacing, int perRow, ref int currentRow, ref int currentCol, System.Action action)
        {
            GameObject buttonObj = new GameObject("Btn_" + text);
            buttonObj.transform.SetParent(demoCanvas.transform, false);

            RectTransform rt = buttonObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(20 + (currentCol * (width + spacing)), -20 - (currentRow * (height + spacing)));

            Image img = buttonObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f);

            Button btn = buttonObj.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.25f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.35f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.2f);
            btn.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 12;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() =>
            {
                ResetAllObjects();
                action.Invoke();
            });

            currentCol++;
            if (currentCol >= perRow)
            {
                currentCol = 0;
                currentRow++;
            }
        }

        void ResetAllObjects()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] == null) continue;

                demoObjects[i].position = initialPos[i];
                demoObjects[i].rotation = Quaternion.identity;
                demoObjects[i].localScale = Vector3.one;
            }

            for (int i = 0; i < allMaterials.Count && i < objectColors.Length; i++)
            {
                if (allMaterials[i] == null) continue;

                Color col = objectColors[i];
                col.a = 1f;
                allMaterials[i].color = col;
            }
        }

        // Shake demos
        void Demo_ShakePosition()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.ShakePosition(demoObjects[i], 0.5f, 0.5f, 20, 90f, true);
                }
            }
        }

        void Demo_CameraShake()
        {
            if (mainCamera != null)
            {
                RTweenEffects.ShakeCamera(mainCamera, 0.5f, 0.5f, 20, true);
            }
        }

        void Demo_ShakeRotation()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.ShakeRotation(demoObjects[i], 15f, 0.5f, 10, 90f, true);
                }
            }
        }

        void Demo_ShakeScale()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.ShakeScale(demoObjects[i], 0.2f, 0.5f, 10, 90f, true);
                }
            }
        }

        void Demo_ImpactShake()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.ImpactShake(demoObjects[i], 0.5f);
                }
            }
        }

        void Demo_Wobble()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.WobbleEffect(demoObjects[i], 15f, 0.5f);
                }
            }
        }

        // Punch demos
        void Demo_PunchPosition()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.PunchPosition(demoObjects[i], Vector3.up * 1f, 0.5f, 10, 1f);
                }
            }
        }

        void Demo_PunchScale()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.PunchScale(demoObjects[i], Vector3.one * 0.3f, 0.5f, 10, 1f);
                }
            }
        }

        void Demo_PunchRotation()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.PunchRotation(demoObjects[i], new Vector3(0, 0, 45f), 0.5f, 10, 1f);
                }
            }
        }

        void Demo_HitPunch()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.HitPunch(demoObjects[i], Vector3.down * 0.5f, 0.5f);
                }
            }
        }

        void Demo_PopEffect()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTweenEffects.PopEffect(demoObjects[i], 0.3f);
                }
            }
        }

        // Basic tween demos
        void Demo_Move()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTween.Move(demoObjects[i], demoObjects[i].position + Vector3.up * 2f, 1f)
                        .SetEase(EaseType.OutBounce);
                }
            }
        }

        void Demo_Scale()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTween.Scale(demoObjects[i], Vector3.one * 1.5f, 0.5f)
                        .SetEase(EaseType.OutElastic);
                }
            }
        }

        void Demo_Rotate()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                int index = i;
                if (demoObjects[index] != null)
                {
                    demoObjects[index].localRotation = Quaternion.identity;

                    RTween.Value(0f, 360f, 1f, (value) =>
                    {
                        if (index < demoObjects.Count && demoObjects[index] != null)
                        {
                            demoObjects[index].localRotation = Quaternion.Euler(0, value, 0);
                        }
                    }).SetEase(EaseType.InOutQuad);
                }
            }
        }

        void Demo_Color()
        {
            int matCount = allMaterials.Count;
            int colorCount = objectColors.Length;

            for (int i = 0; i < matCount; i++)
            {
                int materialIndex = i;
                if (allMaterials[materialIndex] == null) continue;

                Color targetColor = objectColors[(materialIndex + 1) % colorCount];
                Color originalColor = objectColors[materialIndex % colorCount];

                RTween.Color(allMaterials[materialIndex], targetColor, 0.5f)
                    .OnComplete(() =>
                    {
                        if (materialIndex < allMaterials.Count && allMaterials[materialIndex] != null)
                        {
                            RTween.Color(allMaterials[materialIndex], originalColor, 0.5f);
                        }
                    });
            }
        }

        void Demo_Fade()
        {
            int matCount = allMaterials.Count;

            for (int i = 0; i < matCount; i++)
            {
                int materialIndex = i;
                if (allMaterials[materialIndex] == null) continue;

                RTween.Alpha(allMaterials[materialIndex], 0f, 0.5f)
                    .OnComplete(() =>
                    {
                        if (materialIndex < allMaterials.Count && allMaterials[materialIndex] != null)
                        {
                            RTween.Alpha(allMaterials[materialIndex], 1f, 0.5f);
                        }
                    });
            }
        }

        void Demo_YoyoMove()
        {
            for (int i = 0; i < demoObjects.Count; i++)
            {
                if (demoObjects[i] != null)
                {
                    RTween.MoveY(demoObjects[i], demoObjects[i].position.y + 1.5f, 1f)
                        .SetRepeat(2, RepeatType.Yoyo)
                        .SetEase(EaseType.InOutQuad);
                }
            }
        }

        // Sequence demos
        void Demo_Sequence()
        {
            int objectCount = demoObjects.Count;
            Vector3[] startPositions = new Vector3[objectCount];

            for (int i = 0; i < objectCount; i++)
            {
                if (demoObjects[i] != null)
                {
                    startPositions[i] = demoObjects[i].position;
                }
            }

            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                Vector3 startPos = startPositions[index];

                RTween.DelayedCall(index * 0.1f, () =>
                {
                    if (index < demoObjects.Count && demoObjects[index] != null)
                    {
                        RTween.Sequence()
                            .Add(RTween.Move(demoObjects[index], startPos + Vector3.up * 1.5f, 0.5f))
                            .Add(RTween.Move(demoObjects[index], startPos + Vector3.right * 1f, 0.5f))
                            .Add(RTween.Move(demoObjects[index], startPos, 0.5f))
                            .Play();
                    }
                });
            }
        }

        void Demo_Parallel()
        {
            int objectCount = demoObjects.Count;
            Vector3[] startPositions = new Vector3[objectCount];

            for (int i = 0; i < objectCount; i++)
            {
                if (demoObjects[i] != null)
                {
                    startPositions[i] = demoObjects[i].position;
                }
            }

            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                Vector3 startPos = startPositions[index];

                RTween.DelayedCall(index * 0.1f, () =>
                {
                    if (index < demoObjects.Count && demoObjects[index] != null)
                    {
                        RTween.Sequence()
                            .Add(RTween.Move(demoObjects[index], startPos + Vector3.up * 1.5f, 1f))
                            .With(RTween.Scale(demoObjects[index], Vector3.one * 1.5f, 1f))
                            .Add(RTween.Move(demoObjects[index], startPos, 1f))
                            .With(RTween.Scale(demoObjects[index], Vector3.one, 1f))
                            .Play();
                    }
                });
            }
        }

        void Demo_Complex()
        {
            int objectCount = demoObjects.Count;
            Vector3[] startPositions = new Vector3[objectCount];

            for (int i = 0; i < objectCount; i++)
            {
                if (demoObjects[i] != null)
                {
                    startPositions[i] = demoObjects[i].position;
                }
            }

            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                Vector3 startPos = startPositions[index];

                RTween.DelayedCall(index * 0.15f, () =>
                {
                    if (index < demoObjects.Count && demoObjects[index] != null)
                    {
                        RTween.Sequence()
                            .Add(RTween.Scale(demoObjects[index], Vector3.one * 1.3f, 0.2f).SetEase(EaseType.OutQuad))
                            .With(RTween.LocalRotation(demoObjects[index], Quaternion.Euler(0, 180, 0), 0.2f))
                            .Add(RTween.Move(demoObjects[index], startPos + Vector3.up * 2f, 0.5f).SetEase(EaseType.OutQuad))
                            .Add(RTween.Move(demoObjects[index], startPos, 0.5f).SetEase(EaseType.InQuad))
                            .With(RTween.Scale(demoObjects[index], Vector3.one, 0.5f))
                            .With(RTween.LocalRotation(demoObjects[index], Quaternion.identity, 0.5f))
                            .Play();
                    }
                });
            }
        }

        // Game example demos
        void Demo_TakeDamage()
        {
            if (mainCamera != null)
            {
                RTweenEffects.ShakeCamera(mainCamera, 0.3f, 0.3f, 15, true);
            }

            int objectCount = demoObjects.Count;
            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                if (demoObjects[index] != null)
                {
                    RTweenEffects.HitPunch(demoObjects[index], Vector3.back * 0.3f, 0.3f);

                    if (index < allMaterials.Count && allMaterials[index] != null)
                    {
                        Material mat = allMaterials[index];
                        Color originalColor = mat.color;
                        RTween.Color(mat, Color.red, 0.1f)
                            .OnComplete(() =>
                            {
                                if (mat != null)
                                {
                                    RTween.Color(mat, originalColor, 0.1f);
                                }
                            });
                    }
                }
            }
        }

        void Demo_Explosion()
        {
            if (mainCamera != null)
            {
                RTweenEffects.ShakeCamera(mainCamera, 0.5f, 0.8f, 25, true);
            }

            int objectCount = demoObjects.Count;
            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                if (demoObjects[index] != null)
                {
                    demoObjects[index].localScale = Vector3.zero;

                    RTween.DelayedCall(index * 0.1f, () =>
                    {
                        if (index < demoObjects.Count && demoObjects[index] != null)
                        {
                            RTween.Scale(demoObjects[index], Vector3.one * 2f, 0.5f)
                                .SetEase(EaseType.OutQuad);
                        }
                    });
                }
            }
        }

        void Demo_ButtonPress()
        {
            int objectCount = demoObjects.Count;
            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                RTween.DelayedCall(index * 0.05f, () =>
                {
                    if (index < demoObjects.Count && demoObjects[index] != null)
                    {
                        RTween.Scale(demoObjects[index], Vector3.one * 0.7f, 0.1f)
                            .SetEase(EaseType.OutQuad)
                            .OnComplete(() =>
                            {
                                if (index < demoObjects.Count && demoObjects[index] != null)
                                {
                                    RTween.Scale(demoObjects[index], Vector3.one, 0.2f)
                                        .SetEase(EaseType.OutBack);
                                }
                            });
                    }
                });
            }
        }

        void Demo_Pickup()
        {
            int objectCount = demoObjects.Count;
            for (int i = 0; i < objectCount; i++)
            {
                int index = i;
                if (index < demoObjects.Count && demoObjects[index] != null)
                {
                    RTweenEffects.QuickPunchScale(demoObjects[index]);

                    RTween.DelayedCall(0.2f + (index * 0.1f), () =>
                    {
                        if (index < demoObjects.Count && demoObjects[index] != null)
                        {
                            RTween.Move(demoObjects[index], new Vector3(0, 3f, 0), 0.5f)
                                .SetEase(EaseType.InQuad);
                        }
                    });
                }
            }
        }

        void Demo_PowerUp()
        {
            int objectCount = demoObjects.Count;
            int colorCount = objectColors.Length;

            for (int i = 0; i < objectCount; i++)
            {
                int index = i;

                if (index < demoObjects.Count && demoObjects[index] != null)
                {
                    RTweenEffects.PunchScale(demoObjects[index], Vector3.one * 0.5f, 0.5f, 10, 1f);

                    RTween.DelayedCall(index * 0.1f, () =>
                    {
                        if (index < demoObjects.Count && demoObjects[index] != null)
                        {
                            RTween.LocalRotation(demoObjects[index], Quaternion.Euler(0, 360, 0), 0.5f)
                                .SetEase(EaseType.OutQuad);
                        }
                    });

                    if (index < allMaterials.Count && allMaterials[index] != null)
                    {
                        Material mat = allMaterials[index];
                        Color originalColor = objectColors[index % colorCount];

                        RTween.DelayedCall(index * 0.1f, () =>
                        {
                            if (mat != null)
                            {
                                RTween.Color(mat, Color.yellow, 0.25f)
                                    .OnComplete(() =>
                                    {
                                        if (mat != null)
                                        {
                                            RTween.Color(mat, originalColor, 0.25f);
                                        }
                                    });
                            }
                        });
                    }
                }
            }
        }

        void Demo_Landing()
        {
            if (mainCamera != null)
            {
                RTweenEffects.ImpactShake(mainCamera.transform, 0.3f);
            }

            int objectCount = demoObjects.Count;
            for (int i = 0; i < objectCount; i++)
            {
                int index = i;

                RTween.DelayedCall(index * 0.05f, () =>
                {
                    if (index < demoObjects.Count && demoObjects[index] != null)
                    {
                        RTween.Scale(demoObjects[index], new Vector3(1.3f, 0.7f, 1f), 0.1f)
                            .SetEase(EaseType.OutQuad)
                            .OnComplete(() =>
                            {
                                if (index < demoObjects.Count && demoObjects[index] != null)
                                {
                                    RTween.Scale(demoObjects[index], Vector3.one, 0.2f)
                                        .SetEase(EaseType.OutElastic);
                                }
                            });
                    }
                });
            }
        }
    }
}