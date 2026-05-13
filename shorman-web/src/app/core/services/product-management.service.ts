import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, catchError, retry, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ManagedProduct,
  ManagedProductFilters,
  PagedResult,
  ProductHistoryEntry,
  ProductHistoryFilters,
  ProductUploadRun,
  ProductUploadRunFilters
} from '../models/product-management.model';

@Injectable({ providedIn: 'root' })
export class ProductManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/product-management`;

  getProducts(filters: ManagedProductFilters): Observable<PagedResult<ManagedProduct>> {
    return this.http.get<PagedResult<ManagedProduct>>(`${this.baseUrl}/products`, {
      params: this.buildParams(filters)
    }).pipe(
      retry({ count: 1, delay: 300 }),
      catchError(error => throwError(() => error))
    );
  }

  getHistory(filters: ProductHistoryFilters): Observable<PagedResult<ProductHistoryEntry>> {
    return this.http.get<PagedResult<ProductHistoryEntry>>(`${this.baseUrl}/history`, {
      params: this.buildParams(filters)
    }).pipe(
      retry({ count: 1, delay: 300 }),
      catchError(error => throwError(() => error))
    );
  }

  getUploads(filters: ProductUploadRunFilters): Observable<PagedResult<ProductUploadRun>> {
    return this.http.get<PagedResult<ProductUploadRun>>(`${this.baseUrl}/uploads`, {
      params: this.buildParams(filters)
    }).pipe(
      retry({ count: 1, delay: 300 }),
      catchError(error => throwError(() => error))
    );
  }

  uploadSheet(file: File): Observable<ProductUploadRun> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<ProductUploadRun>(`${this.baseUrl}/upload`, formData)
      .pipe(catchError(error => throwError(() => error)));
  }

  private buildParams(filters: object): HttpParams {
    let params = new HttpParams();

    Object.entries(filters as Record<string, unknown>).forEach(([key, value]) => {
      if (value !== undefined && value !== null && `${value}`.trim() !== '') {
        params = params.set(key, `${value}`);
      }
    });

    return params;
  }
}