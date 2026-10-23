# RTween Quick Start Guide

Get started with RTween in just a few minutes! This guide covers the essentials to start animating in Unity.

## Installation

### Method 1: Unity Package Manager (Recommended)
1. Open **Window > Package Manager**
2. Click **+ > Add package from git URL**
3. Enter your repository URL
4. Click **Add**

### Method 2: Unity Package
1. Import the `.unitypackage` file
2. All scripts and demos will be imported automatically

## Your First Tween (30 seconds)

Create a new C# script and add this code:

```csharp
using UnityEngine;
using RG.Tweening;

public class MyFirstTween : MonoBehaviour
{
    void Start()
    {
        // Move the object up by 5 units over 1 second
        RTween.Move(transform, transform.position + Vector3.up * 5, 1f);
    }
}
```

Attach this script to any GameObject and press Play. That's it!

## Common Use Cases

### 1. Simple Movement
```csharp
// Move to a specific position
RTween.Move(transform, new Vector3(5, 0, 0), 1f);

// Move on a single axis
RTween.MoveY(transform, 10f, 2f);
```

### 2. UI Fade In/Out
```csharp
CanvasGroup panel = GetComponent<CanvasGroup>();

// Fade out
RTween.Alpha(panel, 0f, 0.5f);

// Fade in
RTween.Alpha(panel, 1f, 0.5f);
```

### 3. Scale Animation
```csharp
// Grow
RTween.Scale(transform, Vector3.one * 2, 0.5f);

// Shrink
RTween.Scale(transform, Vector3.one * 0.5f, 0.5f);
```

### 4. Rotation
```csharp
// Rotate 180 degrees
RTween.Rotation(transform, Quaternion.Euler(0, 180, 0), 1f);
```

### 5. UI Movement
```csharp
RectTransform button = GetComponent<RectTransform>();

// Slide from left
RTween.UIMoveX(button, 0f, 0.5f);
```

## Adding Polish with Easing

Easing makes animations feel natural and professional:

```csharp
RTween.Move(transform, Vector3.up * 5, 1f)
    .SetEase(EaseType.OutBounce); // Bouncy ending
```

**Popular Easing Types:**
- `OutQuad` - Smooth deceleration (default feel)
- `InOutQuad` - Smooth start and end
- `OutBack` - Overshoots then settles (springy)
- `OutBounce` - Bounces at the end
- `OutElastic` - Elastic/rubber band effect
- `Linear` - Constant speed (mechanical)

## Using Callbacks

Execute code when animations complete:

```csharp
RTween.Move(transform, Vector3.up * 5, 1f)
    .OnComplete(() => 
    {
        Debug.Log("Movement finished!");
        // Do something else...
    });
```

**Available Callbacks:**
- `OnStart()` - Called when tween begins
- `OnUpdate()` - Called every frame during tween
- `OnComplete()` - Called when tween finishes

## Chaining with Sequences

Create complex animations by chaining tweens:

```csharp
RTween.Sequence()
    .Add(RTween.Move(transform, Vector3.up * 2, 0.5f))
    .Wait(0.3f)
    .Add(RTween.Scale(transform, Vector3.one * 1.5f, 0.3f))
    .Add(RTween.Rotation(transform, Quaternion.Euler(0, 180, 0), 0.5f))
    .Play();
```

## Visual Animator Component (No Code!)

For non-programmers or quick prototyping:

1. Select any GameObject
2. **Add Component > RTween Animator**
3. Click **"+ Add Animation"**
4. Choose animation type (Move, Scale, Color, etc.)
5. Set target values and duration
6. Choose when to play (OnEnable, OnStart, etc.)
7. Press Play!

## Common Patterns

### Button Press Effect
```csharp
public void OnButtonClick()
{
    RTween.Scale(transform, Vector3.one * 0.9f, 0.1f)
        .SetEase(EaseType.InOutQuad)
        .OnComplete(() => 
        {
            RTween.Scale(transform, Vector3.one, 0.1f);
        });
}
```

### Fade Out and Disable
```csharp
CanvasGroup panel = GetComponent<CanvasGroup>();

RTween.Alpha(panel, 0f, 0.5f)
    .OnComplete(() => gameObject.SetActive(false));
```

### Looping Animation
```csharp
// Infinite bounce
RTween.MoveY(transform, startY + 1f, 1f)
    .SetRepeat(-1, RepeatType.Yoyo)
    .SetEase(EaseType.InOutQuad);
```

### Camera Shake
```csharp
using RG.Tweening;

// Quick camera shake on impact
RTweenEffects.QuickCameraShake(Camera.main);

// Custom shake
RTweenEffects.ShakeCamera(Camera.main, strength: 0.5f, duration: 0.3f);
```

## Controlling Tweens

Save references to control tweens later:

```csharp
TweenHandle moveHandle;

void Start()
{
    moveHandle = RTween.Move(transform, Vector3.up * 5, 1f);
}

void Update()
{
    if (Input.GetKeyDown(KeyCode.Space))
    {
        moveHandle.Pause();
    }
    
    if (Input.GetKeyDown(KeyCode.R))
    {
        moveHandle.Resume();
    }
    
    if (Input.GetKeyDown(KeyCode.K))
    {
        moveHandle.Kill(); // Stop immediately
    }
}
```

## Best Practices

### ✅ DO:
```csharp
// Store handles for tweens you need to control
TweenHandle myTween = RTween.Move(transform, target, 1f);

// Kill tweens when objects are destroyed
void OnDestroy()
{
    if (myTween.IsActive)
        myTween.Kill();
}

// Use appropriate easing for the effect
RTween.Move(transform, target, 1f)
    .SetEase(EaseType.OutQuad); // Smooth deceleration
```

### ❌ DON'T:
```csharp
// Don't create tweens in Update() without control
void Update()
{
    // BAD: Creates new tween every frame!
    RTween.Move(transform, target, 1f);
}

// Don't forget to kill infinite loops
void OnDisable()
{
    // BAD: Infinite tween keeps running
    // GOOD: Kill it first
    if (loopTween.IsActive)
        loopTween.Kill();
}
```

## Common Issues & Solutions

### Tween Not Working?
1. Check GameObject is active
2. Verify component exists (e.g., CanvasGroup for alpha)
3. Check console for errors
4. Open **Window > RTween > Debugger** to see active tweens

### Performance Issues?
1. Open **RTween Debugger** (Window > RTween > Debugger)
2. Check active tween count (should be < 50)
3. Kill unnecessary infinite loops
4. Use sequences instead of many individual tweens

### Object Snapping Back?
```csharp
// If another script is also modifying position:
// Make sure only one system controls the value at a time
// OR use OnUpdate to override:
RTween.Move(transform, target, 1f)
    .OnUpdate(() => 
    {
        // Ensure tween has control
        otherScript.enabled = false;
    });
```

## Next Steps

Now that you know the basics:

1. **Explore Examples**: Check the Demo scenes included with RTween
2. **Read API Docs**: See `API_REFERENCE.md` for complete method list
3. **Try Effects**: Experiment with shake and punch effects
4. **Build Sequences**: Create complex animation choreography
5. **Join Community**: Get help and share creations

## Essential Cheat Sheet

```csharp
using RG.Tweening;

// Movement
RTween.Move(transform, position, duration);
RTween.LocalMove(transform, localPosition, duration);

// Rotation
RTween.Rotation(transform, Quaternion.Euler(x, y, z), duration);

// Scale
RTween.Scale(transform, scale, duration);

// UI
RTween.UIMove(rectTransform, anchoredPos, duration);
RTween.Alpha(canvasGroup, alpha, duration);

// Visual
RTween.Color(image, color, duration);
RTween.Alpha(image, alpha, duration);

// Modifiers (all chainable)
.SetEase(EaseType.OutQuad)
.SetDelay(0.5f)
.SetRepeat(3, RepeatType.Yoyo)
.OnStart(() => { })
.OnUpdate(() => { })
.OnComplete(() => { })

// Control
handle.Pause();
handle.Resume();
handle.Kill();
RTween.KillAll();

// Sequences
RTween.Sequence()
    .Add(tween1)
    .Wait(delay)
    .Add(tween2)
    .With(tween3) // Simultaneous
    .Call(callback)
    .Play();

// Effects
RTweenEffects.QuickCameraShake(camera);
RTweenEffects.QuickPunchScale(transform);
RTweenEffects.ShakePosition(transform, strength, duration);
```

## Support

Need help?
- 📧 Email: zeemdev1@gmail.com
- 💬 Discord: [Join our community](https://discord.gg/RTween)
- 📚 Docs: Full documentation in `README.md` and `API_REFERENCE.md`
- 🐛 Issues: Report bugs via email or Discord

---

**Ready to create amazing animations? Start tweening!** 🎬✨