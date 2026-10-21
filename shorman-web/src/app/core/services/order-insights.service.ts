import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { InsightsRange, OrderInsights } from '../models/order-insights.model';

@Injectable({ providedIn: 'root' })
export class OrderInsightsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/order-insights`;

  getInsights(range: InsightsRange): Observable<OrderInsights> {
    const params = new HttpParams().set('range', range);
    return this.http.get<OrderInsights>(this.baseUrl, { params });
  }
}
