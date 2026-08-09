"use client";

import { useState, type FormEvent } from "react";
import { X, AlertCircle, ArrowDownToLine, ArrowUpFromLine, Scale } from "lucide-react";
import { api } from "@/lib/api";
import type { MovementType, Product } from "@/lib/types";

const TYPES: { value: MovementType; label: string; icon: React.ElementType; help: string }[] = [
  { value: 1, label: "Entrada", icon: ArrowDownToLine, help: "Suma unidades al stock" },
  { value: 2, label: "Salida", icon: ArrowUpFromLine, help: "Descuenta unidades del stock" },
  { value: 3, label: "Ajuste", icon: Scale, help: "Fija el stock al valor contado" },
];

export default function MovementModal({
  product,
  onClose,
  onSaved,
}: {
  product: Product;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [type, setType] = useState<MovementType>(1);
  const [quantity, setQuantity] = useState("");
  const [reason, setReason] = useState("");
  const [reference, setReference] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const parsed = Number(quantity);
  const valid = quantity !== "" && Number.isInteger(parsed) && parsed >= 0;

  // Previsualización del stock resultante, para que el operador
  // confirme antes de guardar.
  const projected =
    !valid ? null
    : type === 1 ? product.stock + parsed
    : type === 2 ? product.stock - parsed
    : parsed;

  const insufficient = type === 2 && valid && parsed > product.stock;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!valid || insufficient) return;

    setError(null);
    setSaving(true);

    try {
      await api.createMovement({
        productId: product.id,
        type,
        quantity: parsed,
        reason: reason.trim() || undefined,
        reference: reference.trim() || undefined,
      });
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "No se pudo registrar el movimiento.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div className="card w-full max-w-md">
        <div className="flex items-start justify-between border-b border-surface-border p-5">
          <div className="min-w-0">
            <h2 className="font-semibold text-white">Movimiento de stock</h2>
            <p className="mt-0.5 truncate text-sm text-slate-400">{product.name}</p>
            <p className="font-mono text-xs text-slate-600">
              {product.sku} · stock actual: {product.stock}
            </p>
          </div>
          <button
            onClick={onClose}
            className="shrink-0 text-slate-500 hover:text-white"
            aria-label="Cerrar"
          >
            <X size={18} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 p-5">
          <div>
            <span className="label">Tipo</span>
            <div className="grid grid-cols-3 gap-2">
              {TYPES.map(({ value, label, icon: Icon }) => (
                <button
                  key={value}
                  type="button"
                  onClick={() => setType(value)}
                  className={`flex flex-col items-center gap-1 rounded-lg border px-2 py-3 text-xs font-medium transition-colors
                    ${
                      type === value
                        ? "border-brand bg-brand-soft text-white"
                        : "border-surface-border text-slate-400 hover:border-slate-600"
                    }`}
                >
                  <Icon size={16} />
                  {label}
                </button>
              ))}
            </div>
            <p className="mt-1.5 text-xs text-slate-600">
              {TYPES.find((t) => t.value === type)?.help}
            </p>
          </div>

          <div>
            <label htmlFor="qty" className="label">
              {type === 3 ? "Stock contado" : "Cantidad"}
            </label>
            <input
              id="qty"
              type="number"
              min={0}
              step={1}
              className="input"
              value={quantity}
              onChange={(e) => setQuantity(e.target.value)}
              placeholder="0"
              required
              autoFocus
            />
          </div>

          {projected !== null && (
            <div
              className={`rounded-lg border p-3 text-sm ${
                insufficient
                  ? "border-red-500/25 bg-red-500/10 text-red-300"
                  : "border-surface-border bg-surface text-slate-400"
              }`}
            >
              {insufficient ? (
                <>Stock insuficiente: hay {product.stock} unidades disponibles.</>
              ) : (
                <>
                  Stock resultante:{" "}
                  <span className="font-semibold text-white">{projected}</span> unidades
                </>
              )}
            </div>
          )}

          <div>
            <label htmlFor="reason" className="label">
              Motivo
            </label>
            <input
              id="reason"
              className="input"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Compra a proveedor, venta, rotura…"
              maxLength={300}
            />
          </div>

          <div>
            <label htmlFor="ref" className="label">
              Comprobante
            </label>
            <input
              id="ref"
              className="input"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="FC-A-00012345"
              maxLength={60}
            />
          </div>

          {error && (
            <div className="flex items-start gap-2 rounded-lg border border-red-500/25 bg-red-500/10 p-3 text-sm text-red-300">
              <AlertCircle size={16} className="mt-0.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <div className="flex gap-2 pt-1">
            <button type="button" onClick={onClose} className="btn-ghost flex-1">
              Cancelar
            </button>
            <button
              type="submit"
              className="btn-primary flex-1"
              disabled={!valid || insufficient || saving}
            >
              {saving ? "Guardando…" : "Registrar"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
