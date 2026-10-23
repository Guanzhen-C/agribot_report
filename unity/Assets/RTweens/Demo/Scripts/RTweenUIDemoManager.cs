using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace RG.Tweening
{
    public class RTweenUIDemoManager : MonoBehaviour
    {
        [Header("UI Settings")]
        [SerializeField] Canvas demoCanvas;
        [SerializeField] Font uiFont;
        [SerializeField]
        Color[] uiColors = new Color[] {
        new Color(0.2f, 0.6f, 1f),
        new Color(1f, 0.4f, 0.4f),
        new Color(0.4f, 1f, 0.6f),
        new Color(1f, 0.8f, 0.2f)
    };

        List<RectTransform> uiElements = new List<RectTransform>();
        List<Image> uiImages = new List<Image>();
        List<Vector2> originalPositions = new List<Vector2>();

        void Start()
        {
            CreateCanvas();
            CreateUIElements();
            CreateControlButtons();
        }

        void CreateCanvas()
        {
            if (demoCanvas == null)
            {
                GameObject canvasObj = new GameObject("UI Demo Canvas");
                demoCanvas = canvasObj.AddComponent<Canvas>();
                demoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
            }
        }

        void CreateUIElements()
        {
            // Create 4 UI panels on the right side
            float spacing = 150f;
            float startY = 200f;

            for (int i = 0; i < 4; i++)
            {
                GameObject panelObj = new GameObject("UI_Panel_" + i);
                panelObj.transform.SetParent(demoCanvas.transform, false);

                RectTransform rect = panelObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(100, 100);
                rect.anchoredPosition = new Vector2(-200, startY - (i * spacing));

                Image img = panelObj.AddComponent<Image>();
                img.color = uiColors[i % uiColors.Length];

                // Add text label
                GameObject textObj = new GameObject("Label");
                textObj.transform.SetParent(panelObj.transform, false);

                Text txt = textObj.AddComponent<Text>();
                txt.text = (i + 1).ToString();
                txt.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 40;
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontStyle = FontStyle.Bold;

                RectTransform textRect = textObj.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                uiElements.Add(rect);
                uiImages.Add(img);
                originalPositions.Add(rect.anchoredPosition);
            }
        }

        void CreateControlButtons()
        {
            int buttonWidth = 140;
            int buttonHeight = 35;
            int buttonSpacing = 10;
            int buttonsPerRow = 4;
            int currentRow = 0;
            int currentCol = 0;
            int startY = -20;

            // UI Movement
            CreateSectionLabel("UI MOVEMENT", ref currentRow, ref currentCol, startY);
            CreateButton("Slide In", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_SlideIn);
            CreateButton("Slide Out", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_SlideOut);
            CreateButton("Move Wave", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_MoveWave);
            CreateButton("Bounce In", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_BounceIn);
            CreateButton("Circular", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Circular);

            // UI Scale & Rotate
            CreateSectionLabel("SCALE & ROTATE", ref currentRow, ref currentCol, startY);
            CreateButton("Scale Pulse", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_ScalePulse);
            CreateButton("Pop In", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_PopIn);
            CreateButton("Flip", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Flip);
            CreateButton("Squash", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Squash);

            // UI Color & Fade
            CreateSectionLabel("COLOR & FADE", ref currentRow, ref currentCol, startY);
            CreateButton("Fade In", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_FadeIn);
            CreateButton("Fade Out", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_FadeOut);
            CreateButton("Color Cycle", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_ColorCycle);
            CreateButton("Rainbow", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Rainbow);
            CreateButton("Blink", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Blink);

            // UI Shake & Punch
            CreateSectionLabel("SHAKE & PUNCH", ref currentRow, ref currentCol, startY);
            CreateButton("Shake UI", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_ShakeUI);
            CreateButton("Punch UI", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_PunchUI);
            CreateButton("Wobble UI", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_WobbleUI);
            CreateButton("Jello", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Jello);

            // UI Sequences
            CreateSectionLabel("UI SEQUENCES", ref currentRow, ref currentCol, startY);
            CreateButton("Sequential", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_Sequential);
            CreateButton("Wave Scale", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, Demo_WaveScale);
            CreateButton("Reset All", buttonWidth, buttonHeight, buttonSpacing, buttonsPerRow, ref currentRow, ref currentCol, startY, ResetAllUI);
        }

        void CreateSectionLabel(string text, ref int currentRow, ref int currentCol, int startY)
        {
            currentRow++;
            currentCol = 0;

            GameObject textObj = new GameObject(text);
            textObj.transform.SetParent(demoCanvas.transform, false);

            Text txt = textObj.AddComponent<Text>();
            txt.text = text;
            txt.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 18;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontStyle = FontStyle.Bold;

            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(20, startY - (currentRow * 45));
            rt.sizeDelta = new Vector2(600, 30);

            currentRow++;
        }

        void CreateButton(string text, int width, int height, int spacing, int perRow, ref int currentRow, ref int currentCol, int startY, System.Action action)
        {
            GameObject buttonObj = new GameObject("Btn_" + text);
            buttonObj.transform.SetParent(demoCanvas.transform, false);

            RectTransform rt = buttonObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(20 + (currentCol * (width + spacing)), startY - (currentRow * (height + spacing)));

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
            buttonText.fontSize = 14;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            btn.onClick.AddListener(() => {
                ResetAllUI();
                action.Invoke();
            });

            currentCol++;
            if (currentCol >= perRow)
            {
                currentCol = 0;
                currentRow++;
            }
        }

        void ResetAllUI()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    uiElements[i].anchoredPosition = originalPositions[i];
                    uiElements[i].localScale = Vector3.one;
                    uiElements[i].localRotation = Quaternion.identity;
                }

                if (i < uiImages.Count && uiImages[i] != null)
                {
                    Color col = uiColors[i % uiColors.Length];
                    col.a = 1f;
                    uiImages[i].color = col;
                }
            }
        }

        // UI Movement Demos
        void Demo_SlideIn()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    uiElements[i].anchoredPosition = new Vector2(2000, originalPositions[i].y);

                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.UIMove(uiElements[index], originalPositions[index], 0.5f)
                                .SetEase(EaseType.OutBack);
                        }
                    });
                }
            }
        }

        void Demo_SlideOut()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.UIMove(uiElements[index], new Vector2(-2000, originalPositions[index].y), 0.5f)
                                .SetEase(EaseType.InBack);
                        }
                    });
                }
            }
        }

        void Demo_MoveWave()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.UIMoveY(uiElements[index], originalPositions[index].y + 100, 0.5f)
                                .SetEase(EaseType.InOutQuad)
                                .OnComplete(() => {
                                    if (index < uiElements.Count && uiElements[index] != null)
                                    {
                                        RTween.UIMoveY(uiElements[index], originalPositions[index].y, 0.5f)
                                            .SetEase(EaseType.InOutQuad);
                                    }
                                });
                        }
                    });
                }
            }
        }

        void Demo_BounceIn()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    uiElements[i].localScale = Vector3.zero;

                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Scale(uiElements[index].transform, Vector3.one, 0.5f)
                                .SetEase(EaseType.OutBounce);
                        }
                    });
                }
            }
        }

        void Demo_Circular()
        {
            float radius = 200f;
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    float angle = (360f / uiElements.Count) * index;

                    RTween.Value(0f, 360f, 2f, (value) => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            float rad = (angle + value) * Mathf.Deg2Rad;
                            Vector2 pos = new Vector2(
                                Mathf.Cos(rad) * radius,
                                Mathf.Sin(rad) * radius
                            );
                            uiElements[index].anchoredPosition = pos;
                        }
                    }).SetEase(EaseType.InOutQuad);
                }
            }
        }

        // Scale & Rotate Demos
        void Demo_ScalePulse()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.15f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Scale(uiElements[index].transform, Vector3.one * 1.3f, 0.3f)
                                .SetRepeat(3, RepeatType.Yoyo)
                                .SetEase(EaseType.InOutQuad);
                        }
                    });
                }
            }
        }

        void Demo_PopIn()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    uiElements[index].localScale = Vector3.zero;

                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Scale(uiElements[index].transform, Vector3.one * 1.2f, 0.3f)
                                .SetEase(EaseType.OutBack)
                                .OnComplete(() => {
                                    if (index < uiElements.Count && uiElements[index] != null)
                                    {
                                        RTween.Scale(uiElements[index].transform, Vector3.one, 0.2f)
                                            .SetEase(EaseType.OutQuad);
                                    }
                                });
                        }
                    });
                }
            }
        }

        void Demo_Rotate()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.LocalRotation(uiElements[index].transform, Quaternion.Euler(0, 0, 360), 1f)
                                .SetEase(EaseType.InOutQuad);
                        }
                    });
                }
            }
        }

        void Demo_Flip()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Value(0f, 180f, 0.5f, (value) => {
                                if (index < uiElements.Count && uiElements[index] != null)
                                {
                                    uiElements[index].localRotation = Quaternion.Euler(0, value, 0);
                                }
                            }).SetEase(EaseType.InOutQuad);
                        }
                    });
                }
            }
        }

        void Demo_Squash()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Scale(uiElements[index].transform, new Vector3(1.3f, 0.7f, 1f), 0.2f)
                                .SetEase(EaseType.OutQuad)
                                .OnComplete(() => {
                                    if (index < uiElements.Count && uiElements[index] != null)
                                    {
                                        RTween.Scale(uiElements[index].transform, Vector3.one, 0.3f)
                                            .SetEase(EaseType.OutElastic);
                                    }
                                });
                        }
                    });
                }
            }
        }

        // Color & Fade Demos
        void Demo_FadeIn()
        {
            for (int i = 0; i < uiImages.Count; i++)
            {
                if (uiImages[i] != null)
                {
                    Color col = uiImages[i].color;
                    col.a = 0f;
                    uiImages[i].color = col;

                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiImages.Count && uiImages[index] != null)
                        {
                            RTween.Alpha(uiImages[index], 1f, 0.5f)
                                .SetEase(EaseType.OutQuad);
                        }
                    });
                }
            }
        }

        void Demo_FadeOut()
        {
            for (int i = 0; i < uiImages.Count; i++)
            {
                if (uiImages[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiImages.Count && uiImages[index] != null)
                        {
                            RTween.Alpha(uiImages[index], 0f, 0.5f)
                                .SetEase(EaseType.InQuad);
                        }
                    });
                }
            }
        }

        void Demo_ColorCycle()
        {
            for (int i = 0; i < uiImages.Count; i++)
            {
                if (uiImages[i] != null)
                {
                    int index = i;
                    Color nextColor = uiColors[(index + 1) % uiColors.Length];

                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiImages.Count && uiImages[index] != null)
                        {
                            RTween.Color(uiImages[index], nextColor, 0.5f)
                                .SetEase(EaseType.InOutQuad);
                        }
                    });
                }
            }
        }

        void Demo_Rainbow()
        {
            Color[] rainbowColors = new Color[] {
            Color.red, new Color(1f, 0.5f, 0f), Color.yellow, Color.green, Color.cyan, Color.blue, Color.magenta
        };

            for (int i = 0; i < uiImages.Count; i++)
            {
                if (uiImages[i] != null)
                {
                    int index = i;
                    int colorIndex = 0;

                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiImages.Count && uiImages[index] != null)
                        {
                            void CycleColor()
                            {
                                if (index < uiImages.Count && uiImages[index] != null)
                                {
                                    colorIndex = (colorIndex + 1) % rainbowColors.Length;
                                    RTween.Color(uiImages[index], rainbowColors[colorIndex], 0.3f)
                                        .OnComplete(() => {
                                            if (colorIndex < rainbowColors.Length - 1)
                                            {
                                                CycleColor();
                                            }
                                        });
                                }
                            }
                            CycleColor();
                        }
                    });
                }
            }
        }

        void Demo_Blink()
        {
            for (int i = 0; i < uiImages.Count; i++)
            {
                if (uiImages[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.1f, () => {
                        if (index < uiImages.Count && uiImages[index] != null)
                        {
                            RTween.Alpha(uiImages[index], 0f, 0.2f)
                                .SetRepeat(5, RepeatType.Yoyo)
                                .SetEase(EaseType.InOutQuad);
                        }
                    });
                }
            }
        }

        // Shake & Punch Demos
        void Demo_ShakeUI()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    RTweenEffects.ShakeUIPosition(uiElements[i], 20f, 0.5f, 20, 90f, true);
                }
            }
        }

        void Demo_PunchUI()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    RTweenEffects.PunchUIPosition(uiElements[i], Vector2.up * 50f, 0.5f, 10, 1f);
                }
            }
        }

        void Demo_WobbleUI()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    RTweenEffects.WobbleEffect(uiElements[i].transform, 15f, 0.5f);
                }
            }
        }

        void Demo_Jello()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                if (uiElements[i] != null)
                {
                    int index = i;
                    RTween.DelayedCall(index * 0.05f, () => {
                        if (index < uiElements.Count && uiElements[index] != null)
                        {
                            RTween.Value(0f, 1f, 0.5f, (t) => {
                                if (index < uiElements.Count && uiElements[index] != null)
                                {
                                    float scaleX = 1f + Mathf.Sin(t * Mathf.PI * 3f) * 0.2f * (1f - t);
                                    float scaleY = 1f + Mathf.Sin(t * Mathf.PI * 3f + Mathf.PI * 0.5f) * 0.2f * (1f - t);
                                    uiElements[index].localScale = new Vector3(scaleX, scaleY, 1f);
                                }
                            }).OnComplete(() => {
                                if (index < uiElements.Count && uiElements[index] != null)
                                {
                                    uiElements[index].localScale = Vector3.one;
                                }
                            });
                        }
                    });
                }
            }
        }

        // Sequence Demos
        void Demo_Sequential()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                int index = i;
                Vector2 originalPos = originalPositions[index];

                RTween.DelayedCall(index * 0.2f, () => {
                    if (index < uiElements.Count && uiElements[index] != null)
                    {
                        RTween.Sequence()
                            .Add(RTween.UIMove(uiElements[index], originalPos + Vector2.up * 100, 0.3f).SetEase(EaseType.OutQuad))
                            .Add(RTween.Scale(uiElements[index].transform, Vector3.one * 1.3f, 0.2f).SetEase(EaseType.OutQuad))
                            .Add(RTween.Scale(uiElements[index].transform, Vector3.one, 0.2f).SetEase(EaseType.InQuad))
                            .Add(RTween.UIMove(uiElements[index], originalPos, 0.3f).SetEase(EaseType.InQuad))
                            .Play();
                    }
                });
            }
        }

        void Demo_Cascade()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                int index = i;

                RTween.DelayedCall(index * 0.15f, () => {
                    if (index < uiElements.Count && uiElements[index] != null)
                    {
                        RTween.Sequence()
                            .Add(RTween.Scale(uiElements[index].transform, Vector3.one * 1.5f, 0.3f).SetEase(EaseType.OutBack))
                            .With(RTween.LocalRotation(uiElements[index].transform, Quaternion.Euler(0, 0, 360), 0.3f))
                            .Add(RTween.Scale(uiElements[index].transform, Vector3.one, 0.3f).SetEase(EaseType.InBack))
                            .Play();
                    }
                });
            }
        }

        void Demo_WaveScale()
        {
            for (int i = 0; i < uiElements.Count; i++)
            {
                int index = i;

                RTween.Value(0f, Mathf.PI * 2f, 2f, (value) => {
                    if (index < uiElements.Count && uiElements[index] != null)
                    {
                        float offset = (Mathf.PI * 2f / uiElements.Count) * index;
                        float scale = 1f + Mathf.Sin(value + offset) * 0.3f;
                        uiElements[index].localScale = Vector3.one * scale;
                    }
                }).SetEase(EaseType.Linear);
            }
        }
    }
}