import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, throwError, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Address, CreateAddressRequest } from '../models/address.model';

// Mock data for development
let MOCK_ADDRESSES: Address[] = [
  {
    id: 1,
    userId: 1,
    label: 'Home',
    street: 'Hauptstraße',
    houseNumber: '123',
    postalCode: '10115',
    city: 'Berlin',
    country: 'Germany',
    isDefault: true
  },
  {
    id: 2,
    userId: 1,
    label: 'Work',
    street: 'Alexanderplatz',
    houseNumber: '5',
    postalCode: '10178',
    city: 'Berlin',
    country: 'Germany',
    isDefault: false
  }
];

let nextId = 3;

@Injectable({ providedIn: 'root' })
export class AddressService {
  private http = inject(HttpClient);
  private useMock = environment.useMockAddresses;

  getAddresses(): Observable<Address[]> {
    console.log('AddressService.getAddresses called, useMock:', this.useMock);
    if (this.useMock) {
      console.log('Returning mock addresses:', MOCK_ADDRESSES);
      return of([...MOCK_ADDRESSES]).pipe(delay(300));
    }
    return this.http.get<unknown>(`${environment.apiUrl}/addresses`).pipe(
      map((response) => this.toAddressArray(response))
    );
  }

  addAddress(req: CreateAddressRequest): Observable<Address> {
    if (this.useMock) {
      const newAddress: Address = {
        id: nextId++,
        userId: 1,
        ...req,
        isDefault: req.isDefault || false
      };
      
      // If new address is default, unset others
      if (newAddress.isDefault) {
        MOCK_ADDRESSES = MOCK_ADDRESSES.map(a => ({ ...a, isDefault: false }));
      }
      
      MOCK_ADDRESSES.push(newAddress);
      return of(newAddress).pipe(delay(300));
    }
    return this.http.post<Address>(`${environment.apiUrl}/addresses`, req);
  }

  updateAddress(id: number, req: Partial<CreateAddressRequest>): Observable<Address> {
    if (this.useMock) {
      const index = MOCK_ADDRESSES.findIndex(a => a.id === id);
      if (index === -1) {
        return throwError(() => new Error('Address not found'));
      }
      
      const updated = { ...MOCK_ADDRESSES[index], ...req };
      
      // If updated address is set to default, unset others
      if (updated.isDefault) {
        MOCK_ADDRESSES = MOCK_ADDRESSES.map(a => ({ ...a, isDefault: a.id === id }));
      }
      
      MOCK_ADDRESSES[index] = updated;
      return of(updated).pipe(delay(300));
    }
    return this.http.put<Address>(`${environment.apiUrl}/addresses/${id}`, req);
  }

  deleteAddress(id: number): Observable<void> {
    if (this.useMock) {
      const wasDefault = MOCK_ADDRESSES.find(a => a.id === id)?.isDefault;
      MOCK_ADDRESSES = MOCK_ADDRESSES.filter(a => a.id !== id);
      
      // If deleted address was default, set first remaining as default
      if (wasDefault && MOCK_ADDRESSES.length > 0) {
        MOCK_ADDRESSES[0].isDefault = true;
      }
      
      return of(void 0).pipe(delay(300));
    }
    return this.http.delete<void>(`${environment.apiUrl}/addresses/${id}`);
  }

  setDefault(id: number): Observable<Address> {
    if (this.useMock) {
      MOCK_ADDRESSES = MOCK_ADDRESSES.map(a => ({ ...a, isDefault: a.id === id }));
      const defaultAddr = MOCK_ADDRESSES.find(a => a.id === id);
      if (!defaultAddr) {
        return throwError(() => new Error('Address not found'));
      }
      return of(defaultAddr).pipe(delay(300));
    }
    return this.http.patch<Address>(`${environment.apiUrl}/addresses/${id}/default`, {});
  }

  private toAddressArray(response: unknown): Address[] {
    if (Array.isArray(response)) {
      return response as Address[];
    }

    if (response && typeof response === 'object') {
      const maybeItems = (response as { items?: unknown; data?: unknown; value?: unknown; $values?: unknown });
      if (Array.isArray(maybeItems.items)) return maybeItems.items as Address[];
      if (Array.isArray(maybeItems.data)) return maybeItems.data as Address[];
      if (Array.isArray(maybeItems.value)) return maybeItems.value as Address[];
      if (Array.isArray(maybeItems.$values)) return maybeItems.$values as Address[];
    }

    return [];
  }
}
