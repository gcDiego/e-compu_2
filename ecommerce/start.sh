#!/bin/bash
# Levanta los servicios del e-commerce.
# MongoDB debe estar corriendo previamente.

set -e

cd "$(dirname "$0")"

if [ -f .env ]; then
    echo "Cargando variables de .env..."
    set -a
    source .env
    set +a
fi

mkdir -p .logs

log() { echo "[$(date +%H:%M:%S)] $1"; }

log "Iniciando Customer.Api..."
dotnet run --project src/Services/Customer/Customer.Api/Customer.Api.csproj --urls "http://localhost:5161" > .logs/customer-api.log 2>&1 &
echo $! > .logs/customer-api.pid

log "Iniciando Order.Api..."
dotnet run --project src/Services/Order/Order.Api/Order.Api.csproj --urls "http://localhost:5162" > .logs/order-api.log 2>&1 &
echo $! > .logs/order-api.pid

log "Iniciando Front.Web..."
dotnet run --project src/Services/Front/Front.Web/Front.Web.csproj > .logs/front-web.log 2>&1 &
echo $! > .logs/front-web.pid

log "Servicios arrancando en segundo plano."
echo "  Customer.Api -> http://localhost:5161"
echo "  Order.Api    -> http://localhost:5162"
echo "  Front.Web    -> ver .logs/front-web.log para el puerto"
echo ""
echo "Logs en .logs/"
echo "Usa ./stop.sh para detenerlos."
