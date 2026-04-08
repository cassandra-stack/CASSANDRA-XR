---
id: architecture-performance-and-risks
title: Performance, Testing, and Risks
sidebar_position: 5
slug: /architecture/performance-and-risks
---

## 12. Performance Characteristics and Constraints

### Strong Areas

- Quest-specific texture formats reduce memory pressure versus desktop float textures
- Raymarch shader is simplified for mobile
- Foveated rendering is enabled
- Progress UI and placeholder brain hide some load latency

### Expensive Operations

- Full VRDF file reads into memory
- CPU-side voxel unpacking
- Runtime creation of `Texture3D` objects
- Material and texture rebind on every modality switch
- Aggressive unload and GC on Android during reload

### Known User-Facing Risk

The README and architecture both point to a consistent bottleneck:

- Android reloads and modality changes can stutter or stall

This is structurally believable given the current synchronous decode and texture recreation path.

## 13. Testing and Observability

### Automated Testing

The Unity Test Framework package is installed, but there is effectively no real custom automated test suite in the repository.

### Manual Test Utility

`Assets/Scripts/UI/NetworkSmokeTest.cs` is a manual smoke-test script that:

- performs cleartext and HTTPS GET requests
- tests websocket connectivity

This is useful for diagnostics but is not a substitute for automated tests.

### Logging

The project relies heavily on `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError`.
Several key systems are verbose:

- `VolumeDVR`
- `PusherClient`
- `GeminiVoiceInterface`
- `PorcupineWakeWordListener`

This is helpful for diagnosis, but the logging strategy is ad hoc rather than centralized.

## 14. Current Technical Debt and Risks

### Architectural Debt

- No custom assembly boundaries
- High scene coupling through inspector references
- Some static global state
- Mixed naming quality and language conventions
- Feature code and sample content live in the same asset tree

### Functional Debt

- Reload path is synchronous and expensive
- Some XR interaction features are present but only partially integrated into the main scene
- Build profile configuration is inconsistent across assets
- Security posture is intentionally permissive
- Test coverage is minimal

### Naming and Consistency Debt

Examples visible in code:

- `Ochestrator` directory spelling
- `Utiils` directory spelling
- `converationURL` field spelling
- `VolumeClipControllerr.cs`

These do not break functionality, but they raise maintenance cost and reduce clarity.

