import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, Subscription, catchError, interval, map, of, startWith, switchMap } from 'rxjs';
import { AuthService } from './auth.service';
import { MenuPermissionService } from './menu-permission.service';
import { OrderService } from './order.service';
import { UserManagementService } from './user-management.service';
import { Order } from '../models/order.model';

export interface OrderAlertNotification {
  count: number;
  message: string;
  route: string;
}

@Injectable({ providedIn: 'root' })
export class OrderAlertService {
  private static readonly POLL_INTERVAL_MS = 5000;

  private readonly auth = inject(AuthService);
  private readonly menuPermissions = inject(MenuPermissionService);
  private readonly orderService = inject(OrderService);
  private readonly userManagementService = inject(UserManagementService);
  private readonly notificationSubject = new BehaviorSubject<OrderAlertNotification | null>(null);

  readonly notification$ = this.notificationSubject.asObservable();

  get currentNotification(): OrderAlertNotification | null {
    return this.notificationSubject.value;
  }

  private pollingSubscription: Subscription | null = null;
  private previousCount: number | null = null;

  constructor() {
    this.auth.currentUser$.subscribe(() => {
      void this.resetPolling();
    });
  }

  private async resetPolling(): Promise<void> {
    this.pollingSubscription?.unsubscribe();
    this.pollingSubscription = null;
    this.previousCount = null;
    this.notificationSubject.next(null);

    await this.auth.ensureReady();

    if (!this.auth.isLoggedIn) {
      return;
    }

    await this.menuPermissions.ensureLoaded();

    if (this.auth.hasRole('Rider') && this.menuPermissions.hasPermission('rider-dashboard')) {
      this.startPolling(
        () => this.orderService.getAvailableRiderOrders().pipe(map(orders => this.countAwaitingPickup(orders))),
        (count, hasIncrease) => ({
          count,
          route: '/rider',
          message: hasIncrease
            ? count === 1
              ? 'New order in the rider queue. Open Rider Dashboard.'
              : `${count} orders are now waiting in the rider queue.`
            : count === 1
              ? '1 order is waiting in the rider queue.'
              : `${count} orders are waiting in the rider queue.`
        })
      );
      return;
    }

    if ((this.auth.hasRole('Admin') || this.auth.hasRole('SuperAdmin')) && this.menuPermissions.hasPermission('user-management.order-summary')) {
      this.startPolling(
        () => this.userManagementService.getAwaitingPickupCount(),
        (count, hasIncrease) => ({
          count,
          route: '/user-management/order-summary',
          message: hasIncrease
            ? count === 1
              ? 'New order placed. Review Order History.'
              : `${count} orders are waiting for rider assignment.`
            : count === 1
              ? '1 order is waiting for rider assignment.'
              : `${count} orders are waiting for rider assignment.`
        })
      );
    }
  }

  private startPolling(
    getCount: () => Observable<number>,
    buildNotification: (count: number, hasIncrease: boolean) => OrderAlertNotification
  ): void {
    this.pollingSubscription = interval(OrderAlertService.POLL_INTERVAL_MS)
      .pipe(
        startWith(0),
        switchMap(() => getCount().pipe(catchError(() => of(0))))
      )
      .subscribe(count => {
        const hasIncrease = this.previousCount !== null && count > this.previousCount;
        this.previousCount = count;
        this.notificationSubject.next(count > 0 ? buildNotification(count, hasIncrease) : null);
      });
  }

  private countAwaitingPickup(orders: Order[]): number {
    return orders.filter(order => order.status === 'AWAITING_PICKUP').length;
  }
}