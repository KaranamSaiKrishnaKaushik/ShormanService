import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, catchError, firstValueFrom, map, of } from 'rxjs';
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
  private readonly guestCartKey = 'shorman_guest_cart';

  private cartSubject = new BehaviorSubject<Cart>(createEmptyCart());
  cart$ = this.cartSubject.asObservable();

  private isOpenSubject = new BehaviorSubject<boolean>(false);
  isOpen$ = this.isOpenSubject.asObservable();

  constructor() {
    this.auth.currentUser$.subscribe(user => {
      if (user) {
        void this.syncAuthenticatedCart();
        return;
      }

      this.setCart(this.loadGuestCart());
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

  private calculateCart(items: CartItem[]): Cart {
    return {
      items,
      total: items.reduce((sum, item) => sum + (item.product.price * item.quantity), 0),
      itemCount: items.reduce((sum, item) => sum + item.quantity, 0)
    };
  }

  private loadGuestCart(): Cart {
    const raw = localStorage.getItem(this.guestCartKey);
    if (!raw) {
      return createEmptyCart();
    }

    try {
      const parsed = JSON.parse(raw) as Partial<Cart>;
      const items = Array.isArray(parsed.items)
        ? parsed.items.filter((item): item is CartItem =>
            !!item &&
            typeof item.productId === 'number' &&
            typeof item.quantity === 'number' &&
            item.quantity > 0 &&
            !!item.product &&
            typeof item.product.id === 'number' &&
            typeof item.product.price === 'number')
        : [];

      return this.calculateCart(items.map(item => ({
        productId: item.productId,
        product: item.product,
        quantity: item.quantity
      })));
    } catch {
      localStorage.removeItem(this.guestCartKey);
      return createEmptyCart();
    }
  }

  private persistGuestCart(cart: Cart): void {
    if (cart.items.length === 0) {
      localStorage.removeItem(this.guestCartKey);
      return;
    }

    localStorage.setItem(this.guestCartKey, JSON.stringify(cart));
  }

  private setGuestCartItems(items: CartItem[]): void {
    const normalizedItems = items
      .filter(item => item.quantity > 0)
      .map(item => ({
        productId: item.productId,
        product: item.product,
        quantity: item.quantity
      }));

    const cart = this.calculateCart(normalizedItems);
    this.persistGuestCart(cart);
    this.setCart(cart);
  }

  private async syncAuthenticatedCart(): Promise<void> {
    const guestCart = this.loadGuestCart();

    if (guestCart.items.length > 0) {
      try {
        for (const item of guestCart.items) {
          await firstValueFrom(
            this.http.post<ApiCartResponse>(`${environment.apiUrl}/cart/items`, {
              productId: item.productId,
              quantity: item.quantity
            })
          );
        }

        localStorage.removeItem(this.guestCartKey);
      } catch {
        this.setCart(guestCart);
        return;
      }
    }

    this.refreshCart();
  }

  private requestCart(): Observable<Cart> {
    if (!this.auth.currentUser) {
      return of(this.loadGuestCart());
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
    if (quantity <= 0) {
      return;
    }

    if (!this.auth.currentUser) {
      const existingItem = this.cart.items.find(item => item.productId === product.id);
      const updatedItems = existingItem
        ? this.cart.items.map(item =>
            item.productId === product.id
              ? { ...item, quantity: item.quantity + quantity }
              : item)
        : [...this.cart.items, { productId: product.id, product, quantity }];

      this.setGuestCartItems(updatedItems);
      return;
    }

    this.http.post<ApiCartResponse>(`${environment.apiUrl}/cart/items`, { productId: product.id, quantity }).pipe(
      map(response => this.toCart(response)),
      catchError(() => of(this.cart))
    ).subscribe(cart => this.setCart(cart));
  }

  updateItem(productId: number, quantity: number): void {
    if (!this.auth.currentUser) {
      const updatedItems = quantity <= 0
        ? this.cart.items.filter(item => item.productId !== productId)
        : this.cart.items.map(item =>
            item.productId === productId
              ? { ...item, quantity }
              : item);

      this.setGuestCartItems(updatedItems);
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
      this.setGuestCartItems(this.cart.items.filter(item => item.productId !== productId));
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
      localStorage.removeItem(this.guestCartKey);
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
