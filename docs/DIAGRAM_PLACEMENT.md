---
title: Diagram Placement Guide
description: Recommended placement of technical diagrams across the public documentation.
---

# Diagram Placement Guide

This document maps the proposed diagrams to the most appropriate pages in the public documentation.

The goal is to keep diagrams close to the part of the text they clarify, instead of concentrating them in one overview page.

## General Rule

Use diagrams where they reduce reading effort for a technically dense section.

In this documentation set:

- overview pages should get system-level diagrams
- deep-dive pages should get pipeline or mechanism diagrams
- flow pages should get sequence diagrams
- onboarding pages should only get navigation or contributor-oriented diagrams if needed

## Recommended Asset Location

Store exported diagram assets under:

- `website-doc/static/img/diagrams/`

Suggested naming convention:

- `system-overview-diagram.png`
- `startup-reload-sequence.png`
- `runtime-state-ownership.png`
- `vrdf-format-and-decode.png`
- `rendering-pipeline.png`
- `raymarching-internals.png`
- `quest-vs-desktop-rendering.png`
- `backend-integration-contracts.png`
- `voice-ai-flow.png`
- `xr-interaction-layer.png`

## Placement Map

### Technical Architecture

Page:

- `docs/TECHNICAL_ARCHITECTURE.md`

Best diagram:

- `System Overview`

Recommended position:

- directly below `## High-Level System Overview`
- keep the existing text map if you want a text-first fallback, but the visual diagram should become the main orientation element

Reason:

- this page is the architecture hub
- it should show the full system shape before deeper pages split the details

### Scene and Runtime Flows

Page:

- `docs/ARCHITECTURE_SCENE_AND_FLOWS.md`

Best diagrams:

- `Startup and Reload Sequence`

Recommended position:

- directly below `## End-to-End Runtime Sequences`

Reason:

- this page is already sequence-oriented
- the startup and websocket reload paths are much easier to grasp visually than in numbered prose alone

### Runtime Subsystems

Page:

- `docs/ARCHITECTURE_RUNTIME_SUBSYSTEMS.md`

Best diagrams:

- `Runtime State Ownership`

Recommended position:

- near the top, after `## Main Runtime Subsystems`
- or near the end, just before the current `Continue Reading` section

Reason:

- this is the densest page in the documentation set
- a state ownership diagram reduces ambiguity around which object owns which runtime data

### VRDF

Page:

- `docs/ARCHITECTURE_VRDF.md`

Best diagrams:

- `VRDF Format and Unity Decode Path`

Recommended position:

- directly below `## Binary Layout Used By The Loader`

Optional second placement:

- a smaller supporting figure below `## VRDF In The Viewer Pipeline`

Reason:

- the page explains both the file structure and the decode handoff into Unity
- this is exactly where a format-to-runtime diagram has the most value

### Rendering Pipeline

Page:

- `docs/ARCHITECTURE_RENDERING_PIPELINE.md`

Best diagrams:

- `Rendering Pipeline`
- `Direct Volume Rendering in CASSANDRA XR`
- `Raymarching Internals Only`
- `Quest vs Desktop Rendering Split`

Recommended position:

- `Rendering Pipeline`:
  below `## Render Pipeline Overview`
- `Direct Volume Rendering in CASSANDRA XR`:
  below `## Material Binding` or just above `## Main Shader Inputs`
- `Raymarching Internals Only`:
  below `## Desktop Shader Path` and before `## Quest Shader Path`
- `Quest vs Desktop Rendering Split`:
  below `## Shader Selection`

Reason:

- this page is the strongest home for renderer-specific visuals
- it is the only page where all four of these diagrams support the text without feeling redundant

### Backend Integration and Contracts

Page:

- `docs/ARCHITECTURE_BACKEND_INTEGRATION.md`

Best diagrams:

- `Backend Integration`

Recommended position:

- directly below `# Backend Integration and Contracts`
- or below `## Scope` if you want a short intro paragraph first

Reason:

- this page is contract-heavy
- a single service-boundary diagram will make the REST, websocket, and runtime-config relationships much easier to scan

### Voice and AI

Current best page:

- `docs/ARCHITECTURE_RUNTIME_SUBSYSTEMS.md`

Best diagram:

- `Voice and AI Flow`

Recommended position:

- directly below `## Voice, AI, and Conversation Runtime`

Better long-term option:

- move it to a dedicated future page if you later split voice/AI into its own document

Reason:

- the flow is too detailed for the overview
- it belongs near the concrete runtime components that implement it

### XR Interaction

Current best page:

- `docs/ARCHITECTURE_RUNTIME_SUBSYSTEMS.md`

Best diagram:

- `XR Interaction`

Recommended position:

- directly below `## XR Interaction Layer`

Better long-term option:

- move it into a dedicated XR interaction page if you later create one

Reason:

- the landing page now emphasizes XR interaction, but the docs still describe it mostly in prose
- this diagram would immediately strengthen that part of the documentation

### Contributor Onboarding

Page:

- `docs/CONTRIBUTOR_ONBOARDING.md`

Optional diagram:

- `Contributor Navigation Map`

Recommended position:

- below `## Main Areas Of The Codebase`

Reason:

- only useful if you want a visual “if you change X, go here” map
- lower priority than the architecture and rendering diagrams

## Priority Order

If you do not want to create all diagrams at once, use this order:

1. `System Overview`
2. `Startup and Reload Sequence`
3. `VRDF Format and Unity Decode Path`
4. `Rendering Pipeline`
5. `Quest vs Desktop Rendering Split`
6. `Backend Integration`
7. `Runtime State Ownership`
8. `Voice and AI Flow`
9. `XR Interaction`
10. `Raymarching Internals Only`

## Practical Recommendation

Do not add all diagrams in one pass.

The strongest first release would be:

- one system-level diagram in `Technical Architecture`
- one sequence diagram in `Scene and Runtime Flows`
- two renderer diagrams in `Rendering Pipeline`
- one contract diagram in `Backend Integration and Contracts`

That would already make the documentation feel significantly more technical and more intentional without overloading the pages.