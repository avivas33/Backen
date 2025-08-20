# Guía de Docker para Api_Celero

## Requisitos Previos

- Docker Desktop instalado (Windows/Mac) o Docker Engine (Linux)
- Docker Compose
- Git

## Configuración Rápida

### 1. Clonar el repositorio (si aún no lo has hecho)
```bash
git clone https://alexisvivas:Infraestru$net2025@github.com/alexisvivas/Api_Celero.git 
cd Api_Celero
```


### 2. Configurar variables de entorno
```bash
# Copiar archivo de ejemplo
cp .env.example .env

# Editar .env con tus valores
nano .env  # o usa tu editor preferido
```

### 3. Construir y ejecutar con Docker

#### Opción A: Usando los scripts (Linux/Mac/WSL)
```bash
# Dar permisos de ejecución
chmod +x docker-*.sh

# Construir imagen
./docker-build.sh

# Ejecutar contenedores
./docker-run.sh

# Detener contenedores
./docker-stop.sh
```

#### Opción B: Comandos manuales
```bash
# Ir al directorio del proyecto
cd Api_Celero

# Construir imagen
docker build -t api-celero:latest .

# Ejecutar con docker-compose
docker-compose up -d

# Ver logs
docker-compose logs -f api-celero

# Detener
docker-compose down
```

## Estructura de Archivos Docker

```
Api_Celero/
├── Dockerfile              # Definición de la imagen
├── docker-compose.yml      # Orquestación de servicios
├── .dockerignore          # Archivos excluidos del contexto
├── .env.example           # Variables de entorno de ejemplo
├── data/                  # Volumen para SQLite
└── logs/                  # Volumen para logs
```

## Variables de Entorno Importantes

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `RESEND_API_KEY` | API Key de Resend para emails | `re_xxx` |
| `HANSA_BASE_URL` | URL base de la API Hansa | `https://api.hansa.com` |
| `COBALT_CLIENT_ID` | Client ID para pagos Cobalt | `xxx` |
| `PAYPAL_CLIENT_ID` | Client ID de PayPal | `xxx` |
| `ALLOWED_ORIGINS` | Orígenes CORS permitidos | `https://tuapp.com` |

## Comandos Útiles

### Ver estado de contenedores
```bash
docker-compose ps
```

### Ver logs en tiempo real
```bash
docker-compose logs -f api-celero
```

### Ejecutar comandos dentro del contenedor
```bash
docker-compose exec api-celero /bin/sh
```

### Reiniciar servicio
```bash
docker-compose restart api-celero
```

### Ver uso de recursos
```bash
docker stats api-celero
```

## Producción

### Build para producción
```bash
# Construir imagen optimizada
docker build -t api-celero:prod --build-arg ASPNETCORE_ENVIRONMENT=Production .

# Tag para registro
docker tag api-celero:prod turegistro.com/api-celero:latest

# Push a registro
docker push turegistro.com/api-celero:latest
```

### Docker Compose para producción
```yaml
# docker-compose.prod.yml
version: '3.8'

services:
  api-celero:
    image: turegistro.com/api-celero:latest
    restart: always
    # ... resto de configuración
```

## Solución de Problemas

### Error: Puerto 7262 en uso
```bash
# Ver qué está usando el puerto
netstat -tulpn | grep 7262

# Cambiar puerto en docker-compose.yml
ports:
  - "8080:7262"
```

### Error: Permisos en SQLite
```bash
# Dar permisos al directorio data
sudo chmod -R 777 ./data
```

### Error: No encuentra archivo .env
```bash
# Asegurarse de estar en el directorio correcto
cd /ruta/a/Api_Celero
cp .env.example .env
```

## Monitoreo

### Healthcheck
El contenedor incluye un healthcheck que verifica:
- Que la aplicación responda en el puerto 7262
- Que los endpoints críticos estén disponibles

### Logs
Los logs se guardan en:
- Contenedor: `/app/logs`
- Host: `./logs`

## Seguridad

1. **No commits de .env**: El archivo `.env` está en `.gitignore`
2. **Usuario no-root**: La aplicación corre con usuario `dotnet` (UID 1000)
3. **Secretos**: Usa Docker Secrets en producción para información sensible
4. **HTTPS**: Configura un proxy reverso (Nginx) con SSL para producción

## Integración con CI/CD

### GitHub Actions
```yaml
- name: Build and push Docker image
  run: |
    docker build -t api-celero:${{ github.sha }} .
    docker push turegistro.com/api-celero:${{ github.sha }}
```

### GitLab CI
```yaml
build:
  stage: build
  script:
    - docker build -t $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA .
    - docker push $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA
```