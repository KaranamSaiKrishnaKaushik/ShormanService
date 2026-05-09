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
  templateUrl: './cart-sidebar.component.html',
  styleUrls: ['./cart-sidebar.component.scss']
})
export class CartSidebarComponent {
  cartService = inject(CartService);
  private auth = inject(AuthService);
  private router = inject(Router);

  get isLoggedIn(): boolean {
    return this.auth.isLoggedIn;
  }

  get checkoutButtonLabel(): string {
    return this.isLoggedIn ? 'Proceed to Checkout →' : 'Sign In to Checkout →';
  }

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
