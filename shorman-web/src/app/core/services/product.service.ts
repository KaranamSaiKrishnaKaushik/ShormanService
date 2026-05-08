import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Product, Category, Supermarket, ProductFilters, ProductPage, UpdateProductRequest } from '../models/product.model';

// Mock data for development when backend is not available
const MOCK_CATEGORIES: Category[] = [
  { id: 1, name: 'Eggs & Dairy', slug: 'eggs-dairy', icon: '🥚' },
  { id: 2, name: 'Meat', slug: 'meat', icon: '🥩' },
  { id: 3, name: 'Fruits & Vegetables', slug: 'fruits-vegetables', icon: '🥦' },
  { id: 4, name: 'Bakery', slug: 'bakery', icon: '🍞' },
  { id: 5, name: 'Beverages', slug: 'beverages', icon: '🥤' },
  { id: 6, name: 'Snacks', slug: 'snacks', icon: '🍿' },
];

const MOCK_SUPERMARKETS: Supermarket[] = [
  { id: 1, name: 'REWE', slug: 'rewe', color: '#CC0000' },
  { id: 2, name: 'ALDI', slug: 'aldi', color: '#00519C' },
  { id: 3, name: 'PENNY', slug: 'penny', color: '#CC0000' },
  { id: 4, name: 'LIDL', slug: 'lidl', color: '#0050AA' },
];

const MOCK_PRODUCTS: Product[] = [
  { id: 1, name: 'Whole Milk 1L', price: 1.29, categoryId: 1, supermarketId: 1, supermarket: MOCK_SUPERMARKETS[0], category: MOCK_CATEGORIES[0], isAvailable: true, unit: '1L' },
  { id: 2, name: 'Free Range Eggs x12', price: 3.49, categoryId: 1, supermarketId: 2, supermarket: MOCK_SUPERMARKETS[1], category: MOCK_CATEGORIES[0], isAvailable: true, unit: '12 pcs' },
  { id: 3, name: 'Chicken Breast 500g', price: 4.99, categoryId: 2, supermarketId: 1, supermarket: MOCK_SUPERMARKETS[0], category: MOCK_CATEGORIES[1], isAvailable: true, unit: '500g' },
  { id: 4, name: 'Ground Beef 400g', price: 5.49, categoryId: 2, supermarketId: 3, supermarket: MOCK_SUPERMARKETS[2], category: MOCK_CATEGORIES[1], isAvailable: true, unit: '400g' },
  { id: 5, name: 'Bananas 1kg', price: 1.49, categoryId: 3, supermarketId: 2, supermarket: MOCK_SUPERMARKETS[1], category: MOCK_CATEGORIES[2], isAvailable: true, unit: '1kg' },
  { id: 6, name: 'Apples Bag 1.5kg', price: 2.99, categoryId: 3, supermarketId: 4, supermarket: MOCK_SUPERMARKETS[3], category: MOCK_CATEGORIES[2], isAvailable: true, unit: '1.5kg' },
  { id: 7, name: 'Sourdough Bread', price: 2.49, categoryId: 4, supermarketId: 1, supermarket: MOCK_SUPERMARKETS[0], category: MOCK_CATEGORIES[3], isAvailable: true, unit: '500g' },
  { id: 8, name: 'Croissants x4', price: 1.89, categoryId: 4, supermarketId: 3, supermarket: MOCK_SUPERMARKETS[2], category: MOCK_CATEGORIES[3], isAvailable: true, unit: '4 pcs' },
  { id: 9, name: 'Orange Juice 1L', price: 1.99, categoryId: 5, supermarketId: 2, supermarket: MOCK_SUPERMARKETS[1], category: MOCK_CATEGORIES[4], isAvailable: true, unit: '1L' },
  { id: 10, name: 'Sparkling Water 6x500ml', price: 2.19, categoryId: 5, supermarketId: 4, supermarket: MOCK_SUPERMARKETS[3], category: MOCK_CATEGORIES[4], isAvailable: true, unit: '6x500ml' },
  { id: 11, name: 'Potato Chips 200g', price: 1.79, categoryId: 6, supermarketId: 3, supermarket: MOCK_SUPERMARKETS[2], category: MOCK_CATEGORIES[5], isAvailable: true, unit: '200g' },
  { id: 12, name: 'Mixed Nuts 250g', price: 3.99, categoryId: 6, supermarketId: 1, supermarket: MOCK_SUPERMARKETS[0], category: MOCK_CATEGORIES[5], isAvailable: true, unit: '250g' },
  { id: 13, name: 'Greek Yogurt 500g', price: 2.29, categoryId: 1, supermarketId: 4, supermarket: MOCK_SUPERMARKETS[3], category: MOCK_CATEGORIES[0], isAvailable: true, unit: '500g' },
  { id: 14, name: 'Salmon Fillet 300g', price: 6.99, categoryId: 2, supermarketId: 2, supermarket: MOCK_SUPERMARKETS[1], category: MOCK_CATEGORIES[1], isAvailable: true, unit: '300g' },
  { id: 15, name: 'Cherry Tomatoes 500g', price: 2.49, categoryId: 3, supermarketId: 1, supermarket: MOCK_SUPERMARKETS[0], category: MOCK_CATEGORIES[2], isAvailable: true, unit: '500g' },
  { id: 16, name: 'Cola 1.5L', price: 1.59, categoryId: 5, supermarketId: 3, supermarket: MOCK_SUPERMARKETS[2], category: MOCK_CATEGORIES[4], isAvailable: true, unit: '1.5L' },
];

@Injectable({ providedIn: 'root' })
export class ProductService {
  private http = inject(HttpClient);
  private useMock = environment.useMockProducts;

  getProducts(filters?: ProductFilters): Observable<ProductPage> {
    if (this.useMock) {
      const products = this.filterMockProducts(filters);
      const page = filters?.page ?? 1;
      const pageSize = filters?.pageSize ?? 30;
      const start = (page - 1) * pageSize;
      return of({
        items: products.slice(start, start + pageSize),
        totalCount: products.length,
        page,
        pageSize
      });
    }

    const params = new URLSearchParams();
    if (filters?.search) params.set('search', filters.search);
    if (filters?.categoryId) params.set('categoryId', String(filters.categoryId));
    if (filters?.supermarketId) params.set('supermarketId', String(filters.supermarketId));
    params.set('page', String(filters?.page ?? 1));
    params.set('pageSize', String(filters?.pageSize ?? 30));

    const query = params.toString();
    const url = query ? `${environment.apiUrl}/products?${query}` : `${environment.apiUrl}/products`;

    return this.http.get<unknown>(url).pipe(
      map((response) => this.toProductPage(response, filters?.page ?? 1, filters?.pageSize ?? 30)),
      catchError(() => of({ items: [], totalCount: 0, page: filters?.page ?? 1, pageSize: filters?.pageSize ?? 30 }))
    );
  }

  getProductById(id: number): Observable<Product> {
    if (this.useMock) {
      const p = MOCK_PRODUCTS.find(p => p.id === id);
      return of(p!);
    }
    return this.http.get<Product>(`${environment.apiUrl}/products/${id}`).pipe(
      catchError((error) => throwError(() => error))
    );
  }

  getAdminProducts(filters?: ProductFilters): Observable<ProductPage> {
    const params = new URLSearchParams();
    if (filters?.search) params.set('search', filters.search);
    if (filters?.categoryId) params.set('categoryId', String(filters.categoryId));
    if (filters?.supermarketId) params.set('supermarketId', String(filters.supermarketId));
    params.set('page', String(filters?.page ?? 1));
    params.set('pageSize', String(filters?.pageSize ?? 50));

    const query = params.toString();
    const url = query ? `${environment.apiUrl}/products/admin?${query}` : `${environment.apiUrl}/products/admin`;

    return this.http.get<unknown>(url).pipe(
      map((response) => this.toProductPage(response, filters?.page ?? 1, filters?.pageSize ?? 50)),
      catchError((error) => throwError(() => error))
    );
  }

  updateProduct(id: number, request: UpdateProductRequest): Observable<Product> {
    return this.http.put<Product>(`${environment.apiUrl}/products/${id}`, request).pipe(
      catchError((error) => throwError(() => error))
    );
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/products/${id}`).pipe(
      catchError((error) => throwError(() => error))
    );
  }

  getCategories(): Observable<Category[]> {
    if (this.useMock) return of(MOCK_CATEGORIES);
    return this.http
      .get<
        Category[]
      >(`${environment.apiUrl}/categories`)
      .pipe(
        map(categories => {
          return categories.map((category, index) => ({
            id: typeof category.id === 'number' ? category.id : index + 1,
            name: category.name,
            slug: category.slug ?? this.toSlug(category.name),
            icon: category.icon
          }));
        }),
        catchError(() => of([]))
      );
  }

  getSupermarkets(): Observable<Supermarket[]> {
    if (this.useMock) return of(MOCK_SUPERMARKETS);
    return this.http.get<Supermarket[]>(`${environment.apiUrl}/supermarkets`).pipe(
      catchError(() => of([]))
    );
  }

  pingHealth(): Observable<boolean> {
    const baseUrl = environment.apiUrl.replace(/\/api\/?$/, '');
    return this.http.get(`${baseUrl}/health`).pipe(
      map(() => true),
      catchError(() => of(false))
    );
  }

  private toSlug(value: string): string {
    return value
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-');
  }

  private filterMockProducts(filters?: ProductFilters): Product[] {
    let results = [...MOCK_PRODUCTS];

    if (filters?.search) {
      const q = filters.search.toLowerCase();
      results = results.filter(p => p.name.toLowerCase().includes(q));
    }
    if (filters?.categoryId) {
      results = results.filter(p => p.categoryId === filters.categoryId);
    }
    if (filters?.supermarketId) {
      results = results.filter(p => p.supermarketId === filters.supermarketId);
    }
    if (filters?.supermarketSlug && filters.supermarketSlug !== 'all') {
      const sm = MOCK_SUPERMARKETS.find(s => s.slug === filters.supermarketSlug);
      if (sm) results = results.filter(p => p.supermarketId === sm.id);
    }
    if (filters?.categorySlug && filters.categorySlug !== 'all') {
      const cat = MOCK_CATEGORIES.find(c => c.slug === filters.categorySlug);
      if (cat) results = results.filter(p => p.categoryId === cat.id);
    }

    return results;
  }

  private toProductPage(response: unknown, fallbackPage: number, fallbackPageSize: number): ProductPage {
    if (Array.isArray(response)) {
      return {
        items: response as Product[],
        totalCount: response.length,
        page: fallbackPage,
        pageSize: fallbackPageSize
      };
    }

    if (response && typeof response === 'object') {
      const maybeItems = response as { items?: unknown; data?: unknown; value?: unknown; $values?: unknown; totalCount?: unknown; page?: unknown; pageSize?: unknown };
      const items = Array.isArray(maybeItems.items)
        ? maybeItems.items as Product[]
        : Array.isArray(maybeItems.data)
          ? maybeItems.data as Product[]
          : Array.isArray(maybeItems.value)
            ? maybeItems.value as Product[]
            : Array.isArray(maybeItems.$values)
              ? maybeItems.$values as Product[]
              : [];
      return {
        items,
        totalCount: typeof maybeItems.totalCount === 'number' ? maybeItems.totalCount : items.length,
        page: typeof maybeItems.page === 'number' ? maybeItems.page : fallbackPage,
        pageSize: typeof maybeItems.pageSize === 'number' ? maybeItems.pageSize : fallbackPageSize
      };
    }

    return { items: [], totalCount: 0, page: fallbackPage, pageSize: fallbackPageSize };
  }
}
