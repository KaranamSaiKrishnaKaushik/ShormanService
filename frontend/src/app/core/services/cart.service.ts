import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { Cart, CartItem, AddToCartRequest } from '../models/cart.model';
import { Product } from '../models/product.model';
import { environment } from '../../../environments/environment';

const CART_KEY = 'shorman_cart';

@Injectable({ providedIn: 'root' })
export class CartService {
  private http = inject(HttpClient);

  private cartSubject = new BehaviorSubject<Cart>(this.loadCart());
  cart$ = this.cartSubject.asObservable();

  private isOpenSubject = new BehaviorSubject<boolean>(false);
  isOpen$ = this.isOpenSubject.asObservable();

  get cart(): Cart {
    return this.cartSubject.value;
  }

  private loadCart(): Cart {
    try {
      const raw = localStorage.getItem(CART_KEY);
      if (raw) return JSON.parse(raw);
    } catch {}
    return { items: [], total: 0, itemCount: 0 };
  }

  private saveCart(cart: Cart): void {
    localStorage.setItem(CART_KEY, JSON.stringify(cart));
    this.cartSubject.next(cart);
  }

  private recalculate(items: CartItem[]): Cart {
    const total = items.reduce((sum, i) => sum + i.product.price * i.quantity, 0);
    const itemCount = items.reduce((sum, i) => sum + i.quantity, 0);
    return { items, total, itemCount };
  }

  getItemQuantity(productId: number): number {
    const item = this.cart.items.find(i => i.productId === productId);
    return item ? item.quantity : 0;
  }

  addToCart(product: Product, quantity = 1): void {
    const items = [...this.cart.items];
    const idx = items.findIndex(i => i.productId === product.id);
    if (idx >= 0) {
      items[idx] = { ...items[idx], quantity: items[idx].quantity + quantity };
    } else {
      items.push({ productId: product.id, product, quantity });
    }
    this.saveCart(this.recalculate(items));
  }

  updateItem(productId: number, quantity: number): void {
    let items = [...this.cart.items];
    if (quantity <= 0) {
      items = items.filter(i => i.productId !== productId);
    } else {
      const idx = items.findIndex(i => i.productId === productId);
      if (idx >= 0) items[idx] = { ...items[idx], quantity };
    }
    this.saveCart(this.recalculate(items));
  }

  removeItem(productId: number): void {
    const items = this.cart.items.filter(i => i.productId !== productId);
    this.saveCart(this.recalculate(items));
  }

  clearCart(): void {
    this.saveCart({ items: [], total: 0, itemCount: 0 });
  }

  openCart(): void {
    this.isOpenSubject.next(true);
  }

  closeCart(): void {
    this.isOpenSubject.next(false);
  }

  toggleCart(): void {
    this.isOpenSubject.next(!this.isOpenSubject.value);
  }

  getCart(): Observable<Cart> {
    return this.http.get<Cart>(`${environment.apiUrl}/cart`);
  }
}
