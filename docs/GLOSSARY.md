---
id: glossary
title: Glossary
sidebar_position: 2
slug: /glossary
---

# Glossary

This glossary defines the main terms used across the CASSANDRA XR documentation.

## CASSANDRA XR

The Unity-based XR viewer layer of the broader CASSANDRA platform. It is responsible for spatial visualization, interaction, report viewing, and the voice-assisted experience around medical imaging data.

## Study

The backend-defined unit of medical content loaded by the viewer. A study typically includes metadata, modality references, report material, and configuration values used by the runtime.

## Session

The active runtime context used by the viewer after the current study has been fetched, configured, and prepared for interaction.

## Modality

A specific medical imaging representation exposed by the study, such as a volume dataset or a derived view that can be loaded by the viewer.

## Multimodal Fusion

The coordinated presentation of multiple imaging signals or modality-specific assets inside the same viewer workflow.

## VRDF

The custom binary container format used by the project to store volumetric data, transfer-function information, and other volume-related payloads before conversion into Unity runtime resources.

## VolumeDVR

The main runtime component responsible for loading decoded volume assets, configuring materials, managing label visibility, and driving the rendered volume in the scene.

## StudyRuntimeSO

The central runtime `ScriptableObject` used to store the active study state and notify dependent systems when the study changes.

## SessionDataController

The orchestration component that fetches studies, prepares runtime configuration, downloads VRDF assets, and coordinates reload behavior.

## PusherClient

The websocket client used to receive backend-driven real-time events and forward them onto the Unity main thread.

## MainScene

The primary Unity scene where the viewer runtime is composed. Most of the active application behavior is instantiated and wired there.

## Texture3D

The Unity GPU resource used to hold volumetric data after VRDF decoding and before shader-based rendering.

## URP

The Universal Render Pipeline used by the project for custom shader integration and runtime rendering on both desktop and Quest targets.

## Quest Path

The mobile-oriented rendering and texture format path used when the application runs on Meta Quest hardware, with optimizations and tradeoffs tailored to device constraints.

## Desktop Path

The editor and desktop runtime path used when the application runs outside Quest, typically with less constrained texture precision and rendering cost.

## XR Interaction

The set of natural user interface behaviors used to manipulate or inspect the viewer in immersive contexts, including gesture-based input, mid-air interaction, and hand-tracking-driven controls.

## AI-Assisted Interpretation

The voice and conversation layer that connects the viewer to backend STT, conversational inference, and TTS services in order to support interpretive workflows around the active study.

## Continue Reading

- [Documentation Overview](/docs/)
- [Technical Architecture](/docs/technical-architecture)
- [Runtime Subsystems](/docs/architecture/runtime-subsystems)