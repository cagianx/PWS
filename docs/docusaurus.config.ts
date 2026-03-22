import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const rawBaseUrl = process.env.DOCS_BASE_URL?.trim();
const baseUrl = !rawBaseUrl
  ? '/'
  : rawBaseUrl === 'blank'
    ? '/'
    : rawBaseUrl.startsWith('/')
      ? (rawBaseUrl.endsWith('/') ? rawBaseUrl : `${rawBaseUrl}/`)
      : `/${rawBaseUrl}${rawBaseUrl.endsWith('/') ? '' : '/'}`;

const config: Config = {
  title: 'PWS Browser',
  tagline: 'Browser .NET MAUI nativo per Linux/GTK4 con contenuti da IContentProvider',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  url: 'https://github.com',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub pages deployment, it is often '/<projectName>/'
  // Se DOCS_BASE_URL è vuoto / "blank", il sito viene buildato alla root ('/').
  // Esempi:
  //   DOCS_BASE_URL=blank       => '/'
  //   DOCS_BASE_URL=/PWS_MAUI/  => '/PWS_MAUI/'
  baseUrl,

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'cagianx', // Usually your GitHub org/user name.
  projectName: 'PWS_MAUI', // Usually your repo name.

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'it',
    locales: ['it'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: '/docs',
        },
        blog: false, // blog non utilizzato
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    // Replace with your project's social card
    image: 'img/docusaurus-social-card.jpg',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'PWS Browser',
      logo: {
        alt: 'PWS Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'pwsSidebar',
          position: 'left',
          label: 'Documentazione',
        },
        {
          href: 'https://github.com/cagianx/PWS_MAUI',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            { label: 'Introduzione', to: '/docs/intro' },
            { label: 'Architettura', to: '/docs/architecture/overview' },
            { label: 'Content Providers', to: '/docs/providers/interface' },
          ],
        },
        {
          title: 'Progetto',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/cagianx/PWS_MAUI',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} PWS Browser. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
