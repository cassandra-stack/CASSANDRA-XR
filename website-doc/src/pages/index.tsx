import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Layout from '@theme/Layout';

import styles from './index.module.css';

const pillars = [
  {
    title: 'AI-Assisted Interpretation',
    text: 'Inference-driven interpretation is integrated into viewer workflows, keeping analysis contextual to the displayed study state.',
  },
  {
    title: 'Volume Rendering Pipeline',
    text: 'The runtime documents how VRDF datasets are decoded, fused across imaging inputs, converted into textures, and rendered through the XR shader path.',
  },
  {
    title: 'XR Interaction',
    text: 'The viewer is built around natural user interaction, including mid-air manipulation, vision-based hand tracking, and spatial UI behaviors for immersive use.',
  },
];

const entries = [
  {
    title: 'System Architecture',
    text: 'System shape, scene composition, orchestration, and how the XR viewer fits into the broader CASSANDRA platform.',
    to: '/docs/technical-architecture',
    meta: 'System overview',
  },
  {
    title: 'Rendering Pipeline',
    text: 'VRDF loading, texture preparation, shader selection, multimodal composition, and the constraints of rendering medical volumes on XR hardware.',
    to: '/docs/architecture/runtime-subsystems#volume-data-format-and-decoding',
    meta: 'Rendering internals',
  },
  {
    title: 'Runtime Flows',
    text: 'How the viewer connects session data, multimodal assets, voice runtime, and study-level interaction flows.',
    to: '/docs/architecture/scene-and-flows#end-to-end-runtime-sequences',
    meta: 'Execution paths',
  },
];

const facts = [
  'Unity 6 / URP',
  'Meta Quest and Desktop',
  'VRDF Volume Pipeline',
  'Websocket Session Sync',
  'Voice and AI Runtime',
];

export default function Home(): ReactNode {
  const walkthroughUrl = useBaseUrl('/video/CASSANDRA-Demo_Interaction.mp4');
  const walkthroughPosterUrl = useBaseUrl('/img/cassandra-social-card.svg');

  return (
    <Layout
      title="Documentation"
      description="Technical documentation for the CASSANDRA XR viewer.">
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.ringAura} />
          <div className={clsx('container', styles.heroInner)}>
            <div className={styles.heroCopy}>
              <p className={styles.kicker}>XR Technical Documentation</p>
              <h1>Engineering the XR Layer of CASSANDRA</h1>
              <p className={styles.lead}>
                Technical documentation for the viewer layer behind multimodal fusion,
                volume rendering, XR interaction, and AI-assisted interpretation.
              </p>
              <div className={styles.actions}>
                <Link className="button button--primary button--lg" to="/docs/">
                  Open Documentation
                </Link>
                <Link className={styles.textLink} to="/docs/technical-architecture">
                  Technical Architecture
                </Link>
              </div>
            </div>

            <div className={styles.mediaBlock}>
              <p className={styles.mediaLabel}>Viewer Walkthrough</p>
              <div className={styles.mediaCard}>
                <video
                  className={styles.video}
                  autoPlay
                  controls
                  muted
                  loop
                  playsInline
                  preload="metadata"
                  poster={walkthroughPosterUrl}
                  aria-label="CASSANDRA XR walkthrough video">
                  <source src={walkthroughUrl} type="video/mp4" />
                </video>
              </div>
              <div className={styles.mediaCaption}>
                Runtime behavior, viewer interaction, and spatial exploration in the XR client.
              </div>
            </div>
          </div>
        </section>

        <section className={clsx('container', styles.factsBand)}>
          {facts.map((fact) => (
            <span key={fact} className={styles.factItem}>{fact}</span>
          ))}
        </section>

        <section className={clsx('container', styles.pillars)}>
          {pillars.map((pillar) => (
            <article key={pillar.title} className={styles.pillar}>
              <h2>{pillar.title}</h2>
              <p>{pillar.text}</p>
            </article>
          ))}
        </section>

        <section className={clsx('container', styles.platformBand)}>
          <div className={styles.bandLine} />
          <p>
            CASSANDRA XR is the spatial imaging layer of the broader CASSANDRA platform, turning multimodal medical data into an interactive viewer for exploration, interpretation, and engineering integration.
          </p>
          <div className={styles.bandLine} />
        </section>

        <section className={clsx('container', styles.entriesSection)}>
          <div className={styles.entriesIntro}>
            <p className={styles.sectionLabel}>Documentation Entry Points</p>
            <h2>Navigate The Viewer By Concern</h2>
            <p>
              The architecture documentation is split into focused sections so you can move directly into structure, rendering internals, or runtime execution flows.
            </p>
          </div>

          <div className={styles.entriesPanel}>
            {entries.map((entry) => (
              <article key={entry.title} className={styles.entryRow}>
                <div className={styles.entryCopy}>
                  <p className={styles.entryMeta}>{entry.meta}</p>
                  <h3>{entry.title}</h3>
                  <p>{entry.text}</p>
                </div>
                <Link className={styles.entryLink} to={entry.to}>
                  Open section
                </Link>
              </article>
            ))}
          </div>
        </section>
      </main>
    </Layout>
  );
}

