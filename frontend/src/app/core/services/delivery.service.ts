import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DeliveryCheckResult {
  eligible: boolean;
  message?: string;
  deliveryFee?: number;
}

// Eligible postal codes (mock)
const ELIGIBLE_CODES = ['10115', '10117', '10119', '10178', '10179', '12043', '12045', '13353'];

@Injectable({ providedIn: 'root' })
export class DeliveryService {
  private http = inject(HttpClient);

  checkDelivery(postalCode: string): Observable<DeliveryCheckResult> {
    // Mock check - replace with real API call
    const eligible = ELIGIBLE_CODES.includes(postalCode.trim());
    return of({
      eligible,
      message: eligible ? 'Delivery available to your area!' : 'Sorry, we do not deliver to this postal code yet.',
      deliveryFee: eligible ? 2.99 : 0
    });
    // return this.http.get<DeliveryCheckResult>(`${environment.apiUrl}/delivery/check?postalCode=${postalCode}`);
  }
}
