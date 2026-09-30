---
sidebar_position: 99
description: Termini di dominio di PWS, usati con lo stesso significato nella documentazione, nel codice, nei commit e negli artefatti OpenSpec.
---

# Glossario

Termini propri di PWS. I termini tecnici generali (caso d'uso, test di integrazione, OpenSpec, Conventional Commits…) sono definiti nel glossario di MyDocs, incluso nel repository come submodule in `my-docs/docs/glossario.md`.

## Archivio .pws

File **Portable WebSite**: un archivio ZIP che contiene uno o più [siti](#sito), il [manifest](#manifest) e un [token](#token-del-sito) per sito. Si crea con `PwsPacker` (o `pwstool pack`) e si apre con `PwsReader`. Vedi [Formato .pws](format/overview.md).

## Sito

Insieme di file statici (output di build di Docusaurus, Hugo, ecc.) contenuto in un archivio sotto il prefisso `sites/{id}/`. È identificato da un **site id** alfanumerico con trattini, usato anche come host negli URI `pws://{id}/…`. In codice: `PwsSiteSource` in scrittura, `SiteManifest` e `SiteClaims` in lettura.

## Manifest

File `manifest.json` alla radice dell'archivio: versione del formato, data di creazione, [chiave pubblica](#chiave-di-firma) opzionale e l'elenco dei siti con il loro token. In codice: `PwsManifest`.

## Token del sito

JWT associato a ogni sito, con titolo, [entry point](#entry-point), numero di file e [content hash](#content-hash). È firmato con la [chiave di firma](#chiave-di-firma) scelta al momento del packing e verificato all'apertura. In codice: i claim decodificati sono esposti da `SiteClaims`.

## Content hash

Hash Merkle SHA-256 di tutti i file di un sito, nel formato `sha256:…`, indipendente dall'ordine dei file. È registrato nel token e ricalcolato all'apertura: una differenza indica che l'archivio è stato manomesso. In codice: `MerkleHasher`.

## Chiave di firma

Chiave usata per firmare e verificare i token. Tre varianti:

- **None** (`alg:none`): nessuna firma, solo per sviluppo;
- **HMAC** (HS256): chiave simmetrica, non incorporata nel manifest, il lettore deve conoscerla;
- **ECDSA** (ES256): coppia di chiavi P-256, la chiave pubblica viene incorporata nel manifest e consente la verifica automatica.

In codice: `IPwsSigningKey`, creata con la factory `PwsSigningKey`.

## Entry point

Percorso, relativo alla radice del sito, della pagina iniziale. Default `index.html`.

## Content provider

Astrazione da cui il browser ottiene ogni contenuto: la WebView non legge mai dal filesystem. Ogni provider dichiara quali URI sa servire (`CanHandle`) e li risolve (`GetAsync`). In codice: `IContentProvider`. Vedi [Content Providers](providers/interface.md).

## Provider composito

[Content provider](#content-provider) che delega la richiesta al primo provider registrato in grado di gestire l'URI. In codice: `CompositeContentProvider`.

## Server loopback

Piccolo server HTTP su `127.0.0.1`, su porta casuale, dedicato a un singolo sito di un archivio aperto. Espone i file del sito alla WebView, che li carica come un normale sito HTTP. In codice: `LoopbackContentServer`.

## pwstool

CLI del progetto per creare (`pack`) e validare (`validate`) archivi .pws. In codice: progetto `PWS.Tool`. Vedi [CLI](cli/index.md).
