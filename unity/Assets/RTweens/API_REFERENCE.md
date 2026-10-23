# RTween API Reference

Complete API documentation for RTween system.

## Table of Contents
- [Core API](#core-api)
- [Transform Tweening](#transform-tweening)
- [UI Tweening](#ui-tweening)
- [Visual Tweening](#visual-tweening)
- [Camera Tweening](#camera-tweening)
- [Audio Tweening](#audio-tweening)
- [Light Tweening](#light-tweening)
- [Custom Values](#custom-values)
- [Sequences](#sequences)
- [Effects](#effects)
- [Control Methods](#control-methods)
- [Enums](#enums)

---

## Core API

### RTween Class
Main static class for creating and controlling tweens.

#### Namespace
```csharp
using RG.Tweening;
```

---

## Transform Tweening

### Move
Animates a transform's world position.

```csharp
TweenHandle Move(Transform target, Vector3 endValue, float duration)
```

**Parameters:**
- `target` - The transform to animate
- `endValue` - Target world position
- `duration` - Animation duration in seconds

**Returns:** `TweenHandle` - Handle to control the tween

**Example:**
```csharp
RTween.Move(transform, new Vector3(5, 0, 0), 1f);
```

---

### MoveX / MoveY / MoveZ
Animates a single axis of world position.

```csharp
TweenHandle MoveX(Transform target, float endValue, float duration)
TweenHandle MoveY(Transform target, float endValue, float duration)
TweenHandle MoveZ(Transform target, float endValue, float duration)
```

**Parameters:**
- `target` - The transform to animate
- `endValue` - Target position on specified axis
- `duration` - Animation duration in seconds

**Example:**
```csharp
RTween.MoveY(transform, 10f, 2f);
```

---

### LocalMove
Animates a transform's local position.

```csharp
TweenHandle LocalMove(Transform target, Vector3 endValue, float duration)
```

**Example:**
```csharp
RTween.LocalMove(transform, Vector3.up * 2, 1f);
```

---

### LocalMoveX / LocalMoveY / LocalMoveZ
Animates a single axis of local position.

```csharp
TweenHandle LocalMoveX(Transform target, float endValue, float duration)
TweenHandle LocalMoveY(Transform target, float endValue, float duration)
TweenHandle LocalMoveZ(Transform target, float endValue, float duration)
```

---

### Rotation
Animates a transform's world rotation.

```csharp
TweenHandle Rotation(Transform target, Quaternion endValue, float duration)
```

**Example:**
```csharp
RTween.Rotation(transform, Quaternion.Euler(0, 180, 0), 1f);
```

---

### LocalRotation
Animates a transform's local rotation.

```csharp
TweenHandle LocalRotation(Transform target, Quaternion endValue, float duration)
```

---

### Scale
Animates a transform's local scale.

```csharp
TweenHandle Scale(Transform target, Vector3 endValue, float duration)
```

**Example:**
```csharp
RTween.Scale(transform, Vector3.one * 2, 0.5f);
```

---

### ScaleX / ScaleY / ScaleZ
Animates a single axis of local scale.

```csharp
TweenHandle ScaleX(Transform target, float endValue, float duration)
TweenHandle ScaleY(Transform target, float endValue, float duration)
TweenHandle ScaleZ(Transform target, float endValue, float duration)
```

---

### Jump
Animates a transform in an arc motion.

```csharp
TweenHandle Jump(Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration)
```

**Parameters:**
- `target` - The transform to animate
- `endValue` - Target position
- `jumpPower` - Height of the jump
- `numJumps` - Number of jumps to perform
- `duration` - Total animation duration

**Example:**
```csharp
RTween.Jump(transform, new Vector3(5, 0, 0), 2f, 3, 1.5f);
```

---

## UI Tweening

### UIMove
Animates a RectTransform's anchored position.

```csharp
TweenHandle UIMove(RectTransform target, Vector2 endValue, float duration)
```

**Example:**
```csharp
RectTransform rect = GetComponent<RectTransform>();
RTween.UIMove(rect, new Vector2(100, 50), 1f);
```

---

### UIMoveX / UIMoveY
Animates a single axis of anchored position.

```csharp
TweenHandle UIMoveX(RectTransform target, float endValue, float duration)
TweenHandle UIMoveY(RectTransform target, float endValue, float duration)
```

---

### SizeDelta
Animates a RectTransform's size delta.

```csharp
TweenHandle SizeDelta(RectTransform target, Vector2 endValue, float duration)
```

**Example:**
```csharp
RTween.SizeDelta(rect, new Vector2(200, 100), 0.5f);
```

---

## Visual Tweening

### Color
Animates color of UI or sprite components. Auto-detects: Image, Text, TextMeshPro, SpriteRenderer, Material.

```csharp
TweenHandle Color(Image target, Color endValue, float duration)
TweenHandle Color(Text target, Color endValue, float duration)
TweenHandle Color(TMP_Text target, Color endValue, float duration)
TweenHandle Color(SpriteRenderer target, Color endValue, float duration)
TweenHandle Color(Material target, Color endValue, float duration)
```

**Example:**
```csharp
Image image = GetComponent<Image>();
RTween.Color(image, Color.red, 1f);
```

---

### Alpha
Animates alpha transparency. Auto-detects: Image, Text, TextMeshPro, SpriteRenderer, Material, CanvasGroup.

```csharp
TweenHandle Alpha(Image target, float endValue, float duration)
TweenHandle Alpha(Text target, float endValue, float duration)
TweenHandle Alpha(TMP_Text target, float endValue, float duration)
TweenHandle Alpha(SpriteRenderer target, float endValue, float duration)
TweenHandle Alpha(Material target, float endValue, float duration)
TweenHandle Alpha(CanvasGroup target, float endValue, float duration)
```

**Example:**
```csharp
CanvasGroup group = GetComponent<CanvasGroup>();
RTween.Alpha(group, 0f, 0.5f); // Fade out
```

---

## Camera Tweening

### FieldOfView
Animates camera field of view (perspective cameras).

```csharp
TweenHandle FieldOfView(Camera target, float endValue, float duration)
```

**Example:**
```csharp
RTween.FieldOfView(Camera.main, 90f, 2f);
```

---

### OrthographicSize
Animates camera orthographic size (orthographic cameras).

```csharp
TweenHandle OrthographicSize(Camera target, float endValue, float duration)
```

---

## Audio Tweening

### Volume
Animates AudioSource volume.

```csharp
TweenHandle Volume(AudioSource target, float endValue, float duration)
```

**Example:**
```csharp
AudioSource audio = GetComponent<AudioSource>();
RTween.Volume(audio, 0f, 1f); // Fade out
```

---

### Pitch
Animates AudioSource pitch.

```csharp
TweenHandle Pitch(AudioSource target, float endValue, float duration)
```

---

## Light Tweening

### Intensity
Animates Light intensity.

```csharp
TweenHandle Intensity(Light target, float endValue, float duration)
```

**Example:**
```csharp
Light light = GetComponent<Light>();
RTween.Intensity(light, 5f, 1f);
```

---

### Color (Light)
Animates Light color.

```csharp
TweenHandle Color(Light target, Color endValue, float duration)
```

---

## Custom Values

### Value
Animates a custom float value with callback.

```csharp
TweenHandle Value(float startValue, float endValue, float duration, Action<float> onUpdate)
```

**Parameters:**
- `startValue` - Starting value
- `endValue` - Target value
- `duration` - Animation duration
- `onUpdate` - Callback invoked each frame with current value

**Example:**
```csharp
RTween.Value(0f, 100f, 2f, value => 
{
    Debug.Log($"Current: {value}");
    customSlider.value = value;
});
```

---

### DelayedCall
Executes a callback after a delay.

```csharp
TweenHandle DelayedCall(float delay, Action callback)
```

**Example:**
```csharp
RTween.DelayedCall(2f, () => Debug.Log("Delayed!"));
```

---

## Sequences

### Sequence
Creates a new tween sequence for chaining animations.

```csharp
TweenSequence Sequence()
```

#### Sequence Methods

**Add** - Adds a tween to play after the previous one completes
```csharp
TweenSequence Add(TweenHandle tween)
```

**Wait** - Adds a delay
```csharp
TweenSequence Wait(float interval)
```

**Call** - Executes a callback
```csharp
TweenSequence Call(Action callback)
```

**With** - Adds a tween to play simultaneously with previous
```csharp
TweenSequence With(TweenHandle tween)
```

**At** - Adds a tween at specific time
```csharp
TweenSequence At(float time, TweenHandle tween)
```

**Play** - Starts the sequence
```csharp
TweenHandle Play()
```

**Example:**
```csharp
RTween.Sequence()
    .Add(RTween.Move(transform, Vector3.up * 2, 0.5f))
    .Wait(0.3f)
    .Add(RTween.Scale(transform, Vector3.one * 1.5f, 0.3f))
    .Call(() => Debug.Log("Done!"))
    .Play();
```

---

## Effects

### RTweenEffects Class
Static class for shake and punch effects.

#### Shake Effects

**ShakePosition** - Shakes world position
```csharp
TweenHandle ShakePosition(Transform target, float strength, float duration, 
    int vibrato = 10, float randomness = 90f, bool fadeOut = true)
```

**ShakeLocalPosition** - Shakes local position
```csharp
TweenHandle ShakeLocalPosition(Transform target, float strength, float duration, 
    int vibrato = 10, float randomness = 90f, bool fadeOut = true)
```

**ShakeRotation** - Shakes rotation
```csharp
TweenHandle ShakeRotation(Transform target, float strength, float duration, 
    int vibrato = 10, float randomness = 90f, bool fadeOut = true)
```

**ShakeScale** - Shakes scale
```csharp
TweenHandle ShakeScale(Transform target, float strength, float duration, 
    int vibrato = 10, float randomness = 90f, bool fadeOut = true)
```

**ShakeCamera** - Shakes camera
```csharp
TweenHandle ShakeCamera(Camera camera, float strength, float duration, 
    int vibrato = 10, bool fadeOut = true)
```

**ShakeUIPosition** - Shakes UI element
```csharp
TweenHandle ShakeUIPosition(RectTransform target, float strength, float duration, 
    int vibrato = 10, float randomness = 90f, bool fadeOut = true)
```

**Example:**
```csharp
RTweenEffects.ShakeCamera(Camera.main, 0.5f, 0.3f);
```

---

#### Punch Effects

**PunchPosition** - Punches world position
```csharp
TweenHandle PunchPosition(Transform target, Vector3 punch, float duration, 
    int vibrato = 10, float elasticity = 1f)
```

**PunchLocalPosition** - Punches local position
```csharp
TweenHandle PunchLocalPosition(Transform target, Vector3 punch, float duration, 
    int vibrato = 10, float elasticity = 1f)
```

**PunchRotation** - Punches rotation
```csharp
TweenHandle PunchRotation(Transform target, Vector3 punch, float duration, 
    int vibrato = 10, float elasticity = 1f)
```

**PunchScale** - Punches scale
```csharp
TweenHandle PunchScale(Transform target, Vector3 punch, float duration, 
    int vibrato = 10, float elasticity = 1f)
```

**PunchUIPosition** - Punches UI position
```csharp
TweenHandle PunchUIPosition(RectTransform target, Vector2 punch, float duration, 
    int vibrato = 10, float elasticity = 1f)
```

**Example:**
```csharp
RTweenEffects.PunchScale(transform, Vector3.one * 0.2f, 0.3f);
```

---

#### Quick Effects

**QuickCameraShake** - Quick camera shake
```csharp
TweenHandle QuickCameraShake(Camera camera, float intensity = 0.3f)
```

**QuickPunchScale** - Quick scale punch
```csharp
TweenHandle QuickPunchScale(Transform target, float intensity = 0.2f)
```

**QuickUIShake** - Quick UI shake
```csharp
TweenHandle QuickUIShake(RectTransform target, float intensity = 10f)
```

---

## Control Methods

### TweenHandle Methods

**SetEase** - Sets easing function or custom curve
```csharp
TweenHandle SetEase(EaseType ease)
TweenHandle SetEase(AnimationCurve curve)
```

**SetDelay** - Sets start delay
```csharp
TweenHandle SetDelay(float delay)
```

**SetRepeat** - Sets repeat behavior
```csharp
TweenHandle SetRepeat(int count, RepeatType repeatType = RepeatType.Restart)
```
- `count`: Number of repeats (0 = no repeat, -1 = infinite)

**OnStart** - Sets start callback
```csharp
TweenHandle OnStart(Action callback)
```

**OnUpdate** - Sets update callback
```csharp
TweenHandle OnUpdate(Action callback)
```

**OnComplete** - Sets completion callback
```csharp
TweenHandle OnComplete(Action callback)
```

**Kill** - Stops the tween
```csharp
void Kill(bool complete = false)
```

**Pause** - Pauses the tween
```csharp
void Pause()
```

**Resume** - Resumes the tween
```csharp
void Resume()
```

**IsActive** - Checks if tween is active
```csharp
bool IsActive { get; }
```

**Direct Await Support** - Await a tween completion asynchronously (Zero allocations!)
```csharp
await RTween.Move(transform, targetPosition, 1f);
```

**Example:**
```csharp
TweenHandle handle = RTween.Move(transform, Vector3.up, 1f)
    .SetEase(EaseType.OutBounce)
    .SetDelay(0.5f)
    .SetRepeat(3, RepeatType.Yoyo)
    .OnComplete(() => Debug.Log("Complete!"));

// Later...
handle.Pause();
handle.Resume();
handle.Kill();
```

---

### Global Control

**Kill** - Stops a specific tween
```csharp
void Kill(TweenHandle handle, bool complete = false)
```

**Kill (Target-based)** - Stops all active tweens targeting a specific object
```csharp
void Kill(object target, bool complete = false)
```

**KillAll** - Stops all active tweens
```csharp
void KillAll()
```

**Pause** - Pauses a specific tween
```csharp
void Pause(TweenHandle handle)
```

**Resume** - Resumes a specific tween
```csharp
void Resume(TweenHandle handle)
```

**PauseAll** - Pauses all active tweens
```csharp
void PauseAll()
```

**ResumeAll** - Resumes all active tweens
```csharp
void ResumeAll()
```

---

## Enums

### EaseType
Available easing functions:

```csharp
public enum EaseType
{
    Linear,
    InSine, OutSine, InOutSine,
    InQuad, OutQuad, InOutQuad,
    InCubic, OutCubic, InOutCubic,
    InQuart, OutQuart, InOutQuart,
    InQuint, OutQuint, InOutQuint,
    InExpo, OutExpo, InOutExpo,
    InCirc, OutCirc, InOutCirc,
    InBack, OutBack, InOutBack,
    InElastic, OutElastic, InOutElastic,
    InBounce, OutBounce, InOutBounce
}
```

---

### RepeatType
Repeat behavior modes:

```csharp
public enum RepeatType
{
    Restart,     // Reset to start value each repeat
    Yoyo,        // Alternate between start and end
    Incremental  // Add the difference each repeat
}
```

**Examples:**
```csharp
// Restart: 0->1, 0->1, 0->1
RTween.MoveX(transform, 5f, 1f).SetRepeat(3, RepeatType.Restart);

// Yoyo: 0->1, 1->0, 0->1
RTween.MoveX(transform, 5f, 1f).SetRepeat(3, RepeatType.Yoyo);

// Incremental: 0->1, 1->2, 2->3
RTween.MoveX(transform, 5f, 1f).SetRepeat(3, RepeatType.Incremental);

// Infinite loop
RTween.MoveX(transform, 5f, 1f).SetRepeat(-1, RepeatType.Yoyo);
```

---

## Complete Example

```csharp
using UnityEngine;
using RG.Tweening;

public class CompleteExample : MonoBehaviour
{
    void Start()
    {
        // Basic tween with options
        RTween.Move(transform, Vector3.up * 5, 2f)
            .SetEase(EaseType.OutBack)
            .SetDelay(0.5f)
            .OnComplete(() => Debug.Log("Movement done!"));

        // UI fade sequence
        CanvasGroup canvas = GetComponent<CanvasGroup>();
        RTween.Sequence()
            .Add(RTween.Alpha(canvas, 0f, 0.5f))
            .Wait(1f)
            .Add(RTween.Alpha(canvas, 1f, 0.5f))
            .Play();

        // Infinite rotation
        RTween.Rotation(transform, Quaternion.Euler(0, 360, 0), 3f)
            .SetRepeat(-1, RepeatType.Incremental)
            .SetEase(EaseType.Linear);

        // Camera shake on hit
        RTweenEffects.QuickCameraShake(Camera.main, 0.5f);
    }
}
```