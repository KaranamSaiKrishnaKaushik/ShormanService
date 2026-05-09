import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface DeliveryCheckResult {
  eligible: boolean;
  message?: string;
  deliveryFee?: number;
}

export interface DeliveryCheckRequest {
  postalCode: string;
  city?: string;
  street?: string;
  houseNumber?: string;
  country?: string;
}

@Injectable({ providedIn: 'root' })
export class DeliveryService {
  private http = inject(HttpClient);

  checkDelivery(request: DeliveryCheckRequest | string): Observable<DeliveryCheckResult> {
    const normalizedRequest = typeof request === 'string'
      ? { postalCode: request.trim() }
      : {
          postalCode: request.postalCode.trim(),
          city: request.city?.trim(),
          street: request.street?.trim(),
          houseNumber: request.houseNumber?.trim(),
          country: request.country?.trim()
        };

    const params: Record<string, string> = {
      postalCode: normalizedRequest.postalCode
    };

    if (normalizedRequest.city) params['city'] = normalizedRequest.city;
    if (normalizedRequest.street) params['street'] = normalizedRequest.street;
    if (normalizedRequest.houseNumber) params['houseNumber'] = normalizedRequest.houseNumber;
    if (normalizedRequest.country) params['country'] = normalizedRequest.country;

    return this.http.get<DeliveryCheckResult>(`${environment.apiUrl}/delivery/check`, {
      params
    });
  }
}

