// CONFIGURACIÓN TYPESCRIPT PARA REACT - API CELERO
// Archivo: src/types/api.ts

// ============================================
// TIPOS PARA LA API CELERO
// ============================================

export interface ApiResponse<T> {
  data: T;
  message?: string;
  success: boolean;
}

// ============================================
// TIPOS DE CLIENTE
// ============================================

export interface Cliente {
  cedula: string;
  nombre: string;
  email: string;
  telefono: string;
  direccion?: string;
  activo: boolean;
}

export interface ClienteDetallado extends Cliente {
  fechaRegistro: string;
  ultimoPago?: string;
  saldoPendiente: number;
}

// ============================================
// TIPOS DE FACTURA
// ============================================

export interface Factura {
  numero: string;
  cedula: string;
  fecha: string;
  fechaVencimiento: string;
  monto: number;
  saldoPendiente: number;
  estado: 'ABIERTA' | 'PAGADA' | 'VENCIDA' | 'CANCELADA';
  descripcion?: string;
}

export interface FacturaDetalle extends Factura {
  items: FacturaItem[];
  impuestos: number;
  descuentos: number;
  total: number;
}

export interface FacturaItem {
  codigo: string;
  descripcion: string;
  cantidad: number;
  precioUnitario: number;
  subtotal: number;
}

// ============================================
// TIPOS DE CUOTA
// ============================================

export interface Cuota {
  numero: string;
  numeroFactura: string;
  fechaVencimiento: string;
  monto: number;
  montoPagado: number;
  saldoPendiente: number;
  estado: 'PENDIENTE' | 'PAGADA' | 'VENCIDA';
}

// ============================================
// TIPOS DE FORMA DE PAGO
// ============================================

export interface FormaPago {
  id: number;
  nombre: string;
  descripcion?: string;
  activo: boolean;
  requiereAutorizacion: boolean;
}

// ============================================
// TIPOS DE RECIBO
// ============================================

export interface ReciboRequest {
  cedula: string;
  facturas: FacturaPago[];
  formaPago: number;
  observaciones?: string;
  email?: string;
}

export interface FacturaPago {
  numero: string;
  monto: number;
}

export interface ReciboResponse {
  numeroRecibo: string;
  fecha: string;
  total: number;
  estado: string;
  archivoUrl?: string;
}

// ============================================
// TIPOS DE PAGO COBALT (TARJETAS)
// ============================================

export interface CobaltVentaRequest {
  cedula: string;
  facturas: FacturaPago[];
  descripcion: string;
  email: string;
}

export interface CobaltVentaResponse {
  transactionId: string;
  paymentUrl: string;
  amount: number;
  status: string;
  expiresAt: string;
}

// ============================================
// TIPOS DE PAGO YAPPY
// ============================================

export interface YappyOrdenRequest {
  cedula: string;
  facturas: FacturaPago[];
  descripcion: string;
  telefono: string;
}

export interface YappyOrdenResponse {
  orderId: string;
  paymentUrl: string;
  qrCode: string;
  amount: number;
  status: string;
  expiresAt: string;
}

// ============================================
// TIPOS DE EMAIL
// ============================================

export interface EmailRequest {
  destinatario: string;
  asunto: string;
  mensaje: string;
  archivoAdjunto?: string; // Base64
  tipoArchivo?: string;
  nombreArchivo?: string;
}

export interface EmailResponse {
  enviado: boolean;
  messageId: string;
  error?: string;
}

// ============================================
// TIPOS DE RECAPTCHA
// ============================================

export interface RecaptchaRequest {
  token: string;
}

export interface RecaptchaResponse {
  valido: boolean;
  score?: number;
  action?: string;
  error?: string;
}

// ============================================
// TIPOS DE ERROR DE API
// ============================================

export interface ApiError {
  message: string;
  statusCode: number;
  details?: any;
  timestamp: string;
}

// ============================================
// CONFIGURACIÓN DE API
// ============================================

export interface ApiConfig {
  baseURL: string;
  timeout: number;
  headers: Record<string, string>;
}

// ============================================
// TIPOS PARA HOOKS PERSONALIZADOS
// ============================================

export interface UseApiResult<T> {
  data: T | null;
  loading: boolean;
  error: Error | null;
  refetch: () => void;
}

export interface UseApiOptions {
  immediate?: boolean;
  onSuccess?: (data: any) => void;
  onError?: (error: Error) => void;
}

// ============================================
// CONSTANTES DE LA API
// ============================================

export const API_ENDPOINTS = {
  // Clientes
  CLIENTES: '/api/clientes',
  CLIENTE_BY_CEDULA: (cedula: string) => `/api/clientes/${cedula}`,
  FACTURAS_ABIERTAS: (cedula: string) => `/api/clientes/${cedula}/facturas-abiertas`,
  CUOTAS: (cedula: string) => `/api/clientes/${cedula}/cuotas`,
  FACTURA_DETALLE: (cedula: string, numero: string) => `/api/clientes/${cedula}/facturas/${numero}`,
  
  // Pagos
  FORMAS_PAGO: '/api/formas-pago',
  FORMA_PAGO_BY_ID: (id: number) => `/api/formas-pago/${id}`,
  RECIBOS: '/api/recibos',
  
  // Cobalt
  COBALT_CREAR_VENTA: '/api/cobalt/crear-venta',
  
  // Yappy
  YAPPY_CREAR_ORDEN: '/api/yappy/crear-orden',
  
  // Email
  EMAIL_ENVIAR: '/api/email/enviar',
  
  // Seguridad
  RECAPTCHA_VERIFICAR: '/api/recaptcha/verificar',
  
  // Health
  HEALTH: '/health',
  SWAGGER: '/swagger',
  OPENAPI: '/openapi/v1.json'
} as const;

export const ESTADOS_FACTURA = {
  ABIERTA: 'ABIERTA',
  PAGADA: 'PAGADA', 
  VENCIDA: 'VENCIDA',
  CANCELADA: 'CANCELADA'
} as const;

export const ESTADOS_CUOTA = {
  PENDIENTE: 'PENDIENTE',
  PAGADA: 'PAGADA',
  VENCIDA: 'VENCIDA'
} as const;

export const HTTP_STATUS = {
  OK: 200,
  CREATED: 201,
  BAD_REQUEST: 400,
  UNAUTHORIZED: 401,
  NOT_FOUND: 404,
  INTERNAL_SERVER_ERROR: 500
} as const;

// ============================================
// UTILIDADES DE TIPO
// ============================================

export type EstadoFactura = typeof ESTADOS_FACTURA[keyof typeof ESTADOS_FACTURA];
export type EstadoCuota = typeof ESTADOS_CUOTA[keyof typeof ESTADOS_CUOTA];
export type HttpStatusCode = typeof HTTP_STATUS[keyof typeof HTTP_STATUS];

// ============================================
// CONFIGURACIÓN DE EXPORTACIÓN
// ============================================

export default {
  ApiResponse,
  Cliente,
  Factura,
  Cuota,
  FormaPago,
  ReciboRequest,
  CobaltVentaRequest,
  YappyOrdenRequest,
  EmailRequest,
  RecaptchaRequest,
  ApiError,
  API_ENDPOINTS,
  ESTADOS_FACTURA,
  ESTADOS_CUOTA,
  HTTP_STATUS
};
