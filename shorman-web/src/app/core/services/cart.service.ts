import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, map, of } from 'rxjs';
import { Cart, CartItem } from '../models/cart.model';
import { Product } from '../models/product.model';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

function createEmptyCart(): Cart {
  return { items: [], total: 0, itemCount: 0 };
}

interface ApiCartItemResponse {
  id: number;
  productId: number;
  product: Product;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

interface ApiCartResponse {
  id: number;
  userId: number;
  items: ApiCartItemResponse[];
  total: number;
  itemCount: number;
  createdAt: string;
  updatedAt?: string | null;
}

@Injectable({ providedIn: 'root' })
export class CartService {
  private http = inject(HttpClient);
  private auth = inject(AuthService);

  private cartSubject = new BehaviorSubject<Cart>(createEmptyCart());
  cart$ = this.cartSubject.asObservable();

  private isOpenSubject = new BehaviorSubject<boolean>(false);
  isOpen$ = this.isOpenSubject.asObservable();

  constructor() {
    this.auth.currentUser$.subscribe(user => {
      if (user) {
        this.refreshCart();
        return;
      }

      this.cartSubject.next(createEmptyCart());
      this.closeCart();
    });
  }

  get cart(): Cart {
    return this.cartSubject.value;
  }

  private toCart(response: ApiCartResponse): Cart {
    const items: CartItem[] = response.items.map(item => ({
      id: item.id,
      productId: item.productId,
      product: item.product,
      quantity: item.quantity
    }));

    return {
      items,
      total: response.total,
      itemCount: response.itemCount
    };
  }

  private setCart(cart: Cart): void {
    this.cartSubject.next(cart);
  }

  private requestCart(): Observable<Cart> {
    if (!this.auth.currentUser) {
      return of(createEmptyCart());
    }

    return this.http.get<ApiCartResponse>(`${environment.apiUrl}/cart`).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(createEmptyCart()))
    );
  }

  private refreshCart(): void {
    this.requestCart().subscribe(cart => this.setCart(cart));
  }

  getItemQuantity(productId: number): number {
    const item = this.cart.items.find(i => i.productId === productId);
    return item ? item.quantity : 0;
  }

  addToCart(product: Product, quantity = 1): void {
    if (!this.auth.currentUser || quantity <= 0) {
      return;
    }

    this.http.post<ApiCartResponse>(`${environment.apiUrl}/cart/items`, { productId: product.id, quantity }).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(this.cart))
    ).subscribe(cart => this.setCart(cart));
  }

  updateItem(productId: number, quantity: number): void {
    if (!this.auth.currentUser) {
      return;
    }

    const item = this.cart.items.find(entry => entry.productId === productId);
    if (!item?.id) {
      return;
    }

    this.http.put<ApiCartResponse>(`${environment.apiUrl}/cart/items/${item.id}`, { quantity }).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(this.cart))
    ).subscribe(cart => this.setCart(cart));
  }

  removeItem(productId: number): void {
    if (!this.auth.currentUser) {
      return;
    }

    const item = this.cart.items.find(entry => entry.productId === productId);
    if (!item?.id) {
      return;
    }

    this.http.delete<ApiCartResponse>(`${environment.apiUrl}/cart/items/${item.id}`).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(this.cart))
    ).subscribe(cart => this.setCart(cart));
  }

  clearCart(): void {
    if (!this.auth.currentUser) {
      this.setCart(createEmptyCart());
      return;
    }

    this.http.delete<ApiCartResponse>(`${environment.apiUrl}/cart`).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(createEmptyCart()))
    ).subscribe(cart => this.setCart(cart));
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
    return this.requestCart();
  }
}
