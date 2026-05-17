import { CartItem } from './cart.model';

export type PaymentMethod = 'STRIPE_CARD' | 'STRIPE_SEPA_DEBIT' | 'STRIPE_KLARNA' | 'STRIPE_PAYPAL' | 'STRIPE_PAYPAL_GERMANY' | 'CASH_ON_DELIVERY';
export type OrderStatus =
  | 'PENDING'
  | 'AWAITING_PICKUP'
  | 'ASSIGNED_TO_RIDER'
  | 'PICKED_UP'
  | 'OUT_FOR_DELIVERY'
  | 'DELIVERED'
  | 'COMPLETED'
  | 'CANCELLED';

export type PaymentStatus = 'PENDING' | 'PAID' | 'FAILED' | 'CASH_PENDING' | 'CASH_COLLECTED';

export interface OrderItem {
  id: number;
  productId: number;
  productName: string;
  productImageUrl?: string;
  supermarketName?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface Order {
  id: number;
  userId: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  addressId: number;
  deliveryAddress?: string;
  items: OrderItem[];
  subtotal: number;
  deliveryFee: number;
  total: number;
  assignedRiderId?: number;
  assignedRiderName?: string;
  createdAt: string;
  updatedAt?: string;
  acceptedAt?: string;
  pickedUpAt?: string;
  outForDeliveryAt?: string;
  deliveredAt?: string;
  cashCollectedAt?: string;
  completedAt?: string;
}

export interface AdminOrderSummary extends Order {
  customerName: string;
  customerEmail: string;
}

export interface CreateOrderRequest {
  addressId: number;
  paymentMethod: PaymentMethod;
  items: { productId: number; quantity: number }[];
}

export interface CheckoutSessionResponse {
  orderId: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  checkoutUrl: string;
  sessionId: string;
}
