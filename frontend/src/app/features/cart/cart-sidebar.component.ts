import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AsyncPipe, NgFor, NgIf, CurrencyPipe } from '@angular/common';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { CartItem } from '../../core/models/cart.model';

@Component({
  selector: 'app-cart-sidebar',
  standalone: true,
  imports: [AsyncPipe, NgFor, NgIf, CurrencyPipe, RouterLink],
  template: `
    <ng-container *ngIf="cartService.isOpen$ | async">
      <!-- Overlay -->
      <div class="overlay" (click)="cartService.closeCart()"></div>

      <!-- Sidebar -->
      <aside class="cart-sidebar">
        <div class="cart-header">
          <h2>Your Cart</h2>
          <span class="item-count" *ngIf="(cartService.cart$ | async)?.itemCount as cnt">
            {{ cnt }} item{{ cnt !== 1 ? 's' : '' }}
          </span>
          <button class="close-btn" (click)="cartService.closeCart()">✕</button>
        </div>

        <ng-container *ngIf="cartService.cart$ | async as cart">
          <div class="cart-empty" *ngIf="cart.items.length === 0">
            <div class="empty-icon">🛒</div>
            <p>Your cart is empty</p>
            <a routerLink="/products" class="btn-shop" (click)="cartService.closeCart()">Start Shopping</a>
          </div>

          <div class="cart-items" *ngIf="cart.items.length > 0">
            <div *ngFor="let item of cart.items; trackBy: trackItem" class="cart-item">
              <div class="item-image">
                <img *ngIf="item.product.imageUrl" [src]="item.product.imageUrl" [alt]="item.product.name" />
                <div *ngIf="!item.product.imageUrl" class="item-placeholder">
                  {{ item.product.name.charAt(0) }}
                </div>
              </div>

              <div class="item-details">
                <div class="item-name">{{ item.product.name }}</div>
                <div class="item-sm">{{ item.product.supermarket?.name }}</div>
                <div class="item-price">{{ item.product.price | currency:'EUR':'symbol':'1.2-2' }}</div>
              </div>

              <div class="item-controls">
                <div class="qty-row">
                  <button class="qty-btn minus" (click)="decrease(item)">−</button>
                  <span class="qty">{{ item.quantity }}</span>
                  <button class="qty-btn plus" (click)="increase(item)">+</button>
                </div>
                <div class="item-total">{{ item.product.price * item.quantity | currency:'EUR':'symbol':'1.2-2' }}</div>
                <button class="remove-btn" (click)="remove(item)" title="Remove">🗑️</button>
              </div>
            </div>
          </div>

          <div class="cart-footer" *ngIf="cart.items.length > 0">
            <div class="subtotal">
              <span>Subtotal</span>
              <strong>{{ cart.total | currency:'EUR':'symbol':'1.2-2' }}</strong>
            </div>
            <div class="delivery-note">🚚 Delivery fee calculated at checkout</div>

            <button class="btn-checkout" (click)="goToCheckout()">
              Proceed to Checkout →
            </button>
            <button class="btn-clear" (click)="cartService.clearCart()">Clear Cart</button>
          </div>
        </ng-container>
      </aside>
    </ng-container>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.45);
      z-index: 300;
      animation: fadeIn 0.2s ease;
    }
    @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }

    .cart-sidebar {
      position: fixed;
      top: 0;
      right: 0;
      bottom: 0;
      width: 400px;
      max-width: 100vw;
      background: #fff;
      z-index: 400;
      display: flex;
      flex-direction: column;
      box-shadow: -4px 0 24px rgba(0,0,0,0.15);
      animation: slideIn 0.25s ease;
    }
    @keyframes slideIn {
      from { transform: translateX(100%); }
      to { transform: translateX(0); }
    }

    .cart-header {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      padding: 1.25rem 1.25rem 1rem;
      border-bottom: 1px solid #eee;
      flex-shrink: 0;
    }
    .cart-header h2 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 800;
      color: #1a1a1a;
      flex: 1;
    }
    .item-count {
      background: #E8F5E9;
      color: #2E7D32;
      font-size: 0.78rem;
      font-weight: 700;
      padding: 0.2rem 0.6rem;
      border-radius: 20px;
    }
    .close-btn {
      background: none;
      border: none;
      font-size: 1.1rem;
      cursor: pointer;
      color: #999;
      padding: 0.25rem;
      line-height: 1;
    }
    .close-btn:hover { color: #333; }

    .cart-empty {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 2rem;
      text-align: center;
    }
    .empty-icon { font-size: 4rem; margin-bottom: 1rem; opacity: 0.6; }
    .cart-empty p { color: #999; margin: 0 0 1.5rem; font-size: 1rem; }
    .btn-shop {
      background: #2E7D32;
      color: #fff;
      text-decoration: none;
      padding: 0.65rem 1.5rem;
      border-radius: 8px;
      font-weight: 700;
      transition: background 0.2s;
    }
    .btn-shop:hover { background: #1B5E20; }

    .cart-items {
      flex: 1;
      overflow-y: auto;
      padding: 0.75rem 1.25rem;
    }

    .cart-item {
      display: flex;
      gap: 0.75rem;
      padding: 0.85rem 0;
      border-bottom: 1px solid #F3F4F6;
      align-items: flex-start;
    }
    .cart-item:last-child { border-bottom: none; }

    .item-image {
      width: 52px;
      height: 52px;
      border-radius: 8px;
      overflow: hidden;
      flex-shrink: 0;
      background: #F9FAFB;
    }
    .item-image img { width: 100%; height: 100%; object-fit: cover; }
    .item-placeholder {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      background: linear-gradient(135deg, #2E7D32, #4CAF50);
      color: #fff;
      font-weight: 800;
      font-size: 1.25rem;
    }

    .item-details { flex: 1; min-width: 0; }
    .item-name {
      font-size: 0.88rem;
      font-weight: 700;
      color: #1a1a1a;
      line-height: 1.3;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .item-sm { font-size: 0.75rem; color: #9CA3AF; margin-top: 0.15rem; }
    .item-price { font-size: 0.85rem; color: #2E7D32; font-weight: 700; margin-top: 0.2rem; }

    .item-controls {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 0.35rem;
      flex-shrink: 0;
    }
    .qty-row {
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }
    .qty-btn {
      width: 26px;
      height: 26px;
      border-radius: 50%;
      border: none;
      cursor: pointer;
      font-size: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      line-height: 1;
    }
    .qty-btn.minus { background: #FEE2E2; color: #DC2626; }
    .qty-btn.minus:hover { background: #DC2626; color: #fff; }
    .qty-btn.plus { background: #D1FAE5; color: #059669; }
    .qty-btn.plus:hover { background: #059669; color: #fff; }
    .qty { min-width: 20px; text-align: center; font-weight: 700; font-size: 0.88rem; }
    .item-total { font-size: 0.85rem; font-weight: 800; color: #1a1a1a; }
    .remove-btn { background: none; border: none; cursor: pointer; font-size: 0.85rem; opacity: 0.5; }
    .remove-btn:hover { opacity: 1; }

    .cart-footer {
      padding: 1rem 1.25rem 1.5rem;
      border-top: 1px solid #eee;
      flex-shrink: 0;
      background: #FAFAFA;
    }
    .subtotal {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 1rem;
      margin-bottom: 0.5rem;
    }
    .subtotal strong { font-size: 1.25rem; color: #2E7D32; }
    .delivery-note {
      font-size: 0.78rem;
      color: #9CA3AF;
      margin-bottom: 1rem;
    }
    .btn-checkout {
      width: 100%;
      background: #2E7D32;
      color: #fff;
      border: none;
      padding: 0.85rem;
      border-radius: 10px;
      font-size: 1rem;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.2s;
      margin-bottom: 0.5rem;
    }
    .btn-checkout:hover { background: #1B5E20; }
    .btn-clear {
      width: 100%;
      background: none;
      border: 1px solid #ddd;
      padding: 0.5rem;
      border-radius: 8px;
      font-size: 0.85rem;
      cursor: pointer;
      color: #999;
      transition: all 0.2s;
    }
    .btn-clear:hover { border-color: #f44; color: #f44; }
  `]
})
export class CartSidebarComponent {
  cartService = inject(CartService);
  private auth = inject(AuthService);
  private router = inject(Router);

  trackItem(_: number, item: CartItem): number {
    return item.productId;
  }

  increase(item: CartItem): void {
    this.cartService.addToCart(item.product);
  }

  decrease(item: CartItem): void {
    this.cartService.updateItem(item.productId, item.quantity - 1);
  }

  remove(item: CartItem): void {
    this.cartService.removeItem(item.productId);
  }

  goToCheckout(): void {
    this.cartService.closeCart();
    if (this.auth.isLoggedIn) {
      this.router.navigate(['/checkout']);
    } else {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/checkout' } });
    }
  }
}
