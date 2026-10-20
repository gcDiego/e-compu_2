#!/bin/bash
# Levanta los servicios del e-commerce.
# MongoDB debe estar accesible previamente.

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

services=(customer-api order-api admin-web administration-api catalog-api cart-api location-api front-web)
ports=(5161 5162 5163 5164 5165 5166 5167 5160)
projects=(
    src/Services/Customer/Customer.Api/Customer.Api.csproj
    src/Services/Order/Order.Api/Order.Api.csproj
    src/Services/Front/Admin.Web/Admin.Web.csproj
    src/Services/Administration/Administration.Api/Administration.Api.csproj
    src/Services/Catalog/Catalog.Api/Catalog.Api.csproj
    src/Services/Cart/Cart.Api/Cart.Api.csproj
    src/Services/Location/Location.Api/Location.Api.csproj
    src/Services/Front/Front.Web/Front.Web.csproj
)
names=(Customer.Api Order.Api Admin.Web Administration.Api Catalog.Api Cart.Api Location.Api Front.Web)
health_paths=(health health Account/Login health health health health Store)

for i in "${!services[@]}"; do
    port="${ports[$i]}"
    if pid=$(lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null); then
        echo "No se puede iniciar ${names[$i]}: el puerto $port ya está ocupado por PID $pid."
        echo "Detén el proceso existente y vuelve a ejecutar ./start.sh."
        exit 1
    fi

    pidfile=".logs/${services[$i]}.pid"
    if [ -f "$pidfile" ] && ! kill -0 "$(cat "$pidfile")" 2>/dev/null; then
        rm "$pidfile"
    fi
done

for i in "${!services[@]}"; do
    service="${services[$i]}"
    port="${ports[$i]}"
    log "Iniciando ${names[$i]} en http://localhost:$port..."
    dotnet run --project "${projects[$i]}" --urls "http://localhost:$port" > ".logs/$service.log" 2>&1 &
    echo $! > ".logs/$service.pid"
done

failed=0
for i in "${!services[@]}"; do
    service="${services[$i]}"
    port="${ports[$i]}"
    pid=$(cat ".logs/$service.pid")
    url="http://localhost:$port/${health_paths[$i]}"
    ready=0

    for _ in {1..30}; do
        if ! kill -0 "$pid" 2>/dev/null; then
            break
        fi
        if curl -s -o /dev/null --max-time 2 "$url"; then
            ready=1
            break
        fi
        sleep 1
    done

    if [ "$ready" -eq 1 ]; then
        log "${names[$i]} listo."
    else
        log "ERROR: ${names[$i]} no inició. Revisa .logs/$service.log"
        failed=1
    fi
done

if [ "$failed" -ne 0 ]; then
    echo "Uno o más servicios fallaron. Ejecuta ./stop.sh después de revisar los logs."
    exit 1
fi

log "Todos los servicios están listos."
echo "  Front.Web          -> http://localhost:5160"
echo "  Customer.Api       -> http://localhost:5161"
echo "  Order.Api          -> http://localhost:5162"
echo "  Admin.Web          -> http://localhost:5163"
echo "  Administration.Api -> http://localhost:5164"
echo "  Catalog.Api        -> http://localhost:5165"
echo "  Cart.Api           -> http://localhost:5166"
echo "  Location.Api       -> http://localhost:5167"
echo ""
echo "Logs en .logs/ (por ejemplo: tail -f .logs/administration-api.log)"
echo "Usa ./stop.sh para detenerlos."
