import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DeliveryCheckResult {
  eligible: boolean;
  message?: string;
  deliveryFee?: number;
}

@Injectable({ providedIn: 'root' })
export class DeliveryService {
  private http = inject(HttpClient);

  checkDelivery(postalCode: string): Observable<DeliveryCheckResult> {
    return this.http.get<DeliveryCheckResult>(`${environment.apiUrl}/delivery/check`, {
      params: { postalCode: postalCode.trim() }
    });
  }
}

