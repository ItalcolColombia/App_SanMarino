# SoporteBot — Loop de soporte automatizado sobre el módulo de Tickets

> **Objetivo del negocio:** que el dev global (moiesbbuga@gmail.com) tenga un "puente" que entra a producción, filtra sus tickets, los toma, los analiza por país, resuelve/propone solución y notifica el cierre — como ciclo de **mejora continua** para bajar la tasa de errores.
>
> **Decisiones del dueño (2026-07-03):** autonomía = *full-auto incl. deploy*; auth = *PAT de servicio*; disparador = *cron (`/schedule`)*.
>
> **Postura de ingeniería:** se entrega el full-auto **con red de seguridad** en la parte irreversible (deploy a prod), porque el combo "agente desatendido + push sin revisar a RDS/ECS" es la causa raíz del incidente SIGSEGV documentado en `CLAUDE.md`. La autonomía es total en triage/análisis/PR; el deploy pasa por compuerta automática con validación + auto-rollback.

---

## 1. Enfoque arquitectónico

Tres piezas desacopladas, ninguna toca la BD de prod directamente — todo va por los endpoints REST ya existentes del módulo de tickets:

```
┌─────────────────┐   PAT     ┌──────────────────────┐   HTTP    ┌───────────────────┐
│  Loop / Cron    │──────────▶│  CLI-puente          │──────────▶│  API prod tickets │
│ (/schedule)     │           │  tools/soporte-bot/  │           │  (JWT-equivalente │
│  orquesta       │◀──────────│  wrappea auth+calls  │◀──────────│   vía PAT)        │
└─────────────────┘  tickets  └──────────────────────┘  DTOs     └───────────────────┘
        │
        ▼  (cambios de código → rama + PR + pipeline existente)
   git / gh / CI  ──▶  push main-produccion  ──▶  ECS deploy  ──▶  verificación post-deploy
```

### A. Backend — PAT de servicio (token de larga duración, exento de reCAPTCHA)
Necesario porque `/api/Auth/login` en prod exige **reCAPTCHA + payload AES** (`AuthController.cs:88-113`) → un cron headless no puede loguear.

- **Entidad nueva** `ServiceToken` (tabla `service_tokens`): `id`, `name`, `token_hash` (SHA-256, nunca el token plano), `user_guid` (mapea a moiesbbuga), `scopes` (p.ej. `tickets:read tickets:write`), `expires_at`, `revoked_at`, `last_used_at`, auditoría.
- **Auth handler nuevo** (esquema `ServiceToken`): valida `Authorization: Bearer sk_...` → carga el `ClaimsPrincipal` equivalente al del usuario dueño del token → **no pasa por login/reCAPTCHA**.
- **Policy de alcance:** el PAT SOLO autoriza rutas `api/tickets/**` (y `auth/ping`). Cualquier otra ruta con PAT = 403.
- **Emisión:** endpoint autenticado (JWT normal, rol admin) `POST /api/service-tokens` → devuelve el token plano **una sola vez**; se persiste solo el hash. `DELETE /api/service-tokens/{id}` para revocar.
- **Migración EF idempotente** (`AddServiceTokens`): `CREATE TABLE IF NOT EXISTS`. Sin seeds de datos sensibles.

### B. CLI-puente — `tools/soporte-bot/`
Encapsula auth (PAT) + las llamadas estándar, para que el loop y el cron usen **la misma ruta testeada**. Runtime: **.NET console** (fit con el ecosistema; solo `HttpClient`).

Comandos:
| Comando | Endpoint envuelto |
|---|---|
| `whoami` | `GET /api/auth/ping` |
| `list --estado --tipo --pais` | `GET /api/tickets/global?assignedToGuid=<yo>` |
| `get <id>` | `GET /api/tickets/{id}` (+ imágenes on-demand) |
| `tomar <id>` | `POST /api/tickets/{id}/tomar` |
| `nota <id> <txt> [--interna]` | `POST /api/tickets/{id}/notas` |
| `estado <id> <nuevo> [--solucion]` | `PATCH /api/tickets/{id}/estado` |

Config (base URL prod, PAT) → variables de entorno / `appsettings` del tool, **nunca hardcodeado**.

### C. El loop (skill `/loop` supervisado o `/schedule` cron desatendido)
Orquestación por vuelta:
1. `list` → cola de tickets míos (ABIERTO / EN_ANALISIS / EN_IMPLEMENTACION), ordenados por prioridad/antigüedad.
2. Por ticket: `tomar` (→ EN_ANALISIS, idempotente) + nota interna "SoporteBot analizando".
3. **Clasificar por país + tipo:** DUDAS · SOPORTE-config/uso · SOPORTE-bug(código) · DESARROLLO.
4. Investigar en el código (grep/read); si es bug, **reproducir en local** (`make up`, :5433).
5. Rama de solución:
   - **DUDAS / config-uso** → nota pública con la respuesta + `SolucionDescripcion` → `estado SOLUCIONADO` (dispara correo automático). Sin código.
   - **bug / desarrollo** → branch, fix, `dotnet build` + `dotnet test` + `yarn build`, **PR** con link al ticket, nota con el PR, ticket → EN_IMPLEMENTACION.
6. **Deploy gate** (ver §2) → solo si pasa todos los guardrails.

---

## 2. Guardrails de deploy (NO NEGOCIABLES)

Estos son la "red de seguridad" del full-auto. Van codificados en el loop:

1. **Migraciones EF = SIEMPRE compuerta humana.** Si el fix incluye `migrations add`, NO se auto-deploya. Causa raíz documentada de los crashes de prod (`CLAUDE.md` §🗄️/🚀).
2. **Nunca `dotnet ef database update` contra RDS prod desde el bot.** Deploy solo por el pipeline (push a `main-produccion`); EF corre lo pendiente al arrancar.
3. **Gate de calidad antes de merge/deploy:** repro local OK + `dotnet build` 0/0 + `dotnet test` verde + `yarn build` OK + **CI verde en el PR**. Si algo falla → no deploya; ticket queda EN_IMPLEMENTACION con nota del bloqueo.
4. **Verificación post-deploy obligatoria** (`CLAUDE.md` §🚀): confirmar TaskDef/imagen corriendo; detectar SIGSEGV/`EssentialContainerExited`/rollback silencioso. Si NO estabiliza → **auto-rollback** (queda la TaskDef previa) + nota de bloqueo + **NO** marcar SOLUCIONADO + alerta.
5. **Kill switch + rate limit + ventana horaria + canary:** flag `bot.enabled`; máx N auto-deploys/día; no deployar fuera de horario hábil sin OK; cada auto-deploy **notifica** (correo/canal).
6. **Alcance por riesgo:** auto-deploy arranca habilitado SOLO para **bajo riesgo** (front, o back sin migración ni cambio de contrato). Migración, cambio de contrato de API, o cambios en **cálculos de liquidación / inventario / indicadores** = compuerta humana siempre.

---

## 3. Entrega por fases (cada una aporta valor y sube el riesgo gradualmente)

- **Fase 0 — Infra sin riesgo prod:** PAT backend + migración idempotente + CLI-puente + smoke test **LOCAL** (:5433).
- **Fase 1 — Triage + análisis (solo lee/toma/nota):** loop contra prod; clasifica y deja diagnóstico. Cero cambio de código. *Ya baja tu carga.*
- **Fase 2 — Auto-fix + PR (código en rama, sin deploy):** repro, fix, build/test, PR, ticket EN_IMPLEMENTACION.
- **Fase 3 — Auto-deploy con guardrails (§2):** arranca por front/low-risk; cron `/schedule`. Se amplía el alcance cuando el output demuestre confianza.

---

## 4. Archivos a crear / modificar

**Backend**
- `Domain/Entities/ServiceToken.cs` (nuevo)
- `Infrastructure/Persistence/Configurations/ServiceTokenConfiguration.cs` (nuevo)
- `Infrastructure/Migrations/<ts>_AddServiceTokens.cs` (nuevo, idempotente)
- `Infrastructure/Auth/ServiceTokenAuthHandler.cs` + registro esquema/policy en `Program.cs`
- `Application/Interfaces/IServiceTokenService.cs` + `Infrastructure/Services/ServiceTokenService.cs`
- `API/Controllers/ServiceTokensController.cs` (emitir/revocar, rol admin)

**Tooling**
- `tools/soporte-bot/` (proyecto .NET console + README)

**Loop / cron**
- Definición del `/schedule` (cron) + prompt del loop con los guardrails embebidos.

Sin cambios en el módulo de tickets existente (se reusa tal cual).

---

## 5. Casos de prueba

- **PAT:** válido → mapea a moiesbbuga; fuera de `api/tickets/**` → 403; revocado/expirado → 401; se persiste solo hash.
- **Loop:** ticket ABIERTO → `tomar` → EN_ANALISIS (idempotente re-run).
- **DUDAS:** → SOLUCIONADO dispara correo (verificar `email_queue` status=sent, plantilla con logo).
- **Bug:** → PR creado, `dotnet build`/`dotnet test`/`yarn build` verdes, ticket EN_IMPLEMENTACION con nota del PR.
- **Deploy fail simulado:** → auto-rollback (TaskDef previa) + ticket NO SOLUCIONADO + alerta.
- **Migración detectada en el diff:** → compuerta (no auto-deploy), nota de bloqueo.
- **Multipaís:** la solución se enmarca por país del ticket (levante/producción/inventario según corresponda).

---

## 6. Fuera de alcance (por ahora)
- Auto-resolución de tickets que tocan cálculos financieros/inventario sin revisión (siempre gate).
- Modificar la máquina de estados o las plantillas de correo existentes.
- Rollback de datos (solo rollback de despliegue/TaskDef).
