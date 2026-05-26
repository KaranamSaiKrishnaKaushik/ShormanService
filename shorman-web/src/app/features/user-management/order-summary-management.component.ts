import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, NgClass, NgFor, NgIf } from '@angular/common';
import { AdminOrderSummary } from '../../core/models/order.model';
import { UserManagementService } from '../../core/services/user-management.service';

@Component({
  selector: 'app-order-summary-management',
  standalone: true,
  imports: [NgFor, NgIf, NgClass, CurrencyPipe, DatePipe],
  template: `
    <section class="orders-page">
      <div class="container">
        <div class="title-row">
          <div>
            <h2>Admin Order History</h2>
            <p>Review customer, rider, payment, and delivery status, then open a row for full details.</p>
          </div>
          <button type="button" class="btn-retry" (click)="loadOrders()" [disabled]="loading">Refresh</button>
        </div>

        <div *ngIf="loading" class="loading">
          <div class="spinner"></div>
          <p>Loading all order history...</p>
        </div>

        <div *ngIf="errorMsg && !loading" class="error-state">
          <div class="error-icon">⚠️</div>
          <p>{{ errorMsg }}</p>
          <button class="btn-retry" (click)="loadOrders()">Retry</button>
        </div>

        <div *ngIf="!loading && !errorMsg && orders.length === 0" class="empty-state">
          <div class="empty-icon">📦</div>
          <h3>No orders available</h3>
          <p>All customer and rider order history will appear here.</p>
        </div>

        <div *ngFor="let order of orders" class="order-card" [class.expanded]="expandedOrderId === order.id">
          <button type="button" class="order-row-btn" (click)="toggleOrder(order.id)">
            <div class="order-header">
              <span class="order-id">#{{ order.id }}</span>
              <span class="order-status" [ngClass]="'status-' + order.status.toLowerCase()">
                {{ order.status.replaceAll('_', ' ') }}
              </span>
              <span class="order-customer">{{ order.customerName }}</span>
              <span class="order-rider">{{ order.assignedRiderName || 'Unassigned' }}</span>
              <span class="order-payment">{{ formatPaymentMethod(order.paymentMethod) }}</span>
              <span class="order-total-inline">{{ order.total | currency:'EUR' }}</span>
              <span class="order-date">{{ order.createdAt | date:'mediumDate' }}</span>
            </div>
          </button>

          <div *ngIf="expandedOrderId === order.id" class="expanded-body">
            <div class="meta-grid">
              <div>
                <span class="meta-label">Customer</span>
                <strong>{{ order.customerName }}</strong>
                <span class="meta-sub">{{ order.customerEmail }}</span>
              </div>
              <div>
                <span class="meta-label">Assigned Rider</span>
                <strong>{{ order.assignedRiderName || 'Unassigned' }}</strong>
                <span class="meta-sub">{{ order.paymentStatus.replaceAll('_', ' ') }}</span>
              </div>
              <div>
                <span class="meta-label">Payment</span>
                <strong>{{ formatPaymentMethod(order.paymentMethod) }}</strong>
                <span class="meta-sub">{{ order.total | currency:'EUR' }}</span>
              </div>
            </div>

            <div class="order-items">
              <div *ngFor="let item of order.items" class="order-item">
                <div class="item-copy">
                  <span>{{ item.productName }} × {{ item.quantity }}</span>
                  <span class="item-store" *ngIf="item.supermarketName">{{ item.supermarketName }}</span>
                </div>
                <span class="item-price">{{ item.totalPrice | currency:'EUR' }}</span>
              </div>
            </div>

            <div class="order-footer">
              <span class="delivery-address" *ngIf="order.deliveryAddress">
                📍 {{ order.deliveryAddress }}
              </span>
              <div class="order-total">
                Total: <strong>{{ order.total | currency:'EUR' }}</strong>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .orders-page {
      background: #f9fafb;
      min-height: 100%;
      border-radius: 22px;
    }
    .container {
      max-width: 960px;
      margin: 0 auto;
      padding: 0.25rem 0 1rem;
    }
    .title-row {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
      margin-bottom: 1.25rem;
    }
    h2 {
      font-size: 1.75rem;
      font-weight: 800;
      margin: 0;
      color: #1a1a1a;
    }
    .title-row p {
      margin: 0.45rem 0 0;
      color: #5f6f68;
    }
    .loading {
      text-align: center;
      padding: 3rem;
      color: #9ca3af;
    }
    .spinner {
      width: 40px;
      height: 40px;
      margin: 0 auto 1rem;
      border: 3px solid #e5e7eb;
      border-top-color: #2e7d32;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .error-state {
      text-align: center;
      padding: 3rem 2rem;
      background: #fee2e2;
      border-radius: 12px;
      margin-bottom: 2rem;
    }
    .error-icon {
      font-size: 3rem;
      margin-bottom: 0.75rem;
    }
    .error-state p {
      color: #991b1b;
      margin-bottom: 1rem;
    }
    .btn-retry {
      background: #174d2b;
      color: #fff;
      border: none;
      padding: 0.6rem 1.1rem;
      border-radius: 999px;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-retry:disabled {
      opacity: 0.7;
      cursor: default;
    }
    .empty-state {
      text-align: center;
      padding: 4rem 2rem;
      background: #fff;
      border-radius: 12px;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.07);
    }
    .empty-icon {
      font-size: 4rem;
      margin-bottom: 1rem;
    }
    .empty-state h3 {
      font-size: 1.25rem;
      color: #333;
      margin: 0 0 0.5rem;
    }
    .empty-state p {
      color: #9ca3af;
      margin: 0;
    }
    .order-card {
      background: #fff;
      border-radius: 18px;
      margin-bottom: 1rem;
      border: 1px solid #e0e8e2;
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
      overflow: hidden;
    }
    .order-card.expanded {
      border-color: #bfd2c4;
      box-shadow: 0 8px 18px rgba(19, 47, 36, 0.08);
    }
    .order-row-btn {
      width: 100%;
      border: none;
      background: transparent;
      text-align: left;
      padding: 1rem 1.25rem;
      cursor: pointer;
    }
    .expanded-body {
      padding: 0 1.25rem 1.25rem;
      border-top: 1px solid #f1f5f2;
    }
    .order-header {
      display: grid;
      grid-template-columns: auto auto minmax(150px, 1.1fr) minmax(140px, 1fr) minmax(160px, 1fr) auto auto;
      gap: 0.85rem;
      align-items: center;
    }
    .order-id {
      font-weight: 800;
      color: #2e7d32;
      font-size: 1rem;
    }
    .order-status {
      padding: 0.2rem 0.75rem;
      border-radius: 20px;
      font-size: 0.8rem;
      font-weight: 700;
      text-transform: uppercase;
      white-space: nowrap;
    }
    .status-awaiting_pickup {
      background: #fff9c4;
      color: #f57f17;
    }
    .status-assigned_to_rider,
    .status-picked_up,
    .status-out_for_delivery {
      background: #e3f2fd;
      color: #1565c0;
    }
    .status-delivered,
    .status-completed {
      background: #e8f5e9;
      color: #2e7d32;
    }
    .status-cancelled {
      background: #ffebee;
      color: #c62828;
    }
    .order-customer,
    .order-rider,
    .order-payment {
      color: #294133;
      font-size: 0.92rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .order-total-inline {
      color: #174d2b;
      font-weight: 800;
      white-space: nowrap;
    }
    .order-date {
      color: #9ca3af;
      font-size: 0.85rem;
      justify-self: end;
    }
    .meta-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 0.9rem;
      padding: 0.9rem 0;
      border-bottom: 1px solid #f1f5f2;
      margin-bottom: 0.9rem;
    }
    .meta-label {
      display: block;
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: #7a8a83;
      margin-bottom: 0.2rem;
    }
    .meta-sub {
      display: block;
      margin-top: 0.2rem;
      color: #6b7280;
      font-size: 0.84rem;
    }
    .order-items {
      margin-bottom: 0.75rem;
    }
    .order-item {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      font-size: 0.88rem;
      color: #555;
      padding: 0.35rem 0;
    }
    .item-copy {
      display: flex;
      flex-direction: column;
      gap: 0.2rem;
      min-width: 0;
    }
    .item-store {
      color: #6b7280;
      font-size: 0.78rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .item-price {
      font-weight: 600;
      color: #1a1a1a;
      white-space: nowrap;
    }
    .order-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 0.75rem;
      padding-top: 0.75rem;
      border-top: 1px solid #f3f4f6;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .delivery-address {
      font-size: 0.8rem;
      color: #9ca3af;
    }
    .order-total {
      font-size: 0.95rem;
      color: #333;
    }
    .order-total strong {
      color: #2e7d32;
      font-size: 1.05rem;
    }
    @media (max-width: 720px) {
      .title-row {
        flex-direction: column;
      }
      .order-header {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
      .order-date {
        justify-self: start;
      }
    }
  `]
})
export class OrderSummaryManagementComponent implements OnInit {
  private readonly userManagementService = inject(UserManagementService);
  private readonly cdr = inject(ChangeDetectorRef);

  orders: AdminOrderSummary[] = [];
  expandedOrderId: number | null = null;
  loading = true;
  errorMsg = '';

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    this.errorMsg = '';

    this.userManagementService.getOrderSummary().subscribe({
      next: orders => {
        this.orders = Array.isArray(orders) ? orders : [];
        this.expandedOrderId = this.orders[0]?.id ?? null;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load admin order history.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  toggleOrder(orderId: number): void {
    this.expandedOrderId = this.expandedOrderId === orderId ? null : orderId;
  }

  formatPaymentMethod(paymentMethod: string): string {
    switch (paymentMethod) {
      case 'STRIPE_CARD':
        return 'Card (Stripe)';
      case 'STRIPE_SEPA_DEBIT':
        return 'SEPA Debit (Stripe)';
      case 'STRIPE_KLARNA':
        return 'Klarna (Stripe)';
      case 'STRIPE_PAYPAL':
      case 'STRIPE_PAYPAL_GERMANY':
        return 'PayPal';
      case 'CASH_ON_DELIVERY':
        return 'Cash on delivery';
      default:
        return paymentMethod.replaceAll('_', ' ');
    }
  }
}
