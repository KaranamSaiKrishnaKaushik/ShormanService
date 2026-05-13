import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { DatePipe, DecimalPipe, NgClass, NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { Category, Supermarket } from '../../core/models/product.model';
import {
  ManagedProduct,
  PagedResult,
  ProductHistoryEntry,
  ProductUploadRun
} from '../../core/models/product-management.model';
import { ProductService } from '../../core/services/product.service';
import { ProductManagementService } from '../../core/services/product-management.service';

type ProductManagementSection = 'catalog' | 'uploads' | 'history';

@Component({
  selector: 'app-product-management',
  standalone: true,
  imports: [FormsModule, NgFor, NgIf, NgClass, DecimalPipe, DatePipe],
  template: `
    <section class="page-shell">
      <header class="hero-card">
        <div>
          <span class="eyebrow">Catalog Operations</span>
          <h1>Product Management</h1>
          <p>Upload store-specific Excel sheets, review import runs, and audit what changed in the live catalog.</p>
        </div>
        <div class="hero-actions">
          <a class="ghost-btn" [href]="templateUrl" download>Download REWE Template</a>
          <button type="button" class="primary-btn" (click)="setSection('uploads')">Open Upload Runs</button>
        </div>
      </header>

      <section class="upload-card">
        <div>
          <h2>Upload New Product Sheet</h2>
          <p>Accepted format: Excel .xlsx with the template headers. image_url remains optional.</p>
        </div>
        <div class="upload-row">
          <input type="file" accept=".xlsx" (change)="onFileSelected($event)" />
          <button type="button" class="primary-btn" [disabled]="uploading" (click)="uploadSheet()">
            {{ uploading ? 'Uploading...' : 'Upload Sheet' }}
          </button>
        </div>
        <p *ngIf="selectedFileName">Selected: {{ selectedFileName }}</p>
        <p *ngIf="uploadMessage" class="success-msg">{{ uploadMessage }}</p>
        <p *ngIf="uploadErrorMsg" class="error-msg">{{ uploadErrorMsg }}</p>
      </section>

      <section class="summary-grid">
        <article class="summary-card">
          <strong>{{ productsPage?.totalCount ?? 0 | number }}</strong>
          <span>Current catalog products</span>
        </article>
        <article class="summary-card">
          <strong>{{ uploadsPage?.totalCount ?? 0 | number }}</strong>
          <span>Upload runs tracked</span>
        </article>
        <article class="summary-card">
          <strong>{{ historyPage?.totalCount ?? 0 | number }}</strong>
          <span>History rows available</span>
        </article>
      </section>

      <section class="section-nav">
        <button type="button" class="section-link" [ngClass]="{ active: section === 'catalog' }" (click)="setSection('catalog')">Current Products</button>
        <button type="button" class="section-link" [ngClass]="{ active: section === 'uploads' }" (click)="setSection('uploads')">Upload Runs</button>
        <button type="button" class="section-link" [ngClass]="{ active: section === 'history' }" (click)="setSection('history')">History</button>
      </section>

      <ng-container *ngIf="section === 'catalog'">
        <section class="toolbar-card">
          <input type="search" [(ngModel)]="productSearch" (keydown.enter)="applyProductFilters()" placeholder="Search product key or product name" />
          <select [(ngModel)]="selectedProductCategoryId" (ngModelChange)="applyProductFilters()">
            <option [ngValue]="null">All Categories</option>
            <option *ngFor="let category of categories" [ngValue]="category.id">{{ category.name }}</option>
          </select>
          <select [(ngModel)]="selectedProductSupermarketId" (ngModelChange)="applyProductFilters()">
            <option [ngValue]="null">All Stores</option>
            <option *ngFor="let supermarket of supermarkets" [ngValue]="supermarket.id">{{ supermarket.name }}</option>
          </select>
          <button type="button" class="primary-btn" (click)="applyProductFilters()">Search</button>
          <button type="button" class="ghost-btn" (click)="resetProductFilters()">Reset</button>
          <button type="button" class="ghost-btn" [disabled]="isExporting" (click)="downloadProductsXlsx()">
            {{ isExporting ? 'Preparing XLSX...' : 'Download as XLSX' }}
          </button>
        </section>

        <p *ngIf="!loadingProducts && exportErrorMsg" class="error-msg">{{ exportErrorMsg }}</p>

        <section *ngIf="loadingProducts" class="state-card">
          <h2>Loading current products</h2>
          <p>Fetching latest catalog entries from all stores.</p>
        </section>

        <p *ngIf="!loadingProducts && productsErrorMsg" class="error-msg">{{ productsErrorMsg }}</p>

        <section *ngIf="!loadingProducts && productsPage as products" class="table-card">
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Name</th>
                <th>Price</th>
                <th>Category</th>
                <th>Store</th>
                <th>Stock</th>
                <th>Availability</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let product of products.items">
                <td>{{ product.productKey || 'N/A' }}</td>
                <td>
                  <strong>{{ product.name }}</strong>
                  <small *ngIf="product.unit">Unit: {{ product.unit }}</small>
                </td>
                <td>{{ product.price | number:'1.2-2' }} EUR</td>
                <td>{{ product.category?.name || product.categoryId }}</td>
                <td>{{ product.supermarket?.name || product.supermarketId }}</td>
                <td>{{ product.stock || 'N/A' }}</td>
                <td>{{ product.isAvailable ? 'Available' : 'Unavailable' }}</td>
              </tr>
            </tbody>
          </table>
          <div class="pager-row">
            <button type="button" class="ghost-btn" [disabled]="products.page <= 1" (click)="goToProductsPage(products.page - 1)">Previous</button>
            <span>Page {{ products.page }} / {{ totalPages(products) }}</span>
            <button type="button" class="ghost-btn" [disabled]="products.page >= totalPages(products)" (click)="goToProductsPage(products.page + 1)">Next</button>
          </div>
        </section>

        <section *ngIf="!loadingProducts && (!productsPage || productsPage.items.length === 0)" class="state-card">
          <h2>No products found</h2>
          <p>Try adjusting filters or upload a new product sheet.</p>
        </section>
      </ng-container>

      <ng-container *ngIf="section === 'uploads'">
        <section class="toolbar-card compact-toolbar">
          <select [(ngModel)]="selectedUploadStoreSlug" (ngModelChange)="applyUploadFilters()">
            <option value="">All Stores</option>
            <option *ngFor="let supermarket of supermarkets" [value]="supermarket.slug">{{ supermarket.name }}</option>
          </select>
          <button type="button" class="primary-btn" (click)="applyUploadFilters()">Filter Uploads</button>
          <button type="button" class="ghost-btn" (click)="resetUploadFilters()">Reset</button>
        </section>

        <section *ngIf="loadingUploads" class="state-card">
          <h2>Loading upload runs</h2>
          <p>Fetching import status and audit details.</p>
        </section>

        <p *ngIf="!loadingUploads && uploadsErrorMsg" class="error-msg">{{ uploadsErrorMsg }}</p>

        <section *ngIf="!loadingUploads && uploadsPage as uploads" class="table-card">
          <table>
            <thead>
              <tr>
                <th>Uploaded</th>
                <th>Store</th>
                <th>File</th>
                <th>Status</th>
                <th>Inserted</th>
                <th>Updated</th>
                <th>Deactivated</th>
                <th>By</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let upload of uploads.items">
                <td>{{ upload.uploadedAt | date:'medium' }}</td>
                <td>{{ upload.storeSlug }}</td>
                <td>
                  <a *ngIf="upload.storedFileUrl" [href]="upload.storedFileUrl" target="_blank" rel="noreferrer">{{ upload.originalFileName }}</a>
                  <span *ngIf="!upload.storedFileUrl">{{ upload.originalFileName }}</span>
                  <small *ngIf="upload.completedAt">Completed {{ upload.completedAt | date:'short' }}</small>
                </td>
                <td>
                  <strong>{{ upload.status }}</strong>
                  <small *ngIf="upload.errorMessage">{{ upload.errorMessage }}</small>
                </td>
                <td>{{ upload.insertedCount }}</td>
                <td>{{ upload.updatedCount }}</td>
                <td>{{ upload.deactivatedCount }}</td>
                <td>{{ upload.uploadedByName || 'Unknown' }}</td>
              </tr>
            </tbody>
          </table>
          <div class="pager-row">
            <button type="button" class="ghost-btn" [disabled]="uploads.page <= 1" (click)="goToUploadsPage(uploads.page - 1)">Previous</button>
            <span>Page {{ uploads.page }} / {{ totalPages(uploads) }}</span>
            <button type="button" class="ghost-btn" [disabled]="uploads.page >= totalPages(uploads)" (click)="goToUploadsPage(uploads.page + 1)">Next</button>
          </div>
        </section>

        <section *ngIf="!loadingUploads && (!uploadsPage || uploadsPage.items.length === 0)" class="state-card">
          <h2>No uploads found</h2>
          <p>Upload a new sheet or change the selected store filter.</p>
        </section>
      </ng-container>

      <ng-container *ngIf="section === 'history'">
        <section class="toolbar-card">
          <input type="search" [(ngModel)]="historySearch" (keydown.enter)="applyHistoryFilters()" placeholder="Search product key or product name" />
          <select [(ngModel)]="selectedHistorySupermarketId" (ngModelChange)="applyHistoryFilters()">
            <option [ngValue]="null">All Stores</option>
            <option *ngFor="let supermarket of supermarkets" [ngValue]="supermarket.id">{{ supermarket.name }}</option>
          </select>
          <button type="button" class="primary-btn" (click)="applyHistoryFilters()">Search History</button>
          <button type="button" class="ghost-btn" (click)="resetHistoryFilters()">Reset</button>
        </section>

        <section *ngIf="loadingHistory" class="state-card">
          <h2>Loading history rows</h2>
          <p>Fetching previous product versions and deactivations.</p>
        </section>

        <p *ngIf="!loadingHistory && historyErrorMsg" class="error-msg">{{ historyErrorMsg }}</p>

        <section *ngIf="!loadingHistory && historyPage as history" class="table-card">
          <table>
            <thead>
              <tr>
                <th>Changed</th>
                <th>Type</th>
                <th>Key</th>
                <th>Product</th>
                <th>Price</th>
                <th>Store</th>
                <th>By</th>
                <th>Run</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let entry of history.items">
                <td>{{ entry.changedAt | date:'medium' }}</td>
                <td>{{ entry.changeType }}</td>
                <td>{{ entry.productKey || 'N/A' }}</td>
                <td>
                  <strong>{{ entry.name }}</strong>
                  <small>{{ entry.category?.name || entry.categoryId }}</small>
                </td>
                <td>{{ entry.price | number:'1.2-2' }} EUR</td>
                <td>{{ entry.supermarket?.name || entry.supermarketId }}</td>
                <td>{{ entry.changedByName || 'Unknown' }}</td>
                <td>{{ entry.importRunId || 'Manual' }}</td>
              </tr>
            </tbody>
          </table>
          <div class="pager-row">
            <button type="button" class="ghost-btn" [disabled]="history.page <= 1" (click)="goToHistoryPage(history.page - 1)">Previous</button>
            <span>Page {{ history.page }} / {{ totalPages(history) }}</span>
            <button type="button" class="ghost-btn" [disabled]="history.page >= totalPages(history)" (click)="goToHistoryPage(history.page + 1)">Next</button>
          </div>
        </section>

        <section *ngIf="!loadingHistory && (!historyPage || historyPage.items.length === 0)" class="state-card">
          <h2>No history records found</h2>
          <p>History appears here after uploads change or deactivate catalog entries.</p>
        </section>
      </ng-container>
    </section>
  `,
  styles: [`
    .page-shell {
      max-width: 1200px;
      margin: 0 auto;
      padding: 2rem 1.25rem 3rem;
      display: grid;
      gap: 1rem;
    }
    .hero-card,
    .upload-card,
    .summary-card,
    .section-nav,
    .toolbar-card,
    .state-card,
    .table-card {
      background: rgba(255, 255, 255, 0.96);
      border: 1px solid #dde6df;
      border-radius: 24px;
      box-shadow: 0 18px 36px rgba(25, 53, 41, 0.08);
    }
    .hero-card,
    .upload-card,
    .toolbar-card,
    .state-card,
    .table-card {
      padding: 1.25rem;
    }
    .hero-card {
      display: flex;
      justify-content: space-between;
      gap: 1.25rem;
      align-items: end;
    }
    .hero-actions {
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .eyebrow {
      display: inline-block;
      margin-bottom: 0.35rem;
      font-size: 0.75rem;
      letter-spacing: 0.08em;
      text-transform: uppercase;
      color: #4f7d66;
      font-weight: 700;
    }
    h1,
    h2 {
      margin: 0;
      color: #1a3f2b;
    }
    p {
      margin: 0.35rem 0 0;
      color: #4e6557;
    }
    .upload-row {
      margin-top: 0.75rem;
      display: flex;
      gap: 0.75rem;
      align-items: center;
      flex-wrap: wrap;
    }
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
      gap: 0.75rem;
    }
    .summary-card {
      padding: 1rem;
      display: grid;
      gap: 0.25rem;
    }
    .summary-card strong {
      font-size: 1.35rem;
      color: #174d2b;
    }
    .summary-card span {
      color: #4e6557;
      font-size: 0.9rem;
    }
    .section-nav {
      padding: 0.5rem;
      display: flex;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
    .section-link,
    .primary-btn,
    .ghost-btn {
      border: none;
      border-radius: 999px;
      padding: 0.75rem 1rem;
      font-weight: 700;
      cursor: pointer;
      text-decoration: none;
      font: inherit;
    }
    .section-link {
      background: transparent;
      color: #335342;
    }
    .section-link.active,
    .primary-btn {
      background: #174d2b;
      color: #fff;
    }
    .ghost-btn {
      background: #eef3ef;
      color: #264636;
    }
    .toolbar-card {
      display: grid;
      gap: 0.75rem;
      grid-template-columns: 2fr repeat(2, 1fr) auto auto auto;
    }
    .compact-toolbar {
      grid-template-columns: minmax(220px, 320px) auto auto;
    }
    input,
    select {
      border: 1px solid #cad6cc;
      border-radius: 12px;
      padding: 0.75rem 0.85rem;
      font: inherit;
      width: 100%;
      background: #fff;
    }
    .success-msg,
    .error-msg {
      margin: 0;
      padding: 0.85rem 1rem;
      border-radius: 14px;
    }
    .success-msg {
      background: #edf8ef;
      color: #175c2d;
      border: 1px solid #cfe7d3;
    }
    .error-msg {
      background: #fff1f0;
      color: #9a2f27;
      border: 1px solid #f1c8c4;
    }
    .table-card {
      overflow: auto;
    }
    table {
      width: 100%;
      border-collapse: collapse;
      min-width: 960px;
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
    td strong,
    td small {
      display: block;
      line-height: 1.4;
    }
    .pager-row {
      display: flex;
      justify-content: flex-end;
      align-items: center;
      gap: 0.75rem;
      padding-top: 1rem;
    }
    @media (max-width: 960px) {
      .hero-card {
        flex-direction: column;
        align-items: start;
      }
      .toolbar-card,
      .compact-toolbar {
        grid-template-columns: 1fr;
      }
      .summary-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class ProductManagementComponent implements OnInit {
  private readonly productManagementService = inject(ProductManagementService);
  private readonly productService = inject(ProductService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly templateUrl = '/templates/rewe-product-upload-template.xlsx';

  categories: Category[] = [];
  supermarkets: Supermarket[] = [];

  section: ProductManagementSection = 'catalog';

  productSearch = '';
  historySearch = '';
  selectedProductCategoryId: number | null = null;
  selectedProductSupermarketId: number | null = null;
  selectedHistorySupermarketId: number | null = null;
  selectedUploadStoreSlug = '';

  productsPage: PagedResult<ManagedProduct> | null = null;
  uploadsPage: PagedResult<ProductUploadRun> | null = null;
  historyPage: PagedResult<ProductHistoryEntry> | null = null;

  loadingProducts = true;
  loadingUploads = true;
  loadingHistory = true;
  uploading = false;

  selectedFile: File | null = null;
  selectedFileName = '';
  uploadMessage = '';
  uploadErrorMsg = '';
  exportErrorMsg = '';
  productsErrorMsg = '';
  uploadsErrorMsg = '';
  historyErrorMsg = '';
  isExporting = false;

  private productsRequestId = 0;
  private uploadsRequestId = 0;
  private historyRequestId = 0;

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
    this.loadUploads();
    this.loadHistory();
  }

  setSection(section: ProductManagementSection): void {
    this.section = section;
  }

  totalPages(page: PagedResult<unknown> | null): number {
    if (!page) {
      return 1;
    }

    return Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  }

  applyProductFilters(): void {
    this.loadProducts(1);
  }

  resetProductFilters(): void {
    this.productSearch = '';
    this.selectedProductCategoryId = null;
    this.selectedProductSupermarketId = null;
    this.loadProducts(1);
  }

  goToProductsPage(page: number): void {
    this.loadProducts(page);
  }

  applyUploadFilters(): void {
    this.loadUploads(1);
  }

  resetUploadFilters(): void {
    this.selectedUploadStoreSlug = '';
    this.loadUploads(1);
  }

  goToUploadsPage(page: number): void {
    this.loadUploads(page);
  }

  applyHistoryFilters(): void {
    this.loadHistory(1);
  }

  resetHistoryFilters(): void {
    this.historySearch = '';
    this.selectedHistorySupermarketId = null;
    this.loadHistory(1);
  }

  goToHistoryPage(page: number): void {
    this.loadHistory(page);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.item(0) ?? null;
    this.selectedFileName = this.selectedFile?.name ?? '';
    this.uploadMessage = '';
    this.uploadErrorMsg = '';
  }

  uploadSheet(): void {
    if (this.uploading) {
      return;
    }

    if (!this.selectedFile) {
      this.uploadErrorMsg = 'Select an Excel .xlsx file before uploading.';
      this.cdr.detectChanges();
      return;
    }

    this.uploading = true;
    this.uploadMessage = '';
    this.uploadErrorMsg = '';

    this.productManagementService.uploadSheet(this.selectedFile).subscribe({
      next: upload => {
        this.uploading = false;
        this.uploadMessage = `Upload ${upload.originalFileName} completed for ${upload.storeSlug}.`;
        this.selectedFile = null;
        this.selectedFileName = '';
        this.section = 'uploads';
        this.loadProducts(1);
        this.loadUploads(1);
        this.loadHistory(1);
        this.cdr.detectChanges();
      },
      error: err => {
        this.uploading = false;
        this.uploadErrorMsg = err?.error?.message || 'Product sheet upload failed.';
        this.cdr.detectChanges();
      }
    });
  }

  async downloadProductsXlsx(): Promise<void> {
    // TODO: Re-enable after installing xlsx package
    // if (this.isExporting) {
    //   return;
    // }

    // this.isExporting = true;
    // this.exportErrorMsg = '';

    // try {
    //   const pageSize = 200;
    //   const baseFilters = {
    //     search: this.productSearch || undefined,
    //     categoryId: this.selectedProductCategoryId ?? undefined,
    //     supermarketId: this.selectedProductSupermarketId ?? undefined,
    //     pageSize
    //   };

    //   const firstPage = await firstValueFrom(this.productManagementService.getProducts({
    //     ...baseFilters,
    //     page: 1
    //   }));

    //   const allItems = [...firstPage.items];
    //   const totalPages = Math.max(1, Math.ceil(firstPage.totalCount / firstPage.pageSize));

    //   for (let page = 2; page <= totalPages; page++) {
    //     const nextPage = await firstValueFrom(this.productManagementService.getProducts({
    //       ...baseFilters,
    //       page
    //     }));

    //     allItems.push(...nextPage.items);
    //   }

    //   if (!allItems.length) {
    //     return;
    //   }

    // const headers = [
    //   'product_key',
    //   'store_slug',
    //   'product_name',
    //   'category_slug',
    //   'price',
    //   'image_url',
    //   'unit',
    //   'stock',
    //   'is_active'
    // ];

    //   const rows = allItems.map(product => ({
    //   product_key: product.productKey ?? '',
    //   store_slug: product.supermarket?.slug ?? this.supermarkets.find(x => x.id === product.supermarketId)?.slug ?? '',
    //   product_name: product.name,
    //   category_slug: product.category?.slug ?? this.categories.find(x => x.id === product.categoryId)?.slug ?? '',
    //   price: product.price,
    //   image_url: product.imageUrl ?? '',
    //   unit: product.unit ?? '',
    //   stock: product.stock ?? '',
    //   is_active: product.isAvailable ? 'TRUE' : 'FALSE'
    // }));

    //   const worksheet = XLSX.utils.json_to_sheet(rows, { header: headers });
    //   XLSX.utils.sheet_add_aoa(worksheet, [headers], { origin: 'A1' });

    //   const notesRows = [
    //     ['Product Upload Template'],
    //     [],
    //     ['Required columns'],
    //     ['product_key: 8-character UID. Pre-filled with an Excel formula you can keep or replace with a stable key.'],
    //     ['store_slug: fixed to rewe in this template. One upload file should contain only one store.'],
    //     ['product_name: display name used in the live catalog.'],
    //     ['category_slug: must match a seeded backend category slug.'],
    //     ['price: decimal price in EUR.'],
    //     [],
    //     ['Optional columns'],
    //     ['image_url: optional. Storefront only shows products that have an image URL.'],
    //     ['unit: optional display unit such as 1 kg or 500 ml.'],
    //     ['stock: optional non-negative whole number.'],
    //     ['is_active: optional TRUE/FALSE flag; defaults to TRUE in empty rows.'],
    //     []
    //   ];
    //   const notesSheet = XLSX.utils.aoa_to_sheet(notesRows);
    //   const randomUid = Math.floor(Math.random() * 0x100000000).toString(16).toUpperCase().padStart(8, '0');
    //   XLSX.utils.sheet_add_aoa(
    //     notesSheet,
    //     [
    //       ['=UPPER(DEC2HEX(RANDBETWEEN(0;4294967295);8))'],
    //       [randomUid]
    //     ],
    //     { origin: 'A15' }
    //   );

    // const workbook = XLSX.utils.book_new();
    // XLSX.utils.book_append_sheet(workbook, worksheet, 'products');
    //   XLSX.utils.book_append_sheet(workbook, notesSheet, 'notes');
    //   XLSX.writeFile(workbook, 'products-upload-ready.xlsx');
    // } catch {
    //   this.exportErrorMsg = 'Failed to export all products. Please try again.';
    // } finally {
    //   this.isExporting = false;
    //   this.cdr.detectChanges();
    // }
  }

  private loadProducts(page = this.productsPage?.page ?? 1): void {
    const requestId = ++this.productsRequestId;
    this.loadingProducts = true;
    this.productsErrorMsg = '';

    this.productManagementService.getProducts({
      search: this.productSearch || undefined,
      categoryId: this.selectedProductCategoryId ?? undefined,
      supermarketId: this.selectedProductSupermarketId ?? undefined,
      page,
      pageSize: 50
    }).subscribe({
      next: response => {
        if (requestId !== this.productsRequestId) {
          return;
        }

        this.productsPage = response;
        this.loadingProducts = false;
        this.cdr.detectChanges();
      },
      error: err => {
        if (requestId !== this.productsRequestId) {
          return;
        }

        this.productsErrorMsg = err?.error?.message || 'Failed to load current products.';
        this.loadingProducts = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadUploads(page = this.uploadsPage?.page ?? 1): void {
    const requestId = ++this.uploadsRequestId;
    this.loadingUploads = true;
    this.uploadsErrorMsg = '';

    this.productManagementService.getUploads({
      storeSlug: this.selectedUploadStoreSlug || undefined,
      page,
      pageSize: 25
    }).subscribe({
      next: response => {
        if (requestId !== this.uploadsRequestId) {
          return;
        }

        this.uploadsPage = response;
        this.loadingUploads = false;
        this.cdr.detectChanges();
      },
      error: err => {
        if (requestId !== this.uploadsRequestId) {
          return;
        }

        this.uploadsErrorMsg = err?.error?.message || 'Failed to load upload runs.';
        this.loadingUploads = false;
        this.cdr.detectChanges();
      }
    });
  }

  private loadHistory(page = this.historyPage?.page ?? 1): void {
    const requestId = ++this.historyRequestId;
    this.loadingHistory = true;
    this.historyErrorMsg = '';

    this.productManagementService.getHistory({
      search: this.historySearch || undefined,
      supermarketId: this.selectedHistorySupermarketId ?? undefined,
      page,
      pageSize: 25
    }).subscribe({
      next: response => {
        if (requestId !== this.historyRequestId) {
          return;
        }

        this.historyPage = response;
        this.loadingHistory = false;
        this.cdr.detectChanges();
      },
      error: err => {
        if (requestId !== this.historyRequestId) {
          return;
        }

        this.historyErrorMsg = err?.error?.message || 'Failed to load product history.';
        this.loadingHistory = false;
        this.cdr.detectChanges();
      }
    });
  }
}
