import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import Layout from '@theme/Layout';

import styles from './index.module.css';

const pillars = [
  {
    title: 'AI-Assisted Interpretation',
    text: 'Interpretive workflows are surfaced through the viewer as part of the technical stack, not as a detached AI layer.',
  },
  {
    title: 'Volume Rendering Pipeline',
    text: 'The documentation exposes how volumetric datasets are decoded, prepared, and rendered in the XR viewer runtime.',
  },
  {
    title: 'Multimodal Fusion',
    text: 'CASSANDRA XR brings multiple imaging signals into a spatial viewing layer designed for exploration and engineering reference.',
  },
];

const entries = [
  {
    title: 'Architecture',
    text: 'System shape, scene composition, runtime orchestration, and how the XR viewer fits into the broader CASSANDRA platform.',
    to: '/docs/technical-architecture',
  },
  {
    title: 'Rendering',
    text: 'VRDF loading, texture preparation, shader selection, and the constraints of rendering medical volumes on XR hardware.',
    to: '/docs/architecture/runtime-subsystems#63-volume-data-format-and-decoding',
  },
  {
    title: 'Integration Flows',
    text: 'How the viewer connects session data, multimodal assets, voice runtime, and study-level interaction flows.',
    to: '/docs/architecture/scene-and-flows#8-end-to-end-runtime-sequences',
  },
];

export default function Home(): ReactNode {
  const walkthroughUrl = useBaseUrl('/video/CASSANDRA-Demo_Interaction.mp4');

  return (
    <Layout
      title="Documentation"
      description="Public technical showcase of the CASSANDRA XR viewer.">
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.ringAura} />
          <div className={clsx('container', styles.heroInner)}>
            <div className={styles.heroCopy}>
              <p className={styles.kicker}>XR Technical Documentation</p>
              <h1>Engineering the XR Layer of CASSANDRA</h1>
              <p className={styles.lead}>
                A public technical view of the viewer architecture behind multimodal
                fusion, volume rendering, and AI-assisted interpretation.
              </p>
              <div className={styles.actions}>
                <a className="button button--primary button--lg" href={walkthroughUrl}>
                  Watch the walkthrough
                </a>
                <Link className="button button--secondary button--lg" to="/docs/technical-architecture">
                  Open the technical docs
                </Link>
              </div>
            </div>

            <div className={styles.mediaCard}>
              <video
                className={styles.video}
                autoPlay
                muted
                loop
                playsInline
                preload="metadata"
                aria-label="CASSANDRA XR walkthrough video">
                <source src={walkthroughUrl} type="video/mp4" />
              </video>
              <div className={styles.mediaCaption}>
                XR walkthrough of the viewer runtime and interaction layer.
              </div>
            </div>
          </div>
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
            CASSANDRA XR is the spatial imaging layer of the broader CASSANDRA
            platform, turning multimodal medical data into an interactive viewer for
            exploration, interpretation, and engineering integration.
          </p>
          <div className={styles.bandLine} />
        </section>

        <section className={clsx('container', styles.entriesSection)}>
          <div className={styles.entriesIntro}>
            <p className={styles.sectionLabel}>Documentation Entry Points</p>
            <h2>Start From The Part You Need</h2>
            <p>
              The architecture documentation is split into focused sections so you can
              move directly into system structure, rendering details, or runtime flows.
            </p>
          </div>

          <div className={styles.entriesPanel}>
            {entries.map((entry) => (
              <article key={entry.title} className={styles.entryRow}>
                <div className={styles.entryCopy}>
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
