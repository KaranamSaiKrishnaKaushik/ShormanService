import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NgFor, NgIf, CurrencyPipe, AsyncPipe } from '@angular/common';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '../../core/services/order.service';
import { AddressService } from '../../core/services/address.service';
import { DeliveryService } from '../../core/services/delivery.service';
import { Address } from '../../core/models/address.model';
import { CheckoutSessionResponse, Order, PaymentMethod } from '../../core/models/order.model';
import { TranslateModule } from '@ngx-translate/core';

interface PaymentOption {
  value: PaymentMethod;
  labelKey: string;
  icon: string;
  descKey: string;
}

const PAYMENT_OPTIONS: PaymentOption[] = [
  { value: 'STRIPE_CARD', labelKey: 'checkout.payment.stripeCardLabel', icon: '💳', descKey: 'checkout.payment.stripeCardDesc' },
  { value: 'STRIPE_SEPA_DEBIT', labelKey: 'checkout.payment.stripeSepaLabel', icon: '🏦', descKey: 'checkout.payment.stripeSepaDesc' },
  { value: 'STRIPE_KLARNA', labelKey: 'checkout.payment.stripeKlarnaLabel', icon: '🧾', descKey: 'checkout.payment.stripeKlarnaDesc' },
  { value: 'STRIPE_PAYPAL', labelKey: 'checkout.payment.paypalLabel', icon: '🅿️', descKey: 'checkout.payment.paypalDesc' },
  { value: 'CASH_ON_DELIVERY', labelKey: 'checkout.payment.codLabel', icon: '💵', descKey: 'checkout.payment.codDesc' },
];

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, NgFor, NgIf, CurrencyPipe, AsyncPipe, TranslateModule],
  templateUrl: './checkout.component.html',
  styleUrls: ['./checkout.component.scss']
})
export class CheckoutComponent implements OnInit {
  private static readonly PLACE_ORDER_TIMEOUT_MS = 20000;

  private fb = inject(FormBuilder);
  cartService = inject(CartService);
  private orderService = inject(OrderService);
  private addressService = inject(AddressService);
  private deliveryService = inject(DeliveryService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);

  paymentOptions = PAYMENT_OPTIONS;
  addresses: Address[] = [];
  selectedAddressId: number | null = null;
  selectedPayment: PaymentMethod = 'CASH_ON_DELIVERY';
  deliveryChecks: Record<string, { eligible: boolean; message?: string; deliveryFee?: number }> = {};
  deliveryFee = 2.99;
  loadingAddresses = true;
  showAddressForm = false;
  savingAddress = false;
  placing = false;
  orderError = '';
  orderSuccess = '';
  private placeOrderWatchdogId: number | null = null;

  addressForm = this.fb.nonNullable.group({
    label: [''],
    street: ['', Validators.required],
    houseNumber: ['', Validators.required],
    postalCode: ['', Validators.required],
    city: ['', Validators.required],
    country: ['Germany'],
    isDefault: [false]
  });

  ngOnInit(): void {
    this.handleCheckoutReturn();

    this.addressService.getAddresses().subscribe({
      next: addrs => {
        this.addresses = [...addrs];
        addrs.forEach(addr => this.checkDelivery(addr));
        const def = addrs.find(a => a.isDefault);
        if (def) this.selectedAddressId = def.id;
        else if (addrs.length > 0) this.selectedAddressId = addrs[0].id;
        this.loadingAddresses = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadingAddresses = false;
        this.cdr.detectChanges();
      }
    });
  }

  checkDelivery(address: Address): void {
    const key = this.deliveryCheckKey(address.id);
    this.deliveryService.checkDelivery({
      postalCode: address.postalCode,
      city: address.city,
      street: address.street,
      houseNumber: address.houseNumber,
      country: address.country
    }).subscribe(result => {
      this.deliveryChecks = { ...this.deliveryChecks, [key]: result };
      if (result.eligible && result.deliveryFee !== undefined) {
        this.deliveryFee = result.deliveryFee;
      }
    });
  }

  saveAddress(): void {
    if (this.addressForm.invalid) return;
    this.savingAddress = true;
    this.addressService.addAddress(this.addressForm.getRawValue()).subscribe({
      next: (addr) => {
        this.addresses = [...this.addresses, addr];
        this.selectedAddressId = addr.id;
        this.checkDelivery(addr);
        this.showAddressForm = false;
        this.savingAddress = false;
        this.addressForm.reset({ country: 'Germany' });
        this.cdr.detectChanges();
      },
      error: () => {
        this.savingAddress = false;
        this.cdr.detectChanges();
      }
    });
  }

  placeOrder(cart: { items: { productId: number; quantity: number }[]; total: number }): void {
    if (!this.selectedAddressId || cart.items.length === 0) return;

    const request = {
      addressId: this.selectedAddressId,
      paymentMethod: this.selectedPayment,
      items: cart.items.map(i => ({ productId: i.productId, quantity: i.quantity }))
    };

    this.clearPlaceOrderWatchdog();
    this.placing = true;
    this.orderError = '';
    this.orderSuccess = '';
    this.placeOrderWatchdogId = window.setTimeout(() => {
      if (!this.placing) {
        return;
      }

      this.placing = false;
      this.orderError = 'Placing the order is taking too long. Check whether the backend is reachable, then try again.';
    }, CheckoutComponent.PLACE_ORDER_TIMEOUT_MS);

    if (this.isStripeManagedPayment(this.selectedPayment)) {
      this.orderService.createCheckoutSession(request).subscribe({
        next: (checkout: CheckoutSessionResponse) => {
          this.clearPlaceOrderWatchdog();
          window.location.assign(checkout.checkoutUrl);
        },
        error: (err: unknown) => {
          this.handlePlaceOrderError(err);
        }
      });

      return;
    }

    this.orderService.createOrder(request).subscribe({
      next: (_order: Order) => {
        this.clearPlaceOrderWatchdog();
        this.cartService.clearCart();
        this.placing = false;
        this.router.navigate(['/orders']);
      },
      error: (err: unknown) => {
        this.handlePlaceOrderError(err);
      }
    });
  }

  deliveryCheckKey(addressId: number): string {
    return `address-${addressId}`;
  }

  selectedAddressEligible(): boolean {
    if (!this.selectedAddressId) {
      return false;
    }

    const check = this.deliveryChecks[this.deliveryCheckKey(this.selectedAddressId)];
    return check?.eligible !== false;
  }

  private clearPlaceOrderWatchdog(): void {
    if (this.placeOrderWatchdogId !== null) {
      window.clearTimeout(this.placeOrderWatchdogId);
      this.placeOrderWatchdogId = null;
    }
  }

  submitLabelKey(): string {
    return this.isStripeManagedPayment(this.selectedPayment)
      ? 'checkout.continueToPayment'
      : 'checkout.placeOrder';
  }

  private handleCheckoutReturn(): void {
    this.route.queryParamMap.subscribe(params => {
      const payment = params.get('payment');
      const orderIdParam = params.get('orderId');
      const sessionId = params.get('session_id');

      if (payment === 'success') {
        const orderId = Number(orderIdParam);
        if (!Number.isNaN(orderId) && sessionId) {
          this.orderService.confirmStripePayment(orderId, sessionId).subscribe({
            next: () => {
              this.cartService.clearCart();
              this.router.navigate(['/orders'], { replaceUrl: true });
            },
            error: () => {
              this.cartService.clearCart();
              this.router.navigate(['/orders'], { replaceUrl: true });
            }
          });
          return;
        }

        this.cartService.clearCart();
        this.router.navigate(['/orders'], { replaceUrl: true });
        return;
      }

      if (payment === 'cancelled' && orderIdParam) {
        const orderId = Number(orderIdParam);
        if (!Number.isNaN(orderId)) {
          this.orderService.cancelPendingPayment(orderId).subscribe({
            next: () => {
              this.orderError = 'Payment was cancelled. No charge was captured.';
              this.router.navigate([], {
                relativeTo: this.route,
                queryParams: {},
                replaceUrl: true
              });
            },
            error: () => {
              this.orderError = 'Payment was cancelled. Please try again.';
            }
          });
        }
      }
    });
  }

  private isStripeManagedPayment(paymentMethod: PaymentMethod): boolean {
    return paymentMethod === 'STRIPE_CARD'
      || paymentMethod === 'STRIPE_SEPA_DEBIT'
      || paymentMethod === 'STRIPE_KLARNA'
      || paymentMethod === 'STRIPE_PAYPAL'
      || paymentMethod === 'STRIPE_PAYPAL_GERMANY';
  }

  private handlePlaceOrderError(err: unknown): void {
    const error = err as { name?: string; error?: { message?: string } };
    this.clearPlaceOrderWatchdog();
    this.orderError = error?.name === 'TimeoutError'
      ? 'Placing the order took too long. Check whether the backend is still starting or unavailable, then try again.'
      : error?.error?.message || 'Failed to place order. Please try again.';
    this.placing = false;
  }
}
