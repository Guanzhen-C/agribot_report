# Changelog

All notable changes to RTween will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.1] - 2026-07-23

### Fixed
- **Unity 6.5 compatibility**: `Object.GetInstanceID()` deprecated in Unity 6000.5 (6.5), replaced with `GetEntityId()`. Added conditional compilation (`#if UNITY_6000_0_OR_NEWER`) for version-safe fallback.
  - Unity 6000.0+: uses `GetEntityId().ToString()`
  - Unity 2023 and older: keeps `GetInstanceID().ToString()`
- `GetInstanceID()` shows deprecation warnings in Unity 6.4, hard error in Unity 6.5.

## [2.0.0] - 2026-06-XX

### Added
- **Zero-Allocation Runtime**: Completely eliminated boxing/unboxing of value types (`Vector3`, `Vector2`, `float`, `Quaternion`, `Color`) by adding typed concrete fields to `TweenData` and specialized method signatures to `TweenEngine`.
- **TweenData Object Pooling**: Implemented high-performance stack-based pooling for `TweenData` using the internal `TweenDataPool` to eliminate heap allocations during tween creation.
- **Async/Await Support**: Added custom zero-allocation `TweenAwaiter` struct and `GetAwaiter()` to `TweenHandle`, allowing direct use of C# `await` on any `TweenHandle` without task allocations.
- **Target-based Kill Overload**: Added `RTween.Kill(object target, bool complete)` to stop and cleanup all active tweens targeting a specific object/Transform.
- **Custom Animation Curve Easing**: Added support for Unity's `AnimationCurve` inside `TweenHandle.SetEase(AnimationCurve)` to allow custom non-linear easing paths.
- **Optimized Unity Object Check**: Cached target type check and added a dedicated `isUnityObject` flag to avoid expensive C++ to C# native bridge crossing on null checks in the main loop.
- **Safe Iterator**: Replaced dictionary key allocations inside the update loop with struct enumerator loops.

## [1.0.0] - 2026-01-XX

### Added
- Core tweening system with 40+ animation types
- Transform animations (Move, Rotate, Scale) for world and local space
- UI animations (RectTransform, anchored position, size delta)
- Visual effects (Color, Alpha) with auto-detection for Image, Text, TextMeshPro, SpriteRenderer, Material
- Camera tweening (FOV, orthographic size)
- Audio tweening (Volume, Pitch)
- Light tweening (Intensity, Color)
- Custom value tweening with callbacks
- 30+ easing functions (Linear, Quad, Cubic, Quart, Quint, Sine, Expo, Circ, Back, Elastic, Bounce)
- Repeat modes: Restart, Yoyo, Incremental
- Infinite loop support (-1 repeat count)
- Sequence system for chaining tweens
- Delay support for individual tweens
- Event callbacks: OnStart, OnUpdate, OnComplete
- RTweenAnimator component for visual animation creation
- Play modes: OnEnable, OnStart, Manual, OnTriggerEnter, OnCollisionEnter, OnMouseDown
- From/To value support in animator
- Custom target support (animate objects other than self)
- Shake effects: Position, Rotation, Scale, Camera, UI
- Punch effects: Position, Rotation, Scale, UI
- Quick effect methods for common use cases
- RTween Debugger window for runtime monitoring
- Real-time tween count display
- Performance warnings (50+ tweens, 100+ critical)
- Individual tween progress visualization
- Memory leak detection (tweens active > 5 minutes)
- Custom editor for RTweenAnimator with foldout states
- Editor play mode controls (Play, Stop, Pause, Resume)
- Auto-cleanup when exiting play mode
- Zero garbage allocation during runtime
- Automatic destroyed object cleanup
- Support for Unity 6 and newer

## [Unreleased]

### Planned Features
- Timeline integration
- Undo/Redo support in editor
- Tween presets/templates
- Path-based motion tweening
- Spline support
- Material property tweening (beyond color)
- Shader property animations
- Mesh deformation tweening
- 2D physics integration
- Rigidbody velocity/force tweening
- Advanced sequence editor
- Visual scripting support
- Addressables integration


### Performance
- Optimized for mobile platforms
- Pooled internal structures to prevent GC
- Efficient dictionary-based tween management
- Batch callback execution to prevent collection modification
- Automatic memory leak warnings

### Documentation
- Comprehensive README with examples
- XML documentation for public APIs
- Example scenes included
- Best practices guide
- Troubleshooting section

---

## Version History

### Version Numbering
- **Major.Minor.Patch** (e.g., 1.0.0)
- **Major**: Breaking API changes
- **Minor**: New features, backwards compatible
- **Patch**: Bug fixes, performance improvements

### Compatibility
- Unity 2022.2.62f or newer
- Unity 6000.0 or newer
- .NET Standard 2.1
- All platforms supported
- Mobile optimized