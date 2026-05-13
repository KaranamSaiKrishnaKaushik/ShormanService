import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf, CurrencyPipe, AsyncPipe, NgStyle } from '@angular/common';
import { Observable, forkJoin, map } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProductService } from '../../core/services/product.service';
import { CartService } from '../../core/services/cart.service';
import { DeliveryCheckResult, DeliveryService } from '../../core/services/delivery.service';
import { Product, Category, Supermarket, ProductPage } from '../../core/models/product.model';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';

interface SupermarketTab {
  slug: string;
  name: string;
  color: string;
  bgColor: string;
}

type ProductMode = 'groceries' | 'beauty';

interface ProductModeOption {
  slug: ProductMode;
  label: string;
  accent: string;
  surface: string;
  stores: string[];
  title: string;
  allStoresLabel: string;
  sidebarTitle: string;
  allCategoriesLabel: string;
}

const SUPERMARKET_TABS: SupermarketTab[] = [
  { slug: 'all', name: 'ALL', color: '#2E7D32', bgColor: '#E8F5E9' },
  { slug: 'rewe', name: 'REWE', color: '#fff', bgColor: '#CC0000' },
  { slug: 'aldi', name: 'ALDI', color: '#fff', bgColor: '#00519C' },
  { slug: 'penny', name: 'PENNY', color: '#FFD600', bgColor: '#CC0000' },
  { slug: 'lidl', name: 'LIDL', color: '#FFD600', bgColor: '#0050AA' },
];

const MODE_OPTIONS: ProductModeOption[] = [
  {
    slug: 'groceries',
    label: 'Groceries',
    accent: '#2E7D32',
    surface: '#E8F5E9',
    stores: ['rewe', 'aldi', 'penny', 'lidl', 'edeka'],
    title: 'Groceries',
    allStoresLabel: 'All groceries',
    sidebarTitle: 'Grocery categories',
    allCategoriesLabel: 'All groceries'
  },
  {
    slug: 'beauty',
    label: 'Drugstore & Beauty',
    accent: '#B83280',
    surface: '#FCE7F3',
    stores: ['dm', 'rossmann'],
    title: 'Drugstore & Beauty',
    allStoresLabel: 'All drugstore',
    sidebarTitle: 'Drugstore categories',
    allCategoriesLabel: 'All drugstore'
  }
];

const BEAUTY_CATEGORY_SLUGS = new Set([
  'hair-care',
  'skin-care',
  'body-bath',
  'makeup-fragrance',
  'health-wellness',
  'baby-kids'
]);

const CATEGORY_ICON_MAP: Record<string, string> = {
  'all': '🛍️',
  'bakery': '🍞',
  'beverages': '🥤',
  'eggs-dairy': '🥚',
  'fruits-vegetables': '🥦',
  'meat': '🥩',
  'snacks': '🍿',
  'hair-care': '🧴',
  'skin-care': '✨',
  'body-bath': '🫧',
  'makeup-fragrance': '💄',
  'health-wellness': '🩹',
  'baby-kids': '🧸',
  'imported-products': '📦',
  'sparkles': '✨',
  'sun': '☀️',
  'droplets': '🫧',
  'palette': '🎨',
  'heart-pulse': '🩹',
  'baby': '🧸',
  'box': '📦',
  'egg': '🥚',
  'leaf': '🥦',
  'bread': '🍞',
  'cup': '🥤',
  'popcorn': '🍿'
};

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf, CurrencyPipe, AsyncPipe, NgStyle, LoadingSpinnerComponent, TranslateModule],
  templateUrl: './products.component.html',
  styleUrls: ['./products.component.scss']
})
export class ProductsComponent implements OnInit {
  private static readonly PAGE_SIZE = 30;
  private static readonly MODE_FETCH_PAGE_SIZE = 120;

  private productService = inject(ProductService);
  private translate = inject(TranslateService);
  private cdr = inject(ChangeDetectorRef);
  private deliveryService = inject(DeliveryService);
  cartService = inject(CartService);

  supermarketTabs = SUPERMARKET_TABS;
  categories: Category[] = [];
  supermarkets: Supermarket[] = [];
  filteredProducts: Product[] = [];
  totalProducts = 0;
  currentPage = 1;
  totalPages = 0;

  readonly productModes = MODE_OPTIONS;
  activeMode: ProductMode = 'groceries';
  activeSupermarket = 'all';
  activeCategory = 'all';
  searchQuery = '';
  loading = true;
  mobileFiltersOpen = false;
  deliveryCheck = {
    street: '',
    houseNumber: '',
    postalCode: '',
    city: 'Frankfurt am Main',
    country: 'Germany'
  };
  deliveryCheckResult: DeliveryCheckResult | null = null;
  checkingDelivery = false;
  deliveryCheckError = '';

  cartOpen$ = this.cartService.isOpen$;

  private smColorMap: Record<number, string> = {};
  private requestSequence = 0;

  private placeholderColors = ['#4CAF50', '#2196F3', '#FF9800', '#9C27B0', '#F44336', '#00BCD4'];

  get activeFiltersCount(): number {
    let count = 0;
    if (this.activeSupermarket !== 'all') count++;
    if (this.activeCategory !== 'all') count++;
    if (this.searchQuery.trim()) count++;
    return count;
  }

  get currentMode(): ProductModeOption {
    return this.productModes.find(mode => mode.slug === this.activeMode) ?? this.productModes[0];
  }

  get modeHeading(): string {
    const modeTitleKey = `products.mode.${this.activeMode}.title`;
    const translatedModeTitle = this.translate.instant(modeTitleKey);
    const modeTitle = translatedModeTitle === modeTitleKey ? this.currentMode.title : translatedModeTitle;
    const nounKey = this.totalProducts === 1 ? 'products.results.productSingular' : 'products.results.productPlural';
    return `${modeTitle} · ${this.totalProducts} ${this.translate.instant(nounKey)}`;
  }

  get visibleSupermarketTabs(): SupermarketTab[] {
    const modeStores = new Set(this.currentMode.stores);
    return this.supermarketTabs
      .filter(tab => tab.slug === 'all' || modeStores.has(tab.slug))
      .map(tab => tab.slug === 'all'
        ? { ...tab, name: this.translate.instant(`products.mode.${this.activeMode}.allStores`).toUpperCase() }
        : tab);
  }

  get visibleCategories(): Category[] {
    return this.categories.filter(category => this.getModeForCategory(category.slug) === this.activeMode);
  }

  get isMobileFiltersActive(): boolean {
    return this.mobileFiltersOpen;
  }

  get isModeWideSelection(): boolean {
    return this.activeSupermarket === 'all';
  }

  get sidebarTitle(): string {
    const key = this.activeMode === 'groceries' ? 'products.sidebar.groceryCategories' : 'products.sidebar.drugstoreCategories';
    return this.translate.instant(key);
  }

  get allCategoriesLabel(): string {
    const key = this.activeMode === 'groceries' ? 'products.sidebar.allGroceries' : 'products.sidebar.allDrugstore';
    return this.translate.instant(key);
  }

  ngOnInit(): void {
    this.productService.pingHealth().subscribe(isUp => {
      console.log('Backend health check:', isUp ? 'OK' : 'FAILED');
    });

    this.loadSupermarkets();
    this.loadCategories();
    this.loadProducts(true);
  }

  setMode(mode: ProductMode): void {
    if (this.activeMode === mode) {
      return;
    }

    this.activeMode = mode;

    if (this.activeSupermarket !== 'all' && this.getModeForStore(this.activeSupermarket) !== mode) {
      this.activeSupermarket = 'all';
    }

    if (this.activeCategory !== 'all' && this.getModeForCategory(this.activeCategory) !== mode) {
      this.activeCategory = 'all';
    }

    this.closeMobileFilters();
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

  getCategoryGlyph(category: Category | 'all'): string {
    if (category === 'all') {
      return CATEGORY_ICON_MAP['all'];
    }

    return CATEGORY_ICON_MAP[category.slug] || CATEGORY_ICON_MAP[category.icon ?? ''] || '•';
  }

  getCategoryLabel(category: Category | undefined | null): string {
    if (!category) {
      return '';
    }

    const key = `categories.${category.slug}`;
    const translated = this.translate.instant(key);
    return translated === key ? category.name : translated;
  }

  getStoreBadgeStyle(product: Product): Record<string, string> {
    const supermarketColor = product.supermarket?.color || this.getSupermarketColor(product.supermarketId);
    const mode = this.getModeForStore(product.supermarket?.slug ?? '');
    const accent = this.productModes.find(option => option.slug === mode)?.accent ?? supermarketColor;

    return {
      background: supermarketColor,
      boxShadow: `0 0 0 2px ${accent}`
    };
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

  checkDeliveryZone(): void {
    if (!this.deliveryCheck.postalCode.trim() || !this.deliveryCheck.city.trim()) {
      this.deliveryCheckError = 'Postal code and city are required to check delivery.';
      this.deliveryCheckResult = null;
      return;
    }

    this.checkingDelivery = true;
    this.deliveryCheckError = '';
    this.deliveryService.checkDelivery(this.deliveryCheck).subscribe({
      next: result => {
        this.deliveryCheckResult = result;
        this.checkingDelivery = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.deliveryCheckError = err?.error?.message || 'Failed to check delivery coverage.';
        this.deliveryCheckResult = null;
        this.checkingDelivery = false;
        this.cdr.detectChanges();
      }
    });
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
      this.smColorMap = supermarkets.reduce<Record<number, string>>((map, supermarket) => {
        map[supermarket.id] = supermarket.color || '#555';
        return map;
      }, {});
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
      if (this.activeCategory !== 'all' && !this.categories.some(category => category.slug === this.activeCategory && this.getModeForCategory(category.slug) === this.activeMode)) {
        this.activeCategory = 'all';
      }
    });
  }

  private loadProducts(reset: boolean): void {
    if (reset) {
      this.currentPage = 1;
    }

    const requestId = ++this.requestSequence;
    this.loading = true;
    this.filteredProducts = [];
    this.totalProducts = 0;
    this.totalPages = 0;
    this.cdr.detectChanges();

    const scopedCategories = this.visibleCategories;
    const scopedSupermarkets = this.supermarkets.filter(supermarket => this.getModeForStore(supermarket.slug) === this.activeMode);

    const activeCategoryId = this.activeCategory === 'all'
      ? undefined
      : scopedCategories.find(category => category.slug === this.activeCategory)?.id;
    const activeSupermarketId = this.activeSupermarket === 'all'
      ? undefined
      : scopedSupermarkets.find(supermarket => supermarket.slug === this.activeSupermarket)?.id;
    const activeSupermarketIds = this.activeSupermarket === 'all'
      ? scopedSupermarkets.map(supermarket => supermarket.id)
      : undefined;

    const productRequest = this.activeSupermarket === 'all' && scopedSupermarkets.length > 1
      ? this.getModeProductsPage(scopedSupermarkets, this.searchQuery.trim() || undefined, activeCategoryId)
      : this.productService.getProducts({
          search: this.searchQuery.trim() || undefined,
          categoryId: activeCategoryId,
          supermarketId: activeSupermarketId,
          supermarketIds: activeSupermarketId ? undefined : activeSupermarketIds,
          page: this.currentPage,
          pageSize: ProductsComponent.PAGE_SIZE
        });

    productRequest.subscribe({
      next: (page) => {
        if (requestId !== this.requestSequence) {
          return;
        }

        const items = Array.isArray(page.items) ? page.items : [];
        this.totalProducts = page.totalCount;
        this.totalPages = Math.max(1, Math.ceil(this.totalProducts / ProductsComponent.PAGE_SIZE));
        this.currentPage = page.page;
        this.filteredProducts = items;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        if (requestId !== this.requestSequence) {
          return;
        }

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

  private getModeProductsPage(supermarkets: Supermarket[], search: string | undefined, categoryId: number | undefined): Observable<ProductPage> {
    const requiredItems = this.currentPage * ProductsComponent.PAGE_SIZE;
    const requiredPages = Math.max(1, Math.ceil(requiredItems / ProductsComponent.MODE_FETCH_PAGE_SIZE));
    const perStoreRequests = supermarkets.map(supermarket =>
      forkJoin(
        Array.from({ length: requiredPages }, (_, index) =>
          this.productService.getProducts({
            search,
            categoryId,
            supermarketId: supermarket.id,
            page: index + 1,
            pageSize: ProductsComponent.MODE_FETCH_PAGE_SIZE
          })
        )
      )
    );

    return forkJoin(perStoreRequests).pipe(
      map(storePages => {
        const totalCount = storePages.reduce((sum, pages) => sum + (pages[0]?.totalCount ?? 0), 0);
        const combinedItems = storePages
          .flatMap(pages => pages.flatMap(page => page.items ?? []))
          .sort((left, right) => left.name.localeCompare(right.name));
        const startIndex = (this.currentPage - 1) * ProductsComponent.PAGE_SIZE;

        return {
          items: combinedItems.slice(startIndex, startIndex + ProductsComponent.PAGE_SIZE),
          totalCount,
          page: this.currentPage,
          pageSize: ProductsComponent.PAGE_SIZE
        };
      })
    );
  }

  private getModeForStore(storeSlug: string): ProductMode {
    return this.currentMode.stores.includes(storeSlug)
      ? this.currentMode.slug
      : BEAUTY_CATEGORY_SLUGS.has(storeSlug)
        ? 'beauty'
        : this.productModes.find(mode => mode.stores.includes(storeSlug))?.slug ?? 'groceries';
  }

  private getModeForCategory(categorySlug: string): ProductMode {
    return BEAUTY_CATEGORY_SLUGS.has(categorySlug) ? 'beauty' : 'groceries';
  }
}
