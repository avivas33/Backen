# 🎯 Configuración Completa para Frontend React - Api_Celero

## 📊 Estado Actual del Despliegue

✅ **API Celero Status**: ACTIVA en producción  
✅ **Dominio**: api.celero.network  
✅ **SSL/HTTPS**: Configurado y funcionando  
✅ **CORS**: Habilitado para frontends autorizados  
✅ **Servicios**: api-celero (activo), nginx (activo)  

## 🌐 URLs Base para React

### Producción
```javascript
const API_BASE_URL = "https://api.celero.network";
```

### Desarrollo Local (si necesitas)
```javascript
const API_BASE_URL_DEV = "http://localhost:5001";
```

## 🔗 Endpoints Disponibles

### 1. 👥 Gestión de Clientes
```javascript
// Obtener todos los clientes
GET https://api.celero.network/api/clientes

// Buscar cliente por cédula
GET https://api.celero.network/api/clientes/{cedula}
// Ejemplo: GET https://api.celero.network/api/clientes/8-123-4567

// Obtener facturas abiertas de un cliente
GET https://api.celero.network/api/clientes/{cedula}/facturas-abiertas
// Ejemplo: GET https://api.celero.network/api/clientes/8-123-4567/facturas-abiertas

// Obtener cuotas de un cliente
GET https://api.celero.network/api/clientes/{cedula}/cuotas
// Ejemplo: GET https://api.celero.network/api/clientes/8-123-4567/cuotas
```

### 2. 📄 Gestión de Facturas
```javascript
// Obtener detalles de una factura específica
GET https://api.celero.network/api/clientes/{cedula}/facturas/{numeroFactura}
// Ejemplo: GET https://api.celero.network/api/clientes/8-123-4567/facturas/FAC-2024-001
```

### 3. 💳 Formas de Pago
```javascript
// Obtener todas las formas de pago disponibles
GET https://api.celero.network/api/formas-pago

// Obtener forma de pago específica
GET https://api.celero.network/api/formas-pago/{id}
// Ejemplo: GET https://api.celero.network/api/formas-pago/1
```

### 4. 🧾 Recibos
```javascript
// Crear un nuevo recibo
POST https://api.celero.network/api/recibos

// Estructura del body:
{
  "cedula": "8-123-4567",
  "facturas": [
    {
      "numero": "FAC-2024-001",
      "monto": 100.50
    }
  ],
  "formaPago": 1,
  "observaciones": "Pago completo",
  "email": "cliente@ejemplo.com"
}
```

### 5. 💰 Pagos Cobalt (Tarjetas)
```javascript
// Crear venta con Cobalt
POST https://api.celero.network/api/cobalt/crear-venta

// Estructura del body:
{
  "cedula": "8-123-4567",
  "facturas": [
    {
      "numero": "FAC-2024-001",
      "monto": 100.50
    }
  ],
  "descripcion": "Pago de factura",
  "email": "cliente@ejemplo.com"
}
```

### 6. 📱 Pagos Yappy
```javascript
// Crear orden de pago Yappy
POST https://api.celero.network/api/yappy/crear-orden

// Estructura del body:
{
  "cedula": "8-123-4567",
  "facturas": [
    {
      "numero": "FAC-2024-001",
      "monto": 100.50
    }
  ],
  "descripcion": "Pago de factura",
  "telefono": "6123-4567"
}
```

### 7. 📧 Servicios de Email
```javascript
// Enviar email
POST https://api.celero.network/api/email/enviar

// Estructura del body:
{
  "destinatario": "cliente@ejemplo.com",
  "asunto": "Recibo de Pago",
  "mensaje": "Adjunto encontrará su recibo de pago.",
  "archivoAdjunto": "base64_del_archivo"
}
```

### 8. 🔒 Verificación reCAPTCHA
```javascript
// Verificar reCAPTCHA
POST https://api.celero.network/api/recaptcha/verificar

// Estructura del body:
{
  "token": "03AIIukzh7CAG4..."
}
```

### 9. 🏥 Health Check y Documentación
```javascript
// Verificar estado de la API
GET https://api.celero.network/health

// Documentación Swagger
GET https://api.celero.network/swagger

// Esquema OpenAPI
GET https://api.celero.network/openapi/v1.json
```

## ⚙️ Configuración en React

### 1. Variables de Entorno (.env)
```bash
# .env.production
REACT_APP_API_BASE_URL=https://api.celero.network
REACT_APP_API_TIMEOUT=30000

# .env.development
REACT_APP_API_BASE_URL=http://localhost:5001
REACT_APP_API_TIMEOUT=30000
```

### 2. Configuración de API (src/config/api.js)
```javascript
const API_CONFIG = {
  baseURL: process.env.REACT_APP_API_BASE_URL || 'https://api.celero.network',
  timeout: parseInt(process.env.REACT_APP_API_TIMEOUT) || 30000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  }
};

export default API_CONFIG;
```

### 3. Cliente HTTP con Axios (src/services/apiClient.js)
```javascript
import axios from 'axios';
import API_CONFIG from '../config/api';

const apiClient = axios.create({
  baseURL: API_CONFIG.baseURL,
  timeout: API_CONFIG.timeout,
  headers: API_CONFIG.headers
});

// Interceptor para logging de requests
apiClient.interceptors.request.use(
  (config) => {
    console.log(`🚀 API Request: ${config.method?.toUpperCase()} ${config.url}`);
    return config;
  },
  (error) => {
    console.error('❌ Request Error:', error);
    return Promise.reject(error);
  }
);

// Interceptor para manejo de responses
apiClient.interceptors.response.use(
  (response) => {
    console.log(`✅ API Response: ${response.status} ${response.config.url}`);
    return response;
  },
  (error) => {
    console.error('❌ API Error:', error);
    
    if (error.response) {
      // Error con respuesta del servidor
      const { status, data } = error.response;
      console.error(`Server Error ${status}:`, data);
    } else if (error.request) {
      // Error de red
      console.error('Network Error:', error.request);
    } else {
      // Error de configuración
      console.error('Config Error:', error.message);
    }
    
    return Promise.reject(error);
  }
);

export default apiClient;
```

### 4. Servicios Específicos

#### Servicio de Clientes (src/services/clienteService.js)
```javascript
import apiClient from './apiClient';

export const clienteService = {
  // Obtener todos los clientes
  obtenerTodos: () => 
    apiClient.get('/api/clientes'),

  // Obtener cliente por cédula
  obtenerPorCedula: (cedula) => 
    apiClient.get(`/api/clientes/${cedula}`),

  // Obtener facturas abiertas
  obtenerFacturasAbiertas: (cedula) => 
    apiClient.get(`/api/clientes/${cedula}/facturas-abiertas`),

  // Obtener cuotas
  obtenerCuotas: (cedula) => 
    apiClient.get(`/api/clientes/${cedula}/cuotas`),

  // Obtener factura específica
  obtenerFactura: (cedula, numeroFactura) => 
    apiClient.get(`/api/clientes/${cedula}/facturas/${numeroFactura}`)
};
```

#### Servicio de Pagos (src/services/pagoService.js)
```javascript
import apiClient from './apiClient';

export const pagoService = {
  // Obtener formas de pago
  obtenerFormasPago: () => 
    apiClient.get('/api/formas-pago'),

  // Crear recibo
  crearRecibo: (reciboData) => 
    apiClient.post('/api/recibos', reciboData),

  // Venta con tarjeta (Cobalt)
  crearVentaCobalt: (ventaData) => 
    apiClient.post('/api/cobalt/crear-venta', ventaData),

  // Crear orden Yappy
  crearOrdenYappy: (ordenData) => 
    apiClient.post('/api/yappy/crear-orden', ordenData)
};
```

#### Servicio de Email (src/services/emailService.js)
```javascript
import apiClient from './apiClient';

export const emailService = {
  // Enviar email
  enviarEmail: (emailData) => 
    apiClient.post('/api/email/enviar', emailData)
};
```

#### Servicio de Seguridad (src/services/securityService.js)
```javascript
import apiClient from './apiClient';

export const securityService = {
  // Verificar reCAPTCHA
  verificarRecaptcha: (token) => 
    apiClient.post('/api/recaptcha/verificar', { token })
};
```

### 5. Hook Personalizado para API
```javascript
// src/hooks/useApi.js
import { useState, useEffect } from 'react';

export const useApi = (apiCall, dependencies = []) => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        setError(null);
        const response = await apiCall();
        setData(response.data);
      } catch (err) {
        setError(err);
        console.error('API Hook Error:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
  }, dependencies);

  const refetch = () => {
    fetchData();
  };

  return { data, loading, error, refetch };
};
```

### 6. Ejemplo de Componente React
```javascript
// src/components/ClienteProfile.jsx
import React, { useState } from 'react';
import { useApi } from '../hooks/useApi';
import { clienteService } from '../services/clienteService';

const ClienteProfile = ({ cedula }) => {
  const { 
    data: cliente, 
    loading, 
    error, 
    refetch 
  } = useApi(() => clienteService.obtenerPorCedula(cedula), [cedula]);

  const { 
    data: facturas, 
    loading: loadingFacturas 
  } = useApi(() => clienteService.obtenerFacturasAbiertas(cedula), [cedula]);

  if (loading) return <div className="loading">Cargando cliente...</div>;
  if (error) return <div className="error">Error: {error.message}</div>;
  if (!cliente) return <div className="no-data">Cliente no encontrado</div>;

  return (
    <div className="cliente-profile">
      <h2>{cliente.nombre}</h2>
      <p><strong>Cédula:</strong> {cliente.cedula}</p>
      <p><strong>Email:</strong> {cliente.email}</p>
      <p><strong>Teléfono:</strong> {cliente.telefono}</p>
      
      <h3>Facturas Abiertas</h3>
      {loadingFacturas ? (
        <div>Cargando facturas...</div>
      ) : (
        <div className="facturas-list">
          {facturas?.map(factura => (
            <div key={factura.numero} className="factura-item">
              <span>{factura.numero}</span>
              <span>${factura.monto}</span>
            </div>
          ))}
        </div>
      )}
      
      <button onClick={refetch}>Actualizar</button>
    </div>
  );
};

export default ClienteProfile;
```

## 🔒 Configuración CORS

Tu API está configurada para aceptar requests desde:
- `https://selfservice.celero.network`
- `https://selfservice-dev.celero.network`  
- `https://celero.network`

**⚠️ Importante**: Asegúrate de que tu frontend React esté desplegado en uno de estos dominios.

## 🧪 Pruebas Rápidas

### Desde el navegador:
```
https://api.celero.network/health
https://api.celero.network/api/formas-pago
https://api.celero.network/swagger
```

### Con curl:
```bash
# Health check
curl https://api.celero.network/health

# Obtener formas de pago
curl https://api.celero.network/api/formas-pago

# Ver documentación OpenAPI
curl https://api.celero.network/openapi/v1.json
```

## 📊 Códigos de Estado HTTP

- **200** - Éxito
- **400** - Error en la petición (datos inválidos)
- **401** - No autorizado
- **404** - Recurso no encontrado
- **429** - Demasiadas peticiones
- **500** - Error interno del servidor

## 🔧 Troubleshooting

### Si hay errores de conexión:
```bash
# Verificar estado del servicio
sudo systemctl status api-celero

# Ver logs en tiempo real
sudo journalctl -u api-celero -f

# Verificar nginx
sudo systemctl status nginx
```

### Si hay errores de CORS:
1. Verifica que tu frontend esté en un dominio autorizado
2. Revisa la configuración en `appsettings.Production.json`
3. Reinicia el servicio si es necesario

## 📞 Soporte

Para resolver problemas:
1. Revisa los logs: `sudo journalctl -u api-celero -f`
2. Verifica conectividad: `curl https://api.celero.network/health`
3. Comprueba certificados SSL
4. Valida configuración CORS

## 🚀 ¡Tu API está lista para producción!

La API Celero está completamente configurada y lista para ser utilizada por tu frontend React. Todos los servicios están activos y funcionando correctamente.
