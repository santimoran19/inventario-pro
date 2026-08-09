"use client";

import { useState, type FormEvent } from "react";
import { useRouter } from "next/navigation";
import { Package, LogIn, AlertCircle } from "lucide-react";
import { api, DEMO_MODE } from "@/lib/api";

export default function LoginPage() {
  const router = useRouter();

  // En modo demo el formulario viene precargado: la idea es que
  // quien abra la demo pueda entrar sin buscar credenciales.
  const [email, setEmail] = useState(DEMO_MODE ? "admin@inventariopro.local" : "");
  const [password, setPassword] = useState(DEMO_MODE ? "Admin#Local2026" : "");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setLoading(true);

    try {
      await api.login(email, password);
      router.replace("/dashboard");
    } catch (err) {
      setError(err instanceof Error ? err.message : "No se pudo iniciar sesión.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <div className="mb-3 inline-flex h-12 w-12 items-center justify-center rounded-xl bg-brand-soft">
            <Package size={24} className="text-brand" />
          </div>
          <h1 className="text-2xl font-bold tracking-tight text-white">InventarioPro</h1>
          <p className="mt-1 text-sm text-slate-500">
            Gestión de inventario con trazabilidad de stock
          </p>
        </div>

        <form onSubmit={handleSubmit} className="card space-y-4 p-6">
          <div>
            <label htmlFor="email" className="label">
              Correo
            </label>
            <input
              id="email"
              type="email"
              className="input"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="admin@inventariopro.local"
              autoComplete="username"
              required
            />
          </div>

          <div>
            <label htmlFor="password" className="label">
              Contraseña
            </label>
            <input
              id="password"
              type="password"
              className="input"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          {error && (
            <div className="flex items-start gap-2 rounded-lg border border-red-500/25 bg-red-500/10 p-3 text-sm text-red-300">
              <AlertCircle size={16} className="mt-0.5 shrink-0" />
              <span>{error}</span>
            </div>
          )}

          <button type="submit" className="btn-primary w-full" disabled={loading}>
            {loading ? (
              <span className="h-4 w-4 animate-spin rounded-full border-2 border-white/30 border-t-white" />
            ) : (
              <>
                <LogIn size={16} />
                Ingresar
              </>
            )}
          </button>

          {DEMO_MODE && (
            <p className="text-center text-xs text-slate-500">
              Demostración con datos de ejemplo. Las credenciales ya están cargadas.
            </p>
          )}
        </form>
      </div>
    </div>
  );
}
