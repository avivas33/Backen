#!/bin/bash

# Script para construir la imagen Docker de Api_Celero

echo "🐳 Construyendo imagen Docker para Api_Celero..."

# Ir al directorio del proyecto
cd Api_Celero

# Construir la imagen
docker build -t api-celero:latest .

if [ $? -eq 0 ]; then
    echo "✅ Imagen construida exitosamente: api-celero:latest"
else
    echo "❌ Error al construir la imagen"
    exit 1
fi