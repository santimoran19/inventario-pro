// Espejo de los DTOs que expone InventarioPro.Api.
// Mantener sincronizado con src/InventarioPro.Api/Dtos/Dtos.cs

export type MovementType = 1 | 2 | 3; // In | Out | Adjustment

export const MOVEMENT_LABEL: Record<MovementType, string> = {
  1: "Entrada",
  2: "Salida",
  3: "Ajuste",
};

export interface Product {
  id: number;
  sku: string;
  name: string;
  description: string | null;
  price: number;
  cost: number;
  stock: number;
  minStock: number;
  isActive: boolean;
  isLowStock: boolean;
  categoryId: number;
  categoryName: string;
  supplierId: number | null;
  supplierName: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface StockMovement {
  id: number;
  productId: number;
  productSku: string;
  productName: string;
  type: MovementType;
  quantity: number;
  stockAfter: number;
  reason: string | null;
  reference: string | null;
  createdAt: string;
  createdBy: string | null;
}

export interface Category {
  id: number;
  name: string;
  description: string | null;
  productCount: number;
}

export interface InventoryValuation {
  totalProducts: number;
  totalUnits: number;
  totalCostValue: number;
  totalSaleValue: number;
  potentialMargin: number;
  lowStockCount: number;
  outOfStockCount: number;
}

export interface CategoryValuation {
  categoryId: number;
  categoryName: string;
  productCount: number;
  totalUnits: number;
  totalCostValue: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  email: string;
  fullName: string | null;
  roles: string[];
}

export interface ProductQuery {
  search?: string;
  categoryId?: number;
  lowStockOnly?: boolean;
  isActive?: boolean;
  sortBy?: string;
  desc?: boolean;
  page?: number;
  pageSize?: number;
}
