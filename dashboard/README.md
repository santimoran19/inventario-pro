<div align="center">

# InventarioPro — Dashboard

**Next.js front-end for the [InventarioPro API](https://github.com/santimoran19/inventario-pro): inventory control with full stock traceability.**

[![Next.js](https://img.shields.io/badge/Next.js-15-000000?style=flat-square&logo=nextdotjs&logoColor=white)](https://nextjs.org/)
[![React](https://img.shields.io/badge/React-19-61DAFB?style=flat-square&logo=react&logoColor=black)](https://react.dev/)
[![TypeScript](https://img.shields.io/badge/TypeScript-5.7-3178C6?style=flat-square&logo=typescript&logoColor=white)](https://www.typescriptlang.org/)
[![Tailwind](https://img.shields.io/badge/Tailwind-3.4-06B6D4?style=flat-square&logo=tailwindcss&logoColor=white)](https://tailwindcss.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)

</div>

---

![Dashboard](docs/screenshots/02-dashboard.png)

---

## What it does

A dashboard for a small shop to control what it has in stock, what it's worth, and what needs restocking. It consumes the InventarioPro REST API (.NET 9 + PostgreSQL).

The design follows the same principle as the backend: **stock is never edited directly**. Every change is a movement — an entry, an exit, or a physical-count adjustment — with a reason and an optional document reference. The dashboard reflects that: there is no "edit stock" field anywhere, only a movement form.

---

## Screens

### Products

Search, category filter, sortable columns, pagination, and colour-coded stock badges — red at zero, amber below the reorder threshold, green above it.

![Products](docs/screenshots/03-products.png)

### Stock movement

The form previews the resulting stock before saving, and blocks an exit larger than what's available — the same rule the API enforces server-side, surfaced early so the operator doesn't hit an error after typing everything.

![Movement](docs/screenshots/04-movement-modal.png)

### Movement history

Append-only log. Every record shows the delta, the resulting stock, the reason, the document and the user.

![Movements](docs/screenshots/05-movements.png)

---

## Running it

```bash
npm install
cp .env.example .env.local
npm run dev
```

Open **http://localhost:3000**.

### Demo mode

`NEXT_PUBLIC_DEMO_MODE=true` runs the dashboard against in-memory sample data, with no backend required. Useful for deploying the front-end on its own, or for evaluating the UI without setting up PostgreSQL. A persistent banner makes it clear the data isn't real.

Set it to `false` and point `NEXT_PUBLIC_API_URL` at a running API instance to use it for real.

```env
NEXT_PUBLIC_API_URL=http://localhost:8080
NEXT_PUBLIC_DEMO_MODE=false
```

---

## Notable implementation details

**Token handling.** Access tokens live in `sessionStorage`, which clears when the tab closes — unlike `localStorage`, which persists indefinitely. A single `refresh` promise is shared across concurrent 401s, so several failing requests trigger one refresh rather than a stampede.

**Search debounce.** Typing in the search box waits 300ms before firing, instead of one request per keystroke.

**Cancelled effects.** Every data-loading effect carries a `cancelled` flag so a response that arrives after the component unmounts doesn't set state on a dead component.

**Optimistic UI is deliberately absent.** Stock is the kind of data where showing a value that later turns out wrong is worse than a 200ms wait. The table refetches after a movement rather than guessing.

---

## Structure

```
src/
├── app/
│   ├── login/          Authentication
│   ├── dashboard/      KPIs, valuation chart, restock alerts
│   ├── products/       Table with filters, sorting, pagination
│   └── movements/      Stock movement history
├── components/
│   ├── AppShell        Sidebar, session guard, navigation
│   ├── MovementModal   Stock movement form
│   └── DemoBanner      Sample-data notice
└── lib/
    ├── api.ts          HTTP client, JWT refresh, demo switch
    ├── demo.ts         In-memory fixtures
    ├── types.ts        Mirrors the API DTOs
    └── format.ts       es-AR currency and date formatting
```

---

## Related

- [inventario-pro](https://github.com/santimoran19/inventario-pro) — the .NET 9 API this dashboard consumes

## License

MIT — see [LICENSE](LICENSE).
