---
id: technical-architecture
title: Technical Architecture
sidebar_position: 1
slug: /technical-architecture
---

This section is the architecture entry point for the XR viewer.

Use the linked sections below to navigate the implementation details:

- [Glossary](/docs/glossary)
- [Contributor Onboarding](/docs/contributor-onboarding)
- [Runtime Subsystems](/docs/architecture/runtime-subsystems)
- [Scene and Runtime Flows](/docs/architecture/scene-and-flows)
- [VRDF](/docs/architecture/vrdf)
- [Rendering Pipeline](/docs/architecture/rendering-pipeline)
- [Backend Integration and Contracts](/docs/architecture/backend-integration)
- [Platform, Build, and Security](/docs/architecture/platform-and-security)
- [Performance, Testing, and Risks](/docs/architecture/performance-and-risks)
- [Extension Guide and Ownership](/docs/architecture/extension-guide)

## Purpose and Scope

This document describes the current technical architecture of the CASSANDRA XR Unity project as implemented in the repository.

The goal is to document:

- The runtime architecture
- The role of the main subsystems
- How data moves from backend services into the Unity scene
- How volumetric medical data is loaded and rendered
- How voice, AI, UI, and XR features interact
- How the project is configured for Quest and desktop targets
- The current technical constraints, assumptions, and risks

This is an implementation-oriented document. It describes what the codebase currently does, not only what the product is intended to do.

## High-Level System Overview

CASSANDRA XR is a Unity-based XR client for interactive exploration of medical volume data in mixed reality and desktop contexts.

At runtime, the application performs five primary jobs:

1. Fetches the active study and asset metadata from a backend REST API
2. Downloads and caches `.vrdf` volumetric datasets locally
3. Decodes those datasets into Unity `Texture3D` resources
4. Renders the volume through custom URP raymarching shaders
5. Exposes the study through immersive UI, report viewing, and a voice/AI assistant

The system is scene-driven. Most application logic is attached to `MonoBehaviour` components placed in `MainScene` and connected through serialized inspector references.

## System Map

The simplified map below shows the main runtime slice and the secondary voice/AI slice:

```text
Backend REST API ------------------------------+
                                               |
Backend WebSocket ---> PusherClient ---------- | ---> SessionDataController ---> StudyRuntimeSO
                                               |                                 |-> StudyInfoPanelController
Backend STT / TTS / AI endpoints -------------+                                 |-> SessionVolumeLoader
                                                                                 |-> GeminiVoiceInterface
                                                                                 |-> other MainScene listeners

Persistent cache <------------------------- SessionDataController
Persistent cache ----> VolumeDVR ----> Texture3D / Material ----> URP shaders ----> XR viewer

GeminiVoiceInterface <----> GeminiClient ----> backend conversation endpoint
GeminiVoiceInterface ----> ChatManager / TTS playback / UI state
XR interaction layer ----> VolumeDVR / UI panels / report objects
```

## Technology Stack

### Engine and Core Runtime

- Unity Editor version: `6000.2.6f2`
- Render pipeline: Universal Render Pipeline (URP)
- Scripting runtime: C# / `Assembly-CSharp`
- Input system: Unity Input System
- XR framework:
  - `com.unity.xr.management`
  - `com.unity.xr.openxr`
  - `com.unity.xr.meta-openxr`
  - `com.unity.xr.hands`
  - `com.unity.xr.interaction.toolkit`
  - Meta XR SDK Core

### Networking and Serialization

- REST: `UnityWebRequest`
- WebSocket: `websocket-sharp.dll`
- JSON:
  - `Newtonsoft.Json` for backend payloads and websocket frames
  - `JsonUtility` for local simple payload parsing

### Voice and AI

- Wake word: Picovoice Porcupine
- STT and TTS: external backend endpoints provided dynamically by backend configuration
- Conversational assistant: Gemini-compatible backend endpoint provided per study

### Volume Rendering

- Custom `.vrdf` binary container format
- Runtime conversion to `Texture3D`
- Custom HLSL URP shaders:
  - `Assets/Shaders/VolumeDVR_URP.shader`
  - `Assets/Shaders/VolumeDVR_URP_Quest.shader`

## Related Repositories and Technical Lineage

The public technical context around this repository is split across three GitHub projects:

- [CASSANDRA XR](https://github.com/cassandra-stack/CASSANDRA-XR)
  - the main application repository documented here
- [VRDF SDK](https://github.com/guillaume-schneider/vrdf-sdk)
  - the companion repository for the `.vrdf` volume container, its export modes, and cross-platform tooling
- [HybridMedRenderer](https://github.com/cassandra-stack/HybridMedRenderer)
  - a related prototype and research-oriented renderer that explored hybrid surface and volume rendering with VRDF support

This documentation focuses on the code shipped in this repository. The VRDF SDK is the public companion reference for the format itself, while HybridMedRenderer provides useful background on the renderer lineage that informed the current viewer.

## Repository Layout

The repository contains both product code and a significant amount of imported Unity sample/package content.

### Primary Product Code

- `Assets/Scripts/API`
- `Assets/Scripts/Chatbot`
- `Assets/Scripts/DVR`
- `Assets/Scripts/Interaction`
- `Assets/Scripts/UI`
- `Assets/Scripts/VRDF`
- `Assets/Scripts/XR`
- `Assets/Shaders`
- `Assets/Scenes/MainScene.unity`
- `Assets/Settings`
- `Assets/Plugins/Android`
- `Assets/android_config`

### External or Imported Content

- `Assets/Samples`
- `Assets/Porcupine` partly third-party
- `Assets/TextMesh Pro`
- XR Interaction Toolkit sample assets
- XR Hands sample assets

### Assembly Organization

There are no custom `asmdef` files for the application code under `Assets/Scripts`.
As a result:

- Most custom code compiles together into `Assembly-CSharp`
- There is no hard compile-time separation between modules
- Boundaries are architectural conventions rather than enforced code boundaries

## Architectural Style

The application follows a Unity-native, scene-composed architecture:

- `MonoBehaviour` components own most runtime behavior
- A `ScriptableObject` is used as the main shared runtime state container
- Event callbacks and serialized references are the primary coordination mechanisms
- A small number of static properties are used for shared configuration values
- Platform and build behavior is partly encoded in Unity assets and partly in editor scripts

This is not a layered enterprise architecture with strict dependency inversion.
It is better understood as a feature-oriented Unity app with a few key vertical slices:

- Study acquisition and synchronization
- Volume load and rendering
- Voice and AI interaction
- Metadata and report display
- XR and UI interactions

## Continue Reading

- [Glossary](/docs/glossary)
- [Contributor Onboarding](/docs/contributor-onboarding)
- [VRDF](/docs/architecture/vrdf)
- [Backend Integration and Contracts](/docs/architecture/backend-integration)