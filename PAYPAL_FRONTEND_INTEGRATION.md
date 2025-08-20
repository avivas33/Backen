# PayPal Frontend Integration Documentation

## Overview
This document provides detailed information for integrating the PayPal payment endpoints from Api_Celero into your frontend application.

## Endpoints

### 1. Create PayPal Order
Creates a payment order with PayPal and returns the approval URL for user redirection.

**Endpoint:** `POST /api/Clientes/paypal/create-order`

**Request Body:**
```json
{
  "clienteCode": "string",      // Required: Client code
  "numeroFactura": "string",    // Required: Invoice number
  "amount": 0.00,              // Required: Payment amount (must be > 0)
  "currency": "USD",           // Optional: Currency code (default: "USD")
  "description": "string",     // Optional: Payment description
  "returnUrl": "string",       // Required: URL to redirect after successful payment
  "cancelUrl": "string",       // Required: URL to redirect if user cancels
  "emailCliente": "string",    // Optional: Client email
  "nombreCliente": "string"    // Optional: Client name
}
```

**Successful Response (200 OK):**
```json
{
  "success": true,
  "message": "Orden de pago creada exitosamente",
  "data": {
    "orderId": "PAYPAL-ORDER-ID",
    "approvalUrl": "https://www.paypal.com/checkoutnow?token=...",
    "status": "CREATED"
  }
}
```

**Error Responses:**

- **400 Bad Request** - Invalid input data
```json
{
  "success": false,
  "message": "El código de cliente y número de factura son requeridos"
}
```

- **500 Internal Server Error** - Server or PayPal authentication error
```json
{
  "success": false,
  "message": "Error al crear orden de pago en PayPal"
}
```

### 2. Capture PayPal Payment
Captures the payment after user approval on PayPal.

**Endpoint:** `POST /api/Clientes/paypal/capture-payment`

**Request Body:**
```json
{
  "orderId": "string",         // Required: PayPal order ID from create-order response
  "clienteCode": "string",     // Required: Client code
  "numeroFactura": "string"    // Required: Invoice number
}
```

## Frontend Implementation Example

### TypeScript/React Example

```typescript
// Types
interface PayPalCreateOrderRequest {
  clienteCode: string;
  numeroFactura: string;
  amount: number;
  currency?: string;
  description?: string;
  returnUrl: string;
  cancelUrl: string;
  emailCliente?: string;
  nombreCliente?: string;
}

interface PayPalCreateOrderResponse {
  success: boolean;
  message: string;
  data?: {
    orderId: string;
    approvalUrl: string;
    status: string;
  };
}

interface PayPalCaptureRequest {
  orderId: string;
  clienteCode: string;
  numeroFactura: string;
}

// Service
class PayPalService {
  private baseUrl = 'https://your-api-url/api/Clientes';

  async createPayPalOrder(request: PayPalCreateOrderRequest): Promise<PayPalCreateOrderResponse> {
    const response = await fetch(`${this.baseUrl}/paypal/create-order`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  }

  async capturePayPalPayment(request: PayPalCaptureRequest): Promise<any> {
    const response = await fetch(`${this.baseUrl}/paypal/capture-payment`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    });

    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    return await response.json();
  }
}

// React Component Example
import React, { useState } from 'react';

const PayPalPaymentComponent: React.FC = () => {
  const [loading, setLoading] = useState(false);
  const paypalService = new PayPalService();

  const handlePayPalPayment = async () => {
    setLoading(true);
    
    try {
      // Step 1: Create PayPal order
      const orderRequest: PayPalCreateOrderRequest = {
        clienteCode: 'CLIENT123',
        numeroFactura: 'INV-2024-001',
        amount: 100.00,
        currency: 'USD',
        description: 'Payment for Invoice INV-2024-001',
        returnUrl: `${window.location.origin}/payment/success`,
        cancelUrl: `${window.location.origin}/payment/cancel`,
        emailCliente: 'client@example.com',
        nombreCliente: 'John Doe'
      };

      const orderResponse = await paypalService.createPayPalOrder(orderRequest);
      
      if (orderResponse.success && orderResponse.data) {
        // Step 2: Redirect to PayPal for payment approval
        window.location.href = orderResponse.data.approvalUrl;
      } else {
        alert(orderResponse.message || 'Error creating PayPal order');
      }
    } catch (error) {
      console.error('PayPal payment error:', error);
      alert('Error processing payment. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <button 
      onClick={handlePayPalPayment} 
      disabled={loading}
      className="paypal-button"
    >
      {loading ? 'Processing...' : 'Pay with PayPal'}
    </button>
  );
};

// Success/Return Page Component
const PayPalReturnPage: React.FC = () => {
  const [capturing, setCapturing] = useState(false);
  const paypalService = new PayPalService();

  React.useEffect(() => {
    // Get PayPal token from URL parameters
    const urlParams = new URLSearchParams(window.location.search);
    const token = urlParams.get('token');
    const payerId = urlParams.get('PayerID');

    if (token && payerId) {
      capturePayment(token);
    }
  }, []);

  const capturePayment = async (orderId: string) => {
    setCapturing(true);
    
    try {
      const captureRequest: PayPalCaptureRequest = {
        orderId: orderId,
        clienteCode: 'CLIENT123', // Should be retrieved from session/state
        numeroFactura: 'INV-2024-001' // Should be retrieved from session/state
      };

      const result = await paypalService.capturePayPalPayment(captureRequest);
      
      if (result.success) {
        // Payment successful
        alert('Payment completed successfully!');
        // Redirect to success page or update UI
      } else {
        alert(result.message || 'Payment capture failed');
      }
    } catch (error) {
      console.error('Payment capture error:', error);
      alert('Error capturing payment');
    } finally {
      setCapturing(false);
    }
  };

  return (
    <div>
      {capturing ? (
        <p>Processing your payment...</p>
      ) : (
        <p>Payment processing complete</p>
      )}
    </div>
  );
};
```

### JavaScript/jQuery Example

```javascript
// PayPal payment function
function initiatePayPalPayment(clienteCode, numeroFactura, amount) {
  const requestData = {
    clienteCode: clienteCode,
    numeroFactura: numeroFactura,
    amount: parseFloat(amount),
    currency: 'USD',
    description: `Payment for invoice ${numeroFactura}`,
    returnUrl: window.location.origin + '/payment/success',
    cancelUrl: window.location.origin + '/payment/cancel'
  };

  $.ajax({
    url: '/api/Clientes/paypal/create-order',
    type: 'POST',
    contentType: 'application/json',
    data: JSON.stringify(requestData),
    success: function(response) {
      if (response.success && response.data) {
        // Redirect to PayPal
        window.location.href = response.data.approvalUrl;
      } else {
        alert(response.message || 'Error creating payment order');
      }
    },
    error: function(xhr, status, error) {
      console.error('PayPal error:', error);
      alert('Error processing payment. Please try again.');
    }
  });
}

// Capture payment on return page
function capturePayPalPayment() {
  const urlParams = new URLSearchParams(window.location.search);
  const token = urlParams.get('token');
  const payerId = urlParams.get('PayerID');

  if (token && payerId) {
    const captureData = {
      orderId: token,
      clienteCode: sessionStorage.getItem('clienteCode'), // Retrieve from storage
      numeroFactura: sessionStorage.getItem('numeroFactura') // Retrieve from storage
    };

    $.ajax({
      url: '/api/Clientes/paypal/capture-payment',
      type: 'POST',
      contentType: 'application/json',
      data: JSON.stringify(captureData),
      success: function(response) {
        if (response.success) {
          alert('Payment completed successfully!');
          // Clear session data
          sessionStorage.removeItem('clienteCode');
          sessionStorage.removeItem('numeroFactura');
          // Redirect to success page
          window.location.href = '/payment/confirmation';
        } else {
          alert(response.message || 'Payment capture failed');
        }
      },
      error: function(xhr, status, error) {
        console.error('Capture error:', error);
        alert('Error capturing payment');
      }
    });
  }
}
```

## Payment Flow

1. **User initiates payment**: Frontend calls `/paypal/create-order` with invoice details
2. **API creates PayPal order**: Returns approval URL
3. **User is redirected to PayPal**: Logs in and approves payment
4. **PayPal redirects back**: To your `returnUrl` with `token` and `PayerID` parameters
5. **Frontend captures payment**: Calls `/paypal/capture-payment` with the order ID
6. **Payment is completed**: API processes the payment and updates records

## Important Considerations

1. **Session Management**: Store `clienteCode` and `numeroFactura` in session/state before redirecting to PayPal
2. **Error Handling**: Implement robust error handling for network failures and PayPal errors
3. **Security**: Never expose sensitive payment information in frontend code
4. **Testing**: Use PayPal sandbox environment for testing before production
5. **URLs**: Ensure `returnUrl` and `cancelUrl` are absolute URLs and properly configured
6. **Currency**: Default is USD, but can be changed based on requirements
7. **Amount Validation**: Ensure amounts are positive numbers with maximum 2 decimal places

## URL Parameters on Return

When PayPal redirects back to your `returnUrl`, it includes:
- `token`: The PayPal order ID (use this for capture)
- `PayerID`: The PayPal payer ID

Example return URL: `https://yoursite.com/payment/success?token=EC-1234567890&PayerID=ABCDEFG`

## Testing

For testing the integration:
1. Use test client codes and invoice numbers
2. Ensure your return URLs are accessible
3. Test both successful and cancelled payment flows
4. Verify error handling for invalid data
5. Check payment capture after approval