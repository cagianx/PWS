#!/usr/bin/env bash
# =============================================================================
# scripts/build-docs-pws.sh
#
# Builds the Docusaurus documentation site, packs it into a .pws archive and
# validates the result — using only pwstool (PWS.Tool).
#
# Usage:
#   ./scripts/build-docs-pws.sh [--no-sign | --hmac <secret>]
#
#   (default: signs with a fresh ECDSA key and saves the public key to
#    artifacts/docs-pubkey.txt)
#
# Options:
#   --no-sign       Pack without signature (alg:none)
#   --hmac <secret> Sign with HMAC-SHA256 using <secret>
#   --help          Show this message
# =============================================================================
set -euo pipefail

# ── Colori ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GRN='\033[0;32m'
YLW='\033[1;33m'
CYN='\033[0;36m'
BLD='\033[1m'
RST='\033[0m'

step()  { echo -e "\n${CYN}${BLD}▶ $*${RST}"; }
ok()    { echo -e "${GRN}✓ $*${RST}"; }
warn()  { echo -e "${YLW}⚠ $*${RST}"; }
die()   { echo -e "${RED}✗ $*${RST}" >&2; exit 1; }

# ── Trova la root del repository ─────────────────────────────────────────────
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# ── Argomenti ─────────────────────────────────────────────────────────────────
SIGN_MODE="ecdsa"   # default
HMAC_SECRET=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-sign)
      SIGN_MODE="none"
      shift ;;
    --hmac)
      [[ $# -ge 2 ]] || die "--hmac richiede un argomento <secret>"
      SIGN_MODE="hmac"
      HMAC_SECRET="$2"
      shift 2 ;;
    --help|-h)
      sed -n '3,16p' "$0" | sed 's/^# \?//'
      exit 0 ;;
    *)
      die "Opzione sconosciuta: $1  (usa --help per l'aiuto)" ;;
  esac
done

# ── Percorsi ──────────────────────────────────────────────────────────────────
DOCS_DIR="$REPO_ROOT/docs"
BUILD_DIR="$DOCS_DIR/build"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
OUTPUT_PWS="$ARTIFACTS_DIR/docs.pws"
OUTPUT_KEY="$ARTIFACTS_DIR/docs-pubkey.txt"
TOOL_CSPROJ="$REPO_ROOT/src/PWS.Tool/PWS.Tool.csproj"

mkdir -p "$ARTIFACTS_DIR"

# ── Costruisci la firma corretta per pwstool ──────────────────────────────────
case "$SIGN_MODE" in
  none)  SIGN_ARG="none" ;;
  ecdsa) SIGN_ARG="ecdsa" ;;
  hmac)  SIGN_ARG="hmac:${HMAC_SECRET}" ;;
esac

# ─────────────────────────────────────────────────────────────────────────────
echo -e "${BLD}╔══════════════════════════════════════════╗${RST}"
echo -e "${BLD}║  build-docs-pws.sh — PWS Browser docs    ║${RST}"
echo -e "${BLD}╚══════════════════════════════════════════╝${RST}"
echo "   Repository : $REPO_ROOT"
echo "   Output     : $OUTPUT_PWS"
echo "   Firma      : $SIGN_MODE"

# ── STEP 1: Build Docusaurus ─────────────────────────────────────────────────
step "1/3  Build Docusaurus (pnpm build)"

if ! command -v pnpm &>/dev/null; then
  die "pnpm non trovato — installare pnpm: npm i -g pnpm"
fi

cd "$DOCS_DIR"
pnpm build
cd "$REPO_ROOT"

ok "Build Docusaurus completata → $BUILD_DIR"

# ── STEP 2: Pack ─────────────────────────────────────────────────────────────
step "2/3  Pack → $OUTPUT_PWS"

PACK_ARGS=(
  pack "$BUILD_DIR"
  --output  "$OUTPUT_PWS"
  --id      "docs"
  --title   "PWS Browser Documentation"
  --entry   "index.html"
  --sign    "$SIGN_ARG"
)

if [[ "$SIGN_MODE" == "ecdsa" ]]; then
  PACK_ARGS+=(--key-out "$OUTPUT_KEY")
fi

dotnet run --project "$TOOL_CSPROJ" -- "${PACK_ARGS[@]}"

ok "Archivio creato: $OUTPUT_PWS ($(du -h "$OUTPUT_PWS" | cut -f1))"

if [[ "$SIGN_MODE" == "ecdsa" && -f "$OUTPUT_KEY" ]]; then
  ok "Chiave pubblica salvata: $OUTPUT_KEY"
fi

# ── STEP 3: Validate ─────────────────────────────────────────────────────────
step "3/3  Validate → $OUTPUT_PWS"

VALIDATE_ARGS=(validate "$OUTPUT_PWS" --verbose)

if [[ "$SIGN_MODE" == "hmac" ]]; then
  VALIDATE_ARGS+=(--key "hmac:${HMAC_SECRET}")
elif [[ "$SIGN_MODE" == "ecdsa" && -f "$OUTPUT_KEY" ]]; then
  # La pubkey è già embedded nel manifest, ma la forniamo esplicitamente
  # come ulteriore dimostrazione dell'opzione --key
  VALIDATE_ARGS+=(--key "$OUTPUT_KEY")
fi

dotnet run --project "$TOOL_CSPROJ" -- "${VALIDATE_ARGS[@]}"

# ── Riepilogo ─────────────────────────────────────────────────────────────────
echo ""
echo -e "${BLD}╔══════════════════════════════════════════╗${RST}"
echo -e "${BLD}║  ✓ Tutto completato con successo!         ║${RST}"
echo -e "${BLD}╚══════════════════════════════════════════╝${RST}"
echo "   Archivio : $OUTPUT_PWS"
echo "   Dimensione: $(du -h "$OUTPUT_PWS" | cut -f1)"
if [[ "$SIGN_MODE" == "ecdsa" && -f "$OUTPUT_KEY" ]]; then
  echo "   Pubkey    : $OUTPUT_KEY"
fi

