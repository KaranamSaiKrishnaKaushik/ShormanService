export interface Category {
  id: number;
  name: string;
  slug: string;
  icon?: string;
}

export interface Supermarket {
  id: number;
  name: string;
  slug: string;
  logoUrl?: string;
  color?: string;
}

export interface Product {
  id: number;
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  categoryId: number;
  category?: Category;
  supermarketId: number;
  supermarket?: Supermarket;
  unit?: string;
  stock?: number;
  isAvailable: boolean;
}

export interface ProductPage {
  items: Product[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UpdateProductRequest {
  name: string;
  description?: string;
  price: number;
  imageUrl?: string;
  categoryId: number;
  supermarketId: number;
  unit?: string;
  stock?: number;
  isAvailable: boolean;
}

export interface ProductFilters {
  search?: string;
  categoryId?: number;
  supermarketId?: number;
  supermarketIds?: number[];
  supermarketSlug?: string;
  categorySlug?: string;
  page?: number;
  pageSize?: number;
}
