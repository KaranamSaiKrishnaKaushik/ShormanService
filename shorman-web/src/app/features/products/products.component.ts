import { ChangeDetectorRef, Component, OnInit, inject, signal, computed } from '@angular/core';
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
  templateUrl: './products.component.html',
  styleUrls: ['./products.component.scss']
})
export class ProductsComponent implements OnInit {
  private static readonly PAGE_SIZE = 30;

  private productService = inject(ProductService);
  private cdr = inject(ChangeDetectorRef);
  cartService = inject(CartService);

  supermarketTabs = SUPERMARKET_TABS;
  categories: Category[] = [];
  supermarkets: Supermarket[] = [];
  filteredProducts: Product[] = [];
  totalProducts = 0;
  currentPage = 1;
  totalPages = 0;

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
    this.productService.pingHealth().subscribe(isUp => {
      console.log('Backend health check:', isUp ? 'OK' : 'FAILED');
    });

    this.loadSupermarkets();
    this.loadCategories();
    this.loadProducts(true);
  }

  setSupermarket(slug: string): void {
    this.activeSupermarket = slug;
    this.loadProducts(true);
  }

  onSearchChange(_: string): void {
    this.loadProducts(true);
  }

  applyFilters(): void {
    this.loadProducts(true);
  }

  resetFilters(): void {
    this.searchQuery = '';
    this.activeSupermarket = 'all';
    this.activeCategory = 'all';
    this.loadProducts(true);
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

  changePage(page: number): void {
    if (this.loading || page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }

    this.currentPage = page;
    this.loadProducts(false);
  }

  get visiblePageNumbers(): number[] {
    if (this.totalPages <= 1) {
      return [1];
    }

    const windowSize = 5;
    const halfWindow = Math.floor(windowSize / 2);
    let start = Math.max(1, this.currentPage - halfWindow);
    let end = Math.min(this.totalPages, start + windowSize - 1);

    if (end - start + 1 < windowSize) {
      start = Math.max(1, end - windowSize + 1);
    }

    const pages: number[] = [];
    for (let page = start; page <= end; page += 1) {
      pages.push(page);
    }
    return pages;
  }

  private loadSupermarkets(): void {
    this.productService.getSupermarkets().subscribe(supermarkets => {
      this.supermarkets = supermarkets;
      const dynamicTabs = supermarkets.map(supermarket => ({
        slug: supermarket.slug,
        name: supermarket.name,
        color: this.getTabTextColor(supermarket.color),
        bgColor: supermarket.color || '#2E7D32'
      }));
      this.supermarketTabs = [{ slug: 'all', name: 'ALL', color: '#2E7D32', bgColor: '#E8F5E9' }, ...dynamicTabs];
      this.cdr.detectChanges();
    });
  }

  private loadCategories(): void {
    this.productService.getCategories().subscribe(cats => {
      this.categories = cats.filter(category => category.slug !== 'imported-products');
      console.log('Categories loaded:', cats);
    });
  }

  private loadProducts(reset: boolean): void {
    if (reset) {
      this.currentPage = 1;
    }
    this.loading = true;

    const activeCategoryId = this.activeCategory === 'all'
      ? undefined
      : this.categories.find(category => category.slug === this.activeCategory)?.id;
    const activeSupermarketId = this.activeSupermarket === 'all'
      ? undefined
      : this.supermarkets.find(supermarket => supermarket.slug === this.activeSupermarket)?.id;

    this.productService.getProducts({
      search: this.searchQuery.trim() || undefined,
      categoryId: activeCategoryId,
      supermarketId: activeSupermarketId,
      page: this.currentPage,
      pageSize: ProductsComponent.PAGE_SIZE
    }).subscribe({
      next: (page) => {
        const items = Array.isArray(page.items) ? page.items : [];
        this.totalProducts = page.totalCount;
        this.totalPages = Math.max(1, Math.ceil(this.totalProducts / ProductsComponent.PAGE_SIZE));
        this.currentPage = page.page;
        this.filteredProducts = items;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.filteredProducts = [];
        this.totalProducts = 0;
        this.totalPages = 0;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private getTabTextColor(backgroundColor?: string): string {
    if (!backgroundColor) {
      return '#fff';
    }

    return backgroundColor.toLowerCase() === '#ffd600' ? '#333' : '#fff';
  }
}
