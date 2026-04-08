import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  docsSidebar: [
    'intro',
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
        'architecture-platform-and-security',
        'architecture-performance-and-risks',
        'architecture-extension-guide',
      ],
    },
  ],
};

export default sidebars;
