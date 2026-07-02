# 🔄 CONTEXTO DE TRASPASO — sesión de refactor/optimización multi-país

> Pegá este archivo (o su ruta) al abrir un chat nuevo para continuar sin perder nada.
> **Fecha de corte:** 2026-07-02 · **Rama:** `refactor/optimizacion-multipais` (main INTOCABLE)
> Working tree **limpio** (todo commiteado). Último commit: `1c48ea3`.

---

## 1) Qué es este trabajo

App San Marino (avícola multi-país): **backend .NET 9 Clean Architecture** + **frontend Angular 20 standalone**, desplegada en **AWS ECS**. Monorepo. Reglas vinculantes en `CLAUDE.md` (raíz).

**Objetivo del loop (petición original del usuario):**
1. Unificar módulos clonados por país con parametrización independiente.
2. Mover cómputo/transformación a **funciones/vistas de BD** → reducir consumo del back y acelerar respuestas; **el front NO debe calcular**.
3. Limpiar código muerto y tablas sin uso; normalizar BD **respetando vistas y funciones de Power BI**.
4. Validación **visual** tras cada cambio. Todo en **rama nueva, jamás tocar `main`**.
5. Al terminar la pasada, correr un **segundo loop** de mejora.

**Modo de trabajo:** loop iterativo, 1 ítem por ciclo → implementar → `dotnet build` + `yarn build` + tests → validación E2E/visual → commit atómico → marcar `[x]` en el tracker.

---

## 2) Fuentes de verdad (LEER SIEMPRE al retomar, en este orden)

1. `tracker_estado.md` — **única fuente de verdad del estado**. Buscá `DECISIÓN USUARIO` para los pendientes que esperan OK.
2. `fase_de_desarrollo/refactor_multipais_optimizacion_plan.md` — plan maestro.
3. `fase_de_desarrollo/c1_indicadores_levante_a_sql_plan.md` — plan del trabajo EN CURSO (C1).
4. `fase_de_desarrollo/candidatos_computo_a_bd.md` — C1/C2/C3 + B1-B3 (qué mover a BD).
5. `fase_de_desarrollo/diagnostico_e2e_paises.md` — pipeline E2E por país (FASE A/B/C).
6. Memorias en `MEMORY.md` (índice) — gotchas recurrentes.

---

## 3) Estado — LO HECHO

### Loop 1 (CERRADO 2026-07-01): 24 ciclos, ~46 commits, `main` intacto
- **Código muerto eliminado**: back (managerUser, RoleService/IRoleService/RolePermissionsController, 5 DTOs huérfanos) + front (70 archivos, −6.461 líneas). Fork completo `aves-engorde-panama` (front+controller+service+entidad) eliminado — era desarrollo NO lanzado (menú no existe en BD).
- **Unificación engorde Colombia/Panamá** → `features/engorde-comun/` + `Application/Calculos/`: compute service, models, tabla-indicadores, modal-detalle, gráficas, modal-cuadrar-saldos (`CuadrarSaldosEngordeApi`), form seguimiento (token `ENGORDE_FORM_OPCIONES`), modal-seguimiento (`isPanama` gates). Back: `LiquidacionEngordeCalculos`, `SeguimientoEngordeCalculos`, `MetadataEngordeCalculos`, `SaldoAlimentoEngordeCalculos` (+ tests).
- **Merma (ciclo 21)**: corregido — **merma es SOLO Ecuador**. Fixes: gate `esEcuador`, resumen Ecuador devuelve merma guardada (precarga estaba rota), URLs cuadrar-saldos apuntaban a controller sin esas rutas → **404 en prod corregido**.
- **NG0103 "Infinite change detection"** (getters/*ngFor que alocan arrays nuevos por ciclo): corregido en 4 lugares (modal-seguimiento-engorde, modales postura Colombia levante+producción, gestion-inventario-page 16 getters + 8 métodos). Patrón fix = memoización `listaEstable` con igualdad de contenido → referencia estable. Ver `MEMORY.md → ng0103-getters-arrays-nuevos.md`.
- **Peso báscula OBLIGATORIO en ventas pollo engorde (Ecuador)** — ciclo 24: front (validadores dinámicos + aviso) + back (`MovimientoPolloEngordeCalculos.ValidarPesoObligatorioEnVenta` en Crud + VentaGranja + VentaPanamá + 10 tests). Motivo: una venta sin peso rompió los reportes.
- **Build final**: `dotnet build` 0 err / **0 warn** · **63/63 tests** · `yarn build` OK.
- **Fase 3/4 BD**: inventario de candidatos a fn SQL (reportes contable/técnico), índices revisados (OK), informe de columnas no mapeadas, scripts entregables `backend/sql/verificacion_tablas_sin_uso.sql` (solo lectura, correr en PROD) y `propuesta_drop_tablas_sin_uso.sql` (DROPs comentados).

### C1 — Indicadores de levante (postura Colombia) → función SQL (EN CURSO)
Decisión usuario: **empezar por levante postura** + **replicar exacto** PERO también **corregir los bugs históricos**.
- [x] Paso 1: `backend/sql/fn_indicadores_levante_postura.sql` + migración `20260702153303_AddFnIndicadoresLevantePostura` (aplicada en local). **PL/pgSQL `VOLATILE`** (usa TEMP TABLE, no puede ser STABLE). Loop FOR por semana espejando el TS. `float8` para bit-exactitud con JS.
- [x] Paso 3: DTO `IndicadorSemanalLevanteDto` + endpoint `GET SeguimientoLoteLevante/por-lote/{loteId:int}/indicadores` (delega vía `SqlQueryRaw`).
- [x] Paso 4: front `tabla-lista-indicadores` consume el endpoint (solo pinta); fallback legacy defensivo aún presente. **Validado E2E** (Colombia lote 13/K345A): 200 OK, 25 semanas, valores coinciden (sem1 aves 9131→9024, consumo 21.0 vs guía 22.5, ganancia 118.7, mort 1.01%). **Bugs corregidos**: usa guía Colombia real (no Ecuador-mixto) + peso con arrastre (carry-forward).
- Commits C1: `32e6594` (plan/spec), `37d308f` (fn+endpoint), `1a2624e` (tabla front), `1c48ea3` (docs validación).

---

## 4) Estado — LO PENDIENTE

### C1 Paso 5 (siguiente trabajo técnico inmediato)
- [ ] Migrar `frontend/.../lote-levante/pages/graficas-principal` para que consuma el endpoint (hoy aún calcula en cliente).
- [ ] Quitar el cómputo legacy/fallback muerto de `tabla-lista-indicadores` una vez confirmado.
- [ ] (Opcional) test xUnit de equivalencia de la fn.
- [ ] Luego **C2**: indicadores de **producción postura** → fn SQL (mismo patrón).

### DECISIÓN USUARIO (esperan OK explícito — buscar en tracker)
- Unificar `modal-liquidacion` (deriva; hoy resuelta parcialmente vía gates `esPanama`).
- `YmdHistoricoEfectivo` en Ecuador (usa FechaOperacion a secas → eventos tardíos caen en día equivocado del recálculo de saldo; Colombia extrae fecha efectiva). Recomendado: adoptar versión Colombia.
- `seguimiento-aves-engorde-list`: ¿Panamá recibe las mejoras de Colombia (tabla diaria BD, chips, mensajería) o su versión simple es intencional?
- `tabs-principal` engorde (819 líneas diff, lógica Panamá propia) — dejar de último.
- Paneles "test" en farm-management (`company-admin-test`, `company-test.component`) — quitarlos cambia UI.
- **DROP de tablas sin uso en PROD**: `_backup_cuadre_expected_2026_06_01`, `_migracion_saldo_alimento_*` (×3), `guia_semana`, `user_paises`, tabla física `seguimiento_diario_aves_engorde_panama`. Requiere: correr `verificacion_tablas_sin_uso.sql` en prod (solo lectura) + backup + OK.
- Priorización reportes → fn SQL: `ReporteContableService` vs `ReporteTecnicoService`/`ReporteTecnicoProduccionService` (¿cuál duele más en prod?).
- Eliminar entidad muerta `SeguimientoDiarioAvesEngordeEcuador` del modelo.
- Datos seed de Panamá para E2E.

---

## 5) GOTCHAS CRÍTICOS (no tropezar de nuevo)

- **BD local real = Postgres NATIVO de Windows en `:5433`** (`sanmarinoapplocal`, 95 tablas), NO el contenedor Docker (volumen vacío, conflicto de puerto). EF conecta al nativo. Credenciales: `backend/src/ZooSanMarino.API/appsettings.Development.json` (única fuente, no hardcodear).
- **Relanzar el backend SIN `ASPNETCORE_ENVIRONMENT=Development` apunta a PROD.** Siempre arrancar en Development para pruebas locales.
- **Error `SECRET_UP no proporcionado en el header X-Secret-Up`** = comportamiento ESPERADO de `PlatformSecretMiddleware`, NO un bug. El middleware rechaza llamadas directas a la API (:5002) sin el header `X-Secret-Up` (AES, lo agrega el interceptor de Angular). Se ve al abrir el backend directo; la app en `:4200` funciona normal.
- **AWS WAF da 403 a cualquier ruta de API con "admin"** → nombrar rutas como "global" (memoria `waf-bloquea-rutas-admin.md`).
- **Migración EF que falla en deploy** deja `__EFMigrationsHistory` inconsistente → SIGSEGV (exit 139) al arrancar ECS. Nunca insertar en `__EFMigrationsHistory` a la ligera. ECS hace **rollback silencioso**: verificar TaskDef/imagen post-deploy (ver `CLAUDE.md § 🚀`).
- **fn con TEMP TABLE debe ser `VOLATILE`** (STABLE falla: "CREATE TABLE AS is not allowed in a non-volatile function").
- **NG0103**: nunca devolver arrays/objetos nuevos por ciclo desde getters usados en template. Memoizar con referencia estable.
- **PowerShell**: commits con `/` en el mensaje dieron problemas; usar `-m` de una línea o heredoc single-quote. Parar el backend de preview antes de `dotnet build`/migraciones (lock del DLL).

---

## 6) Cómo retomar (comandos + credenciales)

**Servidores locales (vía preview tools, NO Bash):**
- Backend `:5002` (Development, BD local nativa) — `dotnet run --launch-profile "Development"` desde `backend/src/ZooSanMarino.API`.
- Front `:4200` — `yarn start` desde `frontend`.
- Validación visual = tools `mcp__Claude_Preview__*` (screenshot/snapshot/console_logs). Para detectar NG0103 sin ruido de buffer viejo, instalar contador propio `window.__ng` vía `preview_eval` y verificar 0 tras el fix.

**Credenciales E2E (reales, provistas por el usuario):**
- Ecuador: `admin.ecuador@italcol.com` / `123456789`
- Panamá: `admin.panama@italcol.com` / `123456789`
- Colombia (postura): `solangyramirez@sanmarino.com.co` / `123456789`
- Datos de prueba se marcan `PRUEBA-E2E` y se eliminan al terminar.

**Validaciones obligatorias por ciclo:** `cd backend && dotnet build` (0/0) + `dotnet test` · `cd frontend && yarn build` · validación visual con el perfil del país afectado. Sin procesos huérfanos (`make down` al terminar).

---

## 7) PRÓXIMO PASO CONCRETO

**C1 Paso 5** — migrar `lote-levante/pages/graficas-principal` al endpoint `getIndicadores(loteId)` (que ya existe en `seguimiento-lote-levante.service.ts`), replicando el patrón de `tabla-lista-indicadores`; después limpiar el cómputo legacy de la tabla y arrancar **C2 (producción postura → fn SQL)**. Validar E2E con la sesión de Colombia (lote 13/K345A). Actualizar `c1_indicadores_levante_a_sql_plan.md` y `tracker_estado.md`.

> Al retomar: `git branch` (confirmar `refactor/optimizacion-multipais`), leer `tracker_estado.md`, arrancar back+front, y continuar el loop.
