#!/bin/bash
# Detiene los servicios del e-commerce arrancados por start.sh.

cd "$(dirname "$0")"

for service in customer-api order-api front-web; do
    pidfile=".logs/${service}.pid"
    if [ -f "$pidfile" ]; then
        pid=$(cat "$pidfile")
        if kill -0 "$pid" 2>/dev/null; then
            echo "Deteniendo $service (pid $pid)..."
            kill "$pid"
            rm "$pidfile"
        else
            echo "$service ya no está corriendo."
            rm "$pidfile"
        fi
    else
        echo "No se encontró $service."
    fi
done
