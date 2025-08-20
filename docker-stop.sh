#!/bin/bash

# Script para detener Api_Celero

echo "🛑 Deteniendo Api_Celero..."

cd Api_Celero

# Detener y remover contenedores
docker-compose down

# Opcional: Remover volúmenes
# docker-compose down -v

echo "✅ Contenedores detenidos"