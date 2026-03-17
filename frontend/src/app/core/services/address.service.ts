import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Address, CreateAddressRequest } from '../models/address.model';

@Injectable({ providedIn: 'root' })
export class AddressService {
  private http = inject(HttpClient);

  getAddresses(): Observable<Address[]> {
    return this.http.get<Address[]>(`${environment.apiUrl}/addresses`);
  }

  addAddress(req: CreateAddressRequest): Observable<Address> {
    return this.http.post<Address>(`${environment.apiUrl}/addresses`, req);
  }

  updateAddress(id: number, req: Partial<CreateAddressRequest>): Observable<Address> {
    return this.http.put<Address>(`${environment.apiUrl}/addresses/${id}`, req);
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/addresses/${id}`);
  }

  setDefault(id: number): Observable<Address> {
    return this.http.patch<Address>(`${environment.apiUrl}/addresses/${id}/default`, {});
  }
}
