#!/bin/bash
# Build both apps, then launch each in its own terminal window when a GUI
# terminal emulator is available; otherwise fall back to combined logs in the
# current terminal (Linux / macOS).
# Backend:  dotnet watch run (http profile, http://localhost:5050)
# Frontend: npm run dev (http://localhost:5173)
# Run dev-setup/setup.sh first if you haven't.

set -u

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACK_DIR="$REPO_ROOT/lighthouse-back"
FRONT_DIR="$REPO_ROOT/lighthouse-front"

CYAN='\033[0;36m'; GREEN='\033[0;32m'; RED='\033[0;31m'; WHITE='\033[0;37m'; YELLOW='\033[0;33m'; NC='\033[0m'

header() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

# ----- Build -----------------------------------------------------------------
header "Build backend (.NET)"
( cd "$BACK_DIR" && dotnet build --nologo -v q ) || { echo -e "${RED}Backend build FAILED.${NC}"; read -rsn1 -p "Press any key to close..."; echo; exit 1; }

header "Build frontend (SvelteKit)"
( cd "$FRONT_DIR" && npm run build ) || { echo -e "${RED}Frontend build FAILED.${NC}"; read -rsn1 -p "Press any key to close..."; echo; exit 1; }

# ----- Launch ----------------------------------------------------------------
header "Launch"

BACK_CMD="cd '$BACK_DIR' && dotnet watch run --project lighthouse-back --launch-profile http"
FRONT_CMD="cd '$FRONT_DIR' && npm run dev"

# Try to open each app in its own GUI terminal window. Returns 0 on success.
open_separate() {
    local os; os="$(uname -s)"
    if [ "$os" = "Darwin" ]; then
        osascript -e "tell application \"Terminal\" to do script \"$BACK_CMD\"" >/dev/null 2>&1 || return 1
        osascript -e "tell application \"Terminal\" to do script \"$FRONT_CMD\"" >/dev/null 2>&1 || return 1
        return 0
    fi
    # Linux: only attempt if a graphical session is present.
    if [ -z "${DISPLAY:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
        return 1
    fi
    local term
    for term in gnome-terminal konsole xfce4-terminal x-terminal-emulator xterm; do
        if command -v "$term" >/dev/null 2>&1; then
            case "$term" in
                gnome-terminal)
                    "$term" --title="Backend (.NET)"        -- bash -c "$BACK_CMD;  exec bash"
                    "$term" --title="Frontend (SvelteKit)"  -- bash -c "$FRONT_CMD; exec bash"
                    ;;
                *)
                    "$term" -e bash -c "$BACK_CMD;  exec bash" &
                    "$term" -e bash -c "$FRONT_CMD; exec bash" &
                    ;;
            esac
            return 0
        fi
    done
    return 1
}

if open_separate; then
    echo -e "  ${GREEN}Opened two terminal windows.${NC}"
    echo -e "  backend  -> http://localhost:5050   (window: Backend)"
    echo -e "  frontend -> http://localhost:5173   (window: Frontend)"
    echo -e "  login     : admin / adminadmin"
    echo -e "\n  Close a window (or Ctrl-C in it) to stop that app."
else
    echo -e "  ${YELLOW}No GUI terminal available - running both here (combined logs).${NC}"
    echo -e "  backend  -> http://localhost:5050"
    echo -e "  frontend -> http://localhost:5173"
    echo -e "  login     : admin / adminadmin"
    echo -e "  ${YELLOW}Ctrl-C stops both.${NC}\n"

    # Kill the whole process group (both servers and their children) on exit/Ctrl-C.
    trap 'echo -e "\n${YELLOW}Stopping...${NC}"; kill 0' SIGINT SIGTERM EXIT

    ( cd "$BACK_DIR" && exec dotnet watch run --project lighthouse-back --launch-profile http ) &
    ( cd "$FRONT_DIR" && exec npm run dev ) &
    wait
fi
