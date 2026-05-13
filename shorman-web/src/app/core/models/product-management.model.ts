import { Category, Supermarket } from './product.model';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ManagedProduct {
  id: number;
  productKey?: string;
  name: string;
  price: number;
  imageUrl?: string;
  categoryId: number;
  category?: Category;
  supermarketId: number;
  supermarket?: Supermarket;
  unit?: string;
  stock?: number;
  isAvailable: boolean;
  dataSource: string;
  updatedAt?: string;
}

export interface ProductUploadRun {
  id: number;
  storeSlug: string;
  originalFileName: string;
  storedFileUrl: string;
  status: string;
  totalRows: number;
  insertedCount: number;
  updatedCount: number;
  unchangedCount: number;
  deactivatedCount: number;
  uploadedAt: string;
  completedAt?: string;
  uploadedByName?: string;
  errorMessage?: string;
}

export interface ProductHistoryEntry {
  id: number;
  productId?: number;
  productKey?: string;
  name: string;
  price: number;
  imageUrl?: string;
  categoryId: number;
  category?: Category;
  supermarketId: number;
  supermarket?: Supermarket;
  unit?: string;
  stock?: number;
  isAvailable: boolean;
  dataSource: string;
  changeType: string;
  changedAt: string;
  changedByName?: string;
  importRunId?: number;
}

export interface ManagedProductFilters {
  search?: string;
  categoryId?: number;
  supermarketId?: number;
  page?: number;
  pageSize?: number;
}

export interface ProductHistoryFilters {
  search?: string;
  supermarketId?: number;
  page?: number;
  pageSize?: number;
}

export interface ProductUploadRunFilters {
  storeSlug?: string;
  page?: number;
  pageSize?: number;
}