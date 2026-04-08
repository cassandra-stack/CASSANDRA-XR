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

- [Technical Architecture](/docs/technical-architecture)
  - the high-level system view, scope, stack, repository layout, and architectural style
- [Runtime Subsystems](/docs/architecture/runtime-subsystems)
  - the main implementation areas: session orchestration, synchronization, volume loading, rendering, voice, UI, and XR interaction
- [Scene and Runtime Flows](/docs/architecture/scene-and-flows)
  - how `MainScene` is composed and how startup, reload, modality switching, and voice flows behave end to end
- [Platform, Build, and Security](/docs/architecture/platform-and-security)
  - Quest/Desktop targeting, build profiles, Android behavior, and current networking/security configuration
- [Performance, Testing, and Risks](/docs/architecture/performance-and-risks)
  - known bottlenecks, current observability, testing gaps, and technical debt
- [Extension Guide and Ownership](/docs/architecture/extension-guide)
  - where to change the system when extending backend fields, modalities, UI behaviors, or AI integrations

## Recommended Reading Path

If you are new to the project:

1. Start with [Technical Architecture](/docs/technical-architecture) for the overall shape.
2. Continue with [Runtime Subsystems](/docs/architecture/runtime-subsystems) to understand the implementation slices.
3. Read [Scene and Runtime Flows](/docs/architecture/scene-and-flows) to understand execution order and interactions.

If you are working on deployment or environment issues:

1. Go to [Platform, Build, and Security](/docs/architecture/platform-and-security).
2. Then check [Performance, Testing, and Risks](/docs/architecture/performance-and-risks).

If you are planning changes:

1. Read [Runtime Subsystems](/docs/architecture/runtime-subsystems).
2. Then use [Extension Guide and Ownership](/docs/architecture/extension-guide).
