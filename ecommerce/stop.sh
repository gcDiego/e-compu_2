#!/bin/bash
# Detiene los servicios del e-commerce arrancados por start.sh.

cd "$(dirname "$0")"

services=(customer-api order-api admin-web administration-api catalog-api front-web)

for service in "${services[@]}"; do
    pidfile=".logs/${service}.pid"
    if [ ! -f "$pidfile" ]; then
        echo "No se encontró $service (no fue iniciado por start.sh o su PID no está registrado)."
        continue
    fi

    pid=$(cat "$pidfile")
    if kill -0 "$pid" 2>/dev/null; then
        echo "Deteniendo $service (PID $pid)..."
        kill "$pid"
        for _ in {1..20}; do
            kill -0 "$pid" 2>/dev/null || break
            sleep 0.25
        done
    else
        echo "$service ya no está corriendo."
    fi
    rm -f "$pidfile"
done
