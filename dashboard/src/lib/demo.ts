import type {
  AuthResponse,
  CategoryValuation,
  InventoryValuation,
  PagedResult,
  Product,
  ProductQuery,
  StockMovement,
} from "./types";

/**
 * Datos de ejemplo en memoria.
 *
 * Sirven para dos cosas: desplegar el dashboard solo (por ejemplo en Vercel)
 * sin tener que exponer una base de datos, y poder tomar capturas para el README.
 * Se activa con NEXT_PUBLIC_DEMO_MODE=true y la interfaz lo indica siempre
 * con un cartel visible, así nadie confunde los datos con reales.
 */

const CATEGORIES = [
  { id: 1, name: "Bebidas" },
  { id: 2, name: "Almacén" },
  { id: 3, name: "Limpieza" },
  { id: 4, name: "Panificados" },
];

const SUPPLIERS = [
  { id: 1, name: "Distribuidora del Centro" },
  { id: 2, name: "Mayorista Sur" },
];

function makeProduct(
  id: number,
  sku: string,
  name: string,
  price: number,
  cost: number,
  stock: number,
  minStock: number,
  categoryId: number,
  supplierId: number | null,
): Product {
  const category = CATEGORIES.find((c) => c.id === categoryId)!;
  const supplier = SUPPLIERS.find((s) => s.id === supplierId) ?? null;

  return {
    id,
    sku,
    name,
    description: null,
    price,
    cost,
    stock,
    minStock,
    isActive: true,
    isLowStock: stock <= minStock,
    categoryId,
    categoryName: category.name,
    supplierId: supplier?.id ?? null,
    supplierName: supplier?.name ?? null,
    createdAt: "2026-06-01T10:00:00Z",
    updatedAt: null,
  };
}

let products: Product[] = [
  makeProduct(1, "BEB-001", "Agua mineral 500ml", 1200, 700, 120, 30, 1, 1),
  makeProduct(2, "BEB-002", "Gaseosa cola 2.25L", 3800, 2400, 45, 20, 1, 1),
  makeProduct(3, "BEB-003", "Jugo de naranja 1L", 2100, 1300, 12, 15, 1, 2),
  makeProduct(4, "ALM-001", "Fideos guiseros 500g", 1500, 900, 80, 25, 2, 2),
  makeProduct(5, "ALM-002", "Arroz largo fino 1kg", 2300, 1500, 8, 20, 2, 2),
  makeProduct(6, "ALM-003", "Aceite de girasol 900ml", 4200, 2900, 35, 15, 2, 1),
  makeProduct(7, "ALM-004", "Yerba mate 1kg", 6800, 4400, 52, 20, 2, 1),
  makeProduct(8, "ALM-005", "Azúcar 1kg", 1400, 850, 18, 20, 2, 2),
  makeProduct(9, "LIM-001", "Detergente 750ml", 2800, 1700, 60, 20, 3, 1),
  makeProduct(10, "LIM-002", "Lavandina 1L", 1600, 950, 0, 10, 3, 1),
  makeProduct(11, "LIM-003", "Jabón en polvo 800g", 5200, 3400, 27, 12, 3, 2),
  makeProduct(12, "PAN-001", "Pan de mesa 500g", 2500, 1600, 25, 10, 4, 2),
];

let movements: StockMovement[] = [
  {
    id: 8, productId: 5, productSku: "ALM-002", productName: "Arroz largo fino 1kg",
    type: 2, quantity: 12, stockAfter: 8, reason: "Venta mostrador",
    reference: "TICKET-4471", createdAt: "2026-08-08T18:12:00Z", createdBy: "admin",
  },
  {
    id: 7, productId: 10, productSku: "LIM-002", productName: "Lavandina 1L",
    type: 2, quantity: 6, stockAfter: 0, reason: "Venta mostrador",
    reference: "TICKET-4468", createdAt: "2026-08-08T16:40:00Z", createdBy: "admin",
  },
  {
    id: 6, productId: 7, productSku: "ALM-004", productName: "Yerba mate 1kg",
    type: 1, quantity: 24, stockAfter: 52, reason: "Compra a proveedor",
    reference: "FC-A-00012345", createdAt: "2026-08-07T09:20:00Z", createdBy: "admin",
  },
  {
    id: 5, productId: 3, productSku: "BEB-003", productName: "Jugo de naranja 1L",
    type: 3, quantity: 4, stockAfter: 12, reason: "Conteo físico: faltaban 4 unidades",
    reference: null, createdAt: "2026-08-06T20:05:00Z", createdBy: "admin",
  },
  {
    id: 4, productId: 1, productSku: "BEB-001", productName: "Agua mineral 500ml",
    type: 1, quantity: 48, stockAfter: 120, reason: "Compra a proveedor",
    reference: "FC-A-00012301", createdAt: "2026-08-05T11:30:00Z", createdBy: "admin",
  },
  {
    id: 3, productId: 9, productSku: "LIM-001", productName: "Detergente 750ml",
    type: 2, quantity: 10, stockAfter: 60, reason: "Venta mayorista",
    reference: "REM-0912", createdAt: "2026-08-04T15:00:00Z", createdBy: "admin",
  },
  {
    id: 2, productId: 2, productSku: "BEB-002", productName: "Gaseosa cola 2.25L",
    type: 1, quantity: 30, stockAfter: 45, reason: "Compra a proveedor",
    reference: "FC-A-00012288", createdAt: "2026-08-03T10:15:00Z", createdBy: "admin",
  },
  {
    id: 1, productId: 12, productSku: "PAN-001", productName: "Pan de mesa 500g",
    type: 1, quantity: 25, stockAfter: 25, reason: "Carga inicial de inventario",
    reference: null, createdAt: "2026-08-01T08:00:00Z", createdBy: "admin",
  },
];

const delay = (ms = 220) => new Promise((r) => setTimeout(r, ms));

export const demoApi = {
  async login(): Promise<AuthResponse> {
    await delay();
    return {
      accessToken: "demo-access-token",
      refreshToken: "demo-refresh-token",
      expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      email: "admin@inventariopro.local",
      fullName: "Usuario Demo",
      roles: ["Admin"],
    };
  },

  async getProducts(query: ProductQuery): Promise<PagedResult<Product>> {
    await delay();

    let result = [...products];

    if (query.search) {
      const term = query.search.toLowerCase();
      result = result.filter(
        (p) =>
          p.sku.toLowerCase().includes(term) ||
          p.name.toLowerCase().includes(term),
      );
    }

    if (query.categoryId) {
      result = result.filter((p) => p.categoryId === query.categoryId);
    }

    if (query.lowStockOnly) {
      result = result.filter((p) => p.stock <= p.minStock);
    }

    const dir = query.desc ? -1 : 1;
    const key = query.sortBy ?? "name";

    result.sort((a, b) => {
      switch (key) {
        case "sku": return a.sku.localeCompare(b.sku) * dir;
        case "price": return (a.price - b.price) * dir;
        case "stock": return (a.stock - b.stock) * dir;
        default: return a.name.localeCompare(b.name) * dir;
      }
    });

    const page = query.page ?? 1;
    const pageSize = query.pageSize ?? 10;
    const totalItems = result.length;
    const totalPages = Math.ceil(totalItems / pageSize) || 1;

    return {
      items: result.slice((page - 1) * pageSize, page * pageSize),
      page,
      pageSize,
      totalItems,
      totalPages,
      hasPrevious: page > 1,
      hasNext: page < totalPages,
    };
  },

  async getMovements(): Promise<PagedResult<StockMovement>> {
    await delay();
    return {
      items: movements,
      page: 1,
      pageSize: 20,
      totalItems: movements.length,
      totalPages: 1,
      hasPrevious: false,
      hasNext: false,
    };
  },

  async createMovement(input: {
    productId: number;
    type: 1 | 2 | 3;
    quantity: number;
    reason?: string;
    reference?: string;
  }): Promise<StockMovement> {
    await delay();

    const product = products.find((p) => p.id === input.productId);
    if (!product) throw new Error("Producto no encontrado.");

    let quantity = input.quantity;
    let newStock: number;

    if (input.type === 1) {
      newStock = product.stock + quantity;
    } else if (input.type === 2) {
      if (quantity > product.stock) {
        throw new Error(
          `Stock insuficiente para "${product.name}": hay ${product.stock} unidades y se intentan retirar ${quantity}.`,
        );
      }
      newStock = product.stock - quantity;
    } else {
      const difference = quantity - product.stock;
      newStock = quantity;
      quantity = Math.abs(difference);
    }

    products = products.map((p) =>
      p.id === product.id
        ? { ...p, stock: newStock, isLowStock: newStock <= p.minStock }
        : p,
    );

    const movement: StockMovement = {
      id: Math.max(0, ...movements.map((m) => m.id)) + 1,
      productId: product.id,
      productSku: product.sku,
      productName: product.name,
      type: input.type,
      quantity,
      stockAfter: newStock,
      reason: input.reason ?? null,
      reference: input.reference ?? null,
      createdAt: new Date().toISOString(),
      createdBy: "admin",
    };

    movements = [movement, ...movements];
    return movement;
  },

  async getValuation(): Promise<InventoryValuation> {
    await delay();

    const active = products.filter((p) => p.isActive);
    const totalCostValue = active.reduce((sum, p) => sum + p.cost * p.stock, 0);
    const totalSaleValue = active.reduce((sum, p) => sum + p.price * p.stock, 0);

    return {
      totalProducts: active.length,
      totalUnits: active.reduce((sum, p) => sum + p.stock, 0),
      totalCostValue,
      totalSaleValue,
      potentialMargin: totalSaleValue - totalCostValue,
      lowStockCount: active.filter((p) => p.stock <= p.minStock && p.stock > 0).length,
      outOfStockCount: active.filter((p) => p.stock === 0).length,
    };
  },

  async getValuationByCategory(): Promise<CategoryValuation[]> {
    await delay();

    return CATEGORIES.map((c) => {
      const inCategory = products.filter((p) => p.categoryId === c.id);
      return {
        categoryId: c.id,
        categoryName: c.name,
        productCount: inCategory.length,
        totalUnits: inCategory.reduce((sum, p) => sum + p.stock, 0),
        totalCostValue: inCategory.reduce((sum, p) => sum + p.cost * p.stock, 0),
      };
    }).sort((a, b) => b.totalCostValue - a.totalCostValue);
  },

  async getCategories() {
    await delay(80);
    return CATEGORIES.map((c) => ({
      ...c,
      description: null,
      productCount: products.filter((p) => p.categoryId === c.id).length,
    }));
  },
};
