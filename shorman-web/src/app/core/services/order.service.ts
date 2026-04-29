import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, throwError, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Order, CreateOrderRequest } from '../models/order.model';

// Mock data for development
let MOCK_ORDERS: Order[] = [
  {
    id: 1,
    userId: 1,
    status: 'DELIVERED',
    paymentMethod: 'PAYPAL',
    addressId: 1,
    deliveryAddress: 'Hauptstraße 123, 10115 Berlin, Germany',
    items: [
      {
        id: 1,
        productId: 1,
        productName: 'Organic Bananas',
        productImageUrl: 'https://images.unsplash.com/photo-1603833665858-e61d17a86224?w=200',
        quantity: 2,
        unitPrice: 1.99,
        totalPrice: 3.98
      },
      {
        id: 2,
        productId: 3,
        productName: 'Whole Milk',
        productImageUrl: 'https://images.unsplash.com/photo-1563636619-e9143da7973b?w=200',
        quantity: 1,
        unitPrice: 1.29,
        totalPrice: 1.29
      }
    ],
    subtotal: 5.27,
    deliveryFee: 3.99,
    total: 9.26,
    createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
    updatedAt: new Date(Date.now() - 6 * 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: 2,
    userId: 1,
    status: 'PROCESSING',
    paymentMethod: 'BANK_TRANSFER',
    addressId: 1,
    deliveryAddress: 'Hauptstraße 123, 10115 Berlin, Germany',
    items: [
      {
        id: 3,
        productId: 2,
        productName: 'Fresh Tomatoes',
        productImageUrl: 'https://images.unsplash.com/photo-1592924357228-91a4daadcfea?w=200',
        quantity: 1,
        unitPrice: 2.49,
        totalPrice: 2.49
      },
      {
        id: 4,
        productId: 4,
        productName: 'Sourdough Bread',
        productImageUrl: 'https://images.unsplash.com/photo-1549931319-a545dcf3bc73?w=200',
        quantity: 1,
        unitPrice: 3.49,
        totalPrice: 3.49
      }
    ],
    subtotal: 5.98,
    deliveryFee: 3.99,
    total: 9.97,
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(), // 2 days ago
    updatedAt: new Date(Date.now() - 1 * 24 * 60 * 60 * 1000).toISOString()
  }
];

let nextOrderId = 3;

@Injectable({ providedIn: 'root' })
export class OrderService {
  private http = inject(HttpClient);
  private useMock = environment.useMockOrders;

  createOrder(req: CreateOrderRequest): Observable<Order> {
    if (this.useMock) {
      // In a real app, you'd fetch product details from ProductService
      // For now, just create a simple mock order
      const newOrder: Order = {
        id: nextOrderId++,
        userId: 1,
        status: 'PENDING',
        paymentMethod: req.paymentMethod,
        addressId: req.addressId,
        deliveryAddress: 'Mock Address',
        items: req.items.map((item, index) => ({
          id: Date.now() + index,
          productId: item.productId,
          productName: `Product ${item.productId}`,
          quantity: item.quantity,
          unitPrice: 2.99,
          totalPrice: 2.99 * item.quantity
        })),
        subtotal: req.items.reduce((sum, item) => sum + (2.99 * item.quantity), 0),
        deliveryFee: 3.99,
        total: req.items.reduce((sum, item) => sum + (2.99 * item.quantity), 0) + 3.99,
        createdAt: new Date().toISOString()
      };
      MOCK_ORDERS.push(newOrder);
      return of(newOrder).pipe(delay(500));
    }
    return this.http.post<Order>(`${environment.apiUrl}/orders`, req);
  }

  getOrders(): Observable<Order[]> {
    console.log('OrderService.getOrders called, useMock:', this.useMock);
    if (this.useMock) {
      console.log('Returning mock orders:', MOCK_ORDERS);
      return of([...MOCK_ORDERS]).pipe(delay(300));
    }
    return this.http.get<unknown>(`${environment.apiUrl}/orders`).pipe(
      map((response) => this.toOrderArray(response))
    );
  }

  getOrderById(id: number): Observable<Order> {
    if (this.useMock) {
      const order = MOCK_ORDERS.find(o => o.id === id);
      if (!order) {
        return throwError(() => new Error('Order not found'));
      }
      return of({ ...order }).pipe(delay(300));
    }
    return this.http.get<Order>(`${environment.apiUrl}/orders/${id}`);
  }

  private toOrderArray(response: unknown): Order[] {
    if (Array.isArray(response)) {
      return response as Order[];
    }

    if (response && typeof response === 'object') {
      const maybeItems = (response as { items?: unknown; data?: unknown; value?: unknown; $values?: unknown });
      if (Array.isArray(maybeItems.items)) return maybeItems.items as Order[];
      if (Array.isArray(maybeItems.data)) return maybeItems.data as Order[];
      if (Array.isArray(maybeItems.value)) return maybeItems.value as Order[];
      if (Array.isArray(maybeItems.$values)) return maybeItems.$values as Order[];
    }

    return [];
  }
}
