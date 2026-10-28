export interface Address {
  id: number;
  userId: number;
  label?: string;
  displayName?: string | null;
  street: string;
  houseNumber: string;
  postalCode: string;
  city: string;
  country: string;
  isDefault: boolean;
}

export interface CreateAddressRequest {
  label?: string;
  displayName?: string;
  street: string;
  houseNumber: string;
  postalCode: string;
  city: string;
  country: string;
  isDefault?: boolean;
}
