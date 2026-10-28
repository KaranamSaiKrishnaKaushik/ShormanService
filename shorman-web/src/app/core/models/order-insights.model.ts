export type InsightsRange = '30d' | '6m' | '1y' | 'all';

export interface OrderInsightsStoreSpend {
  store: string;
  spend: number;
}

export interface OrderInsightsMonthlyStoreSpend {
  monthKey: string;
  monthLabel: string;
  stores: OrderInsightsStoreSpend[];
}

export interface OrderInsightsCategory {
  category: string;
  segment: 'grocery' | 'drugstore' | string;
  spend: number;
  orderCount: number;
}

export interface OrderInsightsHeatmapCell {
  store: string;
  category: string;
  segment: 'grocery' | 'drugstore' | string;
  spend: number;
}

export interface OrderInsightsReorderedProduct {
  productId: number;
  productName: string;
  productImageUrl?: string;
  store: string;
  quantity: number;
  repeatCount: number;
  spend: number;
}

export interface OrderInsights {
  totalSpend: number;
  totalOrders: number;
  averageBasket: number;
  previousTotalSpend: number;
  previousTotalOrders: number;
  previousAverageBasket: number;
  favoriteStore: string;
  favoriteStoreSpend: number;
  favoriteStoreShare: number;
  favoriteCategory: string;
  favoriteCategorySpend: number;
  favoriteCategoryShare: number;
  groceryShare: number;
  drugstoreShare: number;
  storeSpend: OrderInsightsStoreSpend[];
  monthlySpend: OrderInsightsMonthlyStoreSpend[];
  topCategories: OrderInsightsCategory[];
  heatmap: OrderInsightsHeatmapCell[];
  topReorderedProducts: OrderInsightsReorderedProduct[];
}
