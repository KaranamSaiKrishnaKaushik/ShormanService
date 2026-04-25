import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf, NgClass, CurrencyPipe, AsyncPipe } from '@angular/common';
import { ProductService } from '../../core/services/product.service';
import { CartService } from '../../core/services/cart.service';
import { Product, Category, Supermarket } from '../../core/models/product.model';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';

interface SupermarketTab {
  slug: string;
  name: string;
  color: string;
  bgColor: string;
}

const SUPERMARKET_TABS: SupermarketTab[] = [
  { slug: 'all', name: 'ALL', color: '#2E7D32', bgColor: '#E8F5E9' },
  { slug: 'rewe', name: 'REWE', color: '#fff', bgColor: '#CC0000' },
  { slug: 'aldi', name: 'ALDI', color: '#fff', bgColor: '#00519C' },
  { slug: 'penny', name: 'PENNY', color: '#FFD600', bgColor: '#CC0000' },
  { slug: 'lidl', name: 'LIDL', color: '#FFD600', bgColor: '#0050AA' },
];

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf, CurrencyPipe, AsyncPipe, LoadingSpinnerComponent],
  template: `
    <div class="products-page">
      <!-- Header with search -->
      <div class="page-header">
        <div class="header-inner">
          <button class="mobile-filter-btn" (click)="toggleMobileFilters()">
            <span class="filter-icon">☰</span>
            Filters
            <span class="filter-badge" *ngIf="activeFiltersCount > 0">{{ activeFiltersCount }}</span>
          </button>
          <div class="search-bar">
            <span class="search-icon">🔍</span>
            <input
              type="text"
              [(ngModel)]="searchQuery"
              (ngModelChange)="onSearchChange($event)"
              placeholder="Search products…"
              class="search-input"
            />
            <button *ngIf="searchQuery" class="search-clear" (click)="searchQuery = ''; applyFilters()">✕</button>
          </div>
        </div>
      </div>

      <!-- Supermarket Tabs -->
      <div class="supermarket-tabs">
        <div class="tabs-inner">
          <button
            *ngFor="let tab of supermarketTabs"
            class="sm-tab"
            [class.active]="activeSupermarket === tab.slug"
            [style.background]="activeSupermarket === tab.slug ? tab.bgColor : '#fff'"
            [style.color]="activeSupermarket === tab.slug ? tab.color : '#333'"
            [style.border-color]="tab.bgColor"
            (click)="setSupermarket(tab.slug)"
          >
            {{ tab.name }}
          </button>
        </div>
      </div>

      <div class="products-layout">
        <!-- Mobile Filter Overlay -->
        <div class="mobile-filter-overlay" *ngIf="mobileFiltersOpen" (click)="closeMobileFilters()"></div>

        <!-- Sidebar -->
        <aside class="sidebar" [class.mobile-open]="mobileFiltersOpen">
          <div class="mobile-filter-header">
            <h3>Filters</h3>
            <button class="close-btn" (click)="closeMobileFilters()">✕</button>
          </div>

          <div class="sidebar-section">
            <h3 class="sidebar-title">Categories</h3>
            <div class="category-list">
              <label class="category-item" [class.active]="activeCategory === 'all'">
                <input type="radio" name="category" value="all" [(ngModel)]="activeCategory" (change)="applyFilters(); closeMobileFilters()" />
                <span class="cat-icon">🛍️</span>
                <span class="cat-name">All Products</span>
              </label>
              <label *ngFor="let cat of categories" class="category-item" [class.active]="activeCategory === cat.slug">
                <input type="radio" name="category" [value]="cat.slug" [(ngModel)]="activeCategory" (change)="applyFilters(); closeMobileFilters()" />
                <span class="cat-icon">{{ cat.icon }}</span>
                <span class="cat-name">{{ cat.name }}</span>
              </label>
            </div>
          </div>
        </aside>

        <!-- Main Content -->
        <main class="products-main">
          <div class="results-header">
            <span class="results-count">
              <strong>{{ filteredProducts.length }}</strong> product{{ filteredProducts.length !== 1 ? 's' : '' }} found
            </span>
          </div>

          <app-loading-spinner *ngIf="loading"></app-loading-spinner>

          <div *ngIf="!loading && filteredProducts.length === 0" class="empty-state">
            <div class="empty-icon">🔍</div>
            <h3>No products found</h3>
            <p>Try adjusting your search or filters</p>
            <button class="btn-reset" (click)="resetFilters()">Reset Filters</button>
          </div>

          <div class="product-grid" *ngIf="!loading && filteredProducts.length > 0">
            <div *ngFor="let product of filteredProducts" class="product-card">
              <!-- Product Image -->
              <div class="product-image">
                <img *ngIf="product.imageUrl" [src]="product.imageUrl" [alt]="product.name" loading="lazy" />
                <div *ngIf="!product.imageUrl" class="image-placeholder" [style.background]="getPlaceholderColor(product.supermarketId)">
                  {{ product.name.charAt(0).toUpperCase() }}
                </div>
                <div class="sm-badge" [style.background]="getSupermarketColor(product.supermarketId)">
                  {{ product.supermarket?.name }}
                </div>
              </div>

              <!-- Product Info -->
              <div class="product-info">
                <div class="product-category">{{ product.category?.name }}</div>
                <h4 class="product-name">{{ product.name }}</h4>
                <div class="product-unit" *ngIf="product.unit">{{ product.unit }}</div>

                <div class="product-footer">
                  <div class="product-price">{{ product.price | currency:'EUR':'symbol':'1.2-2' }}</div>

                  <div class="cart-controls" *ngIf="getQuantity(product.id) > 0; else addBtn">
                    <button class="qty-btn minus" (click)="decreaseQty(product)">−</button>
                    <span class="qty-value">{{ getQuantity(product.id) }}</span>
                    <button class="qty-btn plus" (click)="increaseQty(product)">+</button>
                  </div>
                  <ng-template #addBtn>
                    <button class="btn-add" (click)="addToCart(product)">+ Add</button>
                  </ng-template>
                </div>
              </div>
            </div>
          </div>
        </main>
      </div>

      <!-- Mobile Cart Button -->
      <button class="mobile-cart-btn" (click)="cartService.openCart()" *ngIf="(cartService.cart$ | async)?.itemCount as count">
        <span class="cart-icon">🛒</span>
        <span class="cart-text">Cart</span>
        <span class="cart-badge">{{ count }}</span>
      </button>
    </div>

    <!-- Cart Sidebar Overlay -->
    <div class="cart-overlay" *ngIf="cartOpen$ | async" (click)="cartService.closeCart()"></div>
  `,
  styles: [`
    .products-page { background: #F9FAFB; min-height: calc(100vh - 128px); }

    /* Header */
    .page-header {
      background: #fff;
      border-bottom: 1px solid #eee;
      padding: 1rem 0;
      position: sticky;
      top: 64px;
      z-index: 900;
    }
    .header-inner {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      gap: 0.75rem;
      align-items: center;
    }
    .mobile-filter-btn {
      display: none;
      align-items: center;
      gap: 0.5rem;
      background: #2E7D32;
      border: none;
      border-radius: 8px;
      padding: 0.6rem 1rem;
      cursor: pointer;
      font-size: 0.9rem;
      font-weight: 600;
      color: #fff;
      position: relative;
      box-shadow: 0 2px 4px rgba(46, 125, 50, 0.3);
      transition: background 0.2s;
    }
    .mobile-filter-btn:hover {
      background: #1B5E20;
    }
    .filter-icon { font-size: 1.1rem; }
    .filter-badge {
      background: #fff;
      color: #2E7D32;
      font-size: 0.7rem;
      padding: 0.15rem 0.4rem;
      border-radius: 10px;
      font-weight: 700;
    }
    .search-bar {
      display: flex;
      align-items: center;
      background: #F3F4F6;
      border: 1.5px solid #E5E7EB;
      border-radius: 10px;
      padding: 0.5rem 0.75rem;
      gap: 0.5rem;
      max-width: 600px;
    }
    .search-icon { font-size: 1rem; color: #9CA3AF; }
    .search-input {
      flex: 1;
      border: none;
      background: none;
      font-size: 0.95rem;
      outline: none;
      color: #111;
    }
    .search-clear {
      background: none; border: none; cursor: pointer;
      color: #9CA3AF; font-size: 0.85rem; padding: 0 0.25rem;
    }
    .search-clear:hover { color: #333; }

    /* Supermarket Tabs */
    .supermarket-tabs {
      background: #fff;
      border-bottom: 1px solid #eee;
      padding: 0.75rem 0;
      position: sticky;
      top: 128px;
      z-index: 850;
    }
    .tabs-inner {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 1rem;
      display: flex;
      gap: 0.5rem;
      overflow-x: auto;
      scrollbar-width: none;
    }
    .tabs-inner::-webkit-scrollbar { display: none; }
    .sm-tab {
      border: 2px solid transparent;
      border-radius: 8px;
      padding: 0.4rem 1.25rem;
      font-size: 0.9rem;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
      transition: all 0.2s;
      letter-spacing: 0.5px;
    }
    .sm-tab:not(.active):hover { background: #f5f5f5 !important; }

    /* Layout */
    .products-layout {
      max-width: 1200px;
      margin: 0 auto;
      padding: 1.5rem 1rem;
      display: grid;
      grid-template-columns: 220px 1fr;
      gap: 1.5rem;
    }

    /* Sidebar */
    .sidebar { }
    .mobile-filter-header {
      display: none;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.25rem;
      border-bottom: 1px solid #eee;
      background: #fff;
    }
    .mobile-filter-header h3 {
      margin: 0;
      font-size: 1.1rem;
      font-weight: 700;
    }
    .mobile-filter-header .close-btn {
      background: none;
      border: none;
      font-size: 1.3rem;
      cursor: pointer;
      color: #999;
      padding: 0;
      line-height: 1;
    }
    .mobile-filter-overlay {
      display: none;
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.4);
      z-index: 150;
    }
    .sidebar-section {
      background: #fff;
      border-radius: 12px;
      padding: 1.25rem;
      box-shadow: 0 1px 4px rgba(0,0,0,0.06);
      position: sticky;
      top: 190px;
    }
    .sidebar-title {
      font-size: 0.85rem;
      font-weight: 700;
      color: #9CA3AF;
      text-transform: uppercase;
      letter-spacing: 1px;
      margin: 0 0 1rem;
      padding-top: 0;
    }
    .category-list { 
      display: flex; 
      flex-direction: column; 
      gap: 0.25rem;
      padding: 0;
      margin: 0;
    }
    .category-item {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.5rem 0.6rem;
      border-radius: 8px;
      cursor: pointer;
      transition: background 0.15s;
      font-size: 0.9rem;
      color: #555;
      visibility: visible;
      opacity: 1;
    }
    .category-item input[type="radio"] { display: none; }
    .category-item:hover { background: #F3F4F6; }
    .category-item.active { background: #E8F5E9; color: #2E7D32; font-weight: 600; }
    .cat-icon { font-size: 1.1rem; width: 24px; text-align: center; }
    .cat-name { flex: 1; }

    /* Products Main */
    .products-main { }
    .results-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 1rem;
    }
    .results-count { color: #6B7280; font-size: 0.9rem; }

    /* Product Grid */
    .product-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 1rem;
    }

    .product-card {
      background: #fff;
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 1px 4px rgba(0,0,0,0.07);
      transition: transform 0.2s, box-shadow 0.2s;
      display: flex;
      flex-direction: column;
    }
    .product-card:hover {
      transform: translateY(-3px);
      box-shadow: 0 6px 20px rgba(0,0,0,0.12);
    }

    .product-image {
      position: relative;
      height: 160px;
      overflow: hidden;
      background: #F9FAFB;
    }
    .product-image img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .image-placeholder {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 3rem;
      font-weight: 800;
      color: rgba(255,255,255,0.9);
    }
    .sm-badge {
      position: absolute;
      top: 8px;
      right: 8px;
      color: #fff;
      padding: 0.2rem 0.5rem;
      border-radius: 5px;
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.5px;
    }

    .product-info {
      padding: 0.85rem;
      flex: 1;
      display: flex;
      flex-direction: column;
    }
    .product-category {
      font-size: 0.72rem;
      color: #9CA3AF;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      margin-bottom: 0.3rem;
    }
    .product-name {
      font-size: 0.95rem;
      font-weight: 700;
      color: #1a1a1a;
      margin: 0 0 0.2rem;
      line-height: 1.3;
      flex: 1;
    }
    .product-unit { font-size: 0.78rem; color: #9CA3AF; margin-bottom: 0.75rem; }

    .product-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-top: auto;
    }
    .product-price {
      font-size: 1.1rem;
      font-weight: 800;
      color: #2E7D32;
    }

    .btn-add {
      background: #2E7D32;
      color: #fff;
      border: none;
      padding: 0.4rem 0.85rem;
      border-radius: 7px;
      font-size: 0.85rem;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-add:hover { background: #1B5E20; }

    .cart-controls {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }
    .qty-btn {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      border: none;
      cursor: pointer;
      font-size: 1.1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s;
      line-height: 1;
    }
    .qty-btn.minus { background: #FEE2E2; color: #DC2626; }
    .qty-btn.minus:hover { background: #DC2626; color: #fff; }
    .qty-btn.plus { background: #D1FAE5; color: #059669; }
    .qty-btn.plus:hover { background: #059669; color: #fff; }
    .qty-value {
      min-width: 24px;
      text-align: center;
      font-weight: 700;
      font-size: 0.9rem;
      color: #1a1a1a;
    }

    /* Empty State */
    .empty-state {
      text-align: center;
      padding: 4rem 2rem;
    }
    .empty-icon { font-size: 4rem; margin-bottom: 1rem; }
    .empty-state h3 { font-size: 1.25rem; color: #333; margin: 0 0 0.5rem; }
    .empty-state p { color: #999; margin: 0 0 1.5rem; }
    .btn-reset {
      background: #2E7D32;
      color: #fff;
      border: none;
      padding: 0.6rem 1.5rem;
      border-radius: 8px;
      cursor: pointer;
      font-weight: 600;
    }

    /* Cart overlay */
    .cart-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.4);
      z-index: 200;
    }

    /* Mobile Cart Button */
    .mobile-cart-btn {
      display: none;
      position: fixed;
      bottom: 20px;
      right: 20px;
      background: #2E7D32;
      color: #fff;
      border: none;
      border-radius: 50px;
      padding: 0.85rem 1.25rem;
      box-shadow: 0 4px 12px rgba(46, 125, 50, 0.4);
      cursor: pointer;
      align-items: center;
      gap: 0.5rem;
      font-weight: 700;
      font-size: 0.95rem;
      z-index: 100;
      transition: all 0.2s;
    }
    .mobile-cart-btn:hover {
      background: #1B5E20;
      transform: scale(1.05);
    }
    .mobile-cart-btn:active {
      transform: scale(0.95);
    }
    .cart-icon { font-size: 1.2rem; }
    .cart-badge {
      background: #fff;
      color: #2E7D32;
      font-size: 0.75rem;
      padding: 0.15rem 0.5rem;
      border-radius: 12px;
      font-weight: 800;
      min-width: 20px;
      text-align: center;
    }

    /* Responsive */
    @media (max-width: 960px) {
      .products-layout { grid-template-columns: 1fr; }
      
      /* Show mobile filter button */
      .mobile-filter-btn { display: flex; }
      
      /* Make sidebar a slide-in panel */
      .sidebar {
        position: fixed;
        top: 0;
        left: 0;
        bottom: 0;
        width: 280px;
        max-width: 85vw;
        background: #fff;
        z-index: 200;
        transform: translateX(-100%);
        transition: transform 0.3s ease;
        overflow: hidden;
        box-shadow: 4px 0 24px rgba(0,0,0,0.15);
        display: flex;
        flex-direction: column;
      }
      .sidebar.mobile-open {
        transform: translateX(0);
      }
      .mobile-filter-overlay {
        display: block;
      }
      .mobile-filter-header {
        display: flex;
        position: relative;
        top: auto;
        z-index: 1;
        background: #fff;
        flex-shrink: 0;
        border-bottom: 2px solid #E5E7EB;
      }
      .sidebar-section {
        position: relative;
        box-shadow: none;
        border-radius: 0;
        padding: 1.25rem;
        flex: 1;
        overflow-y: auto;
        overflow-x: hidden;
        min-height: 0;
      }
      .sidebar-title {
        margin-top: 0;
        padding-top: 0;
      }
      .category-list {
        padding: 0;
        margin: 0;
      }
      .category-item:first-child {
        margin-top: 0;
      }
      
      /* Show mobile cart button */
      .mobile-cart-btn { display: flex; }
      
      .product-grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 500px) {
      .product-grid { grid-template-columns: 1fr; }
      .search-bar { flex: 1; }
      .mobile-cart-btn {
        bottom: 15px;
        right: 15px;
        padding: 0.75rem 1rem;
        font-size: 0.85rem;
      }
    }
  `]
})
export class ProductsComponent implements OnInit {
  private productService = inject(ProductService);
  cartService = inject(CartService);

  supermarketTabs = SUPERMARKET_TABS;
  categories: Category[] = [];
  allProducts: Product[] = [];
  filteredProducts: Product[] = [];

  activeSupermarket = 'all';
  activeCategory = 'all';
  searchQuery = '';
  loading = true;
  mobileFiltersOpen = false;

  cartOpen$ = this.cartService.isOpen$;

  private smColorMap: Record<number, string> = {
    1: '#CC0000',
    2: '#00519C',
    3: '#CC0000',
    4: '#0050AA'
  };

  private placeholderColors = ['#4CAF50', '#2196F3', '#FF9800', '#9C27B0', '#F44336', '#00BCD4'];

  get activeFiltersCount(): number {
    let count = 0;
    if (this.activeSupermarket !== 'all') count++;
    if (this.activeCategory !== 'all') count++;
    if (this.searchQuery.trim()) count++;
    return count;
  }

  ngOnInit(): void {
    this.productService.getCategories().subscribe(cats => {
      this.categories = cats;
      console.log('Categories loaded:', cats);
    });
    this.productService.getProducts().subscribe(products => {
      this.allProducts = products;
      this.filteredProducts = products;
      this.loading = false;
    });
  }

  setSupermarket(slug: string): void {
    this.activeSupermarket = slug;
    this.applyFilters();
  }

  onSearchChange(_: string): void {
    this.applyFilters();
  }

  applyFilters(): void {
    let results = [...this.allProducts];
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      results = results.filter(p => p.name.toLowerCase().includes(q));
    }
    if (this.activeSupermarket !== 'all') {
      results = results.filter(p => p.supermarket?.slug === this.activeSupermarket);
    }
    if (this.activeCategory !== 'all') {
      results = results.filter(p => p.category?.slug === this.activeCategory);
    }
    this.filteredProducts = results;
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.activeSupermarket = 'all';
    this.activeCategory = 'all';
    this.filteredProducts = [...this.allProducts];
  }

  toggleMobileFilters(): void {
    this.mobileFiltersOpen = !this.mobileFiltersOpen;
    if (this.mobileFiltersOpen) {
      // Only need to scroll the sidebar-section now
      setTimeout(() => {
        const sidebarSection = document.querySelector('.sidebar-section') as HTMLElement;
        if (sidebarSection) {
          sidebarSection.scrollTop = 0;
        }
      }, 50);
    }
  }

  closeMobileFilters(): void {
    this.mobileFiltersOpen = false;
  }

  getQuantity(productId: number): number {
    return this.cartService.getItemQuantity(productId);
  }

  addToCart(product: Product): void {
    this.cartService.addToCart(product);
  }

  increaseQty(product: Product): void {
    this.cartService.addToCart(product);
  }

  decreaseQty(product: Product): void {
    const qty = this.cartService.getItemQuantity(product.id);
    this.cartService.updateItem(product.id, qty - 1);
  }

  getSupermarketColor(smId: number): string {
    return this.smColorMap[smId] || '#555';
  }

  getPlaceholderColor(smId: number): string {
    return this.placeholderColors[(smId - 1) % this.placeholderColors.length] || '#2E7D32';
  }
}
