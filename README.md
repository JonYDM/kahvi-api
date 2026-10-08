# ☕ kahvi-api

**Backend del SaaS multi-tenant Kahvi** — gestión para cafeterías, mercado latinoamericano.

> _Kahvi_ (finés: "café"). Mascota: **Vito**, el barista que siempre te acompaña.
> Lema: _"Tu cafetería, en buenas manos."_

---

## ¿Qué es Kahvi?

Sistema POS + gestión para cafeterías: flujo de comandas por estación (Mesero → Cocina → Caja), catálogo de productos, ventas, métricas y control de suscripciones. Multi-tenant: una sola instancia sirve a múltiples cafeterías con datos completamente aislados.

---

## Stack

| Componente | Tecnología |
|---|---|
| Lenguaje | C# / .NET 8 (LTS) |
| Arquitectura | Clean Architecture (Domain / Application / Infrastructure / Api) |
| Autenticación | JWT + BCrypt — usuario + PIN 6 dígitos |
| Persistencia (MVP) | Repositorio en memoria (Singleton) |
| Persistencia (prod) | PostgreSQL + EF Core (pendiente) |
| Empaquetado | Docker |
| Hosting | Railway |

---

## Estructura

```
src/
├── Chiron.Domain/          Entidades ricas: Cafeteria, Usuario, Producto, Venta, Comanda…
├── Chiron.Application/     Casos de uso: PuntoVenta, Comandas, Seguridad, Métricas…
├── Chiron.Infrastructure/  Repos en memoria / EF (futuro), JWT, BCrypt
├── Chiron.Api/             Minimal API — todos los endpoints REST
└── Chiron.ConsoleApp/      Smoke test de lógica de dominio
```

---

## Correr en local (modo en memoria, sin BD)

```bash
cd src/Chiron.Api
dotnet run
```

Swagger disponible en `http://localhost:5000/swagger`.

Usuarios de demo (seed automático):

| Usuario | PIN | Rol |
|---|---|---|
| `superadmin` | `123456` | SuperAdmin |
| `admindemo` | `111111` | Administrador |
| `meserodemo` | `222222` | Mesero |
| `cocinademo` | `333333` | Cocina |
| `cajademo` | `444444` | Caja |

---

## Roles

```
SuperAdmin (99)   — dueño de Kahvi: gestiona cafeterías y suscripciones
Administrador (1) — dueño/gerente: acceso total en su tenant
Mesero (2)        — crea comandas
Cocina (3)        — tablero de comandas, avanza estados
Caja (4)          — cobra comandas, genera ventas, corte del día
```

---

## Flujo principal de comandas

```
Mesero  →  POST /api/comandas
Cocina  →  GET  /api/comandas/activas  (polling)
           POST /api/comandas/{id}/avanzar
Caja    →  POST /api/comandas/{id}/cobrar  →  genera Venta
```

---

## Metodología

- GitHub Flow: `main` (protegida) + ramas `feat/*` / `fix/*`
- Conventional Commits en español, atómicos
- `dotnet build` en verde antes de cualquier PR

---

## Documentación

- [`docs/CONTEXTO.md`](./docs/CONTEXTO.md) — Documento maestro del proyecto
- [`docs/METODOLOGIA.md`](./docs/METODOLOGIA.md) — Git Flow y Conventional Commits
- [`docs/DESPLIEGUE.md`](./docs/DESPLIEGUE.md) — Railway + Docker

---

## Repos del producto

| Repo | Descripción |
|---|---|
| [`kahvi-api`](https://github.com/JonYDM/kahvi-api) | Este repo — backend .NET |
| [`kahvi-web`](https://github.com/JonYDM/kahvi-web) | Frontend React + Vite + PWA (en construcción) |
