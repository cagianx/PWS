#!/usr/bin/env bash
# =============================================================================
# scripts/check-dev-env.sh
#
# Verifica che l'ambiente di sviluppo abbia tutti i prerequisiti di PWS:
# .NET SDK, librerie native GTK4/WebKitGTK 6, Node.js, pnpm e il submodule
# my-docs. Non installa nulla: per ogni mancanza stampa il comando da
# eseguire sulla distribuzione rilevata.
#
# Usage:
#   ./scripts/check-dev-env.sh
#
# Exit code: 0 se tutto è presente, 1 se manca almeno un prerequisito.
#
# Vedi my-docs/docs/regole/ambiente-di-sviluppo.md e
# docs/docs/getting-started/prerequisites.md.
# =============================================================================
set -uo pipefail

# ── Colori ────────────────────────────────────────────────────────────────────
RED='\033[0;31m'
GRN='\033[0;32m'
YLW='\033[1;33m'
CYN='\033[0;36m'
BLD='\033[1m'
RST='\033[0m'

step() { echo -e "\n${CYN}${BLD}▶ $*${RST}"; }
ok()   { echo -e "${GRN}✓ $*${RST}"; }
ko()   { echo -e "${RED}✗ $*${RST}"; MISSING=$((MISSING + 1)); }

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MISSING=0
declare -a PACKAGES=()   # pacchetti di sistema da installare
declare -a HINTS=()      # altri passi (non pacchetti)

# Requisiti minimi, allineati a .github/workflows/ci.yml (pnpm 11 richiede Node ≥ 22.13)
DOTNET_MAJOR=10
NODE_MIN=22.13.0
PNPM_MAJOR=11

# ── Distribuzione ─────────────────────────────────────────────────────────────
DISTRO="unknown"
if [[ -r /etc/os-release ]]; then
    # shellcheck disable=SC1091
    . /etc/os-release
    case " ${ID:-} ${ID_LIKE:-} " in
        *" arch "*)                    DISTRO="arch"   ;;
        *" debian "*|*" ubuntu "*)     DISTRO="debian" ;;
        *" fedora "*|*" rhel "*)       DISTRO="fedora" ;;
    esac
fi

# pkg <arch> <debian> <fedora> → aggiunge il pacchetto giusto per la distro
pkg() {
    case "$DISTRO" in
        arch)   PACKAGES+=("$1") ;;
        debian) PACKAGES+=("$2") ;;
        fedora) PACKAGES+=("$3") ;;
        *)      PACKAGES+=("$1 (Arch) / $2 (Debian) / $3 (Fedora)") ;;
    esac
}

# has_lib <soname> → la libreria è nel cache del linker dinamico.
# Il cache si legge una volta sola: con pipefail, "ldconfig -p | grep -q"
# fallisce per SIGPIPE quando grep esce al primo match.
LDCACHE="$(ldconfig -p 2>/dev/null || /sbin/ldconfig -p 2>/dev/null)"
has_lib() { grep -q "[[:space:]]$1 " <<<"$LDCACHE"; }

# version_ge <a> <b> → a ≥ b (confronto di versioni con sort -V)
version_ge() { [[ "$(printf '%s\n%s\n' "$2" "$1" | sort -V | head -1)" == "$2" ]]; }

echo -e "${BLD}PWS — verifica ambiente di sviluppo${RST} (distro: ${DISTRO})"

# ── .NET SDK ──────────────────────────────────────────────────────────────────
step ".NET SDK"
if command -v dotnet >/dev/null 2>&1; then
    if dotnet --list-sdks 2>/dev/null | grep -q "^${DOTNET_MAJOR}\."; then
        ok ".NET SDK ${DOTNET_MAJOR}.x ($(dotnet --list-sdks | grep "^${DOTNET_MAJOR}\." | tail -1 | cut -d' ' -f1))"
    else
        ko ".NET SDK ${DOTNET_MAJOR}.x non trovato (installati: $(dotnet --list-sdks | cut -d' ' -f1 | tr '\n' ' '))"
        pkg dotnet-sdk dotnet-sdk-${DOTNET_MAJOR}.0 dotnet-sdk-${DOTNET_MAJOR}.0
    fi
else
    ko "dotnet non trovato"
    pkg dotnet-sdk dotnet-sdk-${DOTNET_MAJOR}.0 dotnet-sdk-${DOTNET_MAJOR}.0
fi

# ── Librerie native (caricate a runtime dai binding GirCore) ─────────────────
step "Librerie native GTK4 / WebKitGTK 6"
if has_lib libgtk-4.so.1; then
    ok "GTK4 (libgtk-4.so.1)"
else
    ko "GTK4 non trovato (libgtk-4.so.1)"
    pkg gtk4 libgtk-4-dev gtk4-devel
fi

if has_lib libwebkitgtk-6.0.so.4 && has_lib libjavascriptcoregtk-6.0.so.1; then
    ok "WebKitGTK 6 (libwebkitgtk-6.0.so.4)"
else
    ko "WebKitGTK 6 non trovato (libwebkitgtk-6.0.so.4): la WebView va in crash all'apertura di un .pws"
    pkg webkitgtk-6.0 libwebkitgtk-6.0-dev webkitgtk6.0-devel
fi

# ── Node.js e pnpm (documentazione e test DocsIntegrationTests) ──────────────
step "Node.js e pnpm"
if command -v node >/dev/null 2>&1; then
    if version_ge "$(node --version | sed 's/^v//')" "$NODE_MIN"; then
        ok "Node.js $(node --version)"
    else
        ko "Node.js $(node --version) troppo vecchio (serve ≥ ${NODE_MIN})"
        pkg nodejs nodejs nodejs
    fi
else
    ko "Node.js non trovato"
    pkg nodejs nodejs nodejs
fi

if command -v pnpm >/dev/null 2>&1; then
    pnpm_version="$(pnpm --version 2>/dev/null)"
    if [[ "${pnpm_version%%.*}" == "$PNPM_MAJOR" ]]; then
        ok "pnpm ${pnpm_version}"
    else
        ko "pnpm ${pnpm_version} (serve ${PNPM_MAJOR}.x, come la CI)"
        pkg pnpm pnpm pnpm
    fi
else
    ko "pnpm non trovato (serve a 'cd docs && pnpm build' e a PWS.Format.Tests)"
    pkg pnpm pnpm pnpm
    HINTS+=("in alternativa al pacchetto di sistema: npm install -g pnpm  (o corepack enable pnpm, se corepack è installato)")
fi

# ── Repository ────────────────────────────────────────────────────────────────
step "Repository"
if [[ -f "$REPO_ROOT/my-docs/docs/glossario.md" ]]; then
    ok "submodule my-docs inizializzato"
else
    ko "submodule my-docs non inizializzato"
    HINTS+=("git submodule update --init")
fi

# ── Riepilogo ────────────────────────────────────────────────────────────────
echo
if (( MISSING == 0 )); then
    echo -e "${GRN}${BLD}Ambiente completo.${RST}"
    exit 0
fi

echo -e "${YLW}${BLD}Prerequisiti mancanti: ${MISSING}.${RST}"
if (( ${#PACKAGES[@]} > 0 )); then
    echo -e "\nPacchetti di sistema:"
    case "$DISTRO" in
        arch)   echo "  sudo pacman -S --needed ${PACKAGES[*]}" ;;
        debian) echo "  sudo apt install ${PACKAGES[*]}" ;;
        fedora) echo "  sudo dnf install ${PACKAGES[*]}" ;;
        *)      printf '  - %s\n' "${PACKAGES[@]}" ;;
    esac
fi
if (( ${#HINTS[@]} > 0 )); then
    echo -e "\nAltri passi:"
    printf '  - %s\n' "${HINTS[@]}"
fi
exit 1
