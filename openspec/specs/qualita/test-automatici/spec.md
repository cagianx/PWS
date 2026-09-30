# qualita/test-automatici Specification

## Purpose
Definisce quali comportamenti dei componenti non grafici di PWS (sorgenti di contenuto, server loopback, formato .pws, CLI, stato del browser) sono certificati dalla suite di test automatici, inclusi i casi in cui un input non valido deve essere rifiutato.

## Requirements

### Requirement: Suite eseguibile senza display
Tutte le suite di test automatici MUST girare su una macchina Linux con il solo .NET SDK, senza display e senza caricare librerie native GTK o WebKitGTK. La CI MUST eseguire ogni suite a ogni push.

#### Scenario: Esecuzione in CI senza display
- **WHEN** la CI esegue le suite di test su un runner senza server grafico
- **THEN** tutte le suite vengono eseguite e nessun test fallisce per mancanza di display o di librerie native

### Requirement: Pagine integrate del browser
Il browser SHALL servire le pagine integrate `pws://home` e `pws://about` come HTML. Una pagina registrata in seguito SHALL essere servita al suo percorso, anche sostituendo una pagina già registrata. Il percorso SHALL essere confrontato senza distinzione di maiuscole e ignorando la `/` finale. Una pagina non registrata SHALL produrre una risposta 404.

#### Scenario: Pagine predefinite
- **WHEN** viene richiesto `pws://home` oppure `pws://about`
- **THEN** la risposta ha stato 200, tipo `text/html` e il titolo della pagina

#### Scenario: Pagina registrata e sostituita
- **WHEN** una pagina viene registrata a un percorso già usato
- **THEN** la richiesta a quel percorso restituisce la nuova pagina

#### Scenario: Percorso con maiuscole o slash finale
- **WHEN** viene richiesto `pws://HOME/`
- **THEN** viene servita la pagina `pws://home`

#### Scenario: Pagina non registrata
- **WHEN** viene richiesto un percorso `pws://` non registrato
- **THEN** la risposta ha stato 404

### Requirement: Composizione di più sorgenti di contenuto
Quando più sorgenti di contenuto sono combinate, ogni richiesta SHALL essere servita dalla prima sorgente, nell'ordine di registrazione, che dichiara di gestire l'URI. Se nessuna sorgente gestisce l'URI, la composizione SHALL dichiararlo e la richiesta MUST fallire con un errore esplicito, senza restituire contenuto.

#### Scenario: Prima sorgente capace
- **WHEN** due sorgenti gestiscono lo stesso URI
- **THEN** risponde la prima registrata

#### Scenario: Nessuna sorgente capace
- **WHEN** si richiede un URI che nessuna sorgente gestisce
- **THEN** la composizione dichiara di non poterlo gestire e la richiesta fallisce con un errore esplicito

### Requirement: Percorsi dei file di un sito
I file di un sito aperto SHALL essere raggiungibili sia con URI `pws://<sito>/<percorso>` sia via HTTP sul server loopback del sito. I percorsi con caratteri codificati (spazi, lettere accentate) SHALL essere decodificati prima di cercare il file. Una richiesta MUST NOT restituire file esterni al sito richiesto, anche se il percorso contiene segmenti `..`. L'identificativo del sito nell'host SHALL essere confrontato senza distinzione di maiuscole. Il tipo MIME SHALL dipendere dall'estensione del file, con `application/octet-stream` per le estensioni non riconosciute.

#### Scenario: Nome di file con spazi o accenti
- **WHEN** l'archivio contiene `pagine/città vecchia.html` e viene richiesto `pagine/citt%C3%A0%20vecchia.html`
- **THEN** la risposta ha stato 200 e il contenuto del file

#### Scenario: Segmenti .. nel percorso
- **WHEN** un archivio con i siti `a` e `b` riceve una richiesta per il sito `a` con percorso `../b/index.html`
- **THEN** la risposta non contiene file del sito `b`

#### Scenario: Host con maiuscole
- **WHEN** viene richiesto `pws://DOCS/index.html` per il sito `docs`
- **THEN** viene servito `index.html` del sito `docs`

#### Scenario: URI senza sito e senza sito di default
- **WHEN** viene richiesto `pws:///index.html` a un archivio con più siti e nessun sito di default
- **THEN** l'URI non viene dichiarato gestibile

#### Scenario: Tipo MIME per estensione
- **WHEN** vengono richiesti file con estensione `.html`, `.css`, `.js`, `.svg`, `.woff2` e `.bin`
- **THEN** i tipi MIME sono rispettivamente `text/html`, `text/css`, `application/javascript`, `image/svg+xml`, `font/woff2` e `application/octet-stream`

### Requirement: Rifiuto di archivi malformati
L'apertura di un archivio .pws MUST fallire con un errore di archivio non valido quando il manifest manca, non è JSON valido o è vuoto, o quando un sito dichiarato ha un token vuoto o i suoi file non corrispondono all'hash firmato. Nessun contenuto SHALL essere servito da un archivio rifiutato.

#### Scenario: Manifest non JSON
- **WHEN** si apre un archivio il cui `manifest.json` non è JSON valido
- **THEN** l'apertura fallisce con un errore di archivio non valido

#### Scenario: Manifest vuoto
- **WHEN** si apre un archivio il cui `manifest.json` contiene `null`
- **THEN** l'apertura fallisce con un errore di archivio non valido

#### Scenario: Sito senza token
- **WHEN** il manifest dichiara un sito con token vuoto
- **THEN** l'apertura fallisce con un errore di archivio non valido

### Requirement: Rifiuto di input di impacchettamento non validi
L'impacchettamento MUST fallire, senza produrre un archivio, quando non è indicato alcun sito, quando due siti hanno lo stesso identificativo (senza distinzione di maiuscole) o quando un identificativo non è utilizzabile come host di un URI `pws://`.

#### Scenario: Nessun sito
- **WHEN** si impacchetta senza indicare siti
- **THEN** l'operazione fallisce con un errore di argomento non valido

#### Scenario: Identificativi duplicati
- **WHEN** si impacchettano due siti con identificativi `docs` e `Docs`
- **THEN** l'operazione fallisce indicando l'identificativo duplicato

#### Scenario: Identificativo non valido come host
- **WHEN** si impacchetta un sito con identificativo `mio sito`
- **THEN** l'operazione fallisce indicando che l'identificativo non è valido

### Requirement: Riga di comando di pwstool
`pwstool` SHALL stampare l'aiuto e terminare con codice 1 quando è invocato senza argomenti o con un verbo sconosciuto. Un verbo noto con opzioni obbligatorie mancanti SHALL terminare con codice diverso da 0 senza eseguire il comando.

#### Scenario: Nessun argomento
- **WHEN** `pwstool` viene eseguito senza argomenti
- **THEN** stampa l'aiuto e termina con codice 1

#### Scenario: Verbo sconosciuto
- **WHEN** `pwstool` viene eseguito con il verbo `unpack`
- **THEN** segnala il verbo sconosciuto, stampa l'aiuto e termina con codice 1

#### Scenario: Opzione obbligatoria mancante
- **WHEN** `pwstool pack` viene eseguito senza la sorgente
- **THEN** termina con codice diverso da 0 e non crea alcun archivio

### Requirement: Barra indirizzi e stato di navigazione
Il browser SHALL accettare nella barra indirizzi solo URL assoluti `http` o `https`; per ogni altro input SHALL mostrare un messaggio di stato e non cambiare la pagina caricata. All'apertura di un sito SHALL caricare l'indirizzo del suo server loopback. Durante il caricamento SHALL indicare lo stato occupato e al termine SHALL aggiornare indirizzo e disponibilità di indietro e avanti. I comandi indietro e avanti SHALL essere abilitati solo quando la navigazione corrispondente è possibile, e il comando di stop solo durante un caricamento.

#### Scenario: URL non valido o schema non supportato
- **WHEN** l'utente inserisce un testo vuoto, un URL relativo o un URL `pws://`
- **THEN** compare un messaggio di stato e la pagina caricata non cambia

#### Scenario: URL http valido
- **WHEN** l'utente inserisce un URL `http://` assoluto
- **THEN** la pagina caricata diventa quell'URL e il browser risulta occupato

#### Scenario: Apertura del sito corrente
- **WHEN** il browser si apre con un sito aperto
- **THEN** carica l'indirizzo del server loopback del sito

#### Scenario: Nessun sito aperto
- **WHEN** il browser si apre senza siti aperti
- **THEN** mostra l'invito ad aprire un file .pws e non carica pagine

#### Scenario: Fine del caricamento
- **WHEN** una pagina termina di caricarsi con navigazione indietro possibile e avanti no
- **THEN** il browser non è più occupato, indietro è abilitato e avanti no

### Requirement: Sito aperto servito su loopback
All'apertura di un archivio l'app SHALL avviare un server HTTP su `127.0.0.1` dedicato al sito di default, che serve i file del sito. All'apertura di un secondo archivio l'app SHALL rilasciare il server e l'archivio precedenti: il vecchio indirizzo MUST NOT servire più contenuti.

#### Scenario: Primo archivio
- **WHEN** l'app apre un archivio
- **THEN** l'indirizzo del server loopback serve la pagina iniziale del sito

#### Scenario: Sostituzione dell'archivio
- **WHEN** l'app apre un secondo archivio
- **THEN** il nuovo indirizzo serve il nuovo sito e il vecchio indirizzo non risponde più
