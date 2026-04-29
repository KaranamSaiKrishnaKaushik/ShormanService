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
  private productService = inject(ProductService);
  private cdr = inject(ChangeDetectorRef);
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
    this.productService.pingHealth().subscribe(isUp => {
      console.log('Backend health check:', isUp ? 'OK' : 'FAILED');
    });

    this.loadCategories();
    this.loadProducts();
  }

  setSupermarket(slug: string): void {
    this.activeSupermarket = slug;
    // Useful for backend breakpoint testing: each supermarket click refreshes categories from API.
    this.loadCategories();
    this.loadProducts();
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

  private loadCategories(): void {
    this.productService.getCategories().subscribe(cats => {
      this.categories = cats;
      console.log('Categories loaded:', cats);
    });
  }

  private loadProducts(): void {
    this.loading = true;
    this.productService.getProducts().subscribe({
      next: (products) => {
        this.allProducts = Array.isArray(products) ? products : [];
        this.applyFilters();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.allProducts = [];
        this.filteredProducts = [];
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }
}
