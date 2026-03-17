import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Order, CreateOrderRequest } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class OrderService {
  private http = inject(HttpClient);

  createOrder(req: CreateOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${environment.apiUrl}/orders`, req);
  }

  getOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${environment.apiUrl}/orders`);
  }

  getOrderById(id: number): Observable<Order> {
    return this.http.get<Order>(`${environment.apiUrl}/orders/${id}`);
  }
}
