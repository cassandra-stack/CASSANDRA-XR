---
id: architecture-scene-and-flows
title: Scene and Runtime Flows
sidebar_position: 3
slug: /architecture/scene-and-flows
---

## 7. MainScene Composition

`Assets/Scenes/MainScene.unity` is the primary runtime scene.

Based on the serialized scene wiring, the key active objects include:

- `WebsocketManager`
  - `PusherClient`
- `StartupManager`
  - `StudyService`
  - `SessionVolumeLoader`
  - `SessionDataController`
- `DVRVolume`
  - `VolumeDVR`
- Dropdown and loading UI
  - `DropdownVolumeLoader`
  - `ModernProgressBar`
  - `ProgressPanelAnimator`
- Chat and voice
  - `ChatManager`
  - `PorcupineWakeWordListener`
  - `GeminiVoiceInterface`
  - `MicButtonPulse`
- Metadata
  - `StudyInfoPanelController`
  - `FaceUser`
- Report object
  - `PageDisplay`
  - `ReportGrabAndSwipe`
- XR and platform support
  - `XR Origin (Mobile AR)`
  - `AR Session`
  - `ForceFFR`
  - `PermissionsCheck`

The scene therefore combines:

- startup orchestration
- networking
- volume visualization
- voice/AI interaction
- metadata display
- report display
- platform-specific XR support

in a single scene composition.

## 8. End-to-End Runtime Sequences

## 8.1 Initial Startup Sequence

1. `MainScene` loads
2. `PusherClient` starts and attempts websocket connection
3. `SessionVolumeLoader.Start()` shows loading UI and calls `SessionDataController.BeginLoad()`
4. `SessionDataController` calls `StudyService.FetchStudies()`
5. REST payload is mapped into `StudyForUnity`
6. Backend config values are published:
   - Picovoice access key
   - STT URL
   - TTS URL
7. `StudyRuntimeSO` is updated with the active study
8. All VRDF assets are downloaded into persistent storage
9. `SessionVolumeLoader` calls `VolumeDVR.LoadVolumeByCodeAsync()`
10. `VolumeDVR` decodes the selected modality file
11. Textures and LUTs are bound to the material
12. The placeholder brain crossfades into the rendered volume
13. Loading UI is faded out

## 8.2 Websocket Reload Sequence

1. Backend sends `vr.status.changed`
2. `PusherClient` parses payload and enqueues it
3. `PusherClient.Update()` dispatches the payload on the main thread
4. `SessionDataController.HandleVrStatusChanged()` compares current study state
5. If study code or VR mode changed:
   - `StudyRuntimeSO.Clear()`
   - `OnReloadRequested` emitted
6. `SessionVolumeLoader` receives the reload event
7. Visuals reset and a new load pipeline starts

## 8.3 Modality Change Sequence

1. User changes dropdown selection
2. `DropdownVolumeLoader` starts a transition coroutine
3. Progress panel and placeholder are shown
4. Selected modality code is converted to `{code}_lw.vrdf`
5. `VolumeDVR.LoadVolumeByCodeAsync()` loads the new cached volume
6. New textures are bound to the material
7. Placeholder crossfades to the new volume
8. Progress panel fades out

## 8.4 Voice Interaction Sequence

1. Wake word or input action starts listening
2. `GeminiVoiceInterface.StartRecording()` begins microphone capture
3. Silence detector auto-stops after inactivity
4. `WavUtility` converts the clip to WAV bytes
5. STT endpoint returns transcription text
6. `GeminiClient.SendPrompt()` posts the prompt to the active conversation endpoint
7. Chat UI shows typing placeholders during processing
8. Response is parsed and rendered in the chat window
9. TTS endpoint is called and audio chunks are played back

## 9. Data and Event Model

### Core Runtime Events

`SessionDataController`:

- `OnAccessKeyReady`
- `OnReloadRequested`
- `OnStudiesReady`
- `OnDownloadProgress`
- `OnDownloadCompleted`
- `OnError`

`PusherClient`:

- `OnVrStatusChanged`

`StudyRuntimeSO`:

- `OnChanged`

`GeminiVoiceInterface`:

- `OnStartListening`
- `OnStopListening`

### Runtime State Ownership

- Active study metadata: `StudyRuntimeSO`
- Downloaded volume cache: `Application.persistentDataPath`
- Active volume textures: `VolumeDVR`
- Active chat display/history: `ChatManager`
- Active voice configuration: `SessionDataController` static properties

