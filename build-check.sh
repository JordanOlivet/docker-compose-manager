#!/bin/bash
# Simple build/check script for backend and frontend

set -e

CYAN='\033[0;36m'
GREEN='\033[0;32m'
RED='\033[0;31m'
NC='\033[0m'

backend_result=0
frontend_result=0

echo -e "\n${CYAN}========================================${NC}"
echo -e "${CYAN}  Backend Build (.NET)${NC}"
echo -e "${CYAN}========================================${NC}\n"

pushd docker-compose-manager-back > /dev/null
dotnet build --nologo -v q || backend_result=$?
popd > /dev/null

echo -e "\n${CYAN}========================================${NC}"
echo -e "${CYAN}  Frontend Check (SvelteKit)${NC}"
echo -e "${CYAN}========================================${NC}\n"

pushd docker-compose-manager-front > /dev/null
npm run check || frontend_result=$?
popd > /dev/null

echo -e "\n${CYAN}========================================${NC}"
echo -e "${CYAN}  Results${NC}"
echo -e "${CYAN}========================================${NC}\n"

if [ $backend_result -eq 0 ]; then
    echo -e "  Backend:  ${GREEN}OK${NC}"
else
    echo -e "  Backend:  ${RED}FAILED${NC}"
fi

if [ $frontend_result -eq 0 ]; then
    echo -e "  Frontend: ${GREEN}OK${NC}"
else
    echo -e "  Frontend: ${RED}FAILED${NC}"
fi

echo ""

if [ $backend_result -eq 0 ] && [ $frontend_result -eq 0 ]; then
    echo -e "${GREEN}All checks passed!${NC}"
    exit 0
else
    echo -e "${RED}Some checks failed.${NC}"
    exit 1
fi
