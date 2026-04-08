---
id: intro
title: Documentation Overview
sidebar_position: 1
slug: /
---

# CASSANDRA XR Documentation

This site is the public technical entry point for the XR layer of the CASSANDRA platform.

It is intended to explain how the viewer is structured, how its runtime behaves, and how the main technical systems fit together across rendering, orchestration, multimodal data handling, and interaction flows.

## Intended Audience

This documentation is primarily written for:

- engineers
- developers
- researchers

It is designed as a technical reading surface rather than a product brochure or internal project dump.

## Documentation Map

Use the architecture section as a structured walkthrough of the XR viewer:

- [Glossary](/docs/glossary)
  - shared terms used across the documentation, including runtime objects, viewer concepts, and rendering vocabulary
- [Contributor Onboarding](/docs/contributor-onboarding)
  - where to start in the repo, how to run the docs site, and which files to touch for common changes
- [Technical Architecture](/docs/technical-architecture)
  - the high-level system view, scope, stack, repository layout, and architectural style
- [Runtime Subsystems](/docs/architecture/runtime-subsystems)
  - the main implementation areas: session orchestration, synchronization, volume loading, rendering, voice, UI, and XR interaction
- [Scene and Runtime Flows](/docs/architecture/scene-and-flows)
  - how `MainScene` is composed and how startup, reload, modality switching, and voice flows behave end to end
- [VRDF](/docs/architecture/vrdf)
  - the `.vrdf` container, the loader expectations, supported runtime modes, and how the viewer builds textures from it
- [Rendering Pipeline](/docs/architecture/rendering-pipeline)
  - how a selected modality becomes textures, shader inputs, label controls, and the final rendered volume
- [Backend Integration and Contracts](/docs/architecture/backend-integration)
  - the REST, websocket, and runtime-config contracts that connect the viewer to the backend
- [Platform, Build, and Security](/docs/architecture/platform-and-security)
  - Quest/Desktop targeting, build profiles, Android behavior, and current networking/security configuration
- [Performance, Testing, and Risks](/docs/architecture/performance-and-risks)
  - known bottlenecks, current observability, testing gaps, and technical debt
- [Extension Guide and Ownership](/docs/architecture/extension-guide)
  - where to change the system when extending backend fields, modalities, UI behaviors, or AI integrations

## Related Repositories

- [CASSANDRA XR](https://github.com/cassandra-stack/CASSANDRA-XR)
  - the primary Unity/XR application repository and the implementation described by this documentation
- [VRDF SDK](https://github.com/guillaume-schneider/vrdf-sdk)
  - the companion repository for the `.vrdf` format, including the format description, Python encoder, and reference Unity runtime
- [HybridMedRenderer](https://github.com/cassandra-stack/HybridMedRenderer)
  - a related prototype that explored hybrid medical rendering, VRDF integration, and immersive visualization workflows

This site documents the current XR viewer implementation in `cassandra-stack/CASSANDRA-XR`, while the other two repositories provide format-level and renderer-lineage context.

## Recommended Reading Path

If you are new to the project:

1. Start with [Glossary](/docs/glossary) if you want the project vocabulary first.
2. Use [Contributor Onboarding](/docs/contributor-onboarding) for the practical repo entrypoints.
3. Continue with [Technical Architecture](/docs/technical-architecture) for the overall shape.
4. Read [Runtime Subsystems](/docs/architecture/runtime-subsystems) to understand the implementation slices.
5. Then use [Scene and Runtime Flows](/docs/architecture/scene-and-flows) for execution order and interactions.

If you are working on rendering or data loading:

1. Start with [VRDF](/docs/architecture/vrdf).
2. Then read [Rendering Pipeline](/docs/architecture/rendering-pipeline).
3. Use [Performance, Testing, and Risks](/docs/architecture/performance-and-risks) for the practical constraints.

If you are working on backend or runtime integration:

1. Start with [Backend Integration and Contracts](/docs/architecture/backend-integration).
2. Then read [Scene and Runtime Flows](/docs/architecture/scene-and-flows).
3. Use [Platform, Build, and Security](/docs/architecture/platform-and-security) when the change touches transport or deployment behavior.

If you are planning changes:

1. Start with [Contributor Onboarding](/docs/contributor-onboarding).
2. Read [Runtime Subsystems](/docs/architecture/runtime-subsystems).
3. Then use [Extension Guide and Ownership](/docs/architecture/extension-guide).