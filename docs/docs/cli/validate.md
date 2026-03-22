---
sidebar_position: 2
title: validate
---

# `validate` — Verifica integrità archivio

Apre un archivio `.pws` e ne verifica ogni livello di integrità:

1. **Struttura ZIP** — il file deve essere un archivio ZIP valido con `manifest.json`.
2. **Manifest** — `manifest.json` deve essere deserializzabile e contenere almeno un sito.
3. **JWT per ogni sito** — il token JWT viene verificato con la chiave pubblica embedded nel manifest
   (o con chiave `none` se il pacchetto non è firmato).
4. **Hash Merkle** — il contenuto effettivo di ogni sito viene ricalcolato e confrontato
   con il campo `pws:hash` nel JWT. Un singolo file modificato invalida l'archivio.

## Uso

```
pwstool validate <file.pws> [opzioni]
```

### Argomenti posizionali

| Argomento | Obbligatorio | Descrizione |
|-----------|:------------:|-------------|
| `file.pws` | ✓ | Percorso del file `.pws` da validare. |

### Opzioni

| Opzione | Breve | Default | Descrizione |
|---------|:-----:|:-------:|-------------|
| `--require-signed` | `-s` | `false` | Rifiuta archivi con token non firmati (`alg:none`). |
| `--verbose` | `-v` | `false` | Mostra hash Merkle, numero di file, data emissione JWT e lista completa dei file. |

## Exit code

| Codice | Significato |
|:------:|-------------|
| `0` | Archivio valido. |
| `1` | Errore: file non trovato, manifest non valido, JWT errato, hash mismatch, ecc. |

## Esempi

```bash
# Validazione base
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- validate artifacts/docs.pws

# Con dettagli: mostra hash Merkle e lista file
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- validate artifacts/docs.pws --verbose

# Rifiuta pacchetti non firmati
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- validate artifacts/docs.pws --require-signed
```

## Output di esempio

```
📂 File    : /path/to/docs.pws
   Dimensione: 387.9 KB

✓ Manifest valido
   Versione : 1
   Creato   : 2026-03-22 11:03:29Z
   Chiave   : ES256

   Siti trovati: 1

  ▸ docs  [🔒 firmato]
    Titolo    : PWS Browser Documentation
    EntryPoint: index.html

✓ Archivio .pws valido.
```

Con `--verbose` vengono aggiunti hash, file count e lista completa delle entry.

## Come funziona l'hash Merkle

Ogni sito viene firmato con un **hash Merkle** calcolato su tutti i file del sito.
Se un qualsiasi file viene modificato dopo la firma, l'hash ricalcolato al momento
della validazione non corrisponderà a quello nel JWT → errore `content hash mismatch`.

Vedere [Hash Merkle deterministico](../format/overview#hash-merkle-deterministico) per i dettagli dell'algoritmo.


