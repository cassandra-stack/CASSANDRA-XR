import clsx from 'clsx';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import styles from './index.module.css';

const pillars = [
  {
    title: 'Trust and Clarity',
    text: 'The official charter centers the product on trust, transparency, science, empathy, and a calm clinical presentation.',
  },
  {
    title: 'Medical XR Interface',
    text: 'Soft blue contrast, pearl-to-white backgrounds, glass secondary actions, and restrained alerts define the UI language.',
  },
  {
    title: 'Technical Depth',
    text: 'The documentation covers runtime orchestration, VRDF ingestion, volume rendering, voice runtime, and platform constraints.',
  },
];

export default function Home() {
  return (
    <Layout
      title="Documentation"
      description="Technical and brand documentation for CASSANDRA XR."
    >
      <main className={styles.page}>
        <section className={styles.hero}>
          <div className={styles.ringAura} />
          <div className={clsx('container', styles.heroInner)}>
            <div className={styles.heroCopy}>
              <p className={styles.kicker}>Human-Centered Medical Intelligence</p>
              <h1>CASSANDRA XR</h1>
              <p className={styles.lead}>
                Documentation for the Unity XR client and its official visual system,
                aligned with the Cassandra graphic charter: trust, transparency,
                scientific rigor, and a calm medical interface language.
              </p>
              <div className={styles.actions}>
                <Link className="button button--primary button--lg" to="/docs/">
                  Open Docs
                </Link>
                <Link className="button button--secondary button--lg" to="/docs/technical-architecture">
                  Technical Architecture
                </Link>
                <Link className="button button--secondary button--lg" to="/docs/brand-guidelines">
                  Brand Guidelines
                </Link>
              </div>
            </div>

            <div className={styles.brandCard}>
              <img
                className={styles.logo}
                src="/img/cassandra-logo.png"
                alt="CASSANDRA official logo"
              />
              <div className={styles.brandGrid}>
                <div>
                  <span className={styles.label}>Palette</span>
                  <p>Bleu Glacier, Bleu Arctique, Gris Perle, Anthracite, Blanc</p>
                </div>
                <div>
                  <span className={styles.label}>Voice</span>
                  <p>Empathic, pedagogic, transparent, forward-looking</p>
                </div>
                <div>
                  <span className={styles.label}>UI Rules</span>
                  <p>12px corners, soft shadows, glass secondary actions, no pure black</p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className={clsx('container', styles.pillars)}>
          {pillars.map((pillar) => (
            <article key={pillar.title} className={styles.card}>
              <h2>{pillar.title}</h2>
              <p>{pillar.text}</p>
            </article>
          ))}
        </section>
      </main>
    </Layout>
  );
}
