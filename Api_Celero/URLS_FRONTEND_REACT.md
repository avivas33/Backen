# 🌐 URLs DE LA API PARA EL FRONTEND REACT

## 📍 **CONFIGURACIÓN ACTUAL DE LA API**

Tu API Celero está desplegada y configurada con:
- **Dominio**: `api.celero.network`
- **Puerto interno**: `5001`
- **SSL**: Habilitado con certificados existentes
- **CORS**: Configurado para permitir tu frontend

## 🎯 **URLs PARA EL FRONTEND REACT**

### **URL BASE DE PRODUCCIÓN:**
```javascript
const API_BASE_URL = "https://api.celero.network";
```

### **URLs COMPLETAS POR ENDPOINT:**

#### **📋 GESTIÓN DE CLIENTES**
```javascript
// Obtener información de cliente
GET https://api.celero.network/api/clientes?range=CU-20037

// Obtener facturas de cliente
GET https://api.celero.network/api/clientes/facturas?range=CU-21541

// Obtener facturas abiertas
GET https://api.celero.network/api/clientes/facturas-abiertas?range=CU-21541

// Obtener facturas filtradas
GET https://api.celero.network/api/clientes/facturas-filtradas?range=CU-21541
```

#### **💳 PROCESAMIENTO DE PAGOS**
```javascript
// Crear recibo de pago
POST https://api.celero.network/api/clientes/recibos

// Procesar venta con tarjeta (Cobalt)
POST https://api.celero.network/api/clientes/venta-tarjeta

// Crear orden de pago Yappy
POST https://api.celero.network/api/clientes/yappy/crear-orden

// Crear orden Yappy para frontend
POST https://api.celero.network/api/clientes/yappy/crear-orden-frontend

// Webhook IPN de Yappy
GET https://api.celero.network/api/clientes/yappy/ipn
```

#### **📧 SERVICIOS DE EMAIL**
```javascript
// Enviar email genérico
POST https://api.celero.network/api/clientes/email/send

// Enviar confirmación de pago
POST https://api.celero.network/api/clientes/email/payment-confirmation

// Enviar recordatorio de factura
POST https://api.celero.network/api/clientes/email/invoice-reminder

// Enviar email de bienvenida
POST https://api.celero.network/api/clientes/email/welcome
```

#### **🔒 SEGURIDAD**
```javascript
// Verificar Google reCAPTCHA
POST https://api.celero.network/api/clientes/verificar-recaptcha
```

## 🛠️ **CONFIGURACIÓN EN REACT**

### **1. Archivo de configuración de API (api.js):**
```javascript
// src/config/api.js
const API_CONFIG = {
  baseURL: 'https://api.celero.network',
  timeout: 30000, // 30 segundos
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  }
};

export default API_CONFIG;
```

### **2. Cliente HTTP con Axios:**
```javascript
// src/services/apiClient.js
import axios from 'axios';
import API_CONFIG from '../config/api';

const apiClient = axios.create({
  baseURL: API_CONFIG.baseURL,
  timeout: API_CONFIG.timeout,
  headers: API_CONFIG.headers
});

// Interceptor para manejo de errores
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

export default apiClient;
```

### **3. Servicios específicos:**
```javascript
// src/services/clienteService.js
import apiClient from './apiClient';

export const clienteService = {
  // Obtener cliente
  getCliente: (range = 'CU-20037') => 
    apiClient.get(`/api/clientes?range=${range}`),
  
  // Obtener facturas
  getFacturas: (range = 'CU-21541') => 
    apiClient.get(`/api/clientes/facturas?range=${range}`),
  
  // Obtener facturas abiertas
  getFacturasAbiertas: (range = 'CU-21541') => 
    apiClient.get(`/api/clientes/facturas-abiertas?range=${range}`),
  
  // Crear recibo
  crearRecibo: (data) => 
    apiClient.post('/api/clientes/recibos', data),
  
  // Verificar reCAPTCHA
  verificarRecaptcha: (token) => 
    apiClient.post('/api/clientes/verificar-recaptcha', { token })
};
```

### **4. Servicio de pagos:**
```javascript
// src/services/pagoService.js
import apiClient from './apiClient';

export const pagoService = {
  // Venta con tarjeta
  ventaTarjeta: (data) => 
    apiClient.post('/api/clientes/venta-tarjeta', data),
  
  // Crear orden Yappy
  crearOrdenYappy: (data) => 
    apiClient.post('/api/clientes/yappy/crear-orden', data),
  
  // Crear orden Yappy frontend
  crearOrdenYappyFrontend: (data) => 
    apiClient.post('/api/clientes/yappy/crear-orden-frontend', data)
};
```

### **5. Servicio de emails:**
```javascript
// src/services/emailService.js
import apiClient from './apiClient';

export const emailService = {
  // Enviar email genérico
  enviarEmail: (data) => 
    apiClient.post('/api/clientes/email/send', data),
  
  // Confirmación de pago
  enviarConfirmacionPago: (data) => 
    apiClient.post('/api/clientes/email/payment-confirmation', data),
  
  // Recordatorio de factura
  enviarRecordatorioFactura: (data) => 
    apiClient.post('/api/clientes/email/invoice-reminder', data),
  
  // Email de bienvenida
  enviarEmailBienvenida: (data) => 
    apiClient.post('/api/clientes/email/welcome', data)
};
```

## 🔧 **EJEMPLO DE USO EN COMPONENTE REACT:**

```javascript
// src/components/ClienteInfo.jsx
import React, { useState, useEffect } from 'react';
import { clienteService } from '../services/clienteService';

const ClienteInfo = ({ codigoCliente }) => {
  const [cliente, setCliente] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchCliente = async () => {
      try {
        setLoading(true);
        const response = await clienteService.getCliente(codigoCliente);
        setCliente(response.data.data.CUVc[0]);
      } catch (err) {
        setError('Error al cargar información del cliente');
        console.error(err);
      } finally {
        setLoading(false);
      }
    };

    if (codigoCliente) {
      fetchCliente();
    }
  }, [codigoCliente]);

  if (loading) return <div>Cargando...</div>;
  if (error) return <div>Error: {error}</div>;
  if (!cliente) return <div>Cliente no encontrado</div>;

  return (
    <div>
      <h2>{cliente.Name}</h2>
      <p>Código: {cliente.Code}</p>
      <p>Email: {cliente.eMail}</p>
      <p>Móvil: {cliente.Mobile}</p>
    </div>
  );
};

export default ClienteInfo;
```

## 🌐 **VARIABLES DE ENTORNO PARA REACT:**

```bash
# .env.production
REACT_APP_API_BASE_URL=https://api.celero.network
REACT_APP_API_TIMEOUT=30000

# .env.development (si tienes desarrollo local)
REACT_APP_API_BASE_URL=http://localhost:5001
REACT_APP_API_TIMEOUT=30000
```

## ⚠️ **IMPORTANTE - DNS:**

**Mientras configuras el DNS para `api.celero.network`**, puedes usar temporalmente:

```javascript
// Configuración temporal (hasta que DNS esté listo)
const API_CONFIG = {
  baseURL: 'https://tu-ip-del-servidor', // Usar IP del servidor
  // O usar un subdominio existente temporalmente
  baseURL: 'https://celero.network/api',
  timeout: 30000
};
```

## 🧪 **PRUEBAS DESDE EL NAVEGADOR:**

Puedes probar directamente desde el navegador:
```
https://api.celero.network/api/clientes
https://api.celero.network/api/clientes/facturas
```

## ✅ **CORS CONFIGURADO:**

Tu API ya está configurada para permitir requests desde:
- `https://selfservice.celero.network`
- `https://selfservice-dev.celero.network`
- `https://celero.network`
- `https://api.celero.network`

¡Tu frontend React ya puede conectarse perfectamente a la API! 🚀
