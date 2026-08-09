"use client";

import { useEffect, useState } from "react";
import { ArrowDownToLine, ArrowUpFromLine, Scale } from "lucide-react";
import AppShell from "@/components/AppShell";
import { api } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import { MOVEMENT_LABEL, type MovementType, type StockMovement } from "@/lib/types";

const TYPE_STYLE: Record<MovementType, { icon: React.ElementType; className: string; sign: string }> = {
  1: { icon: ArrowDownToLine, className: "bg-emerald-500/15 text-emerald-400", sign: "+" },
  2: { icon: ArrowUpFromLine, className: "bg-red-500/15 text-red-400", sign: "−" },
  3: { icon: Scale, className: "bg-brand-soft text-brand", sign: "±" },
};

export default function MovementsPage() {
  const [movements, setMovements] = useState<StockMovement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<MovementType | "all">("all");

  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        const result = await api.getMovements({ pageSize: 50 });
        if (!cancelled) setMovements(result.items);
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "No se pudo cargar el historial.");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, []);

  const visible =
    filter === "all" ? movements : movements.filter((m) => m.type === filter);

  return (
    <AppShell>
      <div className="mb-6">
        <h1 className="text-2xl font-bold tracking-tight text-white">Movimientos</h1>
        <p className="mt-1 text-sm text-slate-500">
          Historial completo de cambios de stock. Cada registro es inmutable.
        </p>
      </div>

      <div className="mb-4 flex flex-wrap gap-2">
        <button
          onClick={() => setFilter("all")}
          className={filter === "all" ? "btn-primary" : "btn-ghost"}
        >
          Todos
        </button>
        {([1, 2, 3] as MovementType[]).map((type) => (
          <button
            key={type}
            onClick={() => setFilter(type)}
            className={filter === type ? "btn-primary" : "btn-ghost"}
          >
            {MOVEMENT_LABEL[type]}
          </button>
        ))}
      </div>

      {error && (
        <div className="card mb-4 border-red-500/25 bg-red-500/10 p-4 text-sm text-red-300">
          {error}
        </div>
      )}

      {loading ? (
        <div className="space-y-2">
          {[...Array(6)].map((_, i) => (
            <div key={i} className="card h-16 animate-pulse bg-surface-raised/60" />
          ))}
        </div>
      ) : visible.length === 0 ? (
        <div className="card p-12 text-center text-sm text-slate-500">
          No hay movimientos registrados para este filtro.
        </div>
      ) : (
        <ul className="space-y-2">
          {visible.map((movement) => {
            const style = TYPE_STYLE[movement.type];
            const Icon = style.icon;

            return (
              <li key={movement.id} className="card p-4">
                <div className="flex items-start gap-3">
                  <div className={`shrink-0 rounded-lg p-2 ${style.className}`}>
                    <Icon size={16} />
                  </div>

                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-baseline gap-x-2">
                      <span className="font-medium text-slate-200">
                        {movement.productName}
                      </span>
                      <span className="font-mono text-xs text-slate-600">
                        {movement.productSku}
                      </span>
                    </div>

                    {movement.reason && (
                      <p className="mt-0.5 text-sm text-slate-500">{movement.reason}</p>
                    )}

                    <div className="mt-1 flex flex-wrap items-center gap-x-3 text-xs text-slate-600">
                      <span>{formatDateTime(movement.createdAt)}</span>
                      {movement.reference && (
                        <span className="font-mono">{movement.reference}</span>
                      )}
                      {movement.createdBy && <span>por {movement.createdBy}</span>}
                    </div>
                  </div>

                  <div className="shrink-0 text-right">
                    <p className={`text-sm font-semibold ${style.className.split(" ")[1]}`}>
                      {style.sign}
                      {movement.quantity}
                    </p>
                    <p className="mt-0.5 text-xs text-slate-600">
                      queda {movement.stockAfter}
                    </p>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </AppShell>
  );
}
