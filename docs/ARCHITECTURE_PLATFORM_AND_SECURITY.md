---
id: architecture-platform-and-security
title: Platform, Build, and Security
sidebar_position: 4
slug: /architecture/platform-and-security
---

## 10. Build and Platform Configuration

## 10.1 Unity Project Settings

The root `ProjectSettings.asset` still contains template-like values:

- `companyName: DefaultCompany`
- `productName: CGPU`
- `bundleVersion: 0.1.0`

This should not be assumed to represent the final shipped Android identity.

## 10.2 Build Profiles

The project uses Unity 6 build profiles under `Assets/Settings/Build Profiles`.

### Meta Quest 1 Build Profile

This appears to be the meaningful Android profile at present.

Notable configuration:

- Overrides the global scene list
- Includes `Assets/Scenes/MainScene.unity`
- Uses:
  - `companyName: Holonauts`
  - `productName: Cassandra`
- Sets `ForceInternetPermission: 1`
- Sets `insecureHttpOption: 2`

### Meta Quest Build Profile

This profile appears closer to template/default state:

- Does not override scenes
- Has empty scene list
- Uses template-like names and product metadata

Operationally, the repository suggests `Meta Quest 1.asset` is the build profile that matches the current product identity.

## 10.3 Quality and Render Pipeline Assets

The project defines quality tiers in `ProjectSettings/QualitySettings.asset`:

- `Mobile`
- `PC`
- `Meta Quest (Build Profile)`

Render pipeline assets:

- `Assets/Settings/Mobile_RPAsset.asset`
  - Render scale `0.8`
  - MSAA `4`
  - Shadow distance `2.5`
- `Assets/Settings/PC_RPAsset.asset`
  - Render scale `1.0`
  - MSAA `4`
  - Shadow distance `50`
  - Additional lights enabled

Current quality settings indicate:

- `Mobile` -> `Mobile_RPAsset`
- `PC` -> `PC_RPAsset`
- `Meta Quest (Build Profile)` -> currently references `PC_RPAsset`

That last mapping should be validated because it may not match the expected mobile optimization path.

## 10.4 Android Customization

### Files

- `Assets/Plugins/Android/AndroidManifest.xml`
- `Assets/android_config/AllowClearText/res/xml/network_security_config.xml`
- `Assets/Editor/ForceHttpAllowed.cs`
- `Assets/Editor/ZipAlignPostBuild.cs`

### Current Android Behavior

- Custom manifest defines Quest-compatible activity setup
- Supported devices include Quest 2, Quest Pro, Quest 3, Quest 3S
- Head tracking feature is marked required
- Network security config allows cleartext traffic
- Editor build hook forces insecure HTTP to be allowed
- Post-build hook tries to `zipalign` and re-sign APKs

### ForceFFR

`Assets/Scripts/XR/ForceFFR.cs` forces high foveated rendering on Meta/Oculus runtime startup.

## 11. Networking and Security Configuration

This section documents the current implementation, not a recommended production posture.

### Current State

- REST endpoint uses HTTPS
- Websocket endpoint uses plain `ws://`
- `CertsHandler` accepts all TLS certificates
- Android network config allows cleartext traffic
- Build preprocessing forces insecure HTTP allowance

### Practical Meaning

The app is currently optimized for connectivity and deployment convenience rather than hardened transport security.
That may be acceptable for a lab prototype or controlled environment, but it is not a secure production configuration for a medical system.

