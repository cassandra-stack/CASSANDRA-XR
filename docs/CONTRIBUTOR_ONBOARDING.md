---
id: contributor-onboarding
title: Contributor Onboarding
sidebar_position: 3
slug: /contributor-onboarding
---

# Contributor Onboarding

This page is the fastest way for an engineer to orient inside the repository and start making intentional changes.

## What This Repository Contains

This repository contains the Unity XR client for CASSANDRA.

It includes:

- the viewer runtime
- the VRDF load and render path
- the XR interaction layer
- the voice and AI client runtime
- the public Docusaurus documentation site in `website-doc/`

It does not contain the full backend implementation.

## Prerequisites

The current Unity project version is:

- `6000.2.6f2`

The repo also includes a Node-based docs site in `website-doc/`, which currently expects:

- Node.js `>=20`
- npm

## First Local Entry Points

If you are opening the Unity project for the first time, start here:

- scene: `Assets/Scenes/MainScene.unity`
- package manifest: `Packages/manifest.json`
- project version: `ProjectSettings/ProjectVersion.txt`

If you are opening the docs site for the first time, start here:

- app config: `website-doc/docusaurus.config.ts`
- sidebar: `website-doc/sidebars.ts`
- landing page: `website-doc/src/pages/index.tsx`

## Main Areas Of The Codebase

The product code is concentrated in:

- `Assets/Scripts/API`
- `Assets/Scripts/Chatbot`
- `Assets/Scripts/DVR`
- `Assets/Scripts/Interaction`
- `Assets/Scripts/UI`
- `Assets/Scripts/VRDF`
- `Assets/Shaders`

Use that split as your first mental model:

- API and orchestration
- rendering and VRDF
- UI and interaction
- voice and AI

## What To Read First

For architecture:

1. [Technical Architecture](/docs/technical-architecture)
2. [Runtime Subsystems](/docs/architecture/runtime-subsystems)
3. [Scene and Runtime Flows](/docs/architecture/scene-and-flows)

For rendering work:

1. [VRDF](/docs/architecture/vrdf)
2. [Rendering Pipeline](/docs/architecture/rendering-pipeline)
3. `Assets/Scripts/VRDF/VolumeVRDFLoader.cs`
4. `Assets/Scripts/DVR/VolumeDVR.cs`

For backend-facing work:

1. [Backend Integration and Contracts](/docs/architecture/backend-integration)
2. `Assets/Scripts/API/Ochestrator/StudyService.cs`
3. `Assets/Scripts/API/DataContract/StudyMapper.cs`
4. `Assets/Scripts/API/Websocket/PusherClient.cs`

## How To Run The Docs Site

From `website-doc/`:

```bash
npm install
npm run start
```

To build the static site:

```bash
npm run build
```

Useful maintenance commands:

```bash
npm run clear
npm run typecheck
```

## How To Work On The Unity Side

The practical runtime entrypoint is `MainScene`.

The main sequence is:

1. open `Assets/Scenes/MainScene.unity`
2. verify the main serialized references resolve correctly
3. enter Play Mode
4. watch the startup pipeline fetch study data and download `.vrdf` assets

If a change affects startup, reload, or modality switching, test it against that scene flow first.

## Common Change Areas

If you need to add a backend study field:

- edit `Assets/Scripts/API/DataContract/JsonFormatUtility.cs`
- update `Assets/Scripts/API/DataContract/StudyMapper.cs`
- propagate into `Assets/Scripts/API/StudyRuntimeSO.cs`
- update the UI or voice consumers

If you need to change volume loading or shader input:

- edit `Assets/Scripts/VRDF/VolumeVRDFLoader.cs`
- edit `Assets/Scripts/DVR/VolumeDVR.cs`
- update the relevant shader under `Assets/Shaders`

If you need to change study reload behavior:

- inspect `Assets/Scripts/API/Websocket/PusherClient.cs`
- inspect `Assets/Scripts/API/Ochestrator/SessionDataController.cs`
- inspect `Assets/Scripts/API/Ochestrator/SessionVolumeLoader.cs`

## Important Architectural Realities

Before making large changes, keep these facts in mind:

- there are no custom `asmdef` boundaries for the main application code
- many dependencies are inspector-wired through `MainScene`
- some runtime configuration is static rather than dependency-injected
- reloads rebuild textures rather than reusing a streaming pool

This means changes can have broader coupling than the file layout suggests at first glance.

## Current Local Validation

The repository currently has very limited automated test coverage.

In practice, contributors should validate changes through:

- targeted Play Mode checks in `MainScene`
- modality load and reload testing
- Quest versus desktop checks when the change touches rendering
- `npm run build` for documentation changes

## Related Repositories

Useful external context:

- [CASSANDRA XR](https://github.com/cassandra-stack/CASSANDRA-XR)
- [VRDF SDK](https://github.com/guillaume-schneider/vrdf-sdk)
- [HybridMedRenderer](https://github.com/cassandra-stack/HybridMedRenderer)

## Continue Reading

- [Technical Architecture](/docs/technical-architecture)
- [VRDF](/docs/architecture/vrdf)
- [Backend Integration and Contracts](/docs/architecture/backend-integration)