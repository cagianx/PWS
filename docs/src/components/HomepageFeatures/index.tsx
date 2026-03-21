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
    title: 'SSG → .pws → browser',
    emoji: '📦',
    description: (
      <>
        <code>pws pack build/</code> impacchetta l'output di Docusaurus, Hugo o
        Next.js in un singolo file <code>.pws</code>. PWS Browser lo apre e lo
        renderizza — nessun server, nessuna estrazione su disco.
      </>
    ),
  },
  {
    title: 'Zero estrazione su disco',
    emoji: '🔒',
    description: (
      <>
        Ogni risorsa (HTML, CSS, JS, immagini) viene servita direttamente
        dall'archivio ZIP in-memory tramite <code>IContentProvider</code>.
        La WebView non ha accesso diretto al filesystem.
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
