# Script para liberar el archivo SQLite de procesos .NET
# Uso: Ejecuta este script en PowerShell antes de correr migraciones o iniciar la API

# Cambia el nombre del archivo si tu base de datos tiene otro nombre
$databaseFile = "recibos_offline.db"

# Busca procesos que tengan abierto el archivo (requiere handle.exe de Sysinternals)
$handlePath = "$PSScriptRoot\handle.exe"
if (-Not (Test-Path $handlePath)) {
    Write-Host "Descarga handle.exe de https://docs.microsoft.com/en-us/sysinternals/downloads/handle y colócalo en la misma carpeta que este script."
    exit 1
}

$handles = & $handlePath $databaseFile | Select-String -Pattern 'pid: (\d+)' | ForEach-Object {
    if ($_ -match 'pid: (\d+)') { $matches[1] }
}

if ($handles.Count -eq 0) {
    Write-Host "No se encontraron procesos bloqueando $databaseFile."
    exit 0
}

Write-Host "Procesos que bloquean $databaseFile: $handles"
foreach ($pid in $handles) {
    try {
        Stop-Process -Id $pid -Force -ErrorAction Stop
        Write-Host "Proceso $pid terminado."
    } catch {
        Write-Host "No se pudo terminar el proceso $pid: $_"
    }
}
Write-Host "Listo. Ahora puedes correr tus migraciones o iniciar la API."
