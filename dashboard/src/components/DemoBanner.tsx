import { Info } from "lucide-react";
import { DEMO_MODE } from "@/lib/api";

/**
 * Cartel permanente cuando la app corre con datos de ejemplo.
 * Es deliberadamente visible: nadie debería confundir la demo con datos reales.
 */
export default function DemoBanner() {
  if (!DEMO_MODE) return null;

  return (
    <div className="flex items-center gap-2 border-b border-amber-500/20 bg-amber-500/10 px-4 py-2 text-xs text-amber-300">
      <Info size={14} className="shrink-0" />
      <span>
        Modo demostración: los datos son de ejemplo y se reinician al recargar la página.
      </span>
    </div>
  );
}
