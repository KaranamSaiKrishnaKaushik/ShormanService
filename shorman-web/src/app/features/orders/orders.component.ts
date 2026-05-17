import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf, NgClass, CurrencyPipe, DatePipe } from '@angular/common';
import { OrderService } from '../../core/services/order.service';
import { Order } from '../../core/models/order.model';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [NgFor, NgIf, NgClass, CurrencyPipe, DatePipe],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss']
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  private auth = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  orders: Order[] = [];
  loading = true;
  errorMsg = '';

  get isRider(): boolean {
    return this.auth.hasRole('Rider');
  }

  get pageTitle(): string {
    return this.isRider ? 'Rider Order History' : 'Order History';
  }

  get emptyTitle(): string {
    return this.isRider ? 'No rider orders yet' : 'No orders yet';
  }

  get emptyMessage(): string {
    return this.isRider
      ? 'Accepted and completed rider deliveries will appear here.'
      : 'Your order history will appear here.';
  }

  get loadingMessage(): string {
    return this.isRider ? 'Loading your rider orders...' : 'Loading your orders...';
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

  formatPaymentStatus(paymentStatus: string): string {
    return paymentStatus.replaceAll('_', ' ');
  }

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {
    this.loading = true;
    this.errorMsg = '';

    const request$ = this.isRider
      ? this.orderService.getMyRiderOrders()
      : this.orderService.getOrders();

    request$.subscribe({
      next: orders => {
        this.orders = Array.isArray(orders) ? orders : [];
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load orders. Please check your connection and try again.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
