# Kahvi — Roadmap de producto (próximas fases)

> Documentado en sesión 2026-10-09. Retomar en próxima sesión.

---

## Fase 2 — Catálogo público / página de la cafetería

**Qué es:** URL pública por cafetería donde los clientes ven el menú sin login.

**Rutas propuestas:**
```
kahviapp.netlify.app/              → landing pública de Kahvi (SaaS)
kahviapp.netlify.app/menu/[slug]   → menú público de una cafetería
kahviapp.netlify.app/login         → login del staff (actual)
kahviapp.netlify.app/app/*         → app del admin (actual)
```

**Implementación:**
- El `slug` es el nombre normalizado de la cafetería (ej: `cafe-demo`)
- Nuevo endpoint público en backend: `GET /api/publico/menu/{slug}` — devuelve productos activos + info de la cafetería sin auth
- Nueva ruta en frontend `/menu/:slug` sin ProtectedRoute
- El Admin configura su slug desde `/app/admin` (Settings)
- No requiere cambios grandes al backend — es un GET público

**Por qué primero:** es el gancho comercial más rápido. "Mira, tus clientes pueden ver tu menú aquí sin descargar nada."

---

## Fase 3 — Sistema de recompensas con QR

**Qué es:** Tarjeta de puntos por cliente, QR escaneable, canje de recompensas.

**Flujo:**
1. Cliente se registra (con Google o teléfono) → recibe tarjeta digital con QR
2. Cajero escanea el QR al momento de cobrar → suma puntos automáticamente
3. Al alcanzar X visitas/puntos → recompensa disponible
4. El cajero escanea el QR al canjear → marca recompensa como usada
5. Admin ve dashboard: contador de clientes, puntos activos, recompensas canjeadas

**Componentes a implementar:**
- Backend: entidades `ClienteKahvi`, `TarjetaPuntos`, `Recompensa`, `CanjeRecompensa`
- QR: librería `qrcode` en frontend para generar, `html5-qrcode` para escanear desde la cámara
- Portal cliente: vista `/portal` con historial de compras + puntos + QR propio
- Vista admin: `/app/recompensas` con configuración (X visitas = qué recompensa) + dashboard

**Diferenciador clave:** hace que el cliente QUIERA volver a la misma cafetería.

---

## Fase 4 — Portal del cliente con Google Auth

**Qué es:** Los clientes finales se loguean con Google y ven sus compras, puntos y QR.

**Implementación:**
- Google OAuth en backend .NET (`Microsoft.AspNetCore.Authentication.Google`)
- Nuevo rol `ClienteKahvi` (diferente al staff de la cafetería)
- Nuevo endpoint: `POST /api/auth/google` — intercambia token de Google por JWT de Kahvi
- Frontend: botón "Continuar con Google" en el login (paso separado del staff)
- Rutas: `/portal/*` para el cliente final

**Nota:** El portal de cliente de Chiron (veterinarias) fue podado al convertir a Kahvi. La arquitectura multi-tenant ya soporta esto — solo hay que agregar el rol y los endpoints.

---

## Orden recomendado

1. **Catálogo público** — más rápido, mejor gancho comercial para conseguir cafeterías
2. **Recompensas + QR** — el diferenciador que hace que el dueño pague la renta
3. **Portal del cliente + Google Auth** — el enganche del cliente final

---

## Notas técnicas

- El backend ya tiene `PlanSuscripcion` y `Cobros` para monetizar por tenant
- El QR puede generarse en el frontend con `qrcode.react` (sin backend)
- El escaneo del QR desde la cámara requiere HTTPS (ya tenemos en Netlify/Railway)
- Google OAuth requiere crear un proyecto en Google Cloud Console y configurar `client_id` + `client_secret` como variables de entorno en Railway
