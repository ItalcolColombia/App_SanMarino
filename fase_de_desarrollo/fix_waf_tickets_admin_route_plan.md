# Fix — WAF bloquea rutas `/admin` en módulo Tickets (403 en transferencia)

## Síntoma
En producción, en **Gestionar Tickets** (`/tickets/admin`), al abrir la bandeja y/o transferir, las llamadas:
- `GET /api/tickets/admin?anio=...&page=...&pageSize=...`
- `GET /api/tickets/admin/resolutores`

devuelven **403 Forbidden** con `server: awselb/2.0` y body HTML. La página queda rota (percibido como 404).

## Diagnóstico (causa raíz)
El 403 **NO** lo genera la app .NET, lo genera la capa **ALB + AWS WAF** (`server: awselb/2.0`, body HTML genérico). Es la firma del managed rule group **`AWSManagedRulesAdminProtectionRuleSet`** (regla `AdminProtection_URIPATH`), que bloquea cualquier URL cuyo path contenga `/admin`.

Evidencia:
- Solo fallan las 2 URLs que contienen `admin`; el resto del módulo (`/api/tickets/...`) funciona.
- El JWT del usuario trae rol `Admin` + permiso `tickets.admin` → la autorización de la app pasaría.
- `X-Secret-Up` lo agrega el `authInterceptor` en TODAS las requests → no es el diferenciador.

## Enfoque arquitectónico
**Renombrar las rutas de API** para que el path no contenga `admin`: `admin` → `global` (coincide con la semántica existente "Bandeja global del super admin"). Sin tocar WAF, sin debilitar seguridad, todo en el repo y desplegable por el pipeline.

- Refactor SIN cambio de comportamiento: misma lógica, mismo handler, mismos DTOs; solo cambia el string del path HTTP.
- Se mantienen: nombres de métodos del servicio Angular (`admin()`, `getResolutoresAdmin()`), la **ruta del SPA** `/tickets/admin` (carga bien, la sirve CloudFront/S3), el ítem de menú sembrado y las navegaciones internas.

## Archivos a modificar
| Capa | Archivo | Cambio |
|---|---|---|
| Backend | `backend/src/ZooSanMarino.API/Controllers/TicketsController.cs:225` | `[HttpGet("admin")]` → `[HttpGet("global")]` |
| Backend | `backend/src/ZooSanMarino.API/Controllers/TicketsController.cs:241` | `[HttpGet("admin/resolutores")]` → `[HttpGet("global/resolutores")]` |
| Frontend | `frontend/src/app/features/tickets/services/ticket.service.ts:122` | `${this.baseUrl}/admin` → `${this.baseUrl}/global` |
| Frontend | `frontend/src/app/features/tickets/services/ticket.service.ts:126` | `${this.baseUrl}/admin/resolutores` → `${this.baseUrl}/global/resolutores` |

Resultado: `/api/tickets/global` y `/api/tickets/global/resolutores`.

## Cambios de BD / SQL
**Ninguno.** No hay migración. El ítem de menú con `route='/tickets/admin'` (SPA) no se toca.

## Reglas de negocio
Sin cambios. Comportamiento, contratos (query params, DTOs de respuesta) y autorización idénticos.

## Casos de prueba / validación
1. `cd backend && dotnet build` → 0 errores.
2. `cd frontend && yarn build` → OK.
3. Post-deploy (manual): como Admin, abrir bandeja global → `GET /api/tickets/global` 200 con lista paginada; dropdown de resolutores → `GET /api/tickets/global/resolutores` 200; transferir un ticket → OK.
4. Confirmar que ya no hay 403 de `awselb/2.0` en esas URLs.

## Riesgos / notas de deploy
- **Orden de deploy:** desplegar backend (o back+front juntos con `deploy-all`). Si el front cacheado sigue llamando `/admin` un rato, esos usuarios ya estaban rotos (hoy 403) → sin regresión.
- **Pendiente relacionado (mismo bug):** `GET /api/Company/admin` (`CompanyController.cs:31` ← `company.service.ts:46`) cae en la misma regla WAF. No incluido aún; decidir si se renombra (`admin`→`global`) en el mismo deploy.
- Verificación post-deploy obligatoria (sección 🚀 de CLAUDE.md): confirmar TaskDef/imagen reales en ECS.
