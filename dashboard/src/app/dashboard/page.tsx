"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import {
  AlertTriangle,
  Boxes,
  DollarSign,
  PackageX,
  TrendingUp,
  ArrowRight,
} from "lucide-react";
import AppShell from "@/components/AppShell";
import { api } from "@/lib/api";
import { formatCurrency, formatNumber } from "@/lib/format";
import type { CategoryValuation, InventoryValuation, Product } from "@/lib/types";

function KpiCard({
  label,
  value,
  hint,
  icon: Icon,
  tone = "default",
}: {
  label: string;
  value: string;
  hint?: string;
  icon: React.ElementType;
  tone?: "default" | "warning" | "danger" | "success";
}) {
  const tones = {
    default: "text-brand bg-brand-soft",
    warning: "text-amber-400 bg-amber-500/10",
    danger: "text-red-400 bg-red-500/10",
    success: "text-emerald-400 bg-emerald-500/10",
  };

  return (
    <div className="card p-5">
      <div className="flex items-start justify-between">
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {label}
          </p>
          <p className="mt-2 truncate text-2xl font-bold text-white">{value}</p>
          {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
        </div>
        <div className={`rounded-lg p-2 ${tones[tone]}`}>
          <Icon size={18} />
        </div>
      </div>
    </div>
  );
}

export default function DashboardPage() {
  const [valuation, setValuation] = useState<InventoryValuation | null>(null);
  const [byCategory, setByCategory] = useState<CategoryValuation[]>([]);
  const [lowStock, setLowStock] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const [v, c, low] = await Promise.all([
          api.getValuation(),
          api.getValuationByCategory(),
          api.getProducts({ lowStockOnly: true, pageSize: 6, sortBy: "stock" }),
        ]);

        if (cancelled) return;

        setValuation(v);
        setByCategory(c);
        setLowStock(low.items);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "No se pudieron cargar los datos.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <AppShell>
      <div className="mb-6">
        <h1 className="text-2xl font-bold tracking-tight text-white">Panel</h1>
        <p className="mt-1 text-sm text-slate-500">
          Estado general del inventario y alertas de reposición.
        </p>
      </div>

      {error && (
        <div className="card mb-6 border-red-500/25 bg-red-500/10 p-4 text-sm text-red-300">
          {error}
        </div>
      )}

      {loading ? (
        <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="card h-28 animate-pulse bg-surface-raised/60" />
          ))}
        </div>
      ) : (
        valuation && (
          <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              <KpiCard
                label="Productos activos"
                value={formatNumber(valuation.totalProducts)}
                hint={`${formatNumber(valuation.totalUnits)} unidades en total`}
                icon={Boxes}
              />
              <KpiCard
                label="Valor a costo"
                value={formatCurrency(valuation.totalCostValue)}
                hint="Capital inmovilizado"
                icon={DollarSign}
              />
              <KpiCard
                label="Margen potencial"
                value={formatCurrency(valuation.potentialMargin)}
                hint="Si se vendiera todo el stock"
                icon={TrendingUp}
                tone="success"
              />
              <KpiCard
                label="Necesitan reposición"
                value={formatNumber(valuation.lowStockCount + valuation.outOfStockCount)}
                hint={`${valuation.outOfStockCount} sin stock`}
                icon={valuation.outOfStockCount > 0 ? PackageX : AlertTriangle}
                tone={valuation.outOfStockCount > 0 ? "danger" : "warning"}
              />
            </div>

            <div className="mt-6 grid gap-6 lg:grid-cols-5">
              {/* Gráfico */}
              <div className="card p-5 lg:col-span-3">
                <h2 className="mb-4 text-sm font-semibold text-white">
                  Capital inmovilizado por categoría
                </h2>

                <div className="h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart
                      data={byCategory}
                      margin={{ top: 4, right: 4, bottom: 4, left: 4 }}
                    >
                      <CartesianGrid strokeDasharray="3 3" stroke="#1f2836" vertical={false} />
                      <XAxis
                        dataKey="categoryName"
                        stroke="#64748b"
                        fontSize={12}
                        tickLine={false}
                        axisLine={false}
                      />
                      <YAxis
                        stroke="#64748b"
                        fontSize={12}
                        tickLine={false}
                        axisLine={false}
                        tickFormatter={(v: number) => `${Math.round(v / 1000)}k`}
                      />
                      <Tooltip
                        cursor={{ fill: "rgba(79,124,255,0.06)" }}
                        contentStyle={{
                          background: "#121826",
                          border: "1px solid #1f2836",
                          borderRadius: 8,
                          fontSize: 12,
                        }}
                        labelStyle={{ color: "#e2e8f0" }}
                        formatter={(value: number) => [formatCurrency(value), "Valor a costo"]}
                      />
                      <Bar dataKey="totalCostValue" fill="#4f7cff" radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>

              {/* Alertas */}
              <div className="card p-5 lg:col-span-2">
                <div className="mb-4 flex items-center justify-between">
                  <h2 className="text-sm font-semibold text-white">Reposición pendiente</h2>
                  <Link
                    href="/products?lowStock=1"
                    className="flex items-center gap-1 text-xs text-brand hover:underline"
                  >
                    Ver todos <ArrowRight size={12} />
                  </Link>
                </div>

                {lowStock.length === 0 ? (
                  <p className="py-8 text-center text-sm text-slate-500">
                    No hay productos por debajo del mínimo.
                  </p>
                ) : (
                  <ul className="space-y-3">
                    {lowStock.map((product) => (
                      <li key={product.id} className="flex items-center justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm text-slate-200">{product.name}</p>
                          <p className="font-mono text-xs text-slate-600">{product.sku}</p>
                        </div>
                        <span
                          className={`badge shrink-0 ${
                            product.stock === 0
                              ? "bg-red-500/15 text-red-400"
                              : "bg-amber-500/15 text-amber-400"
                          }`}
                        >
                          {product.stock} / {product.minStock}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </>
        )
      )}
    </AppShell>
  );
}
