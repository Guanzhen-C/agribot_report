# RTween - High Performance Tweening System


A lightweight, high-performance tweening animation system for Unity with visual animator, shake/punch effects, and comprehensive editor tools. Optimized for mobile platforms with zero garbage allocation during runtime.

## ✨ Features

### Core Tweening
- **Transform Animations**: Position, rotation, scale (world & local space)
- **UI Animations**: RectTransform, anchored position, size delta
- **Visual Effects**: Color, alpha, fade (auto-detects Image, Text, TextMeshPro, SpriteRenderer, Material)
- **Camera Effects**: Field of view, orthographic size
- **Audio Control**: Volume, pitch
- **Light Control**: Intensity, color
- **Custom Values**: Tween any float value with callback

### Advanced Features
- **30+ Easing Functions**: Linear, Quad, Cubic, Quart, Quint, Sine, Expo, Circ, Back, Elastic, Bounce (In/Out/InOut)
- **Custom Animation Curves**: SetEase(AnimationCurve) support for custom easing
- **Repeat Modes**: Restart, Yoyo, Incremental with infinite loop support
- **Sequences**: Chain multiple tweens with precise timing control
- **Special Effects**: Shake and Punch effects for impacts, camera shake, UI feedback
- **Visual Animator Component**: Create animations directly in the Inspector without code
- **Async/Await Support**: Directly await any TweenHandle for zero-allocation asynchronous flows
- **Target-based Kill**: Kill all active tweens targeting a specific object/Transform via RTween.Kill(target)
- **Runtime Debugger**: Monitor active tweens with real-time performance metrics
- **Memory Safe**: Strict zero GC allocations during runtime using typed struct fields and object pooling

### Performance
- ⚡ Optimized for mobile devices
- 🎯 True Zero GC runtime using strict concrete fields to avoid boxing/unboxing
- 🔄 Object Pooled internally via TweenDataPool to reuse tween states
- 📊 Built-in performance monitoring and warnings
- 🔧 Automatic memory leak detection

## 🚀 Quick Start

### Basic Code Usage

```csharp
using RG.Tweening;
using UnityEngine;

public class TweenExample : MonoBehaviour
{
    // Traditional callback usage
    void Start()
    {
        // Move to position
        RTween.Move(transform, new Vector3(5, 0, 0), 1f)
            .SetEase(EaseType.OutQuad)
            .OnComplete(() => Debug.Log("Movement complete!"));

        // Fade out UI
        RTween.Alpha(GetComponent<CanvasGroup>(), 0f, 0.5f)
            .SetDelay(0.5f);

        // Scale punch effect
        RTweenEffects.QuickPunchScale(transform);
    }

    // Modern async/await usage (Zero GC allocations!)
    async void Awake()
    {
        // Wait for movement to complete
        await RTween.Move(transform, new Vector3(5, 0, 0), 1f)
            .SetEase(EaseType.OutQuad);
            
        Debug.Log("Async movement complete!");
    }
}
```

### Using Sequences

```csharp
RTween.Sequence()
    .Add(RTween.Move(transform, Vector3.up * 2, 0.5f))
    .Wait(0.2f)
    .Add(RTween.Scale(transform, Vector3.one * 1.5f, 0.3f))
    .Call(() => Debug.Log("Sequence step!"))
    .Play();
```

### Visual Animator Component

1. Add **RTweenAnimator** component to any GameObject
2. Click **"+ Add Animation"** in Inspector
3. Configure animation type, duration, easing
4. Set play mode (OnEnable, OnStart, Manual, etc.)
5. Press Play!

No code required for simple animations!

## 📖 API Reference

### Core Tweening Methods

#### Transform
```csharp
RTween.Move(Transform target, Vector3 endValue, float duration)
RTween.MoveX/Y/Z(Transform target, float endValue, float duration)
RTween.LocalMove(Transform target, Vector3 endValue, float duration)
RTween.Rotation(Transform target, Quaternion endValue, float duration)
RTween.Scale(Transform target, Vector3 endValue, float duration)
RTween.Jump(Transform target, Vector3 endValue, float jumpPower, int numJumps, float duration)
```

#### UI
```csharp
RTween.UIMove(RectTransform target, Vector2 endValue, float duration)
RTween.SizeDelta(RectTransform target, Vector2 endValue, float duration)
RTween.Alpha(CanvasGroup target, float endValue, float duration)
```

#### Visual
```csharp
RTween.Color(Image/Text/TMP_Text/SpriteRenderer target, Color endValue, float duration)
RTween.Alpha(Image/Text/TMP_Text/SpriteRenderer target, float endValue, float duration)
```

#### Effects
```csharp
RTweenEffects.ShakePosition(Transform target, float strength, float duration)
RTweenEffects.ShakeCamera(Camera camera, float strength, float duration)
RTweenEffects.PunchScale(Transform target, Vector3 punch, float duration)
RTweenEffects.QuickCameraShake(Camera camera, float intensity = 0.3f)
```

### Tween Control

```csharp
TweenHandle handle = RTween.Move(transform, Vector3.zero, 1f);

// Chaining methods
handle.SetEase(EaseType.OutBack)
      .SetEase(myAnimationCurve) // Custom ease curves
      .SetDelay(0.5f)
      .SetRepeat(3, RepeatType.Yoyo)
      .OnStart(() => { })
      .OnUpdate(() => { })
      .OnComplete(() => { });

// Control
handle.Pause();
handle.Resume();
handle.Kill(); // Stop immediately
handle.Kill(true); // Complete and stop
```

### Global Control

```csharp
RTween.KillAll();
RTween.Kill(targetTransform); // Kill all active tweens on a specific target
RTween.PauseAll();
RTween.ResumeAll();
```

## 🎮 Ease Types

Linear, InSine, OutSine, InOutSine, InQuad, OutQuad, InOutQuad, InCubic, OutCubic, InOutCubic, InQuart, OutQuart, InOutQuart, InQuint, OutQuint, InOutQuint, InExpo, OutExpo, InOutExpo, InCirc, OutCirc, InOutCirc, InBack, OutBack, InOutBack, InElastic, OutElastic, InOutElastic, InBounce, OutBounce, InOutBounce, Custom Curve (AnimationCurve)

## 🔧 Editor Tools

### RTween Animator (Component)
Visual animation creation without coding. Features:
- 40+ animation types
- Play modes: OnEnable, OnStart, Manual, OnTriggerEnter, OnCollisionEnter, OnMouseDown
- Sequence support
- From/To values
- UnityEvents: OnStart, OnUpdate, OnComplete
- Live preview controls in play mode

### RTween Debugger (Window > RTween > Debugger)
Real-time monitoring:
- Active tween count
- Performance warnings
- Individual tween details
- Progress visualization
- Pause/Resume controls
- Kill all tweens

## 📚 Examples Included

The package includes comprehensive example scenes:
- **Demo3D**: 3D object animations, camera effects
- **DemoUI**: UI animations, menu transitions
- **RTweenDemoManager**: Main demo controller
- **RTweenUIDemoManager**: UI-specific demos

## ⚙️ Best Practices

### Performance Tips
1. **Reuse Handles**: Store handles instead of creating new tweens repeatedly
2. **Kill Unused Tweens**: Always kill tweens when objects are destroyed using RTween.Kill(gameObject)
3. **Use Async/Await**: Take advantage of zero-allocation awaits for sequential triggers
4. **Use Sequences**: More efficient than multiple individual tweens
5. **Pool Objects**: Reuse GameObjects when possible

### Mobile Optimization
```csharp
// Good: Minimal allocation (No boxing, pooled internally)
RTween.Move(transform, targetPos, 1f).SetEase(EaseType.OutQuad);

// Best: Kill target when destroyed/disabled
void OnDisable() 
{
    RTween.Kill(gameObject); // Stops all active tweens on this object cleanly
}
```

## 🐛 Troubleshooting

### Tweens Not Working
- Ensure GameObject is active
- Check if tween was killed prematurely
- Verify target component exists
- Check RTween Debugger for active tweens

### Performance Issues
- Open RTween Debugger (Window > RTween > Debugger)
- Check active tween count (should be < 50)
- Look for memory leak warnings
- Kill unnecessary infinite tweens

### Editor Issues
- Tweens automatically cleanup when exiting play mode
- Use RTweenAnimator for inspector-based animations
- Check console for warning messages

## 📝 Changelog

### Version 2.0.0
- **True Zero GC Runtime**: Eliminated boxing of value types through specialized fields.
- **TweenData Pooling**: Added stack pooling to prevent allocations during initialization.
- **Async/Await Support**: Await handles directly with Zero-allocations.
- **Target-based Kill**: Call `RTween.Kill(target)` directly.
- **Custom Animation Curves**: Ease via `AnimationCurve`.
- **Cached Unity Checks**: Highly optimized native-to-managed bridge updates.

### Version 1.0.0
- Initial release
- Core tweening system
- Visual animator component
- Shake and punch effects
- Runtime debugger
- 30+ easing functions
- Sequence support

## 🤝 Support

- **Email**: zeemdev1@gmail.com
- **Discord**: [Join our community](https://discord.gg/xEFerZQR)
- **Forum**: [Unity Forum Thread](https://discussions.unity.com/t/rtween-high-performance-tweening-system/1696750)

## 🙏 Credits

Developed by ZeeM-Dev
Inspired by DOTween, PrimeTween, and the Unity community

---

**⭐ If you enjoy RTween, please leave a review on the Asset Store!**