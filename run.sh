#!/bin/bash
# Build then launch both apps for local development (Linux / macOS).
# Backend:  dotnet watch run (http profile, http://localhost:5050)
# Frontend: npm run dev (http://localhost:5173)
# Press Ctrl-C once to stop both. Run dev-setup/setup.sh first if you haven't.

set -u

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BACK_DIR="$REPO_ROOT/docker-compose-manager-back"
FRONT_DIR="$REPO_ROOT/docker-compose-manager-front"

CYAN='\033[0;36m'; GREEN='\033[0;32m'; RED='\033[0;31m'; WHITE='\033[0;37m'; YELLOW='\033[0;33m'; NC='\033[0m'

header() {
    echo -e "\n${CYAN}========================================${NC}"
    echo -e "${CYAN}  $1${NC}"
    echo -e "${CYAN}========================================${NC}\n"
}

# ----- Build -----------------------------------------------------------------
header "Build backend (.NET)"
( cd "$BACK_DIR" && dotnet build --nologo -v q ) || { echo -e "${RED}Backend build FAILED.${NC}"; exit 1; }

header "Build frontend (SvelteKit)"
( cd "$FRONT_DIR" && npm run build ) || { echo -e "${RED}Frontend build FAILED.${NC}"; exit 1; }

# ----- Launch ----------------------------------------------------------------
header "Launch (Ctrl-C to stop both)"
echo -e "  backend  -> http://localhost:5050"
echo -e "  frontend -> http://localhost:5173"
echo -e "  login     : admin / adminadmin\n"

# Kill the whole process group (both servers and their children) on exit/Ctrl-C.
trap 'echo -e "\n${YELLOW}Stopping...${NC}"; kill 0' SIGINT SIGTERM EXIT

( cd "$BACK_DIR" && exec dotnet watch run --project docker-compose-manager-back --launch-profile http ) &
( cd "$FRONT_DIR" && exec npm run dev ) &

wait
