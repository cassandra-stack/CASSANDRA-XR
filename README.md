# CASSANDRA XR

### *Interactive Mixed Reality Platform for Medical Volume Exploration*

## Demos

Explore the *CASSANDRA XR* system in action through these short demo videos:  


| Demo | Description | Link |
|------|--------------|------|
| **Study-Session Demo** | Real-time study loading and medical data visualization in XR. | [▶️ Watch](https://www.youtube.com/shorts/4tP0xRyO_74) |
| **Medical Assistant Demo** | Interaction with the AI-driven assistant in mixed reality. | [▶️ Watch](https://www.youtube.com/shorts/EcIkscZZDR4) |
| **Brain Visualization Demo** | Volumetric rendering and manipulation of 3D brain scans. | [▶️ Watch](https://www.youtube.com/shorts/A57vztt2YsE) |

> 🎥 All demos were recorded on the **Android (Meta Quest)** build, currently in BETA.  
> The **Windows version** runs smoothly with no major performance issues.

---

> **Status: BETA**
>  — Current **Android build (mobile XR headset)** is in **BETA** and exhibits **major performance issues** when **reloading the brain** (i.e., on **modality changes** or **visualization updates** triggered from the web interface).
> These issues **do not occur in the Windows/Unity Editor version**, which remains stable and performant. Mobile optimization fixes are in progress.

---

## Project Overview

This Unity application is designed for **interactive visualization and exploration of 3D medical volumes** (MRI, CT scans, etc.) in **XR environments** (headsets or screens).
It integrates **volumetric rendering (URP)**, **real-time networking**, **voice + AI interaction**, and **hand-based XR manipulation**.

**Goal:** to provide an immersive, intelligent, and collaborative platform for 3D medical imaging analysis.

---

## Known Issues / Limitations (Nov 2025)

* **Performance drops during brain reload (Android only)**: noticeable frame-time spikes and occasional stalls when switching **modality** (e.g., MRI/CT) or **visualization presets** initiated from the **web interface**.
  *Scope:* affects data pipeline refresh, VRDF re-init, shader state rebinds, and UI redraw.
* **Not reproducible on Windows/Unity Editor builds**: desktop versions show smooth reload and stable frame pacing.
* **Resource pressure (Android)**: temporary **VRAM peak** and **GC allocations** during `.vrdf` swaps may cause hitching on mobile GPUs.
* **Workarounds (temporary)**:

  1. Prefer in-scene preset changes over rapid successive switches from the web UI.
  2. Allow 1–2s between modality toggles to avoid stacking reload events.
  3. If hitches persist, restart session to clear caches.

> Tracking ID: `PERF-RELOAD-001` — Optimization in progress (batched GPU uploads, async streaming, pooled `Texture3D`, debounced UI events).

---

## System Architecture

### 🔹 1. Remote Backend

* **REST API** (`/active`) — provides available studies in JSON format (`RootResponse`, `StudyRaw`, `AssetRaw`, `PatientRaw`).
* **File Storage** — hosts `.vrdf` resources (compressed medical volumes).
* **WebSocket Server (Pusher)** — broadcasts `vr.status.changed` events for real-time synchronization.

### 🔹 2. Local Client (Unity)

The Unity client is structured into **five main layers**:

---

## 1. Data Acquisition and Management

### StudyService

* Performs HTTP GET requests to the REST API.
* Deserializes JSON into `StudyForUnity` objects.
* Maps `.vrdf` assets to their imaging modality using `StudyMapper`.

### PusherClient

* Manages WebSocket connection to the Pusher server.
* Listens for `vr.status.changed` events.
* Maintains connection via ping/pong and automatic reconnection.

### SessionDataController

* Orchestrates the entire data pipeline: retrieval, caching, downloads, and progress signals.
* Emits events such as:
  `OnStudiesReady`, `OnDownloadProgress`, `OnDownloadCompleted`, `OnReloadRequested`.

---

## 2. Volumetric Loading and Rendering

### VRDFLoader

* Decodes `.vrdf` files into Unity `Texture3D` objects.
* Generates color lookup tables (LUTs) based on modality (MRI, T1, CT…).

### VolumeDVR

* Controls the **URP raymarching** rendering process.
* Dynamically adjusts shader parameters (contrast, clipping, opacity, threshold).
* Interfaces directly with the **`VolumeDVR_URP.shader`**.

### URP Shader

* Implements a 3D **raymarcher** in HLSL:

  * Computes ray-volume intersections.
  * Accumulates RGB + alpha samples.
  * Integrates gradient lighting and clipping planes.

---

## 3. XR Interaction Layer

### XRManipulationState & XRHandAimUtils

* Tracks XR hand poses and gestures.
* Maintains manipulation states (rotate, scale, translate).

### HandPinchScaleXRHands / Rotate / FistTranslate

* Enables **natural 3D interaction** (pinch to zoom, rotate, or move the volume).

### PinchToOpenBrainMenu

* Opens contextual brain menus via pinch gesture detection.

### PorcupineWakeWordListener

* Detects a wake word (“Hey Gemini”, “Analyze brain”) to activate the voice interface.

---

## 4. Voice and AI Interaction

### GeminiClient

* Connects to **Google Gemini API** for intelligent responses.
* Sends text prompts and receives structured text or audio replies.

### GeminiVoiceInterface

* Integrates **Speech-To-Text (STT)** and **Text-To-Speech (TTS)**.
* Records audio via `WavUtility` and plays back responses using `AudioSource`.

### ChatManager / ChatText / TypingBubbleAnimator

* Provides immersive chat UI: animated speech bubbles and typing effects.

---

## 5. Immersive UI / UX System

### BrainMenuController & Items

* 3D brain menu to toggle visibility of anatomical structures.
* Interfaces with `VolumeDVR` and shader uniforms.

### StudyInfoPanel / Controller

* Displays study metadata (patient, date, modality).
* Supports anonymization and real-time updates via `StudyRuntimeSO`.

### CanvasXRSetup / FaceUser

* Configures **World Space canvases**.
* Automatically orients panels toward the XR camera.

### ModernProgressBar / ProgressPanelAnimator / MicButtonPulse

* Handle visual feedback: loading progress, voice activity, etc.

---

## Inter-Module Communication

| Event                 | Emitter                 | Receiver                | Effect                     |
| --------------------- | ----------------------- | ----------------------- | -------------------------- |
| `OnVrStatusChanged`   | `PusherClient`          | `SessionDataController` | Study reload               |
| `OnStudiesReady`      | `SessionDataController` | `UI`                    | Update dropdown/modalities |
| `OnDownloadCompleted` | `SessionDataController` | `VolumeDVR`             | Load new 3D volume         |
| `OnChanged`           | `StudyRuntimeSO`        | `InfoPanel`             | Refresh metadata           |

---

## Functional Overview

```text
REST API → StudyService → SessionDataController → VRDFLoader → VolumeDVR → Shader
                     ↑                   ↓
                PusherClient          Local Cache
                     ↓
           UI & XR Hands ←→ Voice AI (Gemini)
```

---

##Technologies

| Domain               | Technology              | Description                     |
| -------------------- | ----------------------- | ------------------------------- |
| Volumetric Rendering | Unity URP + HLSL        | Real-time 3D raymarching        |
| XR Interaction       | Unity XR Hands          | Gesture-based manipulation      |
| Voice & AI           | Porcupine + Gemini API  | Wake word + AI assistant        |
| Networking           | REST + WebSocket        | Data streaming and events       |
| Medical Data         | JSON + VRDF             | Study and volume representation |
| Interface            | TextMeshPro + Canvas XR | Immersive 3D UI                 |

---

## Project Structure

```bash
Assets/
 ├─ Scripts/
 │   ├─ Data/
 │   ├─ Network/
 │   ├─ Volume/
 │   ├─ XR/
 │   ├─ VoiceAI/
 │   ├─ UI/
 │   └─ Utils/
 ├─ Shaders/
 │   └─ VolumeDVR_URP.shader
 └─ Scenes/
     └─ MainXRScene.unity
```

---

## Runtime Flow

1. Connects to REST API and WebSocket.
2. Downloads and caches `.vrdf` volume data.
3. Reconstructs and renders the 3D volume.
4. User interacts via **hands**, **voice**, or **3D menus**.
5. Server updates trigger automatic study reload.
6. Voice AI (Gemini) provides contextual feedback and assistance.
