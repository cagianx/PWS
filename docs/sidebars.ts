import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  pwsSidebar: [
    'intro',
    {
      type: 'category',
      label: '🚀 Getting Started',
      items: [
        'getting-started/prerequisites',
        'getting-started/building',
      ],
    },
    {
      type: 'category',
      label: '🏗️ Architettura',
      items: [
        'architecture/overview',
        'architecture/pws-core',
        'architecture/pws-app',
      ],
    },
    {
      type: 'category',
      label: '📦 Formato .pws',
      items: [
        'format/overview',
        'format/packer',
        'format/reader',
      ],
    },
    {
      type: 'category',
      label: '🔌 Content Providers',
      items: [
        'providers/interface',
        'providers/in-memory',
        'providers/api',
        'providers/composite',
      ],
    },
  ],
};

export default sidebars;
