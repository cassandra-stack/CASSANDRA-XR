const config = {
  title: 'CASSANDRA XR',
  tagline: 'Human-centered augmented medical intelligence',
  favicon: 'img/cassandra-logo.png',

  url: 'https://example.com',
  baseUrl: '/',

  organizationName: 'cassandra-xr',
  projectName: 'docs',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',
  onDuplicateRoutes: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      ({
        docs: {
          path: '../docs',
          routeBasePath: 'docs',
          sidebarPath: require.resolve('./sidebars.js'),
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  themeConfig: ({
    image: 'img/cassandra-social-card.svg',
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
          to: '/docs/technical-architecture',
          label: 'Architecture',
          position: 'left',
        },
        {
          to: '/docs/brand-guidelines',
          label: 'Brand',
          position: 'left',
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
              label: 'Brand Guidelines',
              to: '/docs/brand-guidelines',
            },
          ],
        },
        {
          title: 'Design Rules',
          items: [
            {
              label: 'Color System',
              to: '/docs/brand-guidelines#core-palette',
            },
            {
              label: 'UI Guidance',
              to: '/docs/brand-guidelines#ui-system-rules',
            },
          ],
        },
      ],
      copyright: `Copyright ${new Date().getFullYear()} CASSANDRA XR`,
    },
    docs: {
      sidebar: {
        hideable: true,
      },
    },
    colorMode: {
      defaultMode: 'light',
      disableSwitch: true,
      respectPrefersColorScheme: false,
    },
  }),
};

module.exports = config;
