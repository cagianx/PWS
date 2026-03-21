---
sidebar_position: 1
---

# Formato .pws

Un file `.pws` (**Portable WebSite**) è un archivio ZIP con struttura interna definita.
Contiene uno o più siti web statici e un manifest JSON firmato con JWT.

## Struttura dell'archivio

```
archivio.pws  (= file ZIP)
├── manifest.json          ← radice: metadati + riferimenti JWT per ogni sito
└── sites/
    ├── docs/              ← sito con id "docs"
    │   ├── index.html
    │   ├── assets/
    │   │   ├── main.css
    │   │   └── main.js
    │   └── intro/
    │       └── index.html
    └── blog/              ← sito con id "blog"
        ├── index.html
        └── posts/
            └── hello.html
```

## manifest.json

```json
{
  "version": "1",
  "created": "2026-03-21T10:00:00+00:00",
  "publicKey": "ES256:MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcD...",
  "sites": [
    {
      "id": "docs",
      "path": "sites/docs/",
      "token": "<JWT — vedi sotto>"
    },
    {
      "id": "blog",
      "path": "sites/blog/",
      "token": "<JWT>"
    }
  ]
}
```

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `version` | `string` | Versione del formato (attualmente `"1"`) |
| `created` | `ISO 8601` | Timestamp di creazione dell'archivio |
| `publicKey` | `string?` | Chiave pubblica ES256 per la verifica dei token, formato `"ALG:base64"`. `null` per pacchetti non firmati |
| `sites` | `SiteManifest[]` | Un elemento per ogni sito incluso |

### SiteManifest

| Campo | Tipo | Descrizione |
|-------|------|-------------|
| `id` | `string` | Identificatore unico del sito (alfanumerico + trattino) |
| `path` | `string` | Prefisso ZIP del sito — sempre `sites/{id}/` |
| `token` | `string` | JWT con metadati e hash del contenuto |

## JWT dei siti

Ogni sito nel manifest ha un **JSON Web Token** nel campo `token`.
L'uso di JWT (invece di un semplice hash) offre:

- **Firma crittografica** del contenuto (ES256 / HS256)
- **Metadati strutturati** nello stesso payload verificabile
- **Standard aperto** — verificabile con qualsiasi libreria JWT

### Header

```json
{ "alg": "ES256", "typ": "JWT" }
```

### Payload (claims)

```json
{
  "sub":       "docs",
  "pws:title": "PWS Browser Docs",
  "pws:entry": "index.html",
  "pws:hash":  "sha256:3a7bd3e2360...",
  "pws:files": 42,
  "iat":       1742551200
}
```

| Claim | Descrizione |
|-------|-------------|
| `sub` | Site ID — corrisponde a `SiteManifest.id` |
| `pws:title` | Titolo leggibile del sito |
| `pws:entry` | Entry point relativo alla radice del sito |
| `pws:hash` | Hash Merkle SHA-256 di tutti i file del sito |
| `pws:files` | Numero di file inclusi nell'hash |
| `iat` | Unix timestamp di firma |

### Hash Merkle deterministico

L'hash `pws:hash` garantisce l'integrità di ogni singolo file:

```
1. Ordina i file per path (ordine lessicografico)
2. Per ogni file:  hash_file = SHA-256(contenuto)
3. Concatena:      len(path_utf8) || path_utf8 || hash_file   (per ogni file)
4. Hash finale:    SHA-256(concatenato)  → "sha256:<hex>"
```

Il prefisso di lunghezza evita attacchi di forgiatura per concatenazione.

## Algoritmi supportati

| Algoritmo | Uso consigliato | Note |
|-----------|----------------|------|
| `none` | Sviluppo / debug | Nessuna garanzia di integrità |
| `HS256` | Pipeline interne | Richiede condivisione del segreto |
| `ES256` | Distribuzione pubblica | Chiave pubblica embedded nel manifest |

