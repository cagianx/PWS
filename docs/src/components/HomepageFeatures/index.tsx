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
    title: 'Formato Portable WebSite',
    emoji: '📦',
    description: (
      <>
        Un file <code>.pws</code> è un archivio ZIP che contiene un intero sito web
        statico (HTML, CSS, JS, asset). Un sito = un file — portabile come un{' '}
        <code>.epub</code> o un <code>.docx</code>.
      </>
    ),
  },
  {
    title: 'Zero estrazione su disco',
    emoji: '🔒',
    description: (
      <>
        La WebView non tocca mai il filesystem. Ogni risorsa viene servita
        direttamente dall'archivio in-memory tramite <code>IContentProvider</code>,
        senza file temporanei e senza accesso libero al disco.
      </>
    ),
  },
  {
    title: 'GTK4 nativo su Linux',
    emoji: '🐧',
    description: (
      <>
        Powered by <strong>Platform.Maui.Linux.Gtk4</strong>: rendering nativo GTK4,
        WebKitGTK per la WebView. <strong>PWS.Core</strong> è zero-MAUI e
        testabile in isolamento.
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
