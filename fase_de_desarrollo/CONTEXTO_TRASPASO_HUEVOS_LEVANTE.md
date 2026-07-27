# 🔄 CONTEXTO DE TRASPASO — Huevos en Seguimiento Levante (semana 14+) y arrastre a Producción

> Pegá este archivo (o su ruta) al abrir un chat nuevo para continuar sin perder nada.
> **Fecha de corte:** 2026-07-26 · **Sesión origen:** implementación completa de la Fase 1 (commiteada).
> **Working tree: limpio.** Todo lo hecho está en `main`: `34e47aa` (feature) + `4b7282b` (tracker).

---

## 1) Qué es este trabajo

App San Marino (avícola multi-país): **backend .NET 10 LTS Clean Architecture** + **frontend Angular 22 standalone**, AWS ECS. Monorepo, reglas vinculantes en `CLAUDE.md`.

- **Plan:** [`fase_de_desarrollo/huevos_levante_semana14_arrastre_produccion_plan.md`](huevos_levante_semana14_arrastre_produccion_plan.md) — leerlo completo, incluye §8 con el bug de `date_trunc`.
- **Tracker:** [`tracker_estado.md`](../tracker_estado.md) — Fases 0-6 cerradas.

**Pedido original del usuario (verbatim, fuente de verdad):**

> realiza un cambio en el seguimiento diario levante que a partir de la semana 14 debe tener un campo que se llama huevo que es lo mismo que se realiza en seguimiento diario produccion que se clasifica los huevos que tengan por dia en esa fase, la idea final es que cuando se realize la liquidacion esos huevos pasan automaticamente para la primera semana de produccion, y eso es cuando se liquide levante, aparece el total de huevos y los tipos de huevos que se obtuvieron en levante y cuando se levanta automaticamente produccion se crea el primer registro de huevos sumando todos [...] hoy liquide el levante el 24 de febrero, entonces paso a produccion el dia 24 de febrero, en seguimiento produccion trae todos los huevos que se registraron en levante y lo acomoda en el primer registro [...] si para ese dia realizan seguimiento entonces lo que coloquen en huevos lo sumaran a su tipo de huevo que ya tenia [...] ya que la logica esta si ya tengo un registro no deja ingresar mas, entonces **en este caso particular tiene que cambiar**

**Decisiones ya tomadas por el usuario (26-jul-2026), no re-preguntar:**
1. Alcance = **flag por empresa** `companies.captura_huevos_en_levante` (default `false`, ON sólo Agroavicola Sanmarino).
2. Indicadores / Reporte Técnico Semanal de **levante**: **no** en esta fase (es el pendiente P3).
3. «Huevos iniciales (al cerrar)» → **readonly** con el total real calculado.

---

## 2) Estado real — qué QUEDÓ FUNCIONANDO (Fase 1, commit `34e47aa`)

Todo esto está implementado, validado y en `main`. **No rehacerlo.**

| Pieza | Archivo |
|---|---|
| Cálculo puro (semana 14, `HuevosClasificacion`, Sumar/Delta, peso ponderado, marca en metadata, ventana de merge) | `backend/src/ZooSanMarino.Application/Calculos/HuevosLevanteCalculos.cs` |
| Tests del cálculo puro (48) | `backend/tests/ZooSanMarino.Application.Tests/HuevosLevanteCalculosTests.cs` |
| Servicio de arrastre (upsert por delta + recálculo del espejo) | `backend/src/ZooSanMarino.Infrastructure/Services/ArrastreHuevosLevanteService.cs` + `Application/Interfaces/IArrastreHuevosLevanteService.cs` |
| Enganche transaccional del cierre + total en el resumen | `Infrastructure/Services/LotePosturaLevanteService.cs` (`CerrarLoteYCrearProduccionAsync`, `GetResumenCierreAsync`) |
| Merge del mismo día (sólo fila marcada y ventana abierta) | `Infrastructure/Services/ProduccionService.cs` (`ResolverFilaDuplicada`, `AplicarRequestSobreFilaArrastre`) |
| Gate semana 14 + flag por empresa (fail-closed por `farms.company_id`) | `Infrastructure/Services/SeguimientoLoteLevanteService.cs` (`EmpresaCapturaHuevosEnLevanteAsync`, `AplicarGateHuevosLevanteAsync`, `SinHuevos`) + `.../SeguimientoLoteLevante/Funciones/…Crud.cs` |
| Mapper destapado + `null` = conservar | `.../SeguimientoLoteLevante/Funciones/…Mapeos.cs` (`HuevosDeDto`, `ConservarHuevosPrevios`) |
| Blindaje de pérdida silenciosa (4 focos) | `Infrastructure/Services/SeguimientoDiarioService.cs` (`TieneHuevosCargados`, `teneManualExist`, `FilaSinContenido`, `MergearManualSobreTrasladoAsync`) |
| Flag de empresa (entidad + config + 5 proyecciones) | `Domain/Entities/Company.cs`, `Configurations/CompanyConfiguration.cs`, `CompanyDto`/`CreateCompanyDto`/`UpdateCompanyDto`, `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService` |
| Migraciones idempotentes | `20260726231137_AddCapturaHuevosEnLevanteCompany` (columna) · `20260726231200_SeedCapturaHuevosEnLevante` (data-only, Sanmarino) |
| Tab «Huevos» en el modal vivo (3er tab, reactivo a `fechaRegistro`) | `frontend/.../lote-levante/pages/modal-create-edit/modal-create-edit.component.{ts,html,scss}` |
| Funciones puras + models + README | `frontend/.../lote-levante/funciones/{totales-huevos-levante,semana-vida-levante}.funcion.ts`, `funciones/README.md`, `models/huevo-levante.model.ts` |
| Flag en el front (fail-closed) | `frontend/src/app/core/services/company-config/active-company-config.service.ts` |
| Modal de cierre readonly + toast de error | `frontend/.../seguimiento-lote-levante-list.component.{ts,html}`, `frontend/.../lote/services/lote-postura-levante.service.ts` |
| Sección «Huevos» en el detalle (👁️) | `frontend/.../modal-detalle-seguimiento/modal-detalle-seguimiento.component.{ts,html}` |
| Fix de zona horaria (ver §4) | `Application/Calculos/FechasPuras.cs` (`RangoDiaUtc`) + `FechasPurasTests.cs` |

**Validación ejecutada:** `dotnet build` 0/0 · `dotnet test` **840/840** · `ng build` OK (solo warning de budget preexistente) · **smoke API 58/58** · smoke UI (borde día 90 vs 91, guardado persistido, flag OFF oculta el tab) · BD local restaurada.

---

## 3) PENDIENTES para «dejar alineado todo» — orden recomendado

### ✅ P1 · La tabla diaria de levante NO muestra los huevos — **RESUELTO 27-jul-2026**
**Ya implementado.** 3 columnas en pantalla (Tot / Inc / Peso, entre «Venta aves» y «Observaciones») y las 14 en el Excel (el desglose completo de las 11 categorías va sólo al Excel, que es para análisis). Gateado por el flag de empresa vía `ActiveCompanyConfigService` (fail-closed). Validado con flag ON (29 columnas, 0 filas desalineadas) y OFF (26 columnas, sin columnas de huevos).

**Lo que queda OPCIONAL** (decisión de producto, no bloqueante): el tab «Reporte semana» y su Excel (`exportReporteSemanaExcel`, 2º `const rows` ~línea 804) siguen sin huevos — son agregados por semana y requieren 5 sitios en cascada (`ReporteSemanaFila`, el loop de acumulación de `buildReporteSemanaFilas`, su `out.push`, el thead/tbody del tab y el `headers`+`rows` del export).

- Tabla **inline** en `frontend/.../lote-levante/pages/tabs-principal/tabs-principal.component.html` (bloque de la tabla de registros, ~líneas 220-336).
- TS: `tabs-principal.component.ts` → `interface RegistroDiarioTablaFila` (**línea 26**), `buildDiarioFilas()` (**212**), `get colspanRegistroDiario()` (**167**, hoy `26 + (enriquecerTablaConHistoricoInventario ? 3 : 0)` ⚠️ **hay que subirlo**), export Excel `headers` (**683**) y `rows` (~**721-762**), `exportarAoaExcel` (**772**).
- ⚠️ `headers` y `rows` son **dos listas separadas**: si agregás una columna en una y no en la otra, el Excel sale corrido (bug que ya pasó en este repo).
- ⚠️ Hay **dos** exports en el archivo: `rows` en **721** (tabla de registros diarios) y otro en **804** — revisar cuál corresponde antes de editar.
- Sugerencia: 3 columnas (Huevo Tot / Incubables / Peso huevo) mostradas **sólo si el flag de empresa está ON**, para no ensuciar la grilla de las demás empresas.

### 🟠 P2 · Carga masiva de levante no acepta huevos
Los históricos cargados por Excel entran sin huevos.
- `backend/src/ZooSanMarino.Application/Calculos/MigracionEsquemas.cs` → `SeguimientoLevante` (**línea 42**, hoy **15** columnas, ninguna de huevos).
- `backend/sql/fn_migracion_seguimiento.sql` → `fn_migracion_seguimiento_levante`.
- Tests: `backend/tests/ZooSanMarino.Application.Tests/MigracionEsquemasTests.cs`.
- ⚠️ Aplicar el **mismo gate de semana 14** en la carga masiva, o los históricos van a meter huevos en semanas donde el CRUD los rechaza (inconsistencia).

### 🟡 P3 · Huevos en indicadores / Reporte Técnico Semanal de LEVANTE (la «fase 2» acordada)
Decisión del usuario: **no** se hizo en Fase 1. Cuando se haga:
- `backend/sql/fn_indicadores_levante_postura.sql` → `RETURNS TABLE(` en **línea 73**; y `backend/sql/fn_reporte_semanal_levante_extras.sql`.
- **Requiere `DROP FUNCTION`** (Postgres no deja cambiar el row type con `CREATE OR REPLACE`) — ambos archivos ya traen el `DROP`. Migración EF nueva con `DROP` + `CREATE`.
- Después: DTOs (`IndicadorSemanalLevanteDto`, `ReporteSemanalLevanteExtrasRow`) + columnas en el front + el Excel.
- ⚠️ **No hay guía genética de huevos antes de la semana 26**: `vw_guia_genetica_por_lote_postura` fuerza `NULL` en las columnas de huevos para la rama Levante y filtra `semana BETWEEN 1 AND 25`. Las columnas «guía» van a salir vacías en 14-25 — el reporte tiene que tolerarlo (ya lo hace para la semana 25).
- ⚠️ `fn_reporte_semanal_levante_extras` tiene un filtro anti «semana fantasma» que **no evalúa huevos**: una fila con sólo huevos + traslado en semana > 25 se descartaría.

### ✅ P4 · `backend/sql/` desactualizado (trampa de deploy) — **RESUELTO 26-jul-2026**
`backend/sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql` apunta a **`public.seguimiento_diario`** (líneas 243 y 245), una tabla que **ya no existe**. La versión viva del trigger está dentro de la migración `20260531180558`, sobre `seguimiento_diario_levante`.
**Ya corregido:** apunta a `seguimiento_diario_levante` (verificado contra `pg_trigger`), con cabecera que documenta la cadena de renombres (`seguimiento_diario` → `_levante_reproductoras` → `_levante`) y la migración `20260531180558` como fuente de verdad del despliegue. El cuerpo de la función se comparó contra la migración y es **idéntico**, así que el archivo ya es fiel a lo desplegado.

### 🟡 P5 · Índice único de producción declarado pero ausente en la BD
`Infrastructure/Persistence/Configurations/SeguimientoProduccionConfiguration.cs:232` declara
`builder.HasIndex(x => new { x.LoteId, x.Fecha }).IsUnique();` pero **la BD local NO tiene ese índice** (verificado con `pg_indexes`). Con el fix de `RangoDiaUtc` la unicidad la garantiza el chequeo de aplicación, pero conviene:
1. Verificar si existe en **RDS prod**.
2. Si falta y se quiere crear: **primero** buscar duplicados históricos por `(lote_id, fecha_registro::date)` — si hay, el `CREATE UNIQUE INDEX` falla y la migración deja el historial inconsistente (ver `CLAUDE.md` §migraciones). Usar `CREATE UNIQUE INDEX IF NOT EXISTS`. **Dato nuevo (26-jul-2026):** en la BD local hay **0 grupos duplicados** por `(lote_id, fecha_registro::date)`, así que crear el índice ahí sería seguro. Query de verificación para prod: `select count(*) from (select lote_id, fecha_registro::date d, count(*) c from seguimiento_diario_produccion group by 1,2 having count(*)>1) x;` — si da >0 hay que depurar ANTES de crear el índice.

### 🟢 P6 · Modo «clasificación por ítems» (Santa Reyes) — hoy fail-closed a propósito
Si una empresa tiene `clasificacion_huevo_por_items = true`, `EmpresaCapturaHuevosEnLevanteAsync` devuelve **false**: el tab no se muestra y el backend ignora/rechaza los huevos. Es deliberado (no persistir un desglose que los reportes no sabrían leer). Hoy no molesta: Santa Reyes tiene el flag de por-ítems ON y `captura_huevos_en_levante` OFF.
Si hace falta: reusar `HuevoItemsCalculos` (ya soporta `metadata->'huevoItems'`) y **mover** `lote-produccion/funciones/items-huevo-catalogo.funcion.ts` + `models/huevo-clasificacion.model.ts` a `shared/` re-exportando desde su ubicación actual (no duplicar).

### 🟢 P7 · Confirmar la empresa del flag
La migración `20260726231200` activa el flag **sólo** para `'Agroavicola Sanmarino'`. Si el pedido venía de otra empresa, es un `UPDATE companies SET captura_huevos_en_levante = true WHERE name = '<X>'` (o migración gemela, **ordenada después** del seed que crea esa empresa — regla del `CLAUDE.md`).

### 🟢 P8 · `ReporteContableService` no verá el arrastre
`ObtenerReporteMovimientosHuevosAsync` lee **sólo** `seguimiento_diario_levante` con `tipo_seguimiento='produccion'`; el arrastre vive en `seguimiento_diario_produccion`. Es una **inconsistencia entre reportes preexistente, NO doble conteo**. Decidir si se alinea (leer ambas tablas) o se documenta.

### 🟢 P9 · Pico en los indicadores de producción el día del arrastre (esperado)
`% postura = huevo_tot / saldo_hembras` y HTAA/HIAA del Reporte Técnico Semanal se disparan ese día porque se imputan semanas de huevos a una sola fecha. **Es exactamente lo que pidió el usuario.** La marca `metadata.arrastreHuevosLevante` deja la puerta abierta a anotarlo/excluirlo si más adelante molesta.
Además: si el lote se liquida **antes de la semana 25**, `fn_indicadores_produccion_postura` hace `DELETE FROM _seg WHERE sem_vida < 25` y el front pide `semanaDesde: 26` ⇒ la fila no aparece en indicadores (el dato igual está correcto en el espejo y en la lista). Bajar ese corte es una restricción distribuida (fn + loop + clamp del front + umbral de 175 días) que ya causó un incidente (REQ-012b) ⇒ tratarlo aparte.

---

## 4) Gotchas descubiertos — NO volver a tropezar

1. **Las 13 columnas de huevos YA existían en `seguimiento_diario_levante`.** Levante y producción comparten esa tabla unificada (discriminador `tipo_seguimiento`); el mapper de levante mandaba 15 `null` explícitos. La entidad `SeguimientoLoteLevante.cs` y la tabla `seguimiento_lote_levante_deprecated` son **letra muerta** (sin DbSet) — la viva es `SeguimientoDiario`.

2. **`date_trunc('day', timestamptz)` trunca en la zona de la SESIÓN de la BD.** EF traduce `x.Fecha.Date == fecha.Date` a `date_trunc('day', col) = @param`; con la sesión en `America/Bogota` una fila de 12:00Z da `00:00-05` (=05:00Z) y **nunca** iguala el parámetro en medianoche UTC. Esto tenía **roto de antes** el chequeo de duplicado de producción. **Usar siempre `FechasPuras.RangoDiaUtc`, nunca `.Date ==`, contra columnas `timestamptz`.**
   ⚠️ **Efecto colateral del fix a vigilar en el deploy:** en una BD con sesión no-UTC el 400 `"Ya existe un seguimiento para esta fecha y lote."` ahora **sí** se dispara donde antes se colaba un duplicado.

3. **La ventana de merge se cierra sola.** `metadata.arrastreHuevosLevante` habilita la suma; al registrar el usuario se le marca `seguimientoRegistrado: true` (`PermiteMergeSeguimiento` / `MarcarSeguimientoRegistrado`) ⇒ un segundo alta del mismo día vuelve al 400 histórico. La regla «un registro por día» se conserva.

4. **4 focos que BORRABAN los huevos en silencio** (ya corregidos, revisarlos si se toca el módulo): `teneManualExist` y `FilaSinContenido` no los evaluaban (una fila sólo-huevos se consideraba vacía y **se eliminaba**), `MergearManualSobreTrasladoAsync` no los copiaba, y `SeguimientoDiarioService.UpdateAsync` asigna sin condición (mandar `null` = borrar).

5. **Reversibilidad gratis:** `AbrirLoteAsync` → `EliminarDependientesLoteProduccionAsync` ya hacía `ExecuteDelete` de `SeguimientoProduccion` + `EspejoHuevoProduccion` del LPP. Reabrir borra la fila de arrastre; re-cerrar da el mismo total (no el doble) por el `Delta`.

6. **Angular 22 = OnPush por defecto** (regla nueva de `CLAUDE.md`, commit `14a8bfa`): todo componente nuevo lleva `changeDetection: ChangeDetectionStrategy.Eager` explícito. Los 3 componentes tocados ya lo tienen.

7. **`dotnet build` falla si el backend está corriendo** (DLL locked) — detener el server antes de compilar.

---

## 5) Receta de smoke local (reusable, sin credenciales)

BD local: `sanmarinoapplocal` en **:5433** (PG17; el PG13 de :5432 hace timeout). Backend `:5002` vía `.claude/launch.json` perfil `backend`; front `:4200` perfil `frontend-node22`.

1. **JWT HS256 minteado a mano** con la `JwtSettings.Key` de `backend/src/ZooSanMarino.API/appsettings.Development.json`; claims: `sub`/`nameid` = Guid del usuario, `company_id`, `user_id`, `pais_id`, `iss=ZooSanMarino.API`, `aud=ZooSanMarino.Client`. Usuario Admin company 1: `92afe4c8-bf3e-4ab0-a31a-467890463542` (moiesbbuga@gmail.com). **Dura 1 h — re-mintear si da 401.**
2. **Header `X-Secret-Up`**: NO es el secreto en claro, va **cifrado** — AES-256-CBC, clave derivada con **PBKDF2-SHA256, 10000 iteraciones, salt fijo `sanmarino-salt`** desde `PlatformSecret.EncryptionKey`, IV aleatorio de 16 bytes **al inicio**, todo en base64. Plaintext = `PlatformSecret.SecretUpFrontend`.
3. Header `X-Active-Company: Agroavicola Sanmarino`.
4. **Usar `127.0.0.1`, no `localhost`** (el server escucha en `[::]` y `localhost` puede resolver a algo que rechaza).
5. **Smoke de UI sin login:** inyectar `sessionStorage.setItem('auth_session', <JSON plano>)` (ver `AuthSession` en `core/auth/auth.models.ts`) y navegar a `/daily-log/seguimiento`.
6. **Lotes de prueba (company 1):** `lote_postura_levante_id=6` → `lote_id=114` (A374A, granja 20 LA ESMERALDA / Modulo II / galpón «4», encaset **2025-10-16**) ⇒ **semana 13 = 2026-01-14**, **semana 14 = 2026-01-15**. Sin huevos: `lpl=7` → `lote_id=115`.
7. **Dejar la BD como estaba:** borrar las filas de prueba de `seguimiento_diario_levante`, reabrir los lotes (`POST /api/LotePosturaLevante/{id}/abrir`) y confirmar `estado_cierre='Abierto'` + `lote_postura_produccion` sin LPP de prueba.

---

## 6) Próximo paso concreto

1. Leer el **plan** (§8 incluida) y este archivo. **No** re-explorar el módulo: el mapa está en el plan.
2. Abrir **P1** (columnas de huevos en la tabla diaria + Excel) — es el hueco que el usuario va a notar primero.
3. STEP 2 del workflow: reescribir `tracker_estado.md` con la fase nueva antes de tocar código.
4. Validar con `dotnet build` + `dotnet test` + `yarn build` y **smoke doble** (empresa con flag OFF ⇒ cero cambios visibles / Sanmarino con flag ON).
5. Commit al terminar (sin atribución a Claude, autor moisesmurillo, mensaje Conventional Commits en ASCII). **Commit ≠ push.**
