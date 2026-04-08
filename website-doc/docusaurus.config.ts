import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'CASSANDRA XR',
  tagline: 'Public technical showcase of the XR viewer',
  favicon: 'img/cassandra-logo.png',

  future: {
    v4: true,
  },

  url: 'https://example.com',
  baseUrl: '/',

  organizationName: 'cassandra-xr',
  projectName: 'docs',

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
        src: 'img/cassandra-logo.png',
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
              label: 'Technical Architecture',
              to: '/docs/technical-architecture',
            },
            {
              label: 'Runtime Subsystems',
              to: '/docs/architecture/runtime-subsystems',
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
