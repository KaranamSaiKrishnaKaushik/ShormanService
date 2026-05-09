import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, throwError, map, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Order, CreateOrderRequest } from '../models/order.model';

// Mock data for development
let MOCK_ORDERS: Order[] = [
  {
    id: 1,
    userId: 1,
    status: 'COMPLETED',
    paymentMethod: 'PAYPAL',
    paymentStatus: 'PAID',
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
    assignedRiderId: 4,
    assignedRiderName: 'Rider User',
    createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
    updatedAt: new Date(Date.now() - 6 * 24 * 60 * 60 * 1000).toISOString(),
    acceptedAt: new Date(Date.now() - 6.8 * 24 * 60 * 60 * 1000).toISOString(),
    pickedUpAt: new Date(Date.now() - 6.7 * 24 * 60 * 60 * 1000).toISOString(),
    outForDeliveryAt: new Date(Date.now() - 6.6 * 24 * 60 * 60 * 1000).toISOString(),
    deliveredAt: new Date(Date.now() - 6.5 * 24 * 60 * 60 * 1000).toISOString(),
    completedAt: new Date(Date.now() - 6.4 * 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: 2,
    userId: 1,
    status: 'AWAITING_PICKUP',
    paymentMethod: 'CASH_ON_DELIVERY',
    paymentStatus: 'CASH_PENDING',
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
    createdAt: new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString(),
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
        status: 'AWAITING_PICKUP',
        paymentMethod: req.paymentMethod,
        paymentStatus: req.paymentMethod === 'CASH_ON_DELIVERY' ? 'CASH_PENDING' : 'PAID',
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
    return this.http.post<Order>(`${environment.apiUrl}/orders`, req).pipe(
      timeout(15000)
    );
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

  getAvailableRiderOrders(): Observable<Order[]> {
    if (this.useMock) {
      return of(MOCK_ORDERS.filter(order => order.status === 'AWAITING_PICKUP')).pipe(delay(200));
    }

    return this.http.get<Order[]>(`${environment.apiUrl}/rider/orders/available`);
  }

  getMyRiderOrders(): Observable<Order[]> {
    if (this.useMock) {
      return of(MOCK_ORDERS.filter(order => order.assignedRiderId === 4 && order.status !== 'CANCELLED')).pipe(delay(200));
    }

    return this.http.get<Order[]>(`${environment.apiUrl}/rider/orders/mine`);
  }

  acceptRiderOrder(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        status: 'ASSIGNED_TO_RIDER',
        assignedRiderId: 4,
        assignedRiderName: 'Rider User',
        acceptedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/accept`, {});
  }

  markRiderOrderPickedUp(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        status: 'PICKED_UP',
        pickedUpAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/picked-up`, {});
  }

  markRiderOrderOutForDelivery(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        status: 'OUT_FOR_DELIVERY',
        outForDeliveryAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/out-for-delivery`, {});
  }

  markRiderOrderDelivered(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        status: 'DELIVERED',
        deliveredAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/delivered`, {});
  }

  markRiderOrderCashCollected(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        paymentStatus: 'CASH_COLLECTED',
        cashCollectedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/cash-collected`, {});
  }

  completeRiderOrder(orderId: number): Observable<Order> {
    if (this.useMock) {
      return this.applyMockTransition(orderId, order => ({
        ...order,
        status: 'COMPLETED',
        completedAt: new Date().toISOString(),
        updatedAt: new Date().toISOString()
      }));
    }

    return this.http.post<Order>(`${environment.apiUrl}/rider/orders/${orderId}/complete`, {});
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

  private applyMockTransition(orderId: number, update: (order: Order) => Order): Observable<Order> {
    const existing = MOCK_ORDERS.find(order => order.id === orderId);
    if (!existing) {
      return throwError(() => new Error('Order not found'));
    }

    const updated = update(existing);
    MOCK_ORDERS = MOCK_ORDERS.map(order => order.id === orderId ? updated : order);
    return of(updated).pipe(delay(200));
  }
}
