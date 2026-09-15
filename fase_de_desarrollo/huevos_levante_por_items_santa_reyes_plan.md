# Huevos en LEVANTE por ítems del lote (Santa Reyes) — plan

**Origen (14-sep-2026, capacitación Santa Reyes):** en el Seguimiento Diario de **Levante** aparece el
tab «Huevos» con las **11 categorías fijas de Sanmarino** (limpio, tratado, sucio…) en vez de los
tipos de huevo que se declararon al crear el lote. Pedido del usuario:

1. El tab debe mostrar **los tipos declarados del lote** (`lote_huevo_items`), no la clasificadora fija.
2. Solo **a partir de una semana de vida** — decisión del usuario: **semana configurable por empresa**
   (Santa Reyes = 18).
3. Si el lote **no tiene tipos declarados, el tab no aparece**.

## Diagnóstico (medido, no deducido)

| Hecho | Evidencia |
|---|---|
| Santa Reyes tiene `captura_huevos_en_levante = t` **y** `clasificacion_huevo_por_items = t` | `SELECT … FROM companies` en BD local. El tracker X17.1 lo registraba en `false`: se prendió después (UI de empresas, ver memoria *flag-de-empresa-prendido-desde-la-ui*). |
| El front de levante decide el tab SOLO con `capturaHuevosEnLevante` | `modal-create-edit.component.ts` → `recalcularVisibilidadHuevos()` |
| El backend **descarta en silencio** los huevos de empresas por ítems | `SeguimientoLoteLevanteService.cs:98` → `if (flags.ClasificacionHuevoPorItems) return false;` ⇒ `SinHuevos(dto)` |
| ⇒ Hoy lo que el operario de Santa Reyes escribe en ese tab **se pierde** | combinación de las dos filas anteriores |
| Radio de impacto sobre datos: **cero** | Santa Reyes: 3 lotes, 17 tipos declarados, **0** seguimientos de levante con huevos |

## Enfoque arquitectónico

Patrón obligatorio de CLAUDE.md §Features por EMPRESA: señal tipada en `companies`, decisión pura en
`Application/Calculos` con tests, empresa efectiva por `farms.company_id`, flag en `CompanyDto` + 
`ActiveCompanyConfigService` (fail-closed).

### 1. BD — columna nueva `companies.huevos_levante_desde_semana integer NULL`

- Nombrada por **comportamiento**. `NULL` = sin límite (comportamiento actual) ⇒ Sanmarino/Demo/Ecuador/
  Panamá **byte a byte iguales**.
- Migración EF idempotente `AddHuevosLevanteDesdeSemana`: `ADD COLUMN IF NOT EXISTS` + seed
  `UPDATE companies SET huevos_levante_desde_semana = 18 WHERE name = 'Santa Reyes' AND … IS DISTINCT FROM 18`
  (timestamp posterior al seed de la empresa `20260725190000`). `Down()` inverso exacto.
- Entidad `Company`, `CompanyConfiguration`, `ModelSnapshot`, `.Designer.cs`.

### 2. Backend — cálculo puro (`Application/Calculos`)

- `HuevosLevanteCalculos.PermiteHuevos(fechaEncaset, fechaRegistro, desdeSemana)`: sobrecarga; con
  `desdeSemana = null` delega en la actual (idéntico). Con valor: `semanaVida >= desdeSemana`.
  Sin encaset ⇒ permitido (mismo fail-open de hoy). + `MensajeAntesDeSemana(desde, semana)`.
- `HuevosLevanteCalculos.ResolverModo(captura, porItems)` → `Ninguno | Clasificadora | PorItems`.
  Hoy `porItems ⇒ Ninguno`; pasa a `captura && porItems ⇒ PorItems`.
- `HuevoItemsCalculos.SumarPorItem(a, b)` y `DeltaPorItem(nuevo, aplicado)` (para arrastre y merge).
- Marca de arrastre: `aplicadoItems` (array) además de `aplicado`; `aplicado.huevoTot` = total de
  ítems para que `CicloVidaPosturaCalculos` siga detectando datos del usuario.

### 3. Backend — levante (`SeguimientoLoteLevanteService`)

- `SeguimientoLoteLevanteDto` + request del controller: `List<HuevoItemSeguimientoDto>? HuevoItems = null`
  (último parámetro opcional ⇒ ningún llamador existente cambia).
- Gate `AplicarGateHuevosLevanteAsync` según modo:
  - `Clasificadora` (Sanmarino): igual que hoy + regla `desdeSemana` (null para ellos).
  - `PorItems` (Santa Reyes): neutraliza las 11 columnas; si `HuevoItems != null` ⇒ valida
    (`Validar`, catálogo activo de la empresa, `ValidarPermitidos` contra `lote_huevo_items`,
    vigencia de primera postura, semana desde) ⇒ enriquece desde catálogo ⇒ `metadata.huevoItems`,
    `huevo_tot = suma`, `huevo_inc = 0`, 11 columnas en 0 (misma convención que producción).
  - `Ninguno`: igual que hoy.
- Edición con `HuevoItems = null` (tab oculto) ⇒ **conservar** `metadata.huevoItems` y `huevo_tot`
  del registro previo (el front reconstruye el metadata del consumo y borraría la clave).
- **Centralizar** la validación de ítems hoy privada en `ProduccionService` en un validador compartido
  de Infrastructure (refactor sin cambio de comportamiento) para no duplicar 80 líneas.

### 4. Backend — arrastre a producción (`ArrastreHuevosLevanteService`)

- `LeerLevanteAsync` proyecta también `metadata` y acumula `huevoItems` por `catalogItemId`.
- `ArrastrarAsync`: delta por ítem contra `aplicadoItems`, suma sobre `metadata.huevoItems` de la fila
  de producción y `huevo_tot += delta total`. Las 11 columnas siguen su camino de hoy.
- `ObtenerTotalesParaCierreAsync`: suma el total por ítems.
- `ProduccionService.CrearSeguimientoAsync` (merge sobre la fila de arrastre): si la fila trae
  `huevoItems`, se **suman** con los del request en vez de reemplazarlos. Solo ocurre cuando la fila
  tiene `huevoItems` ⇒ empresas sin ítems sin cambio.

### 5. Frontend

- `CompanyDto` (`company.service.ts`) + `ActiveCompanyConfigService` (los 6 lugares) +
  pantalla de Empresas (campo numérico junto a «primera postura hasta semana»).
- `lote-levante/funciones/`:
  - `semana-vida-levante.funcion.ts` → `permiteHuevosEnLevante(encaset, fecha, desdeSemana = null)`.
  - `huevos-levante-items.funcion.ts` (nueva, pura) → `mostrarTabHuevosLevante(...)` y
    `construirHuevoItemsPayloadLevante(...)`.
- `modal-create-edit` (levante):
  - Flags: `clasificacionHuevoPorItems`, `huevosLevanteDesdeSemana`, `huevoPrimeraPosturaHastaSemana`.
  - Carga `LoteHuevoItemsService.getByLote(loteId)` al abrir/cambiar lote (guardia `loadId`, patrón silos).
  - Tab visible = captura ∧ semana ≥ desde ∧ (modo fijo ∨ lote con tipos declarados). Mientras carga o si
    falla: oculto (fail-closed).
  - Template: modo ítems ⇒ filas fijas agrupadas Primera/Pnc (reusa `construirFilasFijasHuevo` y
    `HuevoFilaFija`); modo fijo ⇒ las 11 categorías de siempre.
  - Payload: modo ítems ⇒ `huevoItems` + 11 columnas en null; tab oculto ⇒ `huevoItems: null`.
  - Edición: rehidrata desde `metadata.huevoItems` (`leerHuevoItemsDeMetadata`).

## Archivos

**Backend:** `Domain/Entities/Company.cs` · `Persistence/Configurations/CompanyConfiguration.cs` ·
`DTOs/CompanyDto.cs`, `CreateCompanyDto.cs`, `UpdateCompanyDto.cs` · `CompanyService.cs`,
`CompanyService.Crud.cs`, `CompanyResolver.cs`, `CompanyPaisService.cs` · `Migrations/2026091418xxxx_AddHuevosLevanteDesdeSemana(.Designer).cs`
+ snapshot · `Calculos/HuevosLevanteCalculos.cs`, `HuevoItemsCalculos.cs` ·
`DTOs/SeguimientoLoteLevanteDto.cs` (+ request) · `SeguimientoLoteLevanteService.cs` y `Funciones/*.Mapeos|Crud.cs` ·
`ArrastreHuevosLevanteService.cs` · `ProduccionService.cs` / `Funciones/ProduccionService.Seguimiento.cs` ·
validador compartido nuevo.

**Tests:** `HuevosLevanteCalculosTests.cs`, `HuevoItemsCalculosTests.cs` (casos nuevos + flag OFF idéntico).

**Frontend:** `core/services/company/company.service.ts` · `core/services/company-config/active-company-config.service.ts` ·
`features/config/company-management/*` · `features/lote-levante/funciones/semana-vida-levante.funcion.ts` (+spec) ·
`features/lote-levante/funciones/huevos-levante-items.funcion.ts` (+spec) ·
`features/lote-levante/services/seguimiento-lote-levante.service.ts` ·
`features/lote-levante/pages/modal-create-edit/*`.

## Reglas de negocio

1. Empresa con clasificación por ítems + captura en levante ⇒ el tab muestra **solo** los tipos que el
   lote declaró; nunca las 11 categorías.
2. Lote sin tipos declarados ⇒ **sin tab** (y el backend rechaza ítems con el mensaje de siempre).
3. Registro con semana de vida < `huevos_levante_desde_semana` ⇒ sin tab; el backend rechaza huevos
   positivos con mensaje explícito. `NULL` = sin límite.
4. Primera postura fuera de vigencia (semana > 22 en Santa Reyes) ⇒ fila marcada, backend rechaza cantidad.
5. Al liquidar el levante los ítems se arrastran al primer registro de producción y se **suman** con lo
   que el operario registre ese día.
6. Editar un registro con el tab oculto **no borra** los huevos guardados.

## Casos de prueba

- xUnit: `PermiteHuevos` con `desde = null` idéntico a la versión actual (tabla de casos existentes);
  semana 17 vs 18 con `desde = 18`; sin encaset. `ResolverModo` en sus 4 combinaciones.
  `SumarPorItem`/`DeltaPorItem` (ítems disjuntos, solapados, delta negativo). Marca con `aplicadoItems`.
- Front (Karma): `permiteHuevosEnLevante` con/sin `desdeSemana`; `mostrarTabHuevosLevante` (sin tipos ⇒
  false, cargando ⇒ false, modo fijo ⇒ ignora tipos); payload ítems (descarta ceros).
- Smoke doble: **Sanmarino** (flag ítems OFF) tab y guardado idénticos; **Santa Reyes** lote con tipos
  semana ≥ 18 ⇒ filas del lote, guarda `metadata.huevoItems` + `huevo_tot`; lote sin tipos ⇒ sin tab;
  semana < 18 ⇒ sin tab / 400.
- Validación: `dotnet build` 0/0 · `dotnet test` · `yarn build` · `node backend/scripts/verificar-sql-llega-por-migracion.js`.
