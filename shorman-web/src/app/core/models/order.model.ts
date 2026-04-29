import { CartItem } from './cart.model';

export type PaymentMethod = 'PAYPAL' | 'BANK_TRANSFER' | 'CASH_ON_DELIVERY';
export type OrderStatus = 'PENDING' | 'CONFIRMED' | 'PROCESSING' | 'SHIPPED' | 'DELIVERED' | 'CANCELLED';

export interface OrderItem {
  id: number;
  productId: number;
  productName: string;
  productImageUrl?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface Order {
  id: number;
  userId: number;
  status: OrderStatus;
  paymentMethod: PaymentMethod;
  addressId: number;
  deliveryAddress?: string;
  items: OrderItem[];
  subtotal: number;
  deliveryFee: number;
  total: number;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateOrderRequest {
  addressId: number;
  paymentMethod: PaymentMethod;
  items: { productId: number; quantity: number }[];
}
