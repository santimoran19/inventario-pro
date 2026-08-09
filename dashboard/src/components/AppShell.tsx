"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import Link from "next/link";
import {
  Boxes,
  LayoutDashboard,
  ArrowLeftRight,
  LogOut,
  Package,
  Menu,
  X,
} from "lucide-react";
import { api, session, DEMO_MODE, type SessionUser } from "@/lib/api";
import DemoBanner from "./DemoBanner";

const NAV = [
  { href: "/dashboard", label: "Panel", icon: LayoutDashboard },
  { href: "/products", label: "Productos", icon: Boxes },
  { href: "/movements", label: "Movimientos", icon: ArrowLeftRight },
];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();

  const [user, setUser] = useState<SessionUser | null>(null);
  const [checked, setChecked] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);

  // Guardia de sesión: sin usuario, al login.
  useEffect(() => {
    const current = session.user;
    if (!current) {
      router.replace("/login");
      return;
    }
    setUser(current);
    setChecked(true);
  }, [router]);

  async function handleLogout() {
    await api.logout();
    router.replace("/login");
  }

  if (!checked) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-surface-border border-t-brand" />
      </div>
    );
  }

  return (
    <div className="min-h-screen">
      <DemoBanner />

      <div className="flex">
        {/* Sidebar */}
        <aside
          className={`fixed inset-y-0 left-0 z-40 w-60 border-r border-surface-border bg-surface-raised
                      transition-transform lg:static lg:translate-x-0
                      ${menuOpen ? "translate-x-0" : "-translate-x-full"}`}
        >
          <div className="flex h-14 items-center gap-2 border-b border-surface-border px-4">
            <Package size={20} className="text-brand" />
            <span className="font-bold tracking-tight text-white">InventarioPro</span>
          </div>

          <nav className="p-3">
            <ul className="space-y-1">
              {NAV.map(({ href, label, icon: Icon }) => {
                const active = pathname === href;
                return (
                  <li key={href}>
                    <Link
                      href={href}
                      onClick={() => setMenuOpen(false)}
                      className={`flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors
                        ${
                          active
                            ? "bg-brand-soft text-white"
                            : "text-slate-400 hover:bg-white/5 hover:text-slate-200"
                        }`}
                    >
                      <Icon size={17} />
                      {label}
                    </Link>
                  </li>
                );
              })}
            </ul>
          </nav>

          <div className="absolute inset-x-0 bottom-0 border-t border-surface-border p-3">
            <div className="mb-2 px-3">
              <p className="truncate text-sm font-medium text-slate-200">
                {user?.fullName ?? user?.email}
              </p>
              <p className="truncate text-xs text-slate-500">
                {user?.roles.join(", ")}
              </p>
            </div>
            <button
              onClick={handleLogout}
              className="flex w-full items-center gap-3 rounded-lg px-3 py-2 text-sm
                         text-slate-400 transition-colors hover:bg-white/5 hover:text-slate-200"
            >
              <LogOut size={17} />
              Cerrar sesión
            </button>
          </div>
        </aside>

        {menuOpen && (
          <div
            className="fixed inset-0 z-30 bg-black/60 lg:hidden"
            onClick={() => setMenuOpen(false)}
          />
        )}

        {/* Contenido */}
        <div className="min-w-0 flex-1">
          <header className="flex h-14 items-center gap-3 border-b border-surface-border px-4 lg:hidden">
            <button
              onClick={() => setMenuOpen(!menuOpen)}
              className="text-slate-400 hover:text-white"
              aria-label="Abrir menú"
            >
              {menuOpen ? <X size={20} /> : <Menu size={20} />}
            </button>
            <span className="font-bold text-white">InventarioPro</span>
          </header>

          <main className="p-4 sm:p-6 lg:p-8">{children}</main>
        </div>
      </div>
    </div>
  );
}
