import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgFor, NgIf, CurrencyPipe, AsyncPipe } from '@angular/common';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '../../core/services/order.service';
import { AddressService } from '../../core/services/address.service';
import { DeliveryService } from '../../core/services/delivery.service';
import { Address } from '../../core/models/address.model';
import { PaymentMethod } from '../../core/models/order.model';
import { TranslateModule } from '@ngx-translate/core';

interface PaymentOption {
  value: PaymentMethod;
  labelKey: string;
  icon: string;
  descKey: string;
}

const PAYMENT_OPTIONS: PaymentOption[] = [
  { value: 'PAYPAL', labelKey: 'checkout.payment.paypalLabel', icon: '💳', descKey: 'checkout.payment.paypalDesc' },
  { value: 'BANK_TRANSFER', labelKey: 'checkout.payment.bankTransferLabel', icon: '🏦', descKey: 'checkout.payment.bankTransferDesc' },
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

    this.clearPlaceOrderWatchdog();
    this.placing = true;
    this.orderError = '';
    this.placeOrderWatchdogId = window.setTimeout(() => {
      if (!this.placing) {
        return;
      }

      this.placing = false;
      this.orderError = 'Placing the order is taking too long. Check whether the backend is reachable, then try again.';
    }, CheckoutComponent.PLACE_ORDER_TIMEOUT_MS);

    this.orderService.createOrder({
      addressId: this.selectedAddressId,
      paymentMethod: this.selectedPayment,
      items: cart.items.map(i => ({ productId: i.productId, quantity: i.quantity }))
    }).subscribe({
      next: (order) => {
        this.clearPlaceOrderWatchdog();
        this.cartService.clearCart();
        this.placing = false;
        this.router.navigate(['/orders']);
      },
      error: (err) => {
        this.clearPlaceOrderWatchdog();
        this.orderError = err?.name === 'TimeoutError'
          ? 'Placing the order took too long. Check whether the backend is still starting or unavailable, then try again.'
          : err?.error?.message || 'Failed to place order. Please try again.';
        this.placing = false;
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
}
