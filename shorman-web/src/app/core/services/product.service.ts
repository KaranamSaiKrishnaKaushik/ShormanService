import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Product, Category, Supermarket, ProductFilters } from '../models/product.model';

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
  private useMock = false;

  getProducts(filters?: ProductFilters): Observable<Product[]> {
    if (this.useMock) {
      return of(this.filterMockProducts(filters));
    }

    const params = new URLSearchParams();
    if (filters?.search) params.set('search', filters.search);
    if (filters?.categoryId) params.set('categoryId', String(filters.categoryId));
    if (filters?.supermarketId) params.set('supermarketId', String(filters.supermarketId));

    const query = params.toString();
    const url = query ? `${environment.apiUrl}/products?${query}` : `${environment.apiUrl}/products`;

    return this.http.get<unknown>(url).pipe(
      map((response) => this.toProductArray(response)),
      catchError(() => of(this.filterMockProducts(filters)))
    );
  }

  getProductById(id: number): Observable<Product> {
    if (this.useMock) {
      const p = MOCK_PRODUCTS.find(p => p.id === id);
      return of(p!);
    }
    return this.http.get<Product>(`${environment.apiUrl}/products/${id}`).pipe(
      catchError(() => of(MOCK_PRODUCTS.find(p => p.id === id)!))
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
          if (!categories.length) {
            return [...MOCK_CATEGORIES];
          }

          return categories.map((category, index) => ({
            id: typeof category.id === 'number' ? category.id : index + 1,
            name: category.name,
            slug: category.slug ?? this.toSlug(category.name),
            icon: category.icon
          }));
        }),
        catchError(() => of(MOCK_CATEGORIES))
      );
  }

  getSupermarkets(): Observable<Supermarket[]> {
    if (this.useMock) return of(MOCK_SUPERMARKETS);
    return this.http.get<Supermarket[]>(`${environment.apiUrl}/supermarkets`);
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

  private toProductArray(response: unknown): Product[] {
    if (Array.isArray(response)) {
      return response as Product[];
    }

    if (response && typeof response === 'object') {
      const maybeItems = (response as { items?: unknown; data?: unknown; value?: unknown; $values?: unknown });
      if (Array.isArray(maybeItems.items)) return maybeItems.items as Product[];
      if (Array.isArray(maybeItems.data)) return maybeItems.data as Product[];
      if (Array.isArray(maybeItems.value)) return maybeItems.value as Product[];
      if (Array.isArray(maybeItems.$values)) return maybeItems.$values as Product[];
    }

    return [];
  }
}
