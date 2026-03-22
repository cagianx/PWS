---
sidebar_position: 3
title: pack
---

# `pack` — Crea un archivio .pws

Impacchetta una **directory sorgente** o un **file `.zip`** in un archivio `.pws`,
firmando opzionalmente i token di ogni sito con ECDSA o HMAC.

## Uso

```
pwstool pack <source> -o <output.pws> [opzioni]
```

### Argomenti posizionali

| Argomento | Obbligatorio | Descrizione |
|-----------|:------------:|-------------|
| `source` | ✓ | Percorso di una **directory** oppure di un file **`.zip`** da impacchettare. |

### Opzioni

| Opzione | Breve | Obbligatorio | Default | Descrizione |
|---------|:-----:|:------------:|:-------:|-------------|
| `--output` | `-o` | ✓ | — | Percorso del file `.pws` di output. |
| `--id` | `-i` | | `site` | Identificatore del sito (alfanumerico + trattini). |
| `--title` | `-t` | | uguale a `--id` | Titolo leggibile del sito, incorporato nel JWT. |
| `--entry` | `-e` | | `index.html` | Entry-point relativo alla radice del sito. |
| `--sign` | `-s` | | `none` | Algoritmo di firma (vedi sotto). |
| `--key-out` | | | — | Salva la chiave pubblica ES256 su file (solo con `--sign ecdsa`). |

### Algoritmi di firma (`--sign`)

| Valore | Descrizione |
|--------|-------------|
| `none` | Nessuna firma — i token usano `alg:none`. Adatto allo sviluppo. |
| `ecdsa` | Genera una nuova coppia di chiavi **ECDSA P-256** (ES256). La chiave pubblica viene incorporata nel manifest e può essere salvata con `--key-out`. |
| `hmac:<segreto>` | Firma con **HMAC-SHA256** usando il segreto fornito. La chiave non viene incorporata nel manifest: è necessario fornirla a `validate --key` per verificare l'archivio. |

:::caution Nota su ECDSA
Ogni esecuzione di `--sign ecdsa` **genera una nuova coppia di chiavi**. Non è possibile
(al momento) riusare una chiave privata esistente. Salva sempre la chiave pubblica con
`--key-out` per poter verificare l'archivio in seguito.
:::

## Exit code

| Codice | Significato |
|:------:|-------------|
| `0` | Archivio creato con successo. |
| `1` | Errore: sorgente non trovata, algoritmo sconosciuto, errore di I/O, ecc. |

## Esempi

```bash
# Da directory — non firmato (sviluppo)
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- \
  pack ./docs/build -o mysite.pws --id docs --title "La mia Documentazione"

# Da directory — firmato ECDSA, chiave pubblica salvata su file
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- \
  pack ./docs/build -o mysite.pws --sign ecdsa --key-out pubkey.txt

# Da file .zip — firmato HMAC
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- \
  pack ./dist.zip -o mysite.pws --sign hmac:miosegreto

# Da directory — entry-point personalizzato
dotnet run --project src/PWS.Tool/PWS.Tool.csproj -- \
  pack ./dist -o mysite.pws --entry home.html --id webapp --title "My App"
```

## Flusso interno

```
source (dir/zip)
   └─ PwsSiteSource
         ├─ SourceDirectory    ← se input è una directory
         └─ AddFile(...)       ← se input è uno .zip (caricato in memoria)
              ↓
         PwsPacker.PackAsync
              ├─ Per ogni file: scrivi in ZIP sotto sites/{id}/
              ├─ Calcola hash Merkle del sito
              ├─ Firma JWT con la chiave scelta
              └─ Scrivi manifest.json
```

Dopo la creazione, usa [`validate`](./validate) per verificare l'integrità dell'archivio.

