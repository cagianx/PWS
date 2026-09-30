# app-linux/backend-gtk4 Specification

## Purpose
Definisce il backend di piattaforma su cui gira l'app desktop Linux e i comportamenti della finestra che ogni cambio di backend deve preservare.

## Requirements

### Requirement: Backend di piattaforma mantenuto e fissato
L'app Linux MUST dipendere da un backend di piattaforma pubblicato da un progetto mantenuto attivamente, non da un repository dismesso o spostato. La versione del backend MUST essere fissata in modo esatto, senza intervalli né wildcard, perché il backend è sperimentale e può introdurre cambiamenti incompatibili tra una versione e l'altra.

#### Scenario: Build riproducibile del backend
- **WHEN** la solution viene ripristinata e compilata in due momenti diversi senza modifiche al repository
- **THEN** viene usata la stessa identica versione del backend di piattaforma

#### Scenario: Nessuna dipendenza inutilizzata dal backend
- **WHEN** un pacchetto opzionale del backend non viene registrato né usato dall'app
- **THEN** quel pacchetto non compare tra le dipendenze dell'app

### Requirement: Avvio e apertura di un archivio
L'app SHALL avviarsi mostrando la pagina iniziale e SHALL consentire di scegliere un archivio .pws con il selettore di file nativo del desktop. Dopo l'apertura di un archivio valido, l'app SHALL mostrare la pagina del browser con l'entry point del sito caricato.

#### Scenario: Avvio dell'app
- **WHEN** l'utente avvia l'app su un desktop Linux con GTK4 e WebKitGTK 6 installati
- **THEN** si apre una finestra con la pagina iniziale e il comando per aprire un archivio

#### Scenario: Apertura di un archivio valido
- **WHEN** l'utente sceglie un archivio .pws valido dal selettore di file
- **THEN** l'app mostra la pagina del browser con la pagina iniziale del sito

#### Scenario: Selezione annullata
- **WHEN** l'utente chiude il selettore di file senza scegliere un archivio
- **THEN** l'app resta sulla pagina iniziale senza errori

### Requirement: Ridimensionamento della finestra
Quando la finestra viene ridimensionata, il contenuto della pagina visibile SHALL adattarsi alle nuove dimensioni della finestra, anche dopo che l'utente è passato dalla pagina iniziale alla pagina del browser.

#### Scenario: Resize sulla pagina del browser
- **WHEN** l'utente ridimensiona la finestra mentre è visibile la pagina del browser
- **THEN** barra degli strumenti, area di contenuto e barra di stato occupano la nuova larghezza e altezza della finestra

#### Scenario: Resize dopo la navigazione tra pagine
- **WHEN** l'utente apre un archivio, passando dalla pagina iniziale a quella del browser, e poi ridimensiona la finestra più volte
- **THEN** ogni ridimensionamento aggiorna il layout senza errori e senza lasciare il contenuto alle dimensioni precedenti

### Requirement: Aggiornamenti dell'interfaccia da thread in background
Gli aggiornamenti dell'interfaccia originati da operazioni asincrone (caricamento delle pagine, notifiche di navigazione, messaggi di errore) SHALL essere applicati sul thread dell'interfaccia senza bloccarla né generare errori.

#### Scenario: Navigazione all'interno del sito
- **WHEN** l'utente segue un link interno del sito aperto
- **THEN** la pagina di destinazione viene mostrata e la barra degli indirizzi riflette il nuovo indirizzo

#### Scenario: Errore durante l'apertura
- **WHEN** l'utente sceglie un file che non è un archivio .pws valido
- **THEN** l'app mostra un messaggio di errore comprensibile e resta utilizzabile
