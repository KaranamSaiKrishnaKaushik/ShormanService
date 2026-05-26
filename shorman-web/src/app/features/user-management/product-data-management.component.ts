import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DecimalPipe, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Category, Product, ProductFilters, ProductPage, Supermarket, UpdateProductRequest } from '../../core/models/product.model';
import { ProductService } from '../../core/services/product.service';
import { TranslateModule, TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-product-data-management',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf, DecimalPipe, TranslateModule],
  template: `
    <section class="toolbar">
      <input
        type="search"
        [(ngModel)]="filters.search"
        (keydown.enter)="applyFilters()"
        placeholder="Search product name" />
      <select [(ngModel)]="selectedCategoryId" (ngModelChange)="applyFilters()">
        <option [ngValue]="null">{{ 'productManagement.allCategories' | translate }}</option>
        <option *ngFor="let category of categories" [ngValue]="category.id">{{ getCategoryLabel(category) }}</option>
      </select>
      <select [(ngModel)]="selectedSupermarketId" (ngModelChange)="applyFilters()">
        <option [ngValue]="null">All Supermarkets</option>
        <option *ngFor="let supermarket of supermarkets" [ngValue]="supermarket.id">{{ supermarket.name }}</option>
      </select>
      <button type="button" class="action-btn" (click)="applyFilters()">Search</button>
      <button type="button" class="ghost-btn" (click)="resetFilters()">Reset</button>
    </section>

    <p *ngIf="errorMsg" class="error-msg">{{ errorMsg }}</p>

    <section class="meta-row" *ngIf="pageData">
      <div>
        <strong>{{ pageData.totalCount | number }}</strong>
        <span>products in database</span>
      </div>
      <div>
        <button type="button" class="ghost-btn" [disabled]="loading || pageData.page <= 1" (click)="goToPage(pageData.page - 1)">Previous</button>
        <span>Page {{ pageData.page }} / {{ totalPages }}</span>
        <button type="button" class="ghost-btn" [disabled]="loading || pageData.page >= totalPages" (click)="goToPage(pageData.page + 1)">Next</button>
      </div>
    </section>

    <section *ngIf="loading" class="state-card">
      <h2>Loading products</h2>
      <p>Fetching the selected page from the database.</p>
    </section>

    <section *ngIf="!loading && pageData?.items?.length" class="table-shell">
      <table>
        <thead>
          <tr>
            <th>ID</th>
            <th>Name</th>
            <th>Price</th>
            <th>Image URL</th>
            <th>Category</th>
            <th>Supermarket</th>
            <th>Available</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let product of pageData!.items">
            <td>{{ product.id }}</td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else nameView">
                <input [(ngModel)]="draft.name" />
              </ng-container>
              <ng-template #nameView>
                <strong>{{ product.name }}</strong>
              </ng-template>
            </td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else priceView">
                <input type="number" min="0" step="0.01" [(ngModel)]="draft.price" />
              </ng-container>
              <ng-template #priceView>
                {{ product.price | number:'1.2-2' }} €
              </ng-template>
            </td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else imageView">
                <input [(ngModel)]="draft.imageUrl" />
              </ng-container>
              <ng-template #imageView>
                <a *ngIf="product.imageUrl; else noImage" [href]="product.imageUrl" target="_blank" rel="noreferrer">Open</a>
                <ng-template #noImage><span>None</span></ng-template>
              </ng-template>
            </td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else categoryView">
                <select [(ngModel)]="draft.categoryId">
                  <option *ngFor="let category of categories" [ngValue]="category.id">{{ getCategoryLabel(category) }}</option>
                </select>
              </ng-container>
              <ng-template #categoryView>{{ getCategoryLabel(product.category) || product.categoryId }}</ng-template>
            </td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else supermarketView">
                <select [(ngModel)]="draft.supermarketId">
                  <option *ngFor="let supermarket of supermarkets" [ngValue]="supermarket.id">{{ supermarket.name }}</option>
                </select>
              </ng-container>
              <ng-template #supermarketView>{{ product.supermarket?.name || product.supermarketId }}</ng-template>
            </td>
            <td>
              <ng-container *ngIf="editingProductId === product.id; else availableView">
                <input type="checkbox" [(ngModel)]="draft.isAvailable" />
              </ng-container>
              <ng-template #availableView>{{ product.isAvailable ? 'Yes' : 'No' }}</ng-template>
            </td>
            <td>
              <div class="action-group" *ngIf="editingProductId !== product.id; else editActions">
                <button type="button" class="action-btn" (click)="startEdit(product)">Edit</button>
                <button type="button" class="danger-btn" (click)="deleteProduct(product)">Delete</button>
              </div>
              <ng-template #editActions>
                <div class="action-group">
                  <button type="button" class="action-btn" [disabled]="savingProductId === product.id" (click)="saveProduct(product.id)">
                    {{ savingProductId === product.id ? 'Saving...' : 'Save' }}
                  </button>
                  <button type="button" class="ghost-btn" (click)="cancelEdit()">Cancel</button>
                </div>
              </ng-template>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <section *ngIf="!loading && !pageData?.items?.length" class="state-card">
      <h2>No products found</h2>
      <p>Adjust the filters and search again.</p>
    </section>
  `,
  styles: [`
    .toolbar,
    .meta-row,
    .state-card,
    .table-shell {
      background: rgba(255, 255, 255, 0.96);
      border: 1px solid #dde6df;
      border-radius: 22px;
      box-shadow: 0 18px 36px rgba(25, 53, 41, 0.08);
    }
    .toolbar {
      padding: 1rem;
      display: grid;
      gap: 0.75rem;
      grid-template-columns: 2fr repeat(2, 1fr) auto auto;
      margin-bottom: 1rem;
    }
    .toolbar input,
    .toolbar select,
    .table-shell input,
    .table-shell textarea,
    .table-shell select {
      border: 1px solid #cad6cc;
      border-radius: 12px;
      padding: 0.75rem 0.85rem;
      font: inherit;
      width: 100%;
      background: #fff;
    }
    .meta-row {
      padding: 0.9rem 1rem;
      margin-bottom: 1rem;
      display: flex;
      justify-content: space-between;
      gap: 1rem;
      align-items: center;
      color: #496353;
    }
    .meta-row strong {
      margin-right: 0.4rem;
      color: #1d3527;
    }
    .error-msg {
      margin: 0 0 1rem;
      padding: 0.85rem 1rem;
      border-radius: 14px;
      background: #fff1f0;
      color: #9a2f27;
      border: 1px solid #f1c8c4;
    }
    .state-card {
      padding: 1.5rem;
    }
    .table-shell {
      overflow: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      min-width: 1040px;
    }
    th,
    td {
      padding: 0.85rem;
      text-align: left;
      border-bottom: 1px solid #e5ece7;
      vertical-align: top;
    }
    th {
      font-size: 0.85rem;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      color: #567163;
      background: #f7faf7;
    }
    th:nth-child(2),
    td:nth-child(2) {
      width: 26%;
      min-width: 240px;
    }
    th:nth-child(3),
    td:nth-child(3) {
      width: 11%;
      min-width: 110px;
    }
    th:nth-child(4),
    td:nth-child(4),
    th:nth-child(5),
    td:nth-child(5),
    th:nth-child(6),
    td:nth-child(6),
    th:nth-child(7),
    td:nth-child(7) {
      width: 10%;
      min-width: 95px;
    }
    th:last-child,
    td:last-child {
      position: sticky;
      right: 0;
      background: #fff;
      box-shadow: -10px 0 18px rgba(25, 53, 41, 0.08);
      min-width: 170px;
      z-index: 1;
    }
    th:last-child {
      background: #f7faf7;
      z-index: 2;
    }
    td strong,
    td small {
      display: block;
    }
    td small {
      margin-top: 0.25rem;
      color: #66796d;
      line-height: 1.4;
    }
    .action-group {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .action-btn,
    .ghost-btn,
    .danger-btn {
      border: none;
      border-radius: 999px;
      padding: 0.65rem 0.95rem;
      font-weight: 700;
      cursor: pointer;
    }
    .action-btn {
      background: #123f27;
      color: #fff;
    }
    .ghost-btn {
      background: #eef3ef;
      color: #264636;
    }
    .danger-btn {
      background: #fff0ed;
      color: #b13a26;
    }
    @media (max-width: 960px) {
      .toolbar {
        grid-template-columns: 1fr;
      }
      .meta-row {
        flex-direction: column;
        align-items: start;
      }
    }
  `]
})
export class ProductDataManagementComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly translate = inject(TranslateService);
  private readonly cdr = inject(ChangeDetectorRef);

  categories: Category[] = [];
  supermarkets: Supermarket[] = [];
  pageData: ProductPage | null = null;
  filters: ProductFilters = { page: 1, pageSize: 50 };
  selectedCategoryId: number | null = null;
  selectedSupermarketId: number | null = null;
  loading = true;
  errorMsg = '';
  editingProductId: number | null = null;
  savingProductId: number | null = null;
  draft: UpdateProductRequest = this.emptyDraft();

  ngOnInit(): void {
    this.productService.getCategories().subscribe(categories => {
      this.categories = categories;
      this.cdr.detectChanges();
    });

    this.productService.getSupermarkets().subscribe(supermarkets => {
      this.supermarkets = supermarkets;
      this.cdr.detectChanges();
    });

    this.loadProducts();
  }

  getCategoryLabel(category: Category | undefined | null): string {
    if (!category) {
      return '';
    }

    const key = `categories.${category.slug}`;
    const translated = this.translate.instant(key);
    return translated === key ? category.name : translated;
  }

  get totalPages(): number {
    if (!this.pageData) {
      return 1;
    }

    return Math.max(1, Math.ceil(this.pageData.totalCount / this.pageData.pageSize));
  }

  applyFilters(): void {
    this.filters = {
      ...this.filters,
      categoryId: this.selectedCategoryId ?? undefined,
      supermarketId: this.selectedSupermarketId ?? undefined,
      page: 1
    };
    this.loadProducts();
  }

  resetFilters(): void {
    this.selectedCategoryId = null;
    this.selectedSupermarketId = null;
    this.filters = { page: 1, pageSize: 50 };
    this.loadProducts();
  }

  goToPage(page: number): void {
    this.filters = { ...this.filters, page };
    this.loadProducts();
  }

  startEdit(product: Product): void {
    this.editingProductId = product.id;
    this.draft = {
      name: product.name,
      description: product.description,
      price: product.price,
      imageUrl: product.imageUrl,
      categoryId: product.categoryId,
      supermarketId: product.supermarketId,
      unit: product.unit,
      stock: product.stock,
      isAvailable: product.isAvailable
    };
  }

  cancelEdit(): void {
    this.editingProductId = null;
    this.savingProductId = null;
    this.draft = this.emptyDraft();
  }

  saveProduct(productId: number): void {
    this.savingProductId = productId;
    this.errorMsg = '';

    this.productService.updateProduct(productId, this.draft).subscribe({
      next: updated => {
        if (this.pageData) {
          this.pageData = {
            ...this.pageData,
            items: this.pageData.items.map(item => item.id === updated.id ? updated : item)
          };
        }
        this.cancelEdit();
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to save product.';
        this.savingProductId = null;
        this.cdr.detectChanges();
      }
    });
  }

  deleteProduct(product: Product): void {
    if (!window.confirm(`Delete product ${product.id} - ${product.name}?`)) {
      return;
    }

    this.errorMsg = '';
    this.productService.deleteProduct(product.id).subscribe({
      next: () => {
        this.loadProducts();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to delete product.';
        this.cdr.detectChanges();
      }
    });
  }

  private loadProducts(): void {
    this.loading = true;
    this.errorMsg = '';
    this.editingProductId = null;

    this.productService.getAdminProducts(this.filters).subscribe({
      next: pageData => {
        this.pageData = pageData;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        this.errorMsg = err?.error?.message || 'Failed to load products.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private emptyDraft(): UpdateProductRequest {
    return {
      name: '',
      description: '',
      price: 0,
      imageUrl: '',
      categoryId: 0,
      supermarketId: 0,
      unit: '',
      stock: undefined,
      isAvailable: true
    };
  }
}