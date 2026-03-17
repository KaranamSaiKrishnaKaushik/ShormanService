import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgFor, NgIf, CurrencyPipe, AsyncPipe } from '@angular/common';
import { CartService } from '../../core/services/cart.service';
import { OrderService } from '../../core/services/order.service';
import { AddressService } from '../../core/services/address.service';
import { DeliveryService } from '../../core/services/delivery.service';
import { Address } from '../../core/models/address.model';
import { PaymentMethod } from '../../core/models/order.model';

interface PaymentOption {
  value: PaymentMethod;
  label: string;
  icon: string;
  desc: string;
}

const PAYMENT_OPTIONS: PaymentOption[] = [
  { value: 'PAYPAL', label: 'PayPal', icon: '💳', desc: 'Pay securely with PayPal' },
  { value: 'BANK_TRANSFER', label: 'Bank Transfer', icon: '🏦', desc: 'Transfer to our bank account' },
  { value: 'CASH_ON_DELIVERY', label: 'Cash on Delivery', icon: '💵', desc: 'Pay when your order arrives' },
];

@Component({
  selector: 'app-checkout',
  standalone: true,
    imports: [ReactiveFormsModule, FormsModule, NgFor, NgIf, CurrencyPipe, AsyncPipe, RouterLink],
  template: `
    <div class="checkout-page">
      <div class="checkout-container">
        <h1 class="page-title">Checkout</h1>

        <div class="checkout-layout">
          <!-- Left: Form -->
          <div class="checkout-form">

            <!-- 1. Delivery Address -->
            <div class="section-card">
              <h2 class="section-title">
                <span class="step-num">1</span>
                Delivery Address
              </h2>

              <div class="address-list" *ngIf="addresses.length > 0">
                <label *ngFor="let addr of addresses" class="address-option" [class.selected]="selectedAddressId === addr.id">
                  <input type="radio" name="address" [value]="addr.id" [(ngModel)]="selectedAddressId" />
                  <div class="addr-content">
                    <div class="addr-label">
                      {{ addr.label || 'Address' }}
                      <span class="default-tag" *ngIf="addr.isDefault">Default</span>
                    </div>
                    <div class="addr-text">{{ addr.street }} {{ addr.houseNumber }}, {{ addr.postalCode }} {{ addr.city }}</div>
                    <div class="delivery-check" *ngIf="deliveryChecks[addr.postalCode] as check">
                      <span [class.eligible]="check.eligible" [class.ineligible]="!check.eligible">
                        {{ check.eligible ? '✅ ' : '❌ ' }}{{ check.message }}
                        <span *ngIf="check.eligible"> (Fee: {{ check.deliveryFee | currency:'EUR' }})</span>
                      </span>
                    </div>
                    <button type="button" class="btn-check-delivery" *ngIf="!deliveryChecks[addr.postalCode]"
                      (click)="checkDelivery(addr.postalCode)">Check delivery</button>
                  </div>
                </label>
              </div>

              <div class="no-addresses" *ngIf="addresses.length === 0 && !loadingAddresses">
                <p>No addresses saved yet.</p>
              </div>

              <!-- Add new address inline -->
              <div class="add-address-toggle">
                <button type="button" class="btn-add-addr" (click)="showAddressForm = !showAddressForm">
                  {{ showAddressForm ? '✕ Cancel' : '+ Add New Address' }}
                </button>
              </div>

              <div class="address-form-inline" *ngIf="showAddressForm">
                <form [formGroup]="addressForm" (ngSubmit)="saveAddress()">
                  <div class="form-row-2">
                    <div class="form-group">
                      <label>Label (e.g. Home)</label>
                      <input type="text" formControlName="label" placeholder="Home" class="form-control" />
                    </div>
                    <div class="form-group">
                      <label>Postal Code *</label>
                      <input type="text" formControlName="postalCode" placeholder="10115" class="form-control" />
                    </div>
                  </div>
                  <div class="form-row-2">
                    <div class="form-group">
                      <label>Street *</label>
                      <input type="text" formControlName="street" placeholder="Main Street" class="form-control" />
                    </div>
                    <div class="form-group">
                      <label>House Number *</label>
                      <input type="text" formControlName="houseNumber" placeholder="12A" class="form-control" />
                    </div>
                  </div>
                  <div class="form-row-2">
                    <div class="form-group">
                      <label>City *</label>
                      <input type="text" formControlName="city" placeholder="Berlin" class="form-control" />
                    </div>
                    <div class="form-group">
                      <label>Country *</label>
                      <input type="text" formControlName="country" placeholder="Germany" class="form-control" />
                    </div>
                  </div>
                  <button type="submit" class="btn-save-addr" [disabled]="savingAddress">
                    {{ savingAddress ? 'Saving…' : 'Save Address' }}
                  </button>
                </form>
              </div>
            </div>

            <!-- 2. Payment Method -->
            <div class="section-card">
              <h2 class="section-title">
                <span class="step-num">2</span>
                Payment Method
              </h2>

              <div class="payment-options">
                <label *ngFor="let opt of paymentOptions" class="payment-option" [class.selected]="selectedPayment === opt.value">
                  <input type="radio" name="payment" [value]="opt.value" [(ngModel)]="selectedPayment" />
                  <span class="pay-icon">{{ opt.icon }}</span>
                  <div class="pay-details">
                    <div class="pay-label">{{ opt.label }}</div>
                    <div class="pay-desc">{{ opt.desc }}</div>
                  </div>
                  <span class="pay-check" *ngIf="selectedPayment === opt.value">✓</span>
                </label>
              </div>
            </div>
          </div>

          <!-- Right: Order Summary -->
          <div class="order-summary">
            <div class="summary-card">
              <h2 class="section-title" style="margin-bottom:1rem">Order Summary</h2>

              <ng-container *ngIf="cartService.cart$ | async as cart">
                <div class="summary-items">
                  <div *ngFor="let item of cart.items" class="summary-item">
                    <span class="sum-name">{{ item.product.name }} × {{ item.quantity }}</span>
                    <span class="sum-price">{{ item.product.price * item.quantity | currency:'EUR':'symbol':'1.2-2' }}</span>
                  </div>
                </div>

                <div class="summary-divider"></div>
                <div class="summary-row">
                  <span>Subtotal</span>
                  <span>{{ cart.total | currency:'EUR':'symbol':'1.2-2' }}</span>
                </div>
                <div class="summary-row">
                  <span>Delivery Fee</span>
                  <span>{{ deliveryFee | currency:'EUR':'symbol':'1.2-2' }}</span>
                </div>
                <div class="summary-divider"></div>
                <div class="summary-row total">
                  <strong>Total</strong>
                  <strong>{{ cart.total + deliveryFee | currency:'EUR':'symbol':'1.2-2' }}</strong>
                </div>

                <div class="error-msg" *ngIf="orderError">{{ orderError }}</div>
                <div class="success-msg" *ngIf="orderSuccess">{{ orderSuccess }}</div>

                <button class="btn-place-order"
                  [disabled]="placing || cart.items.length === 0 || !selectedAddressId"
                  (click)="placeOrder(cart)">
                  <span *ngIf="placing" class="btn-spinner"></span>
                  {{ placing ? 'Placing Order…' : 'Place Order' }}
                </button>

                <p class="secure-note">🔒 Your payment is secured</p>
              </ng-container>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .checkout-page {
      background: #F9FAFB;
      min-height: calc(100vh - 128px);
      padding: 2rem 0;
    }
    .checkout-container {
      max-width: 1100px;
      margin: 0 auto;
      padding: 0 1rem;
    }
    .page-title {
      font-size: 1.75rem;
      font-weight: 800;
      color: #1a1a1a;
      margin: 0 0 1.5rem;
    }
    .checkout-layout {
      display: grid;
      grid-template-columns: 1fr 360px;
      gap: 1.5rem;
      align-items: flex-start;
    }

    .section-card {
      background: #fff;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.07);
      margin-bottom: 1.25rem;
    }
    .section-title {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      font-size: 1.1rem;
      font-weight: 700;
      color: #1a1a1a;
      margin: 0 0 1.25rem;
    }
    .step-num {
      width: 28px;
      height: 28px;
      background: #2E7D32;
      color: #fff;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.85rem;
      font-weight: 800;
      flex-shrink: 0;
    }

    /* Address */
    .address-list { display: flex; flex-direction: column; gap: 0.75rem; margin-bottom: 1rem; }
    .address-option {
      display: flex;
      gap: 0.75rem;
      padding: 1rem;
      border: 2px solid #E5E7EB;
      border-radius: 10px;
      cursor: pointer;
      transition: border-color 0.2s;
      align-items: flex-start;
    }
    .address-option input[type="radio"] { margin-top: 4px; accent-color: #2E7D32; }
    .address-option.selected { border-color: #2E7D32; background: #F0FDF4; }
    .addr-content { flex: 1; }
    .addr-label { font-weight: 700; font-size: 0.9rem; color: #1a1a1a; margin-bottom: 0.3rem; display: flex; gap: 0.5rem; align-items: center; }
    .default-tag { background: #E8F5E9; color: #2E7D32; font-size: 0.72rem; padding: 0.1rem 0.5rem; border-radius: 20px; font-weight: 600; }
    .addr-text { font-size: 0.88rem; color: #555; }
    .delivery-check { font-size: 0.8rem; margin-top: 0.4rem; }
    .eligible { color: #2E7D32; }
    .ineligible { color: #DC2626; }
    .btn-check-delivery {
      margin-top: 0.4rem;
      background: none;
      border: 1px solid #ddd;
      padding: 0.25rem 0.75rem;
      border-radius: 5px;
      font-size: 0.78rem;
      cursor: pointer;
      color: #2E7D32;
    }
    .btn-check-delivery:hover { background: #E8F5E9; }
    .no-addresses { color: #9CA3AF; font-size: 0.9rem; margin-bottom: 1rem; }
    .add-address-toggle { margin-bottom: 1rem; }
    .btn-add-addr {
      background: none;
      border: 1.5px dashed #2E7D32;
      color: #2E7D32;
      padding: 0.5rem 1rem;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 600;
      font-size: 0.88rem;
      transition: all 0.2s;
    }
    .btn-add-addr:hover { background: #E8F5E9; }

    .address-form-inline {
      background: #F9FAFB;
      border: 1px solid #eee;
      border-radius: 10px;
      padding: 1.25rem;
      margin-top: 0.5rem;
    }
    .form-row-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
    .form-group { margin-bottom: 1rem; }
    .form-group label { display: block; font-size: 0.85rem; font-weight: 600; color: #555; margin-bottom: 0.35rem; }
    .form-control {
      width: 100%;
      padding: 0.6rem 0.8rem;
      border: 1.5px solid #ddd;
      border-radius: 8px;
      font-size: 0.9rem;
      box-sizing: border-box;
    }
    .form-control:focus { outline: none; border-color: #2E7D32; }
    .btn-save-addr {
      background: #2E7D32;
      color: #fff;
      border: none;
      padding: 0.6rem 1.5rem;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-save-addr:disabled { opacity: 0.6; }

    /* Payment */
    .payment-options { display: flex; flex-direction: column; gap: 0.75rem; }
    .payment-option {
      display: flex;
      align-items: center;
      gap: 0.85rem;
      padding: 1rem;
      border: 2px solid #E5E7EB;
      border-radius: 10px;
      cursor: pointer;
      transition: border-color 0.2s;
    }
    .payment-option input[type="radio"] { display: none; }
    .payment-option.selected { border-color: #2E7D32; background: #F0FDF4; }
    .pay-icon { font-size: 1.5rem; width: 32px; text-align: center; }
    .pay-details { flex: 1; }
    .pay-label { font-weight: 700; font-size: 0.95rem; color: #1a1a1a; }
    .pay-desc { font-size: 0.8rem; color: #9CA3AF; }
    .pay-check { color: #2E7D32; font-weight: 800; font-size: 1.1rem; }

    /* Summary */
    .summary-card {
      background: #fff;
      border-radius: 12px;
      padding: 1.5rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.07);
      position: sticky;
      top: 140px;
    }
    .summary-items { display: flex; flex-direction: column; gap: 0.5rem; margin-bottom: 1rem; }
    .summary-item { display: flex; justify-content: space-between; font-size: 0.88rem; color: #555; }
    .sum-name { flex: 1; margin-right: 0.5rem; }
    .sum-price { font-weight: 600; color: #1a1a1a; white-space: nowrap; }
    .summary-divider { border: none; border-top: 1px solid #eee; margin: 0.75rem 0; }
    .summary-row { display: flex; justify-content: space-between; font-size: 0.95rem; color: #555; margin-bottom: 0.5rem; }
    .summary-row.total { font-size: 1.1rem; color: #1a1a1a; margin-top: 0.5rem; }
    .summary-row.total strong { color: #2E7D32; font-size: 1.2rem; }

    .error-msg { background: #FFF3F3; color: #DC2626; padding: 0.75rem; border-radius: 8px; font-size: 0.88rem; margin: 1rem 0; }
    .success-msg { background: #F0FDF4; color: #2E7D32; padding: 0.75rem; border-radius: 8px; font-size: 0.88rem; margin: 1rem 0; }

    .btn-place-order {
      width: 100%;
      background: #FF6F00;
      color: #fff;
      border: none;
      padding: 0.9rem;
      border-radius: 10px;
      font-size: 1rem;
      font-weight: 800;
      cursor: pointer;
      transition: background 0.2s;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      margin-top: 1rem;
    }
    .btn-place-order:hover:not(:disabled) { background: #E65100; }
    .btn-place-order:disabled { opacity: 0.5; cursor: not-allowed; }
    .btn-spinner {
      width: 16px; height: 16px;
      border: 2px solid rgba(255,255,255,0.4);
      border-top-color: #fff;
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .secure-note { text-align: center; font-size: 0.78rem; color: #9CA3AF; margin-top: 0.5rem; }

    @media (max-width: 900px) {
      .checkout-layout { grid-template-columns: 1fr; }
      .summary-card { position: static; }
    }
    @media (max-width: 500px) {
      .form-row-2 { grid-template-columns: 1fr; }
    }
  `]
})
export class CheckoutComponent implements OnInit {
  private fb = inject(FormBuilder);
  cartService = inject(CartService);
  private orderService = inject(OrderService);
  private addressService = inject(AddressService);
  private deliveryService = inject(DeliveryService);
  private router = inject(Router);

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
        this.addresses = addrs;
        this.loadingAddresses = false;
        const def = addrs.find(a => a.isDefault);
        if (def) this.selectedAddressId = def.id;
        else if (addrs.length > 0) this.selectedAddressId = addrs[0].id;
      },
      error: () => { this.loadingAddresses = false; }
    });
  }

  checkDelivery(postalCode: string): void {
    this.deliveryService.checkDelivery(postalCode).subscribe(result => {
      this.deliveryChecks = { ...this.deliveryChecks, [postalCode]: result };
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
        this.addresses.push(addr);
        this.selectedAddressId = addr.id;
        this.showAddressForm = false;
        this.savingAddress = false;
        this.addressForm.reset({ country: 'Germany' });
      },
      error: () => { this.savingAddress = false; }
    });
  }

  placeOrder(cart: { items: { productId: number; quantity: number }[]; total: number }): void {
    if (!this.selectedAddressId || cart.items.length === 0) return;
    this.placing = true;
    this.orderError = '';
    this.orderService.createOrder({
      addressId: this.selectedAddressId,
      paymentMethod: this.selectedPayment,
      items: cart.items.map(i => ({ productId: i.productId, quantity: i.quantity }))
    }).subscribe({
      next: (order) => {
        this.cartService.clearCart();
        this.placing = false;
        this.router.navigate(['/orders']);
      },
      error: (err) => {
        this.orderError = err?.error?.message || 'Failed to place order. Please try again.';
        this.placing = false;
      }
    });
  }
}
