export interface Address {
  id: number;
  userId: number;
  label?: string;
  street: string;
  houseNumber: string;
  postalCode: string;
  city: string;
  country: string;
  isDefault: boolean;
}

export interface CreateAddressRequest {
  label?: string;
  street: string;
  houseNumber: string;
  postalCode: string;
  city: string;
  country: string;
  isDefault?: boolean;
}
