// 🌐 CONFIGURACIÓN DE API PARA REACT + TYPESCRIPT
// src/types/api.types.ts

export interface Cliente {
  Code: string;
  Name: string;
  eMail: string;
  Mobile: string;
}

export interface Factura {
  SerNr: string;
  InvDate: string;
  PayDate: string;
  PayDeal: string;
  Sum4: string;
  OfficialSerNr: string;
}

export interface FacturaAbierta {
  InvoiceNr: string;
  BookRVal: string;
}

export interface ApiResponse<T> {
  data: T;
}

export interface ClienteResponse {
  "@register": string;
  "@sequence": string;
  "@systemversion": string;
  "@linkid": string;
  "@sort": string;
  "@key": string;
  "@range": string;
  CUVc: Cliente[];
}

export interface FacturaResponse {
  "@register": string;
  "@sequence": string;
  "@systemversion": string;
  "@linkid": string;
  "@sort": string;
  "@key": string;
  "@range": string;
  IVVc: Factura[];
}

export interface ReciboRequest {
  cliente: string;
  monto: number;
  concepto: string;
  metodoPago: string;
}

export interface EmailRequest {
  to: string;
  subject: string;
  htmlContent: string;
  textContent?: string;
}

export interface RecaptchaRequest {
  token: string;
  action?: string;
}

// 🛠️ CLIENTE API CON TYPESCRIPT
// src/services/apiClient.ts

import axios, { AxiosInstance, AxiosResponse } from 'axios';
import { ApiResponse } from '../types/api.types';

class ApiClient {
  private client: AxiosInstance;
  
  constructor() {
    this.client = axios.create({
      baseURL: process.env.REACT_APP_API_BASE_URL || 'https://api.celero.network',
      timeout: Number(process.env.REACT_APP_API_TIMEOUT) || 30000,
      headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json'
      }
    });

    this.setupInterceptors();
  }

  private setupInterceptors(): void {
    // Request interceptor
    this.client.interceptors.request.use(
      (config) => {
        console.log(`🌐 API Request: ${config.method?.toUpperCase()} ${config.url}`);
        return config;
      },
      (error) => {
        console.error('❌ Request Error:', error);
        return Promise.reject(error);
      }
    );

    // Response interceptor
    this.client.interceptors.response.use(
      (response: AxiosResponse) => {
        console.log(`✅ API Response: ${response.status} ${response.config.url}`);
        return response;
      },
      (error) => {
        console.error('❌ Response Error:', error.response?.status, error.message);
        return Promise.reject(error);
      }
    );
  }

  // Generic GET method
  async get<T>(url: string, params?: any): Promise<ApiResponse<T>> {
    const response = await this.client.get<ApiResponse<T>>(url, { params });
    return response.data;
  }

  // Generic POST method
  async post<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  // Generic PUT method
  async put<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  // Generic DELETE method
  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url);
    return response.data;
  }
}

export const apiClient = new ApiClient();

// 📋 SERVICIOS ESPECÍFICOS CON TYPESCRIPT
// src/services/clienteService.ts

import { apiClient } from './apiClient';
import { 
  ClienteResponse, 
  FacturaResponse, 
  ReciboRequest, 
  RecaptchaRequest 
} from '../types/api.types';

export class ClienteService {
  private basePath = '/api/clientes';

  async getCliente(range: string = 'CU-20037'): Promise<ClienteResponse> {
    return apiClient.get<ClienteResponse>(`${this.basePath}?range=${range}`);
  }

  async getFacturas(range: string = 'CU-21541'): Promise<FacturaResponse> {
    return apiClient.get<FacturaResponse>(`${this.basePath}/facturas?range=${range}`);
  }

  async getFacturasAbiertas(range: string = 'CU-21541'): Promise<any> {
    return apiClient.get(`${this.basePath}/facturas-abiertas?range=${range}`);
  }

  async getFacturasFiltradas(range: string = 'CU-21541'): Promise<any> {
    return apiClient.get(`${this.basePath}/facturas-filtradas?range=${range}`);
  }

  async crearRecibo(data: ReciboRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/recibos`, data);
  }

  async verificarRecaptcha(data: RecaptchaRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/verificar-recaptcha`, data);
  }
}

export const clienteService = new ClienteService();

// 💳 SERVICIO DE PAGOS
// src/services/pagoService.ts

import { apiClient } from './apiClient';

interface CobaltSaleRequest {
  amount: number;
  currency: string;
  description: string;
  // Agregar más campos según sea necesario
}

interface YappyOrdenRequest {
  amount: number;
  description: string;
  domain: string;
  // Agregar más campos según sea necesario
}

export class PagoService {
  private basePath = '/api/clientes';

  async ventaTarjeta(data: CobaltSaleRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/venta-tarjeta`, data);
  }

  async crearOrdenYappy(data: YappyOrdenRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/yappy/crear-orden`, data);
  }

  async crearOrdenYappyFrontend(data: any): Promise<any> {
    return apiClient.post(`${this.basePath}/yappy/crear-orden-frontend`, data);
  }
}

export const pagoService = new PagoService();

// 📧 SERVICIO DE EMAILS
// src/services/emailService.ts

import { apiClient } from './apiClient';
import { EmailRequest } from '../types/api.types';

interface PaymentConfirmationRequest {
  customerEmail: string;
  customerName: string;
  amount: number;
  invoiceNumber: string;
  paymentDate: string;
}

interface InvoiceReminderRequest {
  customerEmail: string;
  customerName: string;
  invoiceNumber: string;
  amount: number;
  dueDate: string;
}

interface WelcomeEmailRequest {
  customerEmail: string;
  customerName: string;
}

export class EmailService {
  private basePath = '/api/clientes/email';

  async enviarEmail(data: EmailRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/send`, data);
  }

  async enviarConfirmacionPago(data: PaymentConfirmationRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/payment-confirmation`, data);
  }

  async enviarRecordatorioFactura(data: InvoiceReminderRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/invoice-reminder`, data);
  }

  async enviarEmailBienvenida(data: WelcomeEmailRequest): Promise<any> {
    return apiClient.post(`${this.basePath}/welcome`, data);
  }
}

export const emailService = new EmailService();

// 🎯 HOOK PERSONALIZADO PARA CLIENTES
// src/hooks/useCliente.ts

import { useState, useEffect } from 'react';
import { clienteService } from '../services/clienteService';
import { Cliente } from '../types/api.types';

export const useCliente = (codigoCliente?: string) => {
  const [cliente, setCliente] = useState<Cliente | null>(null);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchCliente = async (codigo: string) => {
    try {
      setLoading(true);
      setError(null);
      const response = await clienteService.getCliente(codigo);
      if (response.data.CUVc && response.data.CUVc.length > 0) {
        setCliente(response.data.CUVc[0]);
      } else {
        setError('Cliente no encontrado');
      }
    } catch (err) {
      setError('Error al cargar el cliente');
      console.error('Error fetching cliente:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (codigoCliente) {
      fetchCliente(codigoCliente);
    }
  }, [codigoCliente]);

  return {
    cliente,
    loading,
    error,
    refetch: fetchCliente
  };
};

// 📊 HOOK PARA FACTURAS
// src/hooks/useFacturas.ts

import { useState, useEffect } from 'react';
import { clienteService } from '../services/clienteService';
import { Factura } from '../types/api.types';

export const useFacturas = (codigoCliente?: string) => {
  const [facturas, setFacturas] = useState<Factura[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  const fetchFacturas = async (codigo: string) => {
    try {
      setLoading(true);
      setError(null);
      const response = await clienteService.getFacturas(codigo);
      setFacturas(response.data.IVVc || []);
    } catch (err) {
      setError('Error al cargar las facturas');
      console.error('Error fetching facturas:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (codigoCliente) {
      fetchFacturas(codigoCliente);
    }
  }, [codigoCliente]);

  return {
    facturas,
    loading,
    error,
    refetch: fetchFacturas
  };
};

// 🌐 CONFIGURACIÓN DE VARIABLES DE ENTORNO
// .env.production
// REACT_APP_API_BASE_URL=https://api.celero.network
// REACT_APP_API_TIMEOUT=30000

// .env.development
// REACT_APP_API_BASE_URL=http://localhost:5001
// REACT_APP_API_TIMEOUT=30000
