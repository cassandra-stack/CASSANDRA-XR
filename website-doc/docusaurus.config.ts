import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'CASSANDRA XR',
  tagline: 'Technical documentation for the CASSANDRA XR viewer',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  url: 'https://cassandra-stack.github.io',
  baseUrl: '/CASSANDRA-XR/',
  trailingSlash: true,
  organizationName: 'cassandra-stack',
  projectName: 'CASSANDRA-XR',

  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          path: '../docs',
          routeBasePath: 'docs',
          sidebarPath: './sidebars.ts',
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/cassandra-social-card.svg',
    colorMode: {
      defaultMode: 'light',
      disableSwitch: true,
      respectPrefersColorScheme: false,
    },
    navbar: {
      title: 'CASSANDRA XR',
      logo: {
        alt: 'CASSANDRA logo',
        src: 'img/cassandra-mark.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          type: 'dropdown',
          label: 'Architecture',
          position: 'left',
          items: [
            {
              label: 'Overview',
              to: '/docs/technical-architecture',
            },
            {
              label: 'Runtime Subsystems',
              to: '/docs/architecture/runtime-subsystems',
            },
            {
              label: 'Scene and Runtime Flows',
              to: '/docs/architecture/scene-and-flows',
            },
            {
              label: 'VRDF',
              to: '/docs/architecture/vrdf',
            },
            {
              label: 'Rendering Pipeline',
              to: '/docs/architecture/rendering-pipeline',
            },
            {
              label: 'Backend Integration and Contracts',
              to: '/docs/architecture/backend-integration',
            },
            {
              label: 'Platform, Build, and Security',
              to: '/docs/architecture/platform-and-security',
            },
            {
              label: 'Performance, Testing, and Risks',
              to: '/docs/architecture/performance-and-risks',
            },
            {
              label: 'Extension Guide and Ownership',
              to: '/docs/architecture/extension-guide',
            },
          ],
        },
        {
          label: 'GitHub',
          href: 'https://github.com/cassandra-stack/CASSANDRA-XR',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'light',
      links: [
        {
          title: 'Docs',
          items: [
            {
              label: 'Overview',
              to: '/docs/',
            },
            {
              label: 'Glossary',
              to: '/docs/glossary',
            },
            {
              label: 'Contributor Onboarding',
              to: '/docs/contributor-onboarding',
            },
            {
              label: 'Technical Architecture',
              to: '/docs/technical-architecture',
            },
          ],
        },
        {
          title: 'Repositories',
          items: [
            {
              label: 'CASSANDRA XR',
              href: 'https://github.com/cassandra-stack/CASSANDRA-XR',
            },
            {
              label: 'VRDF SDK',
              href: 'https://github.com/guillaume-schneider/vrdf-sdk',
            },
            {
              label: 'HybridMedRenderer',
              href: 'https://github.com/cassandra-stack/HybridMedRenderer',
            },
          ],
        },
      ],
      copyright: `Copyright ${new Date().getFullYear()} CASSANDRA XR`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.github,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
