# Gates de regresión para los 3 hallazgos de la auditoría (sep-2026)

> Objetivo del pedido: **que estos puntos no vuelvan a pasar**. La respuesta al analista ya existe
> (`respuesta_auditoria_ciberseguridad_2026-09.md`); lo que falta es que las soluciones dejen de
> depender de que alguien se acuerde. Cada mitigación queda **verificada por una máquina** que corta
> el CI, igual que `verificar-sql-llega-por-migracion.js` o el gate de change-detection.
>
> Ámbito: **no cambia comportamiento** salvo un punto — la fuga del §2, que hoy está viva en prod.

---

## Estado real medido (2026-09-08, sobre el código de `main`)

| # | Hallazgo | Mitigación que existe | ¿Puede volver? |
|---|---|---|---|
| 1 | `/api/swagger.json` → 401 | Swagger y `/debug/*` dentro de `if (!app.Environment.IsProduction())` (`Program.cs:874`, `:1027`); `ENV ASPNETCORE_ENVIRONMENT=Production` en `Dockerfile:95` y en los task definitions; el 401 uniforme lo emite `app.UsePlatformSecret()` (`:871`), antes de `UseAuthentication()` (`:1015`) | **Sí.** Nada impide mover un `MapGet("/swagger…")` fuera del `if`, perder el `ENV`, o reordenar el middleware. Cero comprobaciones automáticas. |
| 2 | `/api/DbStudio` en el JS | Ruta renombrada a `api/ConfigColores`, módulo front `config-colores`, `[ApiExplorerSettings(IgnoreApi = true)]`, doble validación (correo **Y** admin) | **No volvió: nunca se fue del todo.** Ver abajo. |
| 3 | `.env`/`.git/config` → 403 | `location ~ /\. { deny all; }` (`frontend/nginx.conf:158`) + WAF | **Sí.** El gate del borde del workflow (paso «Validar nginx y política de caché del borde») comprueba 20 criterios de caché/CSP y **ninguno de dotfiles**. |

### 🔴 Fuga viva del §2 — el señuelo no cierra

El checkbox de validación del rename fue
`grep -rn "db-studio\|DbStudio\|db_studio" frontend/src` ⇒ 0. Ese grep es **case-sensitive** y no ve
la forma camelCase que sí quedó:

- `backend/src/ZooSanMarino.Application/DTOs/DbStudioDtos.cs:403` → `public int DbStudioConnections`
- serializa como `"dbStudioConnections"` en `GET /api/ConfigColores/…` (pool stats)
- `frontend/src/app/features/config-colores/models/config-colores.models.ts:87` y
  `…/config-colores-main.component.html:437` lo consumen **por nombre**

⇒ el literal `dbStudioConnections` **está en el bundle de producción**. Un `grep -i dbstudio main.js`
—exactamente lo que hizo el analista— vuelve a dar positivo. El rename es *obscurity*, no un control;
pero si se hace, se hace completo o no sirve de nada.

---

## Cambios

### A. Cerrar la fuga (único cambio de contrato, ambos lados a la vez)
`DbStudioConnections` → `PoolActiveConnections` (JSON `poolActiveConnections`) en:
`DbStudioDtos.cs` · `DbStudioConcurrencyService.cs:88` · `config-colores.models.ts` ·
`config-colores-main.component.html`. Contrato interno (lo consume solo este módulo) ⇒ sin
compatibilidad hacia atrás que preservar. El nombre interno C# de clases/servicios/config `DbStudio:`
**no se toca** (así lo fijó el plan del rename).

### B. Gate front — `frontend/scripts/verificar-senuelo-modulo.js`
Falla si aparece `dbstudio` / `db-studio` / `db_studio` **case-insensitive** en `frontend/src`.
Tolerancia cero, sin allowlist: si mañana hace falta, se discute en el PR.

### C. Gate backend — `backend/scripts/verificar-superficie-produccion.js`
Sobre el texto de `Program.cs`, `Dockerfile`, task definitions y `DbStudioController.cs`:
1. **Swagger y `/debug/*` solo fuera de Production** — todo `UseSwagger`, `UseSwaggerUI`,
   `Map*("/swagger…")` y `Map*("/debug…")` tiene que caer dentro de un bloque
   `if (!app.Environment.IsProduction())` (se resuelve por balanceo de llaves, no por regex de línea).
2. **`ASPNETCORE_ENVIRONMENT=Production`** presente en `backend/Dockerfile` y en cada
   `backend/deploy/ecs-taskdef*.json`.
3. **Orden del pipeline**: `app.UsePlatformSecret()` antes de `app.UseAuthentication()` y de
   `app.MapControllers()` — es lo que hace que el 401 sea uniforme y no un oráculo de rutas.
4. **Exenciones del filtro de plataforma congeladas**: la lista de paths exentos en
   `PlatformSecretMiddleware` se compara contra un conjunto fijo declarado en el gate. Agregar una
   exención exige tocar el gate ⇒ queda revisada en el PR.
5. **`DbStudioController`**: `[Route("api/ConfigColores")]` + `[Authorize]` +
   `[ApiExplorerSettings(IgnoreApi = true)]` a nivel de clase, y `IgnoreApi = false` **como máximo una
   vez** en el archivo (hoy: `MigrationSummary`).

### D. Gate del borde — dotfiles en el workflow
Tres `check` nuevos en el paso «Validar nginx y política de caché del borde» (mismo helper que ya
usa): `/.env`, `/.git/config`, `/.aws/credentials` ⇒ `403`. Corre contra la imagen ya construida,
antes del push a ECR.

### E. Cableado
Los gates B y C como pasos del job `tests` de `.github/workflows/deploy-production.yml`, junto a los
cinco que ya están.

---

## Casos de prueba

**A** — `dotnet build` 0/0 · `dotnet test` sin regresiones · `yarn build` · la tarjeta de pool del
módulo sigue mostrando el número (verificación visual del template).

**B** — con el árbol actual: **falla** señalando `config-colores.models.ts:87` y el `.html:437`
(prueba de que el gate detecta la fuga real); después de A: **pasa**. Prueba negativa: agregar
`// dbStudio` a un `.ts` ⇒ vuelve a fallar.

**C** — con el árbol actual: pasa los 5 criterios. Pruebas negativas, una por criterio, revirtiendo
sobre copia temporal: mover `app.UseSwagger()` fuera del `if` ⇒ falla (1); borrar el `ENV` del
Dockerfile ⇒ falla (2); mover `UsePlatformSecret()` después de `UseAuthentication()` ⇒ falla (3);
agregar `/api/loquesea` a las exenciones ⇒ falla (4); cambiar la ruta del controller a
`api/[controller]` ⇒ falla (5).

**D** — ✅ validado el 8-sep-2026 sin rebuild, replicando el runtime del CI: `nginx:1.27-alpine` con
`nginx.conf` montada en `conf.d/default.conf`, `nginx-security-headers.conf` en
`/etc/nginx/security-headers.conf` y `dist/browser` como root (las mismas rutas del `Dockerfile`).
`nginx -t` OK; los tres `check` en 403; controles vivos (SPA 200, `.json` inexistente 404, CSP).
Prueba negativa con una copia de la conf sin `location ~ /\.`: los tres responden **200 con
`Content-Type: text/html`** (el index del SPA) y el gate los marca FALLA — que es exactamente la
regresión que este bloque existe para atajar.

## Lo que este plan NO hace (y por qué)

- **No cambia el 401 por 404** en `/api/*`. Es riesgo residual aceptado y documentado (§1 de la
  respuesta); cambiarlo obliga a mover el filtro después del ruteo y debilita el anti-scraping.
- **No mueve el correo autorizado a configuración** (acción #1 recomendada en §2 de la respuesta):
  es un cambio de comportamiento con su propio riesgo de *lockout* y merece su propio plan.
- **No toca la Fase B de la firma por sesión** (bloque abierto de otra sesión en el tracker).
