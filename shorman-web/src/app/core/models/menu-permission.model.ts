import { AppRole } from './user.model';

export type MenuPermissionKey =
  | 'products'
  | 'checkout'
  | 'orders'
  | 'history-stats'
  | 'addresses'
  | 'rider-dashboard'
  | 'product-management'
  | 'product-management.pricing'
  | 'user-management.user-list'
  | 'user-management.role-access'
  | 'user-management.order-summary';

export interface RoleMenuPermission {
  role: AppRole;
  menuKey: MenuPermissionKey;
  isEnabled: boolean;
}

export interface RoleMenuPermissions {
  role: AppRole;
  permissions: RoleMenuPermission[];
}

export interface CurrentMenuPermissions {
  enabledMenuKeys: MenuPermissionKey[];
}

export const USER_MANAGEMENT_MENU_KEYS: MenuPermissionKey[] = [
  'user-management.user-list',
  'user-management.role-access',
  'user-management.order-summary'
];