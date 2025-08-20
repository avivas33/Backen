# Guía de Configuración Segura de Pagos

## 🔐 Configuración de Variables de Entorno

### Para Desarrollo Local
1. Copia `env.example` a `.env` en la raíz del proyecto
2. Configura los valores reales en `.env`
3. **NUNCA** commitees el archivo `.env` al repositorio

### Para Producción (Ubuntu/Docker)
Configura las siguientes variables de entorno en el sistema:

```bash
# Cobalt (Pagos con tarjeta)
export COBALT_BASE_URL="https://metrobank.cobalt.tech"
export COBALT_CLIENT_ID="tu_client_id_real"
export COBALT_CLIENT_SECRET="tu_client_secret_real"

# Yappy (Pagos móviles)
export YAPPY_BASE_URL="https://apipagosbg.bgeneral.cloud"
export YAPPY_MERCHANT_ID="tu_merchant_id_real"

# Google reCAPTCHA
export GOOGLE_RECAPTCHA_PROJECT_ID="tu_project_id"
export GOOGLE_RECAPTCHA_API_KEY="tu_api_key"
export GOOGLE_RECAPTCHA_SITE_KEY="tu_site_key"

# Resend (Email)
export RESEND_API_KEY="tu_resend_api_key"
export RESEND_FROM_EMAIL="tu_email@dominio.com"
export RESEND_FROM_NAME="Tu Nombre"
```

## 🛡️ Mejoras de Seguridad Implementadas

### 1. **Configuración Externalizada**
- ✅ Hosts y URLs configurables
- ✅ Credenciales en variables de entorno
- ✅ Configuración por ambiente (Development/Production)

### 2. **Logging Mejorado**
- ✅ Logs de errores detallados
- ✅ Logs de transacciones exitosas
- ✅ No se loguean credenciales sensibles

### 3. **Manejo de Errores**
- ✅ Try-catch en todos los endpoints de pago
- ✅ Mensajes de error informativos sin exponer detalles internos
- ✅ Códigos de estado HTTP apropiados

### 4. **Validación de Configuración**
- ✅ Verificación de credenciales al inicio
- ✅ Validación de URLs base
- ✅ Manejo de configuración faltante

## 📁 Estructura de Archivos de Configuración

```
Api_Celero/
├── appsettings.json                 # Desarrollo
├── appsettings.Production.json      # Producción
├── env.example                      # Plantilla de variables
├── Models/
│   └── PaymentSettings.cs          # Modelos de configuración
└── Controllers/
    └── ClientesController.cs        # Controlador actualizado
```

## 🔧 Uso en el Código

Los endpoints ahora usan configuración inyectada:

```csharp
// Antes (inseguro)
var tokenResp = await http.PostAsync(
    "https://metrobank.cobalt.tech/oauth/token", // URL hardcodeada
    ...
);

// Después (seguro)
var tokenResp = await http.PostAsync(
    _cobaltSettings.TokenUrl, // URL desde configuración
    ...
);
```

## ⚠️ Notas Importantes

1. **Nunca commits credenciales**: Usa `.gitignore` para excluir archivos de configuración sensibles
2. **Rota credenciales regularmente**: Cambia las llaves de API periódicamente
3. **Usa HTTPS en producción**: Asegúrate de que todas las comunicaciones sean cifradas
4. **Monitorea transacciones**: Revisa logs regularmente para detectar actividad sospechosa
5. **Backups seguros**: Si haces backups de configuración, cífralos

## 🚀 Despliegue

Para desplegar con las nuevas configuraciones:

1. Configura las variables de entorno en el servidor
2. Asegúrate de que `appsettings.Production.json` use variables de entorno
3. Reinicia la aplicación
4. Verifica que las configuraciones se cargan correctamente en los logs
