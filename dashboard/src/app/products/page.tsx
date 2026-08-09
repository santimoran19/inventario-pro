"use client";

import { useCallback, useEffect, useState } from "react";
import { Search, ArrowUpDown, ChevronLeft, ChevronRight, Plus } from "lucide-react";
import AppShell from "@/components/AppShell";
import MovementModal from "@/components/MovementModal";
import { api } from "@/lib/api";
import { formatCurrency } from "@/lib/format";
import type { Category, PagedResult, Product } from "@/lib/types";

const COLUMNS: { key: string; label: string; sortable: boolean; align?: string }[] = [
  { key: "sku", label: "SKU", sortable: true },
  { key: "name", label: "Producto", sortable: true },
  { key: "category", label: "Categoría", sortable: false },
  { key: "price", label: "Precio", sortable: true, align: "text-right" },
  { key: "stock", label: "Stock", sortable: true, align: "text-right" },
  { key: "actions", label: "", sortable: false, align: "text-right" },
];

export default function ProductsPage() {
  const [data, setData] = useState<PagedResult<Product> | null>(null);
  const [categories, setCategories] = useState<Category[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [categoryId, setCategoryId] = useState<number | undefined>();
  const [lowStockOnly, setLowStockOnly] = useState(false);
  const [sortBy, setSortBy] = useState("name");
  const [desc, setDesc] = useState(false);
  const [page, setPage] = useState(1);

  const [selected, setSelected] = useState<Product | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await api.getProducts({
        search: search || undefined,
        categoryId,
        lowStockOnly: lowStockOnly || undefined,
        sortBy,
        desc,
        page,
        pageSize: 10,
      });
      setData(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "No se pudieron cargar los productos.");
    } finally {
      setLoading(false);
    }
  }, [search, categoryId, lowStockOnly, sortBy, desc, page]);

  // Debounce sobre la búsqueda: evita una request por cada tecla.
  useEffect(() => {
    const timer = setTimeout(load, search ? 300 : 0);
    return () => clearTimeout(timer);
  }, [load, search]);

  useEffect(() => {
    api.getCategories().then(setCategories).catch(() => setCategories([]));
  }, []);

  function toggleSort(key: string) {
    if (sortBy === key) {
      setDesc(!desc);
    } else {
      setSortBy(key);
      setDesc(false);
    }
    setPage(1);
  }

  return (
    <AppShell>
      <div className="mb-6">
        <h1 className="text-2xl font-bold tracking-tight text-white">Productos</h1>
        <p className="mt-1 text-sm text-slate-500">
          {data ? `${data.totalItems} productos` : "Cargando…"}
        </p>
      </div>

      {/* Filtros */}
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="relative min-w-[220px] flex-1">
          <Search
            size={16}
            className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-600"
          />
          <input
            className="input pl-9"
            placeholder="Buscar por SKU o nombre…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </div>

        <select
          className="input w-auto"
          value={categoryId ?? ""}
          onChange={(e) => {
            setCategoryId(e.target.value ? Number(e.target.value) : undefined);
            setPage(1);
          }}
        >
          <option value="">Todas las categorías</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>

        <button
          onClick={() => {
            setLowStockOnly(!lowStockOnly);
            setPage(1);
          }}
          className={lowStockOnly ? "btn-primary" : "btn-ghost"}
        >
          Solo reposición
        </button>
      </div>

      {error && (
        <div className="card mb-4 border-red-500/25 bg-red-500/10 p-4 text-sm text-red-300">
          {error}
        </div>
      )}

      {/* Tabla */}
      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="border-b border-surface-border">
              <tr>
                {COLUMNS.map((col) => (
                  <th key={col.key} className={`th ${col.align ?? ""}`}>
                    {col.sortable ? (
                      <button
                        onClick={() => toggleSort(col.key)}
                        className={`inline-flex items-center gap-1 transition-colors hover:text-slate-300
                          ${sortBy === col.key ? "text-brand" : ""}`}
                      >
                        {col.label}
                        <ArrowUpDown size={12} />
                      </button>
                    ) : (
                      col.label
                    )}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody className="divide-y divide-surface-border">
              {loading ? (
                [...Array(6)].map((_, i) => (
                  <tr key={i}>
                    <td colSpan={6} className="td">
                      <div className="h-4 animate-pulse rounded bg-surface" />
                    </td>
                  </tr>
                ))
              ) : data?.items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="td py-12 text-center text-slate-500">
                    No se encontraron productos con esos filtros.
                  </td>
                </tr>
              ) : (
                data?.items.map((product) => (
                  <tr key={product.id} className="transition-colors hover:bg-white/[0.02]">
                    <td className="td font-mono text-xs text-slate-500">{product.sku}</td>
                    <td className="td">
                      <span className="font-medium text-slate-200">{product.name}</span>
                    </td>
                    <td className="td text-slate-500">{product.categoryName}</td>
                    <td className="td text-right">{formatCurrency(product.price)}</td>
                    <td className="td text-right">
                      <span
                        className={`badge ${
                          product.stock === 0
                            ? "bg-red-500/15 text-red-400"
                            : product.isLowStock
                              ? "bg-amber-500/15 text-amber-400"
                              : "bg-emerald-500/15 text-emerald-400"
                        }`}
                      >
                        {product.stock}
                      </span>
                    </td>
                    <td className="td text-right">
                      <button
                        onClick={() => setSelected(product)}
                        className="inline-flex items-center gap-1 text-xs font-medium text-brand hover:underline"
                      >
                        <Plus size={13} />
                        Movimiento
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Paginación */}
        {data && data.totalPages > 1 && (
          <div className="flex items-center justify-between border-t border-surface-border px-4 py-3">
            <p className="text-xs text-slate-500">
              Página {data.page} de {data.totalPages}
            </p>
            <div className="flex gap-2">
              <button
                onClick={() => setPage((p) => p - 1)}
                disabled={!data.hasPrevious}
                className="btn-ghost px-2 py-1"
                aria-label="Página anterior"
              >
                <ChevronLeft size={16} />
              </button>
              <button
                onClick={() => setPage((p) => p + 1)}
                disabled={!data.hasNext}
                className="btn-ghost px-2 py-1"
                aria-label="Página siguiente"
              >
                <ChevronRight size={16} />
              </button>
            </div>
          </div>
        )}
      </div>

      {selected && (
        <MovementModal
          product={selected}
          onClose={() => setSelected(null)}
          onSaved={() => {
            setSelected(null);
            load();
          }}
        />
      )}
    </AppShell>
  );
}
