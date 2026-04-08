---
id: brand-guidelines
title: Brand Guidelines
sidebar_position: 3
slug: /brand-guidelines
---

# CASSANDRA Brand Guidelines

This page summarizes the official Cassandra graphic charter provided in `charte_graphique_cassandra.pdf`.

## Identity Positioning

CASSANDRA is positioned as human-centered augmented medical intelligence.

The charter frames the brand around these values:

- trust
- transparency
- science
- humanism
- ethics
- future orientation

The visual language should feel clinical, calm, rigorous, and empathetic rather than aggressive or alarmist.

## Logo System

The official logo composition is described as a combination of:

- a female side-profile silhouette representing listening, wisdom, and clinical intuition
- a stylized neural network representing cognition, neuroimaging, and applied AI
- concentric circles representing propagation of knowledge and multi-scale contextual analysis
- a stable all-caps wordmark for scientific rigor and geometric clarity

### Usage Rules

- Prefer light backgrounds such as white or pearl gray for official communication.
- Dark backgrounds are acceptable for demos and VR or XR interfaces, using night-blue or anthracite tones rather than pure black.
- A monochrome variant in glacier blue or white is intended for grayscale output, watermarking, and medical reports.
- Keep a protected area around the logo equal to the radius of the inner circle.
- Do not let text, icons, or panel edges intrude into that protected area.

## Core Palette

| Color | HEX | Intended role |
| --- | --- | --- |
| Bleu Glacier | `#6FC3D0` | Calm intelligence, visual trust, soft atmospheric accents |
| Bleu Arctique | `#4A90E2` | Technological accent, primary actions, selected states |
| Gris Perle | `#E6E9EF` | Neutral premium surface, readable and non-aggressive backgrounds |
| Anthracite | `#2F3B45` | Strong text, clinical seriousness, critical framing |
| Blanc | `#FFFFFF` | Purity, transparency, breathing room |

### Palette Constraints

- Do not use pure black as the default background.
- Do not rely on aggressively saturated blue.
- Prefer soft contrast and visual safety over dramatic effect.
- Avoid alarmist color treatment unless the interaction is truly critical.

## Typography

The charter defines a three-level typographic hierarchy.

- Titles and identity: bold sans-serif, with examples such as Helvetica or Latin Modern for geometric stability.
- Body text: readable sans-serif suitable for screens, VR surfaces, and clinical support material.
- Technical values and IDs: monospace, with examples such as Courier, for metrics, IDs, and unambiguous numeric display.

### Typography Rules

- Keep the wordmark `CASSANDRA` in uppercase.
- Do not stretch or compress logotype letterforms.
- Avoid decorative, handwritten, or playful type styles.

## Iconography and Illustration

The visual language should remain vectorial, clean, and biomedical.

- Use soft lines and thin contours.
- Favor slightly rounded geometry.
- Use subtle blue and gray gradients.
- Avoid rainbow-like or noisy color effects.
- Reuse neural motifs, concentric waves, and stylized anatomical structures when decorative texture is needed.

### VR and XR Guidance

- Add a soft blue halo around interactive elements.
- Use Bleu Arctique for clickable or selected controls.
- Avoid saturated red except for actual critical medical alerts.

## UI System Rules

The charter gives direct component guidance that maps well to the Docusaurus site.

- Primary buttons: Bleu Arctique background, white text, rounded corners around `12px`.
- Secondary buttons: translucent glass treatment with a thin Bleu Glacier outline.
- Panels and cards: white or Gris Perle surfaces, `12px` corner radius, soft shadow around `rgba(0,0,0,0.1)`.
- Hover or active feedback: lighten toward `#79D7E1`.
- Recommended background treatment: radial gradient from `#E6E9EF` to `#FFFFFF`.
- Use Anthracite to frame critical zones rather than switching to pure black.

## Voice and Copy Tone

The brand voice should always be:

- empathic
- pedagogic
- transparent
- forward-looking

The charter explicitly rejects fear-driven wording. Product language should guide, contextualize, and acknowledge uncertainty rather than dramatize findings.

## Usage Contexts

The charter distinguishes several main contexts.

- VR and XR dashboards: blue palette, soft contrast, visible interactive halo, one active alert color at a time.
- Internal technical documents: white background, Bleu Arctique titles, Gris Perle information boxes.
- Public presentations and demos: concentric rings as watermark, large Glacier Blue fields, and reuse of the silhouette motif.
- Medical PDF reports: discreet monochrome logo watermark, short sentences, aligned values, Glacier Blue plus Anthracite without dramatic red treatment.

## Notes For This Docs Site

The Docusaurus theme has been aligned to the charter in these ways:

- official palette variables map to the documented color system
- page backgrounds use a pearl-to-white radial gradient
- buttons and cards use the `12px` radius rule
- secondary actions use a glass-like translucent treatment
- the official logo asset is used instead of an inferred emblem
- typography follows the sans-serif plus Courier hierarchy described in the charter
