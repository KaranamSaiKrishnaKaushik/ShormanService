export type AppRole = 'SuperAdmin' | 'Admin' | 'Customer' | 'Rider';

export const APP_ROLES: AppRole[] = ['SuperAdmin', 'Admin', 'Customer', 'Rider'];
export const CUSTOMER_ROLES: AppRole[] = ['SuperAdmin', 'Admin', 'Customer'];
export const RIDER_ROLES: AppRole[] = ['SuperAdmin', 'Rider'];

export interface User {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  displayName?: string | null;
  themePreference?: string | null;
  phone?: string;
  createdAt?: string;
  roles: AppRole[];
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface RegisterResponse {
  email: string;
  message: string;
  verificationRequired: boolean;
  verificationCode?: string | null;
}

export interface UpdateCurrentUserProfileRequest {
  displayName?: string | null;
  themePreference?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phone?: string;
}

export interface VerifyEmailRequest {
  email: string;
  code: string;
}

export interface PasswordResetRequest {
  email: string;
}

export interface PasswordResetConfirmRequest {
  email: string;
  code: string;
  newPassword: string;
}

export interface PasswordResetRequestResponse {
  message: string;
  resetCode?: string | null;
}

export interface AdminUser {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  phone?: string;
  createdAt?: string;
  roles: AppRole[];
}
