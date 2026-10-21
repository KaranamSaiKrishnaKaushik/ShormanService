import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf, NgClass, CurrencyPipe, DatePipe, SlicePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrderService } from '../../core/services/order.service';
import { Order, OrderSortDirection } from '../../core/models/order.model';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [NgFor, NgIf, NgClass, CurrencyPipe, DatePipe, SlicePipe, FormsModule],
  templateUrl: './orders.component.html',
  styleUrls: ['./orders.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OrdersComponent implements OnInit {
  private orderService = inject(OrderService);
  private auth = inject(AuthService);
  private cdr = inject(ChangeDetectorRef);

  orders: Order[] = [];
  selectedOrder: Order | null = null;
  loading = true;
  errorMsg = '';
  readonly pageSize = 20;
  totalCount = 0;
  currentPage = 1;
  searchTerm = '';
  sortDirection: OrderSortDirection = 'desc';

  get selectedOrderStoreGroups(): Array<{ storeName: string; items: Order['items'] }> {
    if (!this.selectedOrder) {
      return [];
    }

    const storeMap = new Map<string, Order['items']>();

    for (const item of this.selectedOrder.items) {
      const storeName = item.supermarketName?.trim() || 'Store not specified';
      const storeItems = storeMap.get(storeName) ?? [];
      storeItems.push(item);
      storeMap.set(storeName, storeItems);
    }

    return Array.from(storeMap.entries()).map(([storeName, items]) => ({ storeName, items }));
  }

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

  get hasSelectedOrder(): boolean {
    return this.selectedOrder !== null;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  get visiblePages(): number[] {
    const start = Math.max(1, this.currentPage - 2);
    const end = Math.min(this.totalPages, start + 4);
    const pages: number[] = [];

    for (let page = Math.max(1, end - 4); page <= end; page++) {
      pages.push(page);
    }

    return pages;
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

  toggleOrderSelection(order: Order): void {
    this.selectedOrder = this.selectedOrder?.id === order.id ? null : order;
  }

  closeOrderSelection(): void {
    this.selectedOrder = null;
    this.cdr.detectChanges();
  }

  applySearch(): void {
    this.currentPage = 1;
    this.closeOrderSelection();
    this.loadOrders();
  }

  clearSearch(): void {
    if (!this.searchTerm) {
      return;
    }

    this.searchTerm = '';
    this.currentPage = 1;
    this.closeOrderSelection();
    this.loadOrders();
  }

  onSortChange(): void {
    this.currentPage = 1;
    this.closeOrderSelection();
    this.loadOrders();
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }

    this.currentPage = page;
    this.closeOrderSelection();
    this.loadOrders();
  }

  isSelected(order: Order): boolean {
    return this.selectedOrder?.id === order.id;
  }

  itemSubtotal(items: Order['items']): number {
    return items.reduce((sum, item) => sum + item.totalPrice, 0);
  }

  trackByOrderId(_: number, order: Order): number {
    return order.id;
  }

  trackByStoreName(_: number, group: { storeName: string }): string {
    return group.storeName;
  }

  trackByOrderItem(_: number, item: Order['items'][number]): string {
    return `${item.id}-${item.productId}-${item.supermarketName ?? 'na'}`;
  }

  private filterAndSortOrders(orders: Order[]): Order[] {
    const search = this.searchTerm.trim().toLowerCase();

    return [...orders]
      .filter(order => {
        if (!search) {
          return true;
        }

        return order.id.toString() === search
          || order.items.some(item => item.productName.toLowerCase().includes(search));
      })
      .sort((left, right) => {
        const leftTime = new Date(left.createdAt).getTime();
        const rightTime = new Date(right.createdAt).getTime();
        return this.sortDirection === 'asc' ? leftTime - rightTime : rightTime - leftTime;
      });
  }

  loadOrders(): void {
    this.loading = true;
    this.errorMsg = '';

    if (this.isRider) {
      this.orderService.getMyRiderOrders().subscribe({
        next: orders => {
          const filteredOrders = this.filterAndSortOrders(Array.isArray(orders) ? orders : []);
          this.totalCount = filteredOrders.length;
          this.orders = filteredOrders.slice((this.currentPage - 1) * this.pageSize, this.currentPage * this.pageSize);
          this.selectedOrder = this.selectedOrder
            ? this.orders.find(order => order.id === this.selectedOrder?.id) ?? null
            : null;
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: err => {
          this.errorMsg = err?.error?.message || 'Failed to load orders. Please check your connection and try again.';
          this.loading = false;
          this.cdr.detectChanges();
        }
      });

      return;
    }

    this.orderService.getOrders({
      page: this.currentPage,
      pageSize: this.pageSize,
      search: this.searchTerm,
      sort: this.sortDirection
    }).subscribe({
      next: response => {
        this.orders = Array.isArray(response.items) ? response.items : [];
        this.totalCount = response.totalCount ?? 0;
        this.currentPage = response.page ?? this.currentPage;
        this.selectedOrder = this.selectedOrder
          ? this.orders.find(order => order.id === this.selectedOrder?.id) ?? null
          : null;
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
