import type {ReactNode} from 'react';
import clsx from 'clsx';
import Heading from '@theme/Heading';
import styles from './styles.module.css';

type FeatureItem = {
  title: string;
  emoji: string;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: 'IContentProvider',
    emoji: '🔌',
    description: (
      <>
        La WebView non tocca mai il filesystem. Ogni contenuto passa attraverso
        l'astrazione <code>IContentProvider</code>, implementabile in qualsiasi modo:
        in-memory, REST API, database, generazione dinamica.
      </>
    ),
  },
  {
    title: 'GTK4 nativo su Linux',
    emoji: '🐧',
    description: (
      <>
        Powered by <strong>Platform.Maui.Linux.Gtk4</strong>: rendering nativo GTK4,
        WebKitGTK per la WebView, nessun workload MAUI tradizionale richiesto.
      </>
    ),
  },
  {
    title: 'Architettura pulita',
    emoji: '🏗️',
    description: (
      <>
        <strong>PWS.Core</strong> è zero-MAUI (testabile ovunque).
        <strong>PWS.App</strong> è il solo layer che conosce MAUI.
        ViewModel, NavigationService e provider sono completamente disaccoppiati.
      </>
    ),
  },
];

function Feature({title, emoji, description}: FeatureItem) {
  return (
    <div className={clsx('col col--4')}>
      <div className="text--center" style={{fontSize: '4rem'}}>
        {emoji}
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
