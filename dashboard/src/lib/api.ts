import { demoApi } from "./demo";
import type {
  AuthResponse,
  Category,
  CategoryValuation,
  InventoryValuation,
  PagedResult,
  Product,
  ProductQuery,
  StockMovement,
} from "./types";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";

export const DEMO_MODE = process.env.NEXT_PUBLIC_DEMO_MODE === "true";

const ACCESS_KEY = "inv_access";
const REFRESH_KEY = "inv_refresh";
const USER_KEY = "inv_user";

export interface SessionUser {
  email: string;
  fullName: string | null;
  roles: string[];
}

// El token vive en memoria durante la sesión de la pestaña.
// sessionStorage se limpia al cerrarla, a diferencia de localStorage.
export const session = {
  get access() {
    if (typeof window === "undefined") return null;
    return sessionStorage.getItem(ACCESS_KEY);
  },
  get refresh() {
    if (typeof window === "undefined") return null;
    return sessionStorage.getItem(REFRESH_KEY);
  },
  get user(): SessionUser | null {
    if (typeof window === "undefined") return null;
    const raw = sessionStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as SessionUser) : null;
  },
  save(auth: AuthResponse) {
    sessionStorage.setItem(ACCESS_KEY, auth.accessToken);
    sessionStorage.setItem(REFRESH_KEY, auth.refreshToken);
    sessionStorage.setItem(
      USER_KEY,
      JSON.stringify({
        email: auth.email,
        fullName: auth.fullName,
        roles: auth.roles,
      }),
    );
  },
  clear() {
    sessionStorage.removeItem(ACCESS_KEY);
    sessionStorage.removeItem(REFRESH_KEY);
    sessionStorage.removeItem(USER_KEY);
  },
};

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * Extrae el mensaje de un ProblemDetails de la API.
 * La API devuelve { title, detail, status } según RFC 7807.
 */
async function readError(response: Response): Promise<string> {
  try {
    const body = await response.json();
    return body.detail || body.title || body.message || "Error en la solicitud.";
  } catch {
    return `Error ${response.status}`;
  }
}

let refreshing: Promise<boolean> | null = null;

/** Renueva el access token. Si ya hay un refresh en curso, se reutiliza esa promesa. */
async function refreshTokens(): Promise<boolean> {
  if (refreshing) return refreshing;

  refreshing = (async () => {
    const refreshToken = session.refresh;
    if (!refreshToken) return false;

    try {
      const response = await fetch(`${BASE_URL}/api/auth/refresh`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ refreshToken }),
      });

      if (!response.ok) {
        session.clear();
        return false;
      }

      session.save((await response.json()) as AuthResponse);
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();

  return refreshing;
}

async function request<T>(path: string, init: RequestInit = {}, retry = true): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");

  const token = session.access;
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${BASE_URL}${path}`, { ...init, headers });

  // Un 401 dispara un único intento de refresh antes de rendirse.
  if (response.status === 401 && retry && session.refresh) {
    if (await refreshTokens()) {
      return request<T>(path, init, false);
    }
  }

  if (!response.ok) {
    throw new ApiError(await readError(response), response.status);
  }

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

function buildQuery(params: object): string {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  }

  const qs = search.toString();
  return qs ? `?${qs}` : "";
}

export const api = {
  async login(email: string, password: string): Promise<AuthResponse> {
    if (DEMO_MODE) {
      const auth = await demoApi.login();
      session.save(auth);
      return auth;
    }

    const auth = await request<AuthResponse>("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password }),
    });

    session.save(auth);
    return auth;
  },

  async logout(): Promise<void> {
    if (!DEMO_MODE) {
      try {
        await request<void>("/api/auth/logout", { method: "POST" });
      } catch {
        // Si el logout del servidor falla igual se limpia la sesión local.
      }
    }
    session.clear();
  },

  getProducts(query: ProductQuery = {}): Promise<PagedResult<Product>> {
    if (DEMO_MODE) return demoApi.getProducts(query);
    return request<PagedResult<Product>>(`/api/products${buildQuery(query)}`);
  },

  getMovements(query: object = {}): Promise<PagedResult<StockMovement>> {
    if (DEMO_MODE) return demoApi.getMovements();
    return request<PagedResult<StockMovement>>(`/api/stock/movements${buildQuery(query)}`);
  },

  createMovement(input: {
    productId: number;
    type: 1 | 2 | 3;
    quantity: number;
    reason?: string;
    reference?: string;
  }): Promise<StockMovement> {
    if (DEMO_MODE) return demoApi.createMovement(input);
    return request<StockMovement>("/api/stock/movements", {
      method: "POST",
      body: JSON.stringify(input),
    });
  },

  getValuation(): Promise<InventoryValuation> {
    if (DEMO_MODE) return demoApi.getValuation();
    return request<InventoryValuation>("/api/reports/valuation");
  },

  getValuationByCategory(): Promise<CategoryValuation[]> {
    if (DEMO_MODE) return demoApi.getValuationByCategory();
    return request<CategoryValuation[]>("/api/reports/valuation/by-category");
  },

  getCategories(): Promise<Category[]> {
    if (DEMO_MODE) return demoApi.getCategories();
    return request<Category[]>("/api/categories");
  },
};
