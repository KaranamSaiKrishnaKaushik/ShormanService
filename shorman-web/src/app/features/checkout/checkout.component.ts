import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
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
  imports: [ReactiveFormsModule, FormsModule, NgFor, NgIf, CurrencyPipe, AsyncPipe],
  templateUrl: './checkout.component.html',
  styleUrls: ['./checkout.component.scss']
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
