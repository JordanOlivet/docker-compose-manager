#!/bin/bash
# Dev environment bootstrap for Lighthouse (Linux / macOS).
# Detects prerequisites, prompts before installing any missing ones, generates
# gitignored local dev config, and installs project dependencies.
# This script NEVER launches the app - use ../run.sh to build and run.

set -u

# Keep the terminal open on exit (success or failure) so the output stays
# readable when launched by double-click instead of from a shell.
trap 'echo; read -rsn1 -p "Press any key to close..."; echo' EXIT

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BACK_DIR="$REPO_ROOT/lighthouse-back"
BACK_PROJ="$BACK_DIR/lighthouse-back"
FRONT_DIR="$REPO_ROOT/lighthouse-front"
SAMPLE_DIR="$SCRIPT_DIR/sample-compose-files"

CYAN='\033[0;36m'; GREEN='\033[0;32m'; RED='\033[0;31m'; YELLOW='\033[0;33m'; NC='\033[0m'

header() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

# yes/no prompt; first arg = prompt, second arg = "y" if default-yes (default).
read_yesno() {
    local prompt="$1"; local default="${2:-y}"; local suffix answer
    if [ "$default" = "y" ]; then suffix="[Y/n]"; else suffix="[y/N]"; fi
    read -r -p "$prompt $suffix " answer
    answer="${answer:-$default}"
    [[ "$answer" =~ ^([yY]|[yY][eE][sS])$ ]]
}

has() { command -v "$1" >/dev/null 2>&1; }

# Detect OS and package manager.
OS="$(uname -s)"
PKG=""
if [ "$OS" = "Darwin" ]; then
    PKG="brew"
elif has apt-get; then PKG="apt"
elif has dnf; then PKG="dnf"
elif has pacman; then PKG="pacman"
fi

# install_tool <brew_pkg> <apt_pkg> <dnf_pkg> <pacman_pkg> <manual_url>
install_tool() {
    local brew_p="$1" apt_p="$2" dnf_p="$3" pac_p="$4" url="$5"
    case "$PKG" in
        brew)   echo -e "${YELLOW}  brew install $brew_p${NC}";   brew install "$brew_p" ;;
        apt)    echo -e "${YELLOW}  sudo apt-get install $apt_p${NC}"; sudo apt-get update && sudo apt-get install -y $apt_p ;;
        dnf)    echo -e "${YELLOW}  sudo dnf install $dnf_p${NC}"; sudo dnf install -y $dnf_p ;;
        pacman) echo -e "${YELLOW}  sudo pacman -S $pac_p${NC}";  sudo pacman -S --noconfirm $pac_p ;;
        *)      echo -e "${RED}  No supported package manager found. Install manually: $url${NC}"; return 1 ;;
    esac
}

# ---------------------------------------------------------------------------
header "Prerequisite check"
missing=()

if has git; then
    echo -e "  git       ${GREEN}OK${NC}   $(git --version)"
else
    echo -e "  git       ${RED}MISSING${NC}"; missing+=("git")
fi

dotnet_ok=0
if has dotnet; then
    dver="$(dotnet --version)"; dmaj="${dver%%.*}"
    if [ "$dmaj" -ge 10 ] 2>/dev/null; then
        echo -e "  .NET SDK  ${GREEN}OK${NC}   $dver"; dotnet_ok=1
    else
        echo -e "  .NET SDK  ${RED}TOO OLD${NC} ($dver, need >= 10)"; missing+=("dotnet")
    fi
else
    echo -e "  .NET SDK  ${RED}MISSING${NC}"; missing+=("dotnet")
fi

node_ok=0
if has node; then
    nver="$(node -v)"; nver="${nver#v}"; nmaj="${nver%%.*}"
    if [ "$nmaj" -ge 26 ] 2>/dev/null; then
        echo -e "  Node      ${GREEN}OK${NC}   v$nver"; node_ok=1
    else
        echo -e "  Node      ${RED}TOO OLD${NC} (v$nver, need >= 26)"; missing+=("node")
    fi
else
    echo -e "  Node      ${RED}MISSING${NC}"; missing+=("node")
fi

if has docker; then
    if docker info >/dev/null 2>&1; then
        echo -e "  Docker    ${GREEN}OK${NC}   (daemon reachable)"
    else
        echo -e "  Docker    ${YELLOW}INSTALLED but daemon NOT running / no permission${NC}"
    fi
else
    echo -e "  Docker    ${RED}MISSING${NC}"; missing+=("docker")
fi

# ---------------------------------------------------------------------------
if [ "${#missing[@]}" -gt 0 ]; then
    header "Install missing prerequisites"
    [ -z "$PKG" ] && echo -e "${YELLOW}  No package manager detected; links will be printed instead.${NC}\n"
    for tool in "${missing[@]}"; do
        case "$tool" in
            git)    brew_p="git";          apt_p="git";          dnf_p="git";          pac_p="git";          url="https://git-scm.com/downloads" ;;
            dotnet) brew_p="dotnet-sdk";   apt_p="dotnet-sdk-10.0"; dnf_p="dotnet-sdk-10.0"; pac_p="dotnet-sdk"; url="https://dotnet.microsoft.com/download/dotnet/10.0" ;;
            node)   brew_p="node@24";      apt_p="nodejs npm";   dnf_p="nodejs";       pac_p="nodejs npm";   url="https://nodejs.org/" ;;
            docker) brew_p="--cask docker"; apt_p="docker.io";   dnf_p="docker";       pac_p="docker";       url="https://docs.docker.com/engine/install/" ;;
        esac
        if read_yesno "  Install '$tool' now?"; then
            install_tool "$brew_p" "$apt_p" "$dnf_p" "$pac_p" "$url" || true
        else
            echo -e "${YELLOW}  Skipped '$tool'. Install manually: $url${NC}"
        fi
    done
    echo -e "\n${YELLOW}  NOTE: re-run this script after installing to continue.${NC}"
fi

# ---------------------------------------------------------------------------
header "Generate local dev config"
mkdir -p "$SAMPLE_DIR"

LOCAL_SETTINGS="$BACK_PROJ/appsettings.Development.local.json"
if [ -f "$LOCAL_SETTINGS" ]; then
    echo -e "${YELLOW}  appsettings.Development.local.json already exists - leaving as is.${NC}"
else
    cat > "$LOCAL_SETTINGS" <<EOF
{
  "Docker": {
    "Host": "unix:///var/run/docker.sock"
  },
  "ComposeDiscovery": {
    "HostPathMapping": "$SAMPLE_DIR"
  }
}
EOF
    echo -e "  ${GREEN}Created${NC} $LOCAL_SETTINGS"
fi

FRONT_ENV="$FRONT_DIR/.env.development"
if [ -f "$FRONT_ENV" ]; then
    echo -e "${YELLOW}  .env.development already exists - leaving as is.${NC}"
else
    # API calls are proxied through the Vite dev server (see vite.config.ts).
    # Set VITE_API_URL here only if your backend runs on a non-default host/port.
    printf "# VITE_API_URL=http://localhost:5050\n" > "$FRONT_ENV"
    echo -e "  ${GREEN}Created${NC} $FRONT_ENV"
fi

# ---------------------------------------------------------------------------
header "Install dependencies"
if [ "$dotnet_ok" -eq 1 ]; then
    echo -e "${CYAN}  dotnet restore (backend)...${NC}"
    ( cd "$BACK_DIR" && dotnet restore )
else
    echo -e "${YELLOW}  Skipped dotnet restore (.NET SDK not ready).${NC}"
fi

if [ "$node_ok" -eq 1 ]; then
    echo -e "${CYAN}  npm install (frontend)...${NC}"
    ( cd "$FRONT_DIR" && npm install )
else
    echo -e "${YELLOW}  Skipped npm install (Node not ready).${NC}"
fi

# ---------------------------------------------------------------------------
header "Optional tools"
if [ "$dotnet_ok" -eq 1 ] && read_yesno "  Install dotnet-ef global tool (only needed to author DB migrations)?" "n"; then
    dotnet tool install --global dotnet-ef
fi

# ---------------------------------------------------------------------------
header "Done"
echo -e "  ${GREEN}Setup complete.${NC} To build and run both apps:"
echo -e "      ./run.sh   (from the repo root)"
echo ""
echo -e "  App URLs:  backend http://localhost:5050   frontend http://localhost:5173"
echo -e "  Login:     admin / adminadmin   (must change on first login)"
echo ""
