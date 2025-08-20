#!/bin/bash

# Script para ejecutar Api_Celero con Docker Compose

echo "🚀 Iniciando Api_Celero con Docker Compose..."

# Verificar si existe el archivo .env
if [ ! -f ".env" ]; then
    echo "⚠️  No se encontró archivo .env"
    echo "📝 Copiando .env.example a .env..."
    cp .env.example .env
    echo "⚠️  Por favor configura las variables en el archivo .env antes de continuar"
    exit 1
fi

# Crear directorios necesarios
mkdir -p data logs

# Ir al directorio del proyecto
cd Api_Celero

# Detener contenedores existentes
echo "🛑 Deteniendo contenedores existentes..."
docker-compose down

# Construir y ejecutar
echo "🐳 Construyendo e iniciando contenedores..."
docker-compose up --build -d

# Verificar estado
if [ $? -eq 0 ]; then
    echo "✅ Contenedores iniciados exitosamente"
    echo ""
    echo "📊 Estado de los contenedores:"
    docker-compose ps
    echo ""
    echo "📝 Para ver los logs:"
    echo "   docker-compose logs -f api-celero"
    echo ""
    echo "🌐 La API está disponible en:"
    echo "   http://localhost:7262"
else
    echo "❌ Error al iniciar los contenedores"
    exit 1
fi