@echo off
REM Script de publicación para Windows
REM Genera los archivos para desplegar en Ubuntu

echo 🔨 Compilando y publicando para Linux...

REM Limpiar publicación anterior
if exist "publish" rmdir /s /q publish

REM Publicar para Linux
dotnet publish -c Release -r linux-x64 --self-contained false -o publish

if %ERRORLEVEL% EQU 0 (
    echo ✅ Publicación completada exitosamente
    echo 📁 Archivos generados en: publish\
    echo.
    echo 📋 Próximos pasos:
    echo 1. Copiar la carpeta 'publish' al servidor Ubuntu
    echo 2. Copiar el archivo 'deploy.sh' al servidor Ubuntu
    echo 3. En Ubuntu, ejecutar: sudo bash deploy.sh
    echo.
    echo 🔐 No olvides configurar las variables de entorno en el servidor
) else (
    echo ❌ Error en la publicación
    pause
)
