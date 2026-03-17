import { Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf, CurrencyPipe, DatePipe } from '@angular/common';
import { OrderService } from '../../core/services/order.service';
import { Order } from '../../core/models/order.model';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [NgFor, NgIf, CurrencyPipe, DatePipe],
  template: `
    <div class="orders-page">
      <div class="container">
        <h1>Order History</h1>
        <div *ngIf="loading" class="loading">Loading orders…</div>
        <div *ngIf="!loading && orders.length === 0" class="empty">No orders yet.</div>
        <div *ngFor="let order of orders" class="order-card">
          <div class="order-header">
            <span class="order-id">#{{ order.id }}</span>
            <span class="order-status" [class]="'status-' + order.status.toLowerCase()">{{ order.status }}</span>
            <span class="order-date">{{ order.createdAt | date:'mediumDate' }}</span>
          </div>
          <div class="order-items">
            <div *ngFor="let item of order.items" class="order-item">
              {{ item.productName }} × {{ item.quantity }} — {{ item.totalPrice | currency:'EUR' }}
            </div>
          </div>
          <div class="order-total">Total: <strong>{{ order.total | currency:'EUR' }}</strong></div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .orders-page { background: #F9FAFB; min-height: calc(100vh - 128px); padding: 2rem 0; }
    .container { max-width: 800px; margin: 0 auto; padding: 0 1rem; }
    h1 { font-size: 1.75rem; font-weight: 800; margin-bottom: 1.5rem; }
    .loading, .empty { color: #999; text-align: center; padding: 3rem; }
    .order-card { background: #fff; border-radius: 12px; padding: 1.25rem; margin-bottom: 1rem; box-shadow: 0 1px 4px rgba(0,0,0,0.07); }
    .order-header { display: flex; gap: 1rem; align-items: center; margin-bottom: 0.75rem; flex-wrap: wrap; }
    .order-id { font-weight: 800; color: #2E7D32; }
    .order-status { padding: 0.2rem 0.75rem; border-radius: 20px; font-size: 0.8rem; font-weight: 700; text-transform: uppercase; }
    .status-pending { background: #FFF9C4; color: #F57F17; }
    .status-confirmed, .status-processing { background: #E3F2FD; color: #1565C0; }
    .status-delivered { background: #E8F5E9; color: #2E7D32; }
    .status-cancelled { background: #FFEBEE; color: #C62828; }
    .order-date { color: #9CA3AF; font-size: 0.85rem; margin-left: auto; }
    .order-item { font-size: 0.88rem; color: #555; padding: 0.2rem 0; }
    .order-total { margin-top: 0.75rem; font-size: 0.95rem; color: #333; }
    .order-total strong { color: #2E7D32; }
  `]
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  orders: Order[] = [];
  loading = true;
  ngOnInit() {
    this.orderService.getOrders().subscribe({ next: o => { this.orders = o; this.loading = false; }, error: () => { this.loading = false; } });
  }
}
