# ☕ Kahvi — Contexto del Proyecto

> **Este es el documento maestro.** Se lee al inicio de cada sesión para retomar el hilo rápidamente.
> Última actualización: 2026-10-08

---

## ¿Qué es Kahvi?

**Kahvi** (finés: "café") es un SaaS multi-tenant de gestión para **cafeterías** del mercado latinoamericano. El producto nace como una adaptación del núcleo técnico de Chiron (SaaS de veterinarias), reutilizando su Clean Architecture, JWT multi-tenant, PuntoVenta y sistema de suscripciones.

La mascota es **Vito**, un barista chibi con gorra y delantal. Lema: _"Tu cafetería, en buenas manos."_

### Modelo de negocio
- **SaaS por suscripción mensual** (se renta, no se vende).
- Precio objetivo accesible (~199-349 MXN/mes por cafetería).
- Una sola instancia en producción sirve a múltiples cafeterías (multi-tenant).

### Propuesta de valor

| Problema de la competencia | Mejora de Kahvi |
|---|---|
| Caro o complicado | Precio accesible, flujo simple |
| Sin flujo de comandas integrado | **Mesero → Cocina → Caja** en tiempo real (polling) |
| Solo escritorio | PWA instalable en tablets y móviles |
| Sin marca amigable | Mascota Vito, identidad cálida y cercana |

### Diferenciador clave
**Flujo de comandas por estación**: el mesero crea la comanda en su tablet, cocina la ve aparecer en su tablero y avanza el estado, caja la cobra y genera la venta. Todo en la misma app PWA, sin papel ni sistemas separados.

---

## Decisiones técnicas

| Decisión | Elección | Razón |
|---|---|---|
| Lenguaje backend | **C# / .NET 8 (LTS)** | SOLID y DI nativos, tipado fuerte, multiplataforma, económico en Railway |
| Arquitectura | **Clean Architecture** (Domain / Application / Infrastructure / Api) | Separación de capas, inversión de dependencias, fácil de extender |
| Persistencia inicial | **Repositorio en memoria** | Validar lógica y demo sin instalar BD |
| Persistencia producción | **PostgreSQL + EF Core** (pendiente) | Gratis, potente en Linux |
| Despliegue | **Railway (Docker)** | Una instancia sirve todos los tenants; Docker evita lock-in |
| Frontend | **React + Vite + TypeScript + PWA** | Instalable en tablet/móvil sin app store |
| Tiempo real comandas | **Polling con TanStack Query** | Suficiente para MVP; sin complejidad de WebSockets |
| Autenticación | **usuario + PIN de 6 dígitos, JWT** | Sin fricción de correo (patrón validado en Chiron/Patwi) |

### Multi-tenant
- **Tenant = Cafetería**: cada cafetería que renta Kahvi es un inquilino independiente.
- Aislamiento de datos: todas las entidades operativas llevan `CafeteriaId`.
- El backend valida el tenant en **cada request** usando el claim `cafeteriaId` del JWT.
- El frontend **nunca decide el tenant**: lo hereda del token.

### Monolito modular
Misma decisión que Chiron: un monolito modular hasta que el negocio justifique otra cosa. Los módulos tienen bajo acoplamiento y alta cohesión; pueden extraerse a servicios independientes en el futuro.

---

## Módulos del backend

| Módulo | Estado | Descripción |
|---|---|---|
| `Cafeterias` | ✅ | Tenant. Nombre, teléfono, suscripción (mensual/anual), activación. |
| `Usuarios` | ✅ | Staff con roles (Administrador/Mesero/Cocina/Caja) y SuperAdmin. PIN + JWT. |
| `PuntoVenta` | ✅ | Catálogo de productos (Cafe/Desayunos/Postres/Bebidas/Otro), ventas con stock. |
| `Comandas` | ✅ | Flujo Mesero→Cocina→Caja. Estados: Recibida/EnPreparacion/Lista/Entregada/Cancelada. |
| `Sucursales` | ✅ | Multi-local por cafetería. |
| `Suscripciones` | ✅ | PagoSuscripcion, renovación, control por SuperAdmin. |
| `Metricas` | ✅ | Dashboard (ventas del día, top productos) y resumen SuperAdmin. |

### Roles del sistema
```
SuperAdmin (99)    — dueño de Kahvi: gestiona cafeterías y suscripciones
Administrador (1)  — dueño/gerente de la cafetería: acceso total dentro de su tenant
Mesero (2)         — toma pedidos y crea comandas
Cocina (3)         — ve el tablero de comandas y avanza estados
Caja (4)           — cobra comandas y ve el corte del día
```

---

## Endpoints REST (modo en memoria / MVP)

```
POST   /api/auth/login                      público
POST   /api/auth/identificar                público (devuelve rol sin token completo)

GET/POST   /api/usuarios/staff              Admin
POST   /api/usuarios/{id}/resetear-pin      Admin
GET    /api/usuarios/me                     autenticado

POST   /api/productos                       Admin
PUT    /api/productos/{id}                  Admin
POST   /api/productos/{id}/desactivar       Admin
GET    /api/productos                       autenticado

POST   /api/ventas                          Caja/Admin
GET    /api/ventas                          Admin
GET    /api/ventas/resumen                  Admin

GET    /api/sucursales                      autenticado

GET    /api/metricas/dashboard              Admin
GET    /api/metricas/superadmin             SuperAdmin

POST   /api/comandas                        Mesero/Admin
GET    /api/comandas/activas               Cocina/Caja/Admin (polling)
POST   /api/comandas/{id}/avanzar          Cocina/Admin
POST   /api/comandas/{id}/cancelar         Mesero/Admin
POST   /api/comandas/{id}/cobrar           Caja/Admin

GET/POST/PUT /api/admin/cafeterias*        SuperAdmin
GET/POST     /api/admin/administradores    SuperAdmin
```

---

## Seed de demo (modo en memoria)

Al arrancar sin PostgreSQL, el sistema siembra automáticamente:

| Usuario | PIN | Rol |
|---|---|---|
| `superadmin` | `123456` | SuperAdmin |
| `admindemo` | `111111` | Administrador |
| `meserodemo` | `222222` | Mesero |
| `cocinademo` | `333333` | Cocina |
| `cajademo` | `444444` | Caja |

Cafetería de demo: **"Cafetería Demo"**.

---

## Metodología de trabajo

- **GitHub Flow**: `main` (protegida) + ramas `feat/*` / `fix/*`. La rama se borra al hacer merge.
- **Conventional Commits en español**, atómicos.
- **Regla de oro**: no se hace `commit`, `push` ni `merge` sin confirmación explícita del usuario.
- **Documentación viva en `docs/`**: actualizar con cada feature.
- Antes de entregar: `dotnet build` en verde (0 errores). Al pasar a Postgres: `dotnet ef migrations add`.

---

## Repositorios

| Repo | URL | Descripción |
|---|---|---|
| `kahvi-api` | https://github.com/JonYDM/kahvi-api | Backend .NET 8 (este repo) |
| `kahvi-web` | https://github.com/JonYDM/kahvi-web | Frontend React + Vite + PWA |

> **Nota**: Kahvi es una adaptación limpia de Chiron (veterinarias). Los repos originales
> `chiron-vet` y `chiron-web` están en producción y **no se modifican**.

---

## Estado actual

- **Backend (kahvi-api)**: rama `feat/conversion-cafeteria`, build verde 0 errores, modo repo en memoria.
  - ✅ Módulos: Cafeterias, Usuarios, PuntoVenta, Comandas, Sucursales, Suscripciones, Metricas.
  - ⏳ Pendiente: Postgres + EF migraciones (cuando se valide el MVP demo).
- **Frontend (kahvi-web)**: pendiente. Partirá de `chiron-web` (Patwi): React + Vite + TanStack Query + PWA.
- **Siguiente paso**: crear `kahvi-web`, rebrand Vito/paleta, cablear al backend.

---

## Datos del contexto de negocio

- Desarrollador: JonYDM / Jonathan Ocampo, Temixco, Morelos.
- Mercado inicial: cafeterías LATAM.
- Clave del mercado LATAM: **los clientes compran cuando VEN el sistema funcionando** → prioridad en demos funcionales.
- Precio objetivo: ~$199-349 MXN/mes por cafetería.
- Paleta de marca (guía de Vito):
  - Café intenso `#2B1F19` (texto, detalles)
  - Café principal `#6B4F3B` (piel de Vito)
  - Caramelo `#C9BB5A` (acento)
  - Crema `#F4E9DB` (fondos)
  - Verde menta `#6FAF9A` (acento UI)
