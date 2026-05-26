import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CurrencyPipe, DatePipe, NgClass, NgFor, NgIf } from '@angular/common';
import { Order } from '../../core/models/order.model';
import { OrderService } from '../../core/services/order.service';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-rider-dashboard',
  standalone: true,
  imports: [NgFor, NgIf, NgClass, DatePipe, CurrencyPipe, TranslateModule],
  template: `
    <section class="rider-shell">
      <header class="hero">
        <div>
          <span class="eyebrow">{{ 'rider.eyebrow' | translate }}</span>
          <h1>{{ 'rider.title' | translate }}</h1>
          <p>{{ 'rider.subtitle' | translate }}</p>
        </div>
        <div class="meta-card">
          <span>{{ today | date: 'EEEE, d MMM y' }}</span>
          <strong>{{ availableOrders.length }} {{ 'rider.openQueue' | translate }}</strong>
        </div>
      </header>

      <p *ngIf="errorMsg" class="error-msg">{{ errorMsg }}</p>

      <div class="layout">
        <section class="column card-shell">
          <div class="section-head">
            <h2>{{ 'rider.availableOrders' | translate }}</h2>
            <button type="button" class="ghost-btn" (click)="loadOrders()" [disabled]="loading">{{ 'common.refresh' | translate }}</button>
          </div>
          <p class="section-copy">{{ 'rider.availableOrdersHint' | translate }}</p>

          <div *ngIf="loading" class="state-msg">{{ 'rider.loadingQueue' | translate }}</div>
          <div *ngIf="!loading && availableOrders.length === 0" class="state-msg">{{ 'rider.noOpenRequests' | translate }}</div>

          <button
            *ngFor="let order of availableOrders"
            type="button"
            class="queue-card"
            [class.selected]="selectedOrder?.id === order.id"
            (click)="selectOrder(order)">
            <div class="queue-top">
              <strong>#{{ order.id }}</strong>
              <span class="status-pill awaiting">{{ formatStatus(order.status) }}</span>
            </div>
            <div class="queue-meta">
              <span>{{ order.items.length }} items</span>
              <span>{{ order.total | currency:'EUR' }}</span>
            </div>
            <div class="queue-address">{{ order.deliveryAddress }}</div>
          </button>
        </section>

        <section class="column card-shell">
          <div class="section-head">
            <h2>{{ 'rider.myActiveDeliveries' | translate }}</h2>
            <span class="count-pill">{{ myOrders.length }}</span>
          </div>
          <p class="section-copy">{{ 'rider.activeDeliveriesHint' | translate }}</p>

          <div *ngIf="!loading && myOrders.length === 0" class="state-msg">{{ 'rider.noActiveDeliveries' | translate }}</div>

          <button
            *ngFor="let order of myOrders"
            type="button"
            class="queue-card"
            [class.selected]="selectedOrder?.id === order.id"
            (click)="selectOrder(order)">
            <div class="queue-top">
              <strong>#{{ order.id }}</strong>
              <span class="status-pill" [ngClass]="statusClass(order.status)">{{ formatStatus(order.status) }}</span>
            </div>
            <div class="queue-meta">
              <span>{{ formatPaymentMethod(order.paymentMethod) }}</span>
              <span>{{ formatPaymentStatus(order.paymentStatus) }}</span>
            </div>
            <div class="queue-address">{{ order.deliveryAddress }}</div>
          </button>

          <div class="history-head">
            <h2>{{ 'rider.completedHistory' | translate }}</h2>
            <span class="count-pill">{{ completedOrders.length }}</span>
          </div>
          <p class="section-copy">{{ 'rider.completedHistoryHint' | translate }}</p>

          <div *ngIf="!loading && completedOrders.length === 0" class="state-msg">{{ 'rider.noCompletedOrders' | translate }}</div>

          <button
            *ngFor="let order of completedOrders"
            type="button"
            class="queue-card history-card"
            [class.selected]="selectedOrder?.id === order.id"
            (click)="selectOrder(order)">
            <div class="queue-top">
              <strong>#{{ order.id }}</strong>
              <span class="status-pill" [ngClass]="statusClass(order.status)">{{ formatStatus(order.status) }}</span>
            </div>
            <div class="queue-meta">
              <span>{{ formatPaymentMethod(order.paymentMethod) }}</span>
              <span>{{ order.total | currency:'EUR' }}</span>
            </div>
            <div class="queue-address">{{ order.deliveryAddress }}</div>
          </button>
        </section>

        <section class="detail-shell card-shell" *ngIf="selectedOrder as order">
          <div class="section-head">
            <div>
              <h2>{{ 'rider.order' | translate }} #{{ order.id }}</h2>
              <p class="detail-copy">{{ formatStatus(order.status) }} · {{ formatPaymentStatus(order.paymentStatus) }}</p>
            </div>
            <a class="ghost-btn link-btn" [href]="mapsLink(order)" target="_blank" rel="noreferrer">{{ 'rider.openInGoogleMaps' | translate }}</a>
          </div>

          <div class="detail-grid">
            <div>
              <span class="label">{{ 'rider.deliveryAddress' | translate }}</span>
              <strong>{{ order.deliveryAddress }}</strong>
            </div>
            <div>
              <span class="label">{{ 'rider.assignedRider' | translate }}</span>
              <strong>{{ order.assignedRiderName || ('rider.unassigned' | translate) }}</strong>
            </div>
            <div>
              <span class="label">{{ 'rider.paymentMethod' | translate }}</span>
              <strong>{{ formatPaymentMethod(order.paymentMethod) }}</strong>
            </div>
            <div>
              <span class="label">{{ 'rider.total' | translate }}</span>
              <strong>{{ order.total | currency:'EUR' }}</strong>
            </div>
          </div>

          <div class="actions-row">
            <button *ngIf="order.status === 'AWAITING_PICKUP'" type="button" class="primary-btn" (click)="acceptOrder(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.acceptOrder' | translate }}</button>
            <button *ngIf="order.status === 'ASSIGNED_TO_RIDER'" type="button" class="primary-btn" (click)="markPickedUp(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.markPickedUp' | translate }}</button>
            <button *ngIf="order.status === 'PICKED_UP'" type="button" class="primary-btn" (click)="markOutForDelivery(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.outForDelivery' | translate }}</button>
            <button *ngIf="order.status === 'OUT_FOR_DELIVERY'" type="button" class="primary-btn" (click)="markDelivered(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.markDelivered' | translate }}</button>
            <button *ngIf="isCashCollectionRequired(order)" type="button" class="primary-btn" (click)="markCashCollected(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.cashCollected' | translate }}</button>
            <button *ngIf="canComplete(order)" type="button" class="success-btn" (click)="completeOrder(order)" [disabled]="actionOrderId === order.id">{{ 'rider.actions.completeOrder' | translate }}</button>
          </div>

          <div class="timeline">
            <span *ngIf="order.acceptedAt">Accepted {{ order.acceptedAt | date:'short' }}</span>
            <span *ngIf="order.pickedUpAt">Picked up {{ order.pickedUpAt | date:'short' }}</span>
            <span *ngIf="order.outForDeliveryAt">Out for delivery {{ order.outForDeliveryAt | date:'short' }}</span>
            <span *ngIf="order.deliveredAt">Delivered {{ order.deliveredAt | date:'short' }}</span>
            <span *ngIf="order.cashCollectedAt && isCashOnDelivery(order)">Cash collected {{ order.cashCollectedAt | date:'short' }}</span>
            <span *ngIf="order.completedAt">Completed {{ order.completedAt | date:'short' }}</span>
          </div>

          <div class="items-list">
            <section *ngFor="let storeGroup of getStoreGroups(order)" class="store-group">
              <div class="store-group-head">
                <span class="store-pill">{{ storeGroup.storeName }}</span>
                <strong>{{ storeGroup.total | currency:'EUR' }}</strong>
              </div>

              <article *ngFor="let item of storeGroup.items" class="item-row">
                <div class="item-main">
                  <strong class="item-name">{{ displayProductName(item.productName) }}</strong>
                  <span class="item-meta">{{ item.quantity }} × {{ item.unitPrice | currency:'EUR' }}</span>
                </div>
                <strong class="item-total">{{ item.totalPrice | currency:'EUR' }}</strong>
              </article>
            </section>
          </div>
        </section>
      </div>
    </section>
  `,
  styles: [`
    .rider-shell {
      padding: 2rem;
      max-width: 1100px;
      margin: 0 auto;
    }
    .hero {
      display: grid;
      grid-template-columns: 1.8fr 1fr;
      gap: 1.25rem;
      align-items: stretch;
      margin-bottom: 1.5rem;
    }
    .eyebrow {
      display: inline-block;
      color: #8b4d00;
      font-size: 0.8rem;
      font-weight: 700;
      letter-spacing: 0.14em;
      text-transform: uppercase;
      margin-bottom: 0.75rem;
    }
    h1 {
      margin: 0 0 0.75rem;
      font-size: clamp(2rem, 4vw, 3.1rem);
      line-height: 1.05;
      color: #132f24;
    }
    p {
      margin: 0;
      color: #53695a;
      line-height: 1.6;
    }
    .meta-card,
    .card-shell {
      background: linear-gradient(180deg, #ffffff 0%, #f7fbf5 100%);
      border: 1px solid #dce6d8;
      border-radius: 22px;
      box-shadow: 0 18px 38px rgba(24, 54, 44, 0.08);
    }
    .meta-card {
      padding: 1.4rem;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      color: #365042;
    }
    .meta-card strong {
      font-size: 2rem;
      color: #174d2b;
    }
    .error-msg {
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: 14px;
      background: #fff1f0;
      color: #9a2f27;
      border: 1px solid #f1c8c4;
    }
    .layout {
      display: grid;
      grid-template-columns: minmax(260px, 320px) minmax(260px, 320px) minmax(340px, 1fr);
      gap: 1rem;
    }
    .card-shell {
      padding: 1.2rem;
    }
    .section-head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
      margin-bottom: 0.75rem;
    }
    .section-head h2 {
      margin: 0 0 0.35rem;
      font-size: 1.15rem;
      color: #20352a;
    }
    .section-copy,
    .detail-copy {
      margin: 0;
      font-size: 0.95rem;
      color: #607568;
    }
    .count-pill,
    .status-pill {
      background: #eef7ed;
      color: #1f6a35;
      border-radius: 999px;
      padding: 0.45rem 0.75rem;
      font-size: 0.82rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .status-pill.awaiting {
      background: #fff5e8;
      color: #965b08;
    }
    .status-pill.assigned_to_rider,
    .status-pill.picked_up,
    .status-pill.out_for_delivery {
      background: #eaf4ff;
      color: #1762a1;
    }
    .status-pill.delivered,
    .status-pill.completed {
      background: #eef7ed;
      color: #1f6a35;
    }
    .state-msg {
      padding: 1rem 0;
      color: #64796c;
    }
    .queue-card {
      width: 100%;
      text-align: left;
      border: 1px solid #dbe5dd;
      background: #fff;
      border-radius: 18px;
      padding: 1rem;
      cursor: pointer;
      margin-bottom: 0.75rem;
    }
    .queue-card.selected {
      border-color: #174d2b;
      box-shadow: 0 10px 24px rgba(23, 77, 43, 0.12);
    }
    .history-card {
      background: #fcfdfc;
    }
    .history-head {
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: start;
      margin: 1rem 0 0.75rem;
      padding-top: 0.25rem;
      border-top: 1px solid #e7eeea;
    }
    .queue-top,
    .queue-meta,
    .detail-grid,
    .item-row,
    .timeline,
    .actions-row {
      display: flex;
      justify-content: space-between;
      gap: 0.75rem;
      flex-wrap: wrap;
    }
    .queue-meta,
    .queue-address,
    .timeline,
    .label,
    .item-row span {
      color: #64796c;
      font-size: 0.92rem;
    }
    .queue-address {
      margin-top: 0.5rem;
      line-height: 1.5;
    }
    .detail-grid {
      margin: 1rem 0;
      padding: 1rem;
      border-radius: 16px;
      background: #fbfcfb;
    }
    .detail-grid > div {
      min-width: 180px;
      display: grid;
      gap: 0.3rem;
    }
    .label {
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-size: 0.72rem;
      font-weight: 700;
    }
    .actions-row {
      margin-bottom: 1rem;
    }
    .ghost-btn,
    .primary-btn,
    .success-btn,
    .link-btn {
      border: none;
      border-radius: 999px;
      padding: 0.75rem 1rem;
      font-weight: 700;
      text-decoration: none;
      cursor: pointer;
    }
    .ghost-btn {
      background: #eef3ef;
      color: #264636;
    }
    .primary-btn {
      background: #123f27;
      color: #fff;
    }
    .success-btn {
      background: #0d7c4a;
      color: #fff;
    }
    .items-list {
      display: grid;
      gap: 0.85rem;
    }
    .store-group {
      padding: 1rem;
      border: 1px solid #e3ebe6;
      border-radius: 18px;
      background: #fff;
    }
    .store-group-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 0.75rem;
      padding-bottom: 0.85rem;
      margin-bottom: 0.2rem;
      border-bottom: 1px solid #edf2ee;
      color: #132f24;
    }
    .item-row {
      padding: 1rem 0;
      border-bottom: 1px solid #e7eeea;
      align-items: start;
      display: grid;
      grid-template-columns: minmax(0, 1fr) auto;
      gap: 1rem;
    }
    .item-main {
      display: grid;
      gap: 0.4rem;
      min-width: 0;
    }
    .item-topline {
      display: flex;
      gap: 0.5rem;
      align-items: center;
    }
    .store-pill {
      display: inline-flex;
      width: fit-content;
      padding: 0.28rem 0.55rem;
      border-radius: 999px;
      background: #eef3ef;
      color: #315244;
      font-size: 0.73rem;
      font-weight: 800;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }
    .item-name {
      font-size: 1rem;
      line-height: 1.45;
      color: #16261d;
      word-break: break-word;
    }
    .item-meta {
      color: #64796c;
      font-size: 0.9rem;
      line-height: 1.5;
    }
    .item-total {
      white-space: nowrap;
      font-size: 1rem;
      color: #132f24;
    }
    .store-group .item-row:last-child {
      border-bottom: none;
      padding-bottom: 0;
    }
    @media (max-width: 820px) {
      .hero {
        grid-template-columns: 1fr;
      }
      .rider-shell {
        padding: 1.25rem;
      }
      .layout {
        grid-template-columns: 1fr;
      }
      .item-row {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class RiderDashboardComponent implements OnInit {
  private readonly orderService = inject(OrderService);
  private readonly cdr = inject(ChangeDetectorRef);

  today = new Date();
  availableOrders: Order[] = [];
  myOrders: Order[] = [];
  completedOrders: Order[] = [];
  selectedOrder: Order | null = null;
  loading = true;
  errorMsg = '';
  actionOrderId: number | null = null;

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    this.errorMsg = '';

    this.orderService.getAvailableRiderOrders().subscribe({
      next: availableOrders => {
        this.availableOrders = Array.isArray(availableOrders) ? availableOrders : [];
        this.orderService.getMyRiderOrders().subscribe({
          next: riderOrders => {
            const assignedOrders = Array.isArray(riderOrders) ? riderOrders : [];
            this.myOrders = assignedOrders.filter(order => order.status !== 'COMPLETED' && order.status !== 'CANCELLED');
            this.completedOrders = assignedOrders.filter(order => order.status === 'COMPLETED');
            const allOrders = [...this.availableOrders, ...this.myOrders, ...this.completedOrders];
            if (!this.selectedOrder || !allOrders.some(order => order.id === this.selectedOrder?.id)) {
              this.selectedOrder = allOrders[0] ?? null;
            } else {
              this.selectedOrder = allOrders.find(order => order.id === this.selectedOrder?.id) ?? this.selectedOrder;
            }
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: err => {
            this.errorMsg = err?.error?.message || 'Failed to load assigned rider orders.';
            this.loading = false;
            this.cdr.detectChanges();
          }
        });
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load available rider orders.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  selectOrder(order: Order): void {
    this.selectedOrder = order;
  }

  mapsLink(order: Order): string {
    return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(order.deliveryAddress ?? '')}`;
  }

  formatStatus(status: string): string {
    return status.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, char => char.toUpperCase());
  }

  formatPaymentStatus(status: string): string {
    return this.formatStatus(status);
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
        return this.formatStatus(paymentMethod);
    }
  }

  displayProductName(name: string): string {
    const textarea = document.createElement('textarea');
    textarea.innerHTML = name;
    return textarea.value;
  }

  getStoreGroups(order: Order): Array<{ storeName: string; items: Order['items']; total: number }> {
    const groups = new Map<string, { storeName: string; items: Order['items']; total: number }>();

    for (const item of order.items) {
      const storeName = item.supermarketName?.trim() || 'Store not captured';
      const existingGroup = groups.get(storeName);

      if (existingGroup) {
        existingGroup.items.push(item);
        existingGroup.total += item.totalPrice;
        continue;
      }

      groups.set(storeName, {
        storeName,
        items: [item],
        total: item.totalPrice
      });
    }

    return Array.from(groups.values());
  }

  statusClass(status: string): string {
    return status.toLowerCase();
  }

  canComplete(order: Order): boolean {
    return order.status === 'DELIVERED' && (order.paymentStatus === 'PAID' || order.paymentStatus === 'CASH_COLLECTED');
  }

  isCashCollectionRequired(order: Order): boolean {
    return order.status === 'DELIVERED' && this.isCashOnDelivery(order) && order.paymentStatus === 'CASH_PENDING';
  }

  isCashOnDelivery(order: Order): boolean {
    return order.paymentMethod === 'CASH_ON_DELIVERY';
  }

  acceptOrder(order: Order): void {
    this.runAction(order.id, this.orderService.acceptRiderOrder(order.id));
  }

  markPickedUp(order: Order): void {
    this.runAction(order.id, this.orderService.markRiderOrderPickedUp(order.id));
  }

  markOutForDelivery(order: Order): void {
    this.runAction(order.id, this.orderService.markRiderOrderOutForDelivery(order.id));
  }

  markDelivered(order: Order): void {
    this.runAction(order.id, this.orderService.markRiderOrderDelivered(order.id));
  }

  markCashCollected(order: Order): void {
    this.runAction(order.id, this.orderService.markRiderOrderCashCollected(order.id));
  }

  completeOrder(order: Order): void {
    this.runAction(order.id, this.orderService.completeRiderOrder(order.id));
  }

  private runAction(orderId: number, operation: ReturnType<OrderService['acceptRiderOrder']>): void {
    this.actionOrderId = orderId;
    this.errorMsg = '';

    operation.subscribe({
      next: updatedOrder => {
        this.selectedOrder = updatedOrder;
        this.actionOrderId = null;
        this.loadOrders();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to update rider order.';
        this.actionOrderId = null;
        this.cdr.detectChanges();
      }
    });
  }
}