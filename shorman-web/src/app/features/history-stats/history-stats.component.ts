import { AfterViewInit, ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Chart, ChartConfiguration, Plugin } from 'chart.js/auto';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { InsightsRange, OrderInsights } from '../../core/models/order-insights.model';
import { OrderInsightsService } from '../../core/services/order-insights.service';
import { LanguageService } from '../../core/services/language.service';

type Segment = 'grocery' | 'drugstore';

interface CategoryStat {
  name: string;
  spend: number;
  orderCount: number;
  segment: Segment;
}

interface ReorderedProduct {
  name: string;
  store: string;
  quantity: number;
  repeatCount: number;
  spend: number;
  imageUrl?: string;
}

interface HeatmapRow {
  store: string;
  values: Record<string, number>;
}

interface HistoryStatsProfile {
  email: string;
  monthlyByStore: Record<string, number[]>;
  categories: CategoryStat[];
  reorderedProducts: ReorderedProduct[];
  heatmapGroceries: HeatmapRow[];
  heatmapDrugstore: HeatmapRow[];
}

@Component({
  selector: 'app-history-stats',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  templateUrl: './history-stats.component.html',
  styleUrl: './history-stats.component.scss'
})
export class HistoryStatsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('donutCanvas') donutCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('areaCanvas') areaCanvas?: ElementRef<HTMLCanvasElement>;
  @ViewChild('categoryCanvas') categoryCanvas?: ElementRef<HTMLCanvasElement>;

  private readonly auth = inject(AuthService);
  private readonly insightsService = inject(OrderInsightsService);
  private readonly translate = inject(TranslateService);
  private readonly languageService = inject(LanguageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly rangeOptions: Array<{ key: InsightsRange; labelKey: string }> = [
    { key: '30d', labelKey: 'history.range.30d' },
    { key: '6m', labelKey: 'history.range.6m' },
    { key: '1y', labelKey: 'history.range.1y' },
    { key: 'all', labelKey: 'history.range.all' }
  ];

  readonly storeColors: Record<string, string> = {
    LIDL: '#0EA5E9',
    ALDI: '#1E3A8A',
    REWE: '#DC2626',
    PENNY: '#F59E0B',
    EDEKA: '#10B981',
    dm: '#475569',
    Rossmann: '#BE185D'
  };

  selectedRange: InsightsRange = '1y';
  currentEmail = '';
  currentName = 'Customer';
  currentLanguage = 'en';

  loading = false;
  errorMsg = '';

  months: string[] = [];
  spendByStore: Array<{ store: string; spend: number }> = [];
  categories: CategoryStat[] = [];
  reorderedProducts: ReorderedProduct[] = [];
  heatmapGroceries: HeatmapRow[] = [];
  heatmapDrugstore: HeatmapRow[] = [];
  groceryCategoryAxis: string[] = [];
  drugstoreCategoryAxis: string[] = [];

  totalSpend = 0;
  totalOrders = 0;
  averageBasket = 0;
  previousTotalSpend = 0;
  previousTotalOrders = 0;
  previousAverageBasket = 0;
  favoriteStore = 'N/A';
  favoriteStoreSpend = 0;
  favoriteStoreShare = 0;
  favoriteCategory = 'N/A';
  favoriteCategorySpend = 0;
  favoriteCategoryShare = 0;
  groceryShare = 0;
  drugstoreShare = 0;

  private monthlyByStore: Record<string, number[]> = {};
  private donutChart?: Chart;
  private areaChart?: Chart;
  private categoryChart?: Chart;
  private viewReady = false;
  private userSub?: Subscription;
  private languageSub?: Subscription;

  private readonly donutCenterPlugin: Plugin<'doughnut'> = {
    id: 'donut-center-text',
    afterDraw: chart => {
      const ctx = chart.ctx;
      const area = chart.chartArea;
      if (!area) {
        return;
      }

      const cx = (area.left + area.right) / 2;
      const cy = (area.top + area.bottom) / 2;
      ctx.save();
      ctx.textAlign = 'center';
      ctx.fillStyle = '#94A3B8';
      ctx.font = '600 11px Segoe UI';
      ctx.fillText(this.translate.instant('history.donut.total'), cx, cy - 12);
      ctx.fillStyle = '#0F172A';
      ctx.font = '700 22px Segoe UI';
      ctx.fillText(`EUR ${this.totalSpend.toFixed(0)}`, cx, cy + 12);
      ctx.restore();
    }
  };

  ngOnInit(): void {
    this.currentLanguage = this.languageService.getCurrentLanguage();
    this.userSub = this.auth.currentUser$.subscribe(user => {
      this.currentEmail = (user?.email ?? '').toLowerCase();
      this.currentName = user?.firstName?.trim() || 'Customer';
      this.loadInsights();
    });

    this.languageSub = this.languageService.currentLanguage$.subscribe(language => {
      this.currentLanguage = language;
      this.months = this.lastMonths(12).slice(Math.max(0, 12 - this.months.length));
      this.cdr.detectChanges();
      this.renderCharts();
    });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderCharts();
  }

  ngOnDestroy(): void {
    this.userSub?.unsubscribe();
    this.languageSub?.unsubscribe();
    this.destroyCharts();
  }

  selectRange(range: InsightsRange): void {
    this.selectedRange = range;
    this.loadInsights();
  }

  profileSubtitle(): string {
    return this.translate.instant('history.subtitle', {
      range: this.translate.instant(`history.range.${this.selectedRange}`),
      orders: this.totalOrders,
      spend: this.totalSpend.toFixed(2)
    });
  }

  comparisonSuffix(): string {
    return this.translate.instant(`history.compare.${this.selectedRange}`);
  }

  spendDeltaPct(): number {
    if (this.previousTotalSpend <= 0) {
      return this.totalSpend > 0 ? 100 : 0;
    }

    return ((this.totalSpend - this.previousTotalSpend) / this.previousTotalSpend) * 100;
  }

  avgBasketDelta(): number {
    return this.averageBasket - this.previousAverageBasket;
  }

  deltaArrow(value: number): string {
    return value >= 0 ? '▲' : '▼';
  }

  deltaClass(value: number): string {
    return value >= 0 ? 'up' : 'down';
  }

  signedDelta(value: number, digits: number): string {
    const abs = Math.abs(value);
    return `${abs.toFixed(digits)}`;
  }

  heatOpacity(value: number, max: number): number {
    if (value <= 0 || max <= 0) {
      return 0;
    }

    return Math.max(0.15, Math.min(0.95, value / max));
  }

  maxHeatValue(rows: HeatmapRow[]): number {
    const values = rows.flatMap(row => Object.values(row.values));
    return values.length ? Math.max(...values) : 1;
  }

  reorderMax(): number {
    return this.reorderedProducts.length ? Math.max(...this.reorderedProducts.map(x => x.repeatCount)) : 1;
  }

  reorderPercent(value: number): number {
    return (value / this.reorderMax()) * 100;
  }

  heatGridCols(count: number): string {
    return `90px repeat(${Math.max(count, 1)}, minmax(0, 1fr))`;
  }

  segmentColor(segment: Segment): string {
    return segment === 'grocery' ? '#047857' : '#BE185D';
  }

  storeColor(store: string): string {
    return this.storeColors[store] ?? '#64748B';
  }

  private loadInsights(): void {
    if (!this.currentEmail) {
      return;
    }

    this.loading = true;
    this.errorMsg = '';

    this.insightsService.getInsights(this.selectedRange).subscribe({
      next: insights => {
        this.loading = false;
        this.applyInsights(insights);
        this.cdr.detectChanges();
        this.renderCharts();
      },
      error: () => {
        this.loading = false;
        this.errorMsg = this.translate.instant('history.state.fallback');
        this.applyMockProfile();
        this.cdr.detectChanges();
        this.renderCharts();
      }
    });
  }

  private applyInsights(insights: OrderInsights): void {
    this.totalSpend = insights.totalSpend;
    this.totalOrders = insights.totalOrders;
    this.averageBasket = insights.averageBasket;
    this.previousTotalSpend = insights.previousTotalSpend;
    this.previousTotalOrders = insights.previousTotalOrders;
    this.previousAverageBasket = insights.previousAverageBasket;

    this.favoriteStore = insights.favoriteStore;
    this.favoriteStoreSpend = insights.favoriteStoreSpend;
    this.favoriteStoreShare = insights.favoriteStoreShare;
    this.favoriteCategory = insights.favoriteCategory;
    this.favoriteCategorySpend = insights.favoriteCategorySpend;
    this.favoriteCategoryShare = insights.favoriteCategoryShare;

    this.groceryShare = insights.groceryShare;
    this.drugstoreShare = insights.drugstoreShare;

    this.spendByStore = [...insights.storeSpend].sort((a, b) => b.spend - a.spend);
    this.months = insights.monthlySpend.map(item => item.monthLabel);

    const stores = this.spendByStore.map(item => item.store);
    this.monthlyByStore = stores.reduce<Record<string, number[]>>((acc, store) => {
      acc[store] = insights.monthlySpend.map(month => month.stores.find(x => x.store === store)?.spend ?? 0);
      return acc;
    }, {});

    this.categories = insights.topCategories.map(item => ({
      name: item.category,
      spend: item.spend,
      orderCount: item.orderCount,
      segment: item.segment === 'drugstore' ? 'drugstore' : 'grocery'
    }));

    this.reorderedProducts = insights.topReorderedProducts.map(item => ({
      name: item.productName,
      store: item.store,
      quantity: item.quantity,
      repeatCount: item.repeatCount,
      spend: item.spend,
      imageUrl: item.productImageUrl
    }));

    this.applyHeatmap(insights);
  }

  private applyHeatmap(insights: OrderInsights): void {
    const groceryCells = insights.heatmap.filter(cell => cell.segment === 'grocery');
    const drugCells = insights.heatmap.filter(cell => cell.segment === 'drugstore');

    this.groceryCategoryAxis = this.topCategoriesByCells(groceryCells, 6);
    this.drugstoreCategoryAxis = this.topCategoriesByCells(drugCells, 4);

    this.heatmapGroceries = this.buildHeatRows(groceryCells, this.groceryCategoryAxis);
    this.heatmapDrugstore = this.buildHeatRows(drugCells, this.drugstoreCategoryAxis);
  }

  private topCategoriesByCells(cells: OrderInsights['heatmap'], take: number): string[] {
    const totals = cells.reduce<Record<string, number>>((acc, cell) => {
      acc[cell.category] = (acc[cell.category] ?? 0) + cell.spend;
      return acc;
    }, {});

    return Object.entries(totals)
      .sort((a, b) => b[1] - a[1])
      .slice(0, take)
      .map(entry => entry[0]);
  }

  private buildHeatRows(cells: OrderInsights['heatmap'], categories: string[]): HeatmapRow[] {
    const stores = cells
      .reduce<Record<string, number>>((acc, cell) => {
        acc[cell.store] = (acc[cell.store] ?? 0) + cell.spend;
        return acc;
      }, {});

    return Object.entries(stores)
      .sort((a, b) => b[1] - a[1])
      .map(([store]) => {
        const values = categories.reduce<Record<string, number>>((map, category) => {
          map[category] = cells
            .filter(cell => cell.store === store && cell.category === category)
            .reduce((sum, cell) => sum + cell.spend, 0);
          return map;
        }, {});

        return { store, values };
      });
  }

  private renderCharts(): void {
    if (!this.viewReady || !this.donutCanvas || !this.areaCanvas || !this.categoryCanvas) {
      return;
    }

    this.destroyCharts();
    this.renderDonut();
    this.renderArea();
    this.renderCategory();
  }

  private renderDonut(): void {
    const canvas = this.donutCanvas?.nativeElement;
    if (!canvas) {
      return;
    }

    const labels = this.spendByStore.map(x => x.store);
    const data = this.spendByStore.map(x => x.spend);
    const colors = this.spendByStore.map(x => this.storeColor(x.store));

    const config: ChartConfiguration<'doughnut'> = {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{ data, backgroundColor: colors, borderColor: '#FFFFFF', borderWidth: 2 }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '68%',
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: ctx => {
                const value = Number(ctx.raw ?? 0);
                const pct = this.totalSpend > 0 ? Math.round((value / this.totalSpend) * 100) : 0;
                return `${ctx.label}: EUR ${value.toFixed(2)} (${pct}%)`;
              }
            }
          }
        }
      },
      plugins: [this.donutCenterPlugin]
    };

    this.donutChart = new Chart(canvas, config);
  }

  private renderArea(): void {
    const canvas = this.areaCanvas?.nativeElement;
    if (!canvas) {
      return;
    }

    const datasets = this.spendByStore.map(item => {
      const monthly = this.monthlyByStore[item.store] ?? [];
      return {
        label: item.store,
        data: monthly,
        borderColor: this.storeColor(item.store),
        backgroundColor: this.hexToRgba(this.storeColor(item.store), 0.8),
        fill: true,
        pointRadius: monthly.length <= 2 ? 3 : 0,
        borderWidth: monthly.length <= 2 ? 2 : 0,
        tension: 0.35
      };
    });

    const config: ChartConfiguration<'line'> = {
      type: 'line',
      data: { labels: this.months, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        scales: {
          x: { stacked: true, grid: { display: false } },
          y: {
            stacked: true,
            beginAtZero: true,
            ticks: { callback: value => `EUR ${value}` }
          }
        },
        plugins: { legend: { display: false } }
      }
    };

    this.areaChart = new Chart(canvas, config);
  }

  private renderCategory(): void {
    const canvas = this.categoryCanvas?.nativeElement;
    if (!canvas) {
      return;
    }

    const labels = this.categories.map(x => x.name);
    const data = this.categories.map(x => x.spend);
    const colors = this.categories.map(x => this.segmentColor(x.segment));

    const config: ChartConfiguration<'bar'> = {
      type: 'bar',
      data: {
        labels,
        datasets: [{ data, backgroundColor: colors, borderRadius: 8 }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        indexAxis: 'y',
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              label: ctx => {
                const category = this.categories[ctx.dataIndex];
                return `EUR ${Number(ctx.raw).toFixed(2)} (${category.orderCount} ${this.translate.instant('history.tooltip.orders')})`;
              }
            }
          }
        },
        scales: {
          x: { beginAtZero: true, ticks: { callback: value => `EUR ${value}` } },
          y: { ticks: { color: '#0F172A' } }
        }
      }
    };

    this.categoryChart = new Chart(canvas, config);
  }

  private destroyCharts(): void {
    this.donutChart?.destroy();
    this.areaChart?.destroy();
    this.categoryChart?.destroy();
    this.donutChart = undefined;
    this.areaChart = undefined;
    this.categoryChart = undefined;
  }

  private hexToRgba(hex: string, alpha: number): string {
    const normalized = hex.replace('#', '');
    const r = Number.parseInt(normalized.substring(0, 2), 16);
    const g = Number.parseInt(normalized.substring(2, 4), 16);
    const b = Number.parseInt(normalized.substring(4, 6), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
  }

  private applyMockProfile(): void {
    const profile = this.getProfiles()[this.currentEmail] ?? this.getProfiles()['demo@shorman.com'];
    const monthSlice = this.selectedRange === '30d' ? 2 : this.selectedRange === '6m' ? 6 : 12;
    const allMonths = this.lastMonths(12);
    const startIndex = Math.max(0, allMonths.length - monthSlice);
    this.months = allMonths.slice(startIndex);

    this.spendByStore = Object.keys(profile.monthlyByStore).map(store => {
      const points = profile.monthlyByStore[store].slice(startIndex);
      return { store, spend: points.reduce((sum, value) => sum + value, 0) };
    }).filter(item => item.spend > 0);

    this.monthlyByStore = Object.keys(profile.monthlyByStore).reduce<Record<string, number[]>>((acc, store) => {
      acc[store] = profile.monthlyByStore[store].slice(startIndex);
      return acc;
    }, {});

    this.categories = [...profile.categories].sort((a, b) => b.spend - a.spend);
    this.reorderedProducts = [...profile.reorderedProducts].sort((a, b) => b.repeatCount - a.repeatCount).slice(0, 10);
    this.heatmapGroceries = profile.heatmapGroceries;
    this.heatmapDrugstore = profile.heatmapDrugstore;
    this.groceryCategoryAxis = this.heatmapGroceries[0] ? Object.keys(this.heatmapGroceries[0].values) : [];
    this.drugstoreCategoryAxis = this.heatmapDrugstore[0] ? Object.keys(this.heatmapDrugstore[0].values) : [];

    this.totalSpend = this.spendByStore.reduce((sum, item) => sum + item.spend, 0);
    this.totalOrders = this.categories.reduce((sum, item) => sum + item.orderCount, 0);
    this.averageBasket = this.totalOrders > 0 ? this.totalSpend / this.totalOrders : 0;

    this.previousTotalSpend = this.totalSpend * 0.9;
    this.previousTotalOrders = Math.max(0, this.totalOrders - 6);
    this.previousAverageBasket = this.averageBasket + 1.2;

    const bestStore = [...this.spendByStore].sort((a, b) => b.spend - a.spend)[0];
    const bestCategory = this.categories[0];

    this.favoriteStore = bestStore?.store ?? 'N/A';
    this.favoriteStoreSpend = bestStore?.spend ?? 0;
    this.favoriteStoreShare = this.totalSpend > 0 ? ((bestStore?.spend ?? 0) / this.totalSpend) * 100 : 0;

    this.favoriteCategory = bestCategory?.name ?? 'N/A';
    this.favoriteCategorySpend = bestCategory?.spend ?? 0;
    this.favoriteCategoryShare = this.totalSpend > 0 ? ((bestCategory?.spend ?? 0) / this.totalSpend) * 100 : 0;

    const grocerySpend = this.spendByStore.filter(x => this.resolveSegment(x.store) === 'grocery').reduce((sum, x) => sum + x.spend, 0);
    const drugstoreSpend = this.spendByStore.filter(x => this.resolveSegment(x.store) === 'drugstore').reduce((sum, x) => sum + x.spend, 0);
    this.groceryShare = this.totalSpend > 0 ? (grocerySpend / this.totalSpend) * 100 : 0;
    this.drugstoreShare = this.totalSpend > 0 ? (drugstoreSpend / this.totalSpend) * 100 : 0;
  }

  private resolveSegment(store: string): Segment {
    const normalized = store.trim().toLowerCase();
    return normalized === 'dm' || normalized === 'rossmann' ? 'drugstore' : 'grocery';
  }

  private lastMonths(count: number): string[] {
    const now = new Date();
    const labels: string[] = [];
    const locale = this.currentLanguage === 'de' ? 'de-DE' : 'en-US';
    for (let i = count - 1; i >= 0; i--) {
      const date = new Date(now.getFullYear(), now.getMonth() - i, 1);
      labels.push(date.toLocaleString(locale, { month: 'short' }));
    }

    return labels;
  }

  private getProfiles(): Record<string, HistoryStatsProfile> {
    return {
      'karanamsaikrishna.kaushik@gmail.com': {
        email: 'karanamsaikrishna.kaushik@gmail.com',
        monthlyByStore: {
          LIDL: [52, 58, 61, 64, 66, 72, 75, 69, 70, 78, 82, 88],
          ALDI: [35, 37, 34, 41, 39, 42, 40, 45, 43, 41, 44, 47],
          REWE: [22, 24, 26, 25, 27, 29, 31, 30, 28, 31, 29, 33],
          PENNY: [10, 12, 11, 10, 13, 14, 12, 15, 16, 15, 17, 18],
          EDEKA: [8, 7, 9, 8, 9, 10, 10, 9, 11, 12, 10, 11],
          dm: [18, 19, 22, 21, 24, 23, 26, 27, 28, 29, 27, 30],
          Rossmann: [10, 11, 10, 12, 11, 13, 14, 12, 13, 14, 15, 16]
        },
        categories: this.baseCategories(1.12),
        reorderedProducts: this.baseReorders('LIDL', 'dm'),
        heatmapGroceries: this.baseHeatGroceries(1.1),
        heatmapDrugstore: this.baseHeatDrugstore(1.15)
      },
      'demo@shorman.com': {
        email: 'demo@shorman.com',
        monthlyByStore: {
          LIDL: [42, 45, 47, 48, 49, 51, 53, 55, 56, 58, 60, 62],
          ALDI: [29, 31, 30, 32, 33, 35, 36, 37, 35, 36, 38, 40],
          REWE: [25, 28, 26, 27, 29, 31, 30, 29, 30, 32, 33, 34],
          PENNY: [9, 10, 11, 11, 10, 12, 12, 13, 13, 14, 14, 15],
          EDEKA: [6, 7, 7, 8, 9, 8, 9, 9, 10, 10, 11, 11],
          dm: [14, 15, 16, 18, 17, 19, 20, 19, 21, 22, 23, 24],
          Rossmann: [8, 9, 9, 10, 11, 10, 11, 12, 12, 13, 14, 14]
        },
        categories: this.baseCategories(1.0),
        reorderedProducts: this.baseReorders('ALDI', 'Rossmann'),
        heatmapGroceries: this.baseHeatGroceries(1.0),
        heatmapDrugstore: this.baseHeatDrugstore(1.0)
      },
      'test@test.com': {
        email: 'test@test.com',
        monthlyByStore: {
          LIDL: [28, 30, 31, 33, 35, 36, 38, 40, 42, 41, 44, 46],
          ALDI: [24, 25, 26, 27, 28, 29, 30, 31, 30, 31, 32, 33],
          REWE: [17, 18, 19, 20, 21, 22, 22, 23, 24, 24, 25, 26],
          PENNY: [6, 7, 7, 8, 8, 9, 10, 9, 10, 10, 11, 11],
          EDEKA: [4, 5, 5, 5, 6, 6, 7, 7, 7, 8, 8, 8],
          dm: [12, 12, 13, 14, 14, 15, 16, 16, 17, 18, 18, 19],
          Rossmann: [7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13]
        },
        categories: this.baseCategories(0.84),
        reorderedProducts: this.baseReorders('REWE', 'dm'),
        heatmapGroceries: this.baseHeatGroceries(0.82),
        heatmapDrugstore: this.baseHeatDrugstore(0.88)
      }
    };
  }

  private baseCategories(multiplier: number): CategoryStat[] {
    const multiply = (value: number) => Number((value * multiplier).toFixed(2));
    return [
      { name: 'Beverages', spend: multiply(198.4), orderCount: Math.round(64 * multiplier), segment: 'grocery' },
      { name: 'Bakery', spend: multiply(176.1), orderCount: Math.round(58 * multiplier), segment: 'grocery' },
      { name: 'Eggs & Dairy', spend: multiply(154.2), orderCount: Math.round(51 * multiplier), segment: 'grocery' },
      { name: 'Fruits & Vegetables', spend: multiply(142.8), orderCount: Math.round(42 * multiplier), segment: 'grocery' },
      { name: 'Snacks', spend: multiply(73.1), orderCount: Math.round(33 * multiplier), segment: 'grocery' },
      { name: 'Skin Care', spend: multiply(87.2), orderCount: Math.round(19 * multiplier), segment: 'drugstore' },
      { name: 'Hair Care', spend: multiply(61.4), orderCount: Math.round(14 * multiplier), segment: 'drugstore' },
      { name: 'Body & Bath', spend: multiply(49.3), orderCount: Math.round(12 * multiplier), segment: 'drugstore' },
      { name: 'Health & Wellness', spend: multiply(38.0), orderCount: Math.round(8 * multiplier), segment: 'drugstore' }
    ];
  }

  private baseReorders(primaryStore: string, secondaryStore: string): ReorderedProduct[] {
    return [
      { name: 'Bauernbrot 750g', store: primaryStore, quantity: 14, repeatCount: 14, spend: 46.2 },
      { name: 'Frische Vollmilch 1L', store: 'ALDI', quantity: 12, repeatCount: 12, spend: 29.4 },
      { name: 'Mineralwasser 1.5L', store: 'REWE', quantity: 11, repeatCount: 11, spend: 24.2 },
      { name: 'Bio-Eier 10er', store: 'ALDI', quantity: 9, repeatCount: 9, spend: 33.3 },
      { name: 'Banane lose 1kg', store: 'LIDL', quantity: 8, repeatCount: 8, spend: 19.2 },
      { name: 'Shampoo Volume', store: secondaryStore, quantity: 7, repeatCount: 7, spend: 35.0 },
      { name: 'Body Lotion 500ml', store: 'Rossmann', quantity: 6, repeatCount: 6, spend: 28.8 },
      { name: 'Dark Chocolate 100g', store: 'LIDL', quantity: 6, repeatCount: 6, spend: 18.0 },
      { name: 'Greek Yogurt 500g', store: 'REWE', quantity: 5, repeatCount: 5, spend: 14.5 },
      { name: 'Toilet Paper 8x', store: 'dm', quantity: 5, repeatCount: 5, spend: 27.5 }
    ];
  }

  private baseHeatGroceries(multiplier: number): HeatmapRow[] {
    const m = (value: number) => Math.round(value * multiplier);
    return [
      { store: 'LIDL', values: { Bakery: m(78), Beverages: m(92), 'Eggs & Dairy': m(54), 'Fruits & Veg': m(89), Meat: m(46), Snacks: m(53) } },
      { store: 'ALDI', values: { Bakery: m(54), Beverages: m(38), 'Eggs & Dairy': m(71), 'Fruits & Veg': m(41), Meat: m(32), Snacks: m(32) } },
      { store: 'REWE', values: { Bakery: m(24), Beverages: m(48), 'Eggs & Dairy': m(22), 'Fruits & Veg': m(12), Meat: m(18), Snacks: m(65) } },
      { store: 'PENNY', values: { Bakery: m(14), Beverages: m(18), 'Eggs & Dairy': m(6), 'Fruits & Veg': m(0), Meat: m(2), Snacks: m(56) } },
      { store: 'EDEKA', values: { Bakery: m(6), Beverages: m(2), 'Eggs & Dairy': m(12), 'Fruits & Veg': m(0), Meat: m(21), Snacks: m(5) } }
    ];
  }

  private baseHeatDrugstore(multiplier: number): HeatmapRow[] {
    const m = (value: number) => Math.round(value * multiplier);
    return [
      { store: 'dm', values: { 'Skin Care': m(62), 'Hair Care': m(34), 'Body & Bath': m(47), Health: m(35) } },
      { store: 'Rossmann', values: { 'Skin Care': m(25), 'Hair Care': m(27), 'Body & Bath': m(12), Health: m(29) } }
    ];
  }
}
