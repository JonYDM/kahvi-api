# ☕ Kahvi Web — Contrato del backend para el frontend

> Documento para el equipo de frontend (`kahvi-web`). Define exactamente qué ofrece
> la API, cómo autenticarse y los patrones de integración. Actualizar con cada cambio de contrato.
> Última actualización: 2026-10-08

---

## Base URL

- **Desarrollo local**: `http://localhost:5000` (o el proxy `/api` de Vite)
- **Producción**: pendiente de despliegue en Railway

---

## Autenticación

**Flujo login:**
```
POST /api/auth/login
Body: { "nombreUsuario": "admindemo", "pin": "111111" }
Response 200: { "token": "<JWT>", "rol": "Administrador", "cafeteriaId": "<guid>", "nombre": "..." }
Response 401: { "error": "Usuario o PIN incorrecto" }
```

El JWT incluye los claims:
- `sub` o `nameidentifier` — userId (Guid)
- `cafeteriaId` — tenant del usuario (Guid.Empty para SuperAdmin)
- `role` — rol del usuario

**Identificar (sin token completo):**
```
POST /api/auth/identificar
Body: { "nombreUsuario": "admindemo" }
Response 200: { "existe": true, "rol": "Administrador" }
```

**Header en requests autenticados:**
```
Authorization: Bearer <token>
```

---

## Endpoints por rol

### Todos los roles autenticados

```
GET /api/productos          — catálogo activo de la cafetería
GET /api/sucursales         — sucursales de la cafetería
```

### Mesero (y Administrador)

```
POST /api/comandas
Body: {
  "mesa": "Mesa 3",
  "meseroNombre": "Ana",
  "items": [
    { "productoId": "<guid>", "nombre": "Latte", "cantidad": 2, "precio": 55.00, "nota": "sin azúcar" }
  ]
}
Response 201: { "id": "<guid>" }
```

### Cocina (y Caja, Administrador)

```
GET /api/comandas/activas
Response 200: [
  {
    "id": "<guid>",
    "folio": 1,
    "mesa": "Mesa 3",
    "meseroNombre": "Ana",
    "estado": "Recibida",          // Recibida | EnPreparacion | Lista | Entregada | Cancelada
    "creadaEn": "2026-10-08T07:30:00Z",
    "total": 110.00,
    "items": [
      { "productoId": "<guid>", "nombre": "Latte", "cantidad": 2, "precioUnitario": 55.00, "nota": "sin azúcar" }
    ]
  }
]
```

> **Polling**: TanStack Query con `refetchInterval: 5000` (5 segundos) en el tablero de cocina.

```
POST /api/comandas/{id}/avanzar
Response 200: { "nuevoEstado": "EnPreparacion" }
```

### Caja (y Administrador)

```
POST /api/comandas/{id}/cancelar
Response 204

POST /api/comandas/{id}/cobrar
Body: { "metodoPago": "Efectivo", "montoRecibido": 200.00 }
Response 200: { "ventaId": "<guid>" }
```

### Administrador

```
GET  /api/ventas                     — historial de ventas del día
GET  /api/ventas/resumen             — totales por método de pago
POST /api/ventas                     — registrar venta directa (sin comanda)

GET  /api/metricas/dashboard         — ventas del día, top productos, comandas activas

POST /api/productos                  — crear producto
     Body: { "nombre": "Espresso", "categoria": "Cafe", "precio": 35.00, "stock": 999 }
PUT  /api/productos/{id}             — editar producto
POST /api/productos/{id}/desactivar  — baja lógica

GET/POST   /api/usuarios/staff       — listar/crear staff de la cafetería
POST /api/usuarios/{id}/resetear-pin — resetear PIN
```

### SuperAdmin

```
GET/POST       /api/admin/cafeterias          — gestionar tenants
GET/PUT        /api/admin/cafeterias/{id}      — detalle + activar/desactivar
POST           /api/admin/cafeterias/{id}/renovar
GET            /api/admin/administradores      — listar admins de todas las cafeterías
POST           /api/admin/usuarios-admin       — crear admin para una cafetería
GET            /api/admin/metricas             — resumen global (ventas, tenants activos)
```

---

## Manejo de errores

El backend siempre devuelve:
```json
{ "error": "Mensaje legible para el usuario" }
```

Códigos relevantes:
- `200` / `201` / `204` — éxito
- `400` — validación fallida (`error` describe el problema)
- `401` — token ausente, inválido o expirado → redirigir al login
- `403` — rol sin permiso para esta acción
- `404` — recurso no encontrado (o de otro tenant — mismo código intencional)

---

## Categorías de producto

```
Cafe | Desayunos | Postres | Bebidas | Otro
```

## Métodos de pago

```
Efectivo | Tarjeta | Transferencia
```

## Estados de comanda

```
Recibida → EnPreparacion → Lista → Entregada
                         ↘ Cancelada
```

---

## CORS

En desarrollo, el frontend usa el proxy de Vite:
```ts
// vite.config.ts
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true,
    }
  }
}
```

En producción, configurar `Cors:Origenes` en las variables de entorno del backend con el dominio de Netlify/Vercel.

---

## Notas de implementación del frontend

- El `cafeteriaId` **nunca** se lee del body de la respuesta para tomar decisiones de filtrado — ya está embebido en el JWT y el backend lo aplica en cada request. El frontend solo lo necesita para mostrar datos de la sesión.
- Patrón de auth: `AuthContext` con `setTokenAccessor` en `lib/http.ts` (ver Patwi/chiron-web como referencia).
- Para el tablero de cocina: `useQuery({ queryKey: ['comandas', 'activas'], queryFn: ..., refetchInterval: 5000 })`.
- El login retorna el rol → redirigir a la ruta inicial por rol (mesero→`/mesero`, cocina→`/cocina`, caja→`/caja`, admin→`/app`, superadmin→`/admin`).
