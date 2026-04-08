import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'intro',
    'glossary',
    'contributor-onboarding',
    {
      type: 'category',
      label: 'Architecture',
      link: {
        type: 'doc',
        id: 'technical-architecture',
      },
      items: [
        'technical-architecture',
        'architecture-runtime-subsystems',
        'architecture-scene-and-flows',
        'architecture-vrdf',
        'architecture-rendering-pipeline',
        'architecture-backend-integration',
        'architecture-platform-and-security',
        'architecture-performance-and-risks',
        'architecture-extension-guide',
      ],
    },
  ],
};

export default sidebars;