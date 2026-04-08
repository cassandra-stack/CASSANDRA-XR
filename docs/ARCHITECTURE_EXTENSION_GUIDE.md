---
id: architecture-extension-guide
title: Extension Guide and Ownership
sidebar_position: 6
slug: /architecture/extension-guide
---

## 15. Extension Guide

## 15.1 Add a New Backend Study Field

1. Add the property in `JsonFormatUtility.cs`
2. Map it in `StudyMapper.cs`
3. Store it in `StudyRuntimeSO.cs` if it is needed after fetch
4. Update any UI presenters that should display it

## 15.2 Add a New Modality

1. Ensure backend exposes the new asset as a VRDF-like asset
2. Ensure `StudyMapper` can derive the modality code
3. Add the modality entry to the dropdown UI if needed
4. Ensure the VRDF file naming matches `VolumeDVR.LoadVolumeByCodeAsync()` expectations

## 15.3 Add a New Segmentation UI Behavior

1. Extend `VolumeDVR` label control methods if needed
2. Update `BrainMenuToggleToVolumeDVR`
3. Optionally add richer item behavior via `XRMenuItemInteractable`

## 15.4 Replace or Extend the AI Backend

1. Update `StudyRuntimeSO` or backend payloads if endpoint discovery changes
2. Extend `GeminiClient` transport logic
3. Update `GeminiVoiceInterface` response parsing and history loading rules
4. Keep confidential-mode behavior aligned with the new backend contract

## 16. Recommended Documentation Ownership

This document should be maintained whenever one of the following changes:

- The startup pipeline changes
- The backend API or websocket contract changes
- The VRDF format changes
- The shader/material contract changes
- The voice/AI backend changes
- Build profiles or Android security settings change

Suggested future companion documents:

- `docs/VRDF_FORMAT.md`
- `docs/VOICE_AND_AI.md`
- `docs/BUILD_AND_RELEASE.md`
- `docs/SECURITY_NOTES.md`

## 17. Summary

CASSANDRA XR is a functional Unity XR application with a clear product focus:

- backend-driven study selection
- custom medical volume loading
- mobile-aware URP volume rendering
- immersive metadata and report viewing
- voice-driven AI assistance

Its current architecture is practical and product-oriented, but still prototype-shaped:

- scene-composed rather than strongly modular
- operationally effective but lightly tested
- performance-aware but not yet fully optimized
- feature-rich but not fully hardened

The most important architectural center of gravity is the path:

`StudyService -> SessionDataController -> StudyRuntimeSO -> VolumeDVR -> Shader`

with the voice/AI system acting as a second major vertical slice:

`PorcupineWakeWordListener -> GeminiVoiceInterface -> GeminiClient -> ChatManager`

Together, those two slices define the core identity of the application.

