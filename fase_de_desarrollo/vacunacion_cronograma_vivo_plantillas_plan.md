# Vacunación — cronograma vivo: plantillas por empresa/línea/raza, bandeja de pendientes y fuera de rango

**Fecha:** 2026-08-15 · **Estado:** propuesta (F0 aplicado)
**Tracker:** bloque `W1` al final de [tracker_estado.md](../tracker_estado.md)
**Hermano:** [implementacion_italjira_firma_home_plan.md](implementacion_italjira_firma_home_plan.md) — comparten el panel de Home (W3/I4).

---

## 0. Motivo

Reporte del usuario: *"el módulo vacunación tiene estos campos que se demoran en mostrar la
información"* (capturas: `Cargando granjas…`, `Cargando granjas, lotes y vacunas…` colgados) y
pedido de fondo: que el cronograma **viva** — que se programe una vez por empresa/línea/raza, que
cada lote sepa solo cuál le toca, que avise cuando toca aplicar y que deje aplicar fuera de fecha
dejando la novedad.

---

## 1. Diagnóstico de la demora (F0 — ya aplicado)

**No era el backend ni la red.** `VacunacionService.getFilterData()` ya trae todo en **un**
round-trip cacheado 5 min (`fn_vacunacion_filter_data`, plan de jul-2026), y el servicio de
Implementación ya tiene timeout de 30 s.

La causa es change detection. En **Angular 22 omitir `changeDetection` equivale a `OnPush`**
(`ChangeDetectionStrategy.OnPush = 0`, y el propio d.ts del framework dice *"OnPush is enabled by
default"*; `Default` quedó deprecado como alias de `Eager = 1`). Los tres `.page.ts` de Vacunación y
los dos modales **omitían la propiedad** → corrían en OnPush → el `finally { this.cargandoFiltros =
false }` que sigue a `await firstValueFrom(...)` **no marca la vista sucia** y el `<select>` se queda
en "Cargando granjas…" aunque la respuesta llegó en milisegundos. La información recién aparece
cuando el usuario provoca otro evento (un click en cualquier parte del componente) — de ahí la
percepción de lentitud intermitente.

Auditoría del repo: de **222 componentes, 208 declaran `changeDetection`**. Los **13 que lo omitían
son exactamente los de Vacunación (5) e Implementación (8)** — los dos módulos que reportó el
usuario, y ninguno más. Esto también explica `Cargando cronogramas…` y `Cargando tus tareas…` de
Implementación.

**Fix aplicado:** `changeDetection: ChangeDetectionStrategy.Eager` explícito en los 13 (la
convención del repo: 184 Eager / 24 OnPush). Refactor puro, sin cambio de comportamiento salvo el
repintado. `yarn build` en verde (único warning: el budget preexistente).

> **Regla que queda:** todo componente/modal nuevo con `subscribe`/`async` lleva `Eager` explícito.
> Ya está en CLAUDE.md; conviene agregar un check de CI (W1.9).

---

## 2. Qué existe hoy (fuente de verdad = código)

| Pieza | Dónde | Estado |
|---|---|---|
| Cronograma **por lote** | `VacunacionCronogramaItem` (FK excluyente `LotePosturaLevanteId` / `LotePosturaProduccionId` / `LoteAveEngordeId`) | ✅ |
| Objetivo Semana / Día / Fecha + franja `RangoDiasAntes/Despues` | misma entidad + `VacunacionCalculos.CalcularFranja` | ✅ |
| Estado, desviación y umbral por empresa | `VacunacionCalculos.CalcularEstadoAplicacion` + `VacunacionConfiguracion.DiasUmbralIncumplido` (default 14) | ✅ |
| **Motivo obligatorio si se aplica fuera de la franja** | `ResultadoAplicacion.RequiereMotivo = diasDesviacion != 0`, validado en `VacunacionRegistroService.Registrar.cs:64` | ✅ **ya existe** |
| Combos en 1 round-trip + caché | `fn_vacunacion_filter_data` + `shareReplay` TTL 5 min | ✅ |
| Scoping de granjas | `fn_vacunacion_filter_data` → `EXISTS (user_farms)` | ⚠️ **solo granja** |
| Plantilla por empresa / línea / raza | — | ❌ |
| Materializar el plan a todos los lotes | — | ❌ |
| Bandeja "hoy me toca vacunar" | — | ❌ |
| Scoping por núcleo/galpón/lote | `user_farm_scopes` + `farms.restrict_locations` existe, **Vacunación no lo usa** | ❌ |

**Conclusión:** el motor de fechas y el registro fuera de rango **ya están construidos y probados**.
Lo que falta es todo lo que está *arriba* (plantillas) y *al lado* (bandeja y scoping fino). No hay
que rehacer nada del cálculo.

---

## 3. Diseño propuesto

### 3.1 Plantilla de vacunación (tabla nueva)

```
vacunacion_plan_plantilla            vacunacion_plan_plantilla_items
─────────────────────────            ───────────────────────────────
id                                   id
company_id        NOT NULL           plantilla_id      FK
pais_id                              item_inventario_id (vacuna)
nombre                               unidad_objetivo   'Semana'|'Dia'
linea_productiva  'Levante'|'Produccion'|'Engorde'
raza              NULL = aplica a toda la línea        valor_objetivo   INT
linea_genetica_id NULL = idem                          rango_dias_antes / _despues
activa            BOOL                                 orden / notas
vigente_desde     DATE (aplica a lotes encasetados desde…)
+ auditoría/soft-delete (patrón del repo)
```

- **`raza` NULL = comodín.** Resolución: `raza` exacta gana sobre comodín; si hay empate, la de
  `vigente_desde` más reciente. Regla determinista, testeada, **una sola plantilla efectiva por
  (empresa, línea, raza)** — sin ambigüedad silenciosa.
- La empresa la manda el dato (`farms.company_id` de la granja del lote), no el front — patrón
  fail-closed de CLAUDE.md §🏢.

### 3.2 Materialización (la decisión de diseño importante)

**Materializar, no resolver al vuelo.** Un registro de aplicación necesita un
`vacunacion_cronograma_items.id` real al cual colgarse; si el cronograma fuera virtual, el reporte
de cumplimiento y el histórico se quedarían sin ancla. Se agrega a la tabla existente:

```
vacunacion_cronograma_items
  + origen_plantilla_item_id  INT NULL   -- de dónde salió esta fila
  + generado_automatico       BOOL DEFAULT false
```

`VacunacionMaterializadorService.MaterializarLoteAsync(linea, loteId)`:
1. Resuelve la plantilla efectiva del lote (empresa + línea + raza).
2. **Idempotente**: `INSERT … WHERE NOT EXISTS` por `(lote, origen_plantilla_item_id)`.
3. **Nunca toca un ítem que ya tiene registro de aplicación.** Invariante duro, con test.
4. Respeta lo hecho a mano: un ítem con `generado_automatico = false` no se pisa jamás.

Disparadores: (a) al **encasetar** un lote, (b) botón *"Aplicar plantilla a los lotes activos"*,
(c) al abrir el cronograma de un lote que no tiene ítems (lazy, misma función idempotente).

**Re-sincronizar tras editar una plantilla** — el punto donde este tipo de módulo suele romperse:
antes de guardar se muestra el impacto (*"N lotes afectados · M ítems se actualizan · K ya aplicados
quedan como están"*) y se confirma. Sin ese preview no se guarda.

### 3.3 Estados y "fuera de rango"

El motor ya distingue `Aplicado` / `AplicadoTardio` / `AplicadoAdelantado` / `NoAplicado` con
`DiasDesviacion` e `Incumplido`. **No se agrega ningún estado nuevo en BD** (rompería reportes). Lo
que cambia es la lectura:

- La UI rotula `AplicadoTardio` / `AplicadoAdelantado` como **"Fuera de rango"** con los días
  (`+6 d`, `−2 d`) y color por `Incumplido`.
- El **campo de novedad se despliega solo** cuando la fecha de hoy cae fuera de la franja, con el
  texto de por qué, y el botón Guardar queda deshabilitado hasta llenarlo (el backend ya lo exige —
  hoy el usuario se entera por el 400).
- **Deja aplicar siempre.** Confirmado en el código actual: no hay bloqueo por fecha.

### 3.4 Scoping vivo (lo que pidió: granja → galpón → lote)

`fn_vacunacion_filter_data` pasa de filtrar solo por `user_farms` a respetar
`farms.restrict_locations` + `user_farm_scopes` (niveles `nucleo` / `galpon` / `lote`), igual que el
resto del sistema. **Fail-closed**: `restrict_locations = true` sin grants ⇒ el usuario no ve lotes
de esa granja. Se resuelve **en la fn SQL**, no en memoria (regla: la BD filtra).

### 3.5 Bandeja "hoy me toca" → Home

`GET /api/VacunacionRegistro/pendientes` (SQL, scoped): ítems sin registro cuya franja **está
abierta hoy o ya venció**, con `loteNombre · galpón · vacuna · fecha objetivo · días de atraso`.
Alimenta el panel desplegable de Home (compartido con Implementación — ver W3).

---

## 4. Fases (entregables independientes)

| Fase | Qué entrega | Riesgo |
|---|---|---|
| **F0** ✅ | Fix de change detection (13 componentes) | nulo — ya en verde |
| **W1** | Plantillas: tablas + CRUD + resolución por raza + cálculo puro con tests | bajo (aditivo) |
| **W2** | Materializador idempotente + preview de impacto + enganche al encaset | **medio** — toca datos de lotes vivos |
| **W3** | Bandeja de pendientes + panel en Home + novedad automática fuera de rango | bajo |
| **W4** | Scoping por núcleo/galpón/lote en la fn + reportes | **medio** — cambia lo que ve cada usuario |

---

## 5. Reglas de negocio (contrato para los tests)

1. Una plantilla por `(company_id, linea_productiva, raza, vigente_desde)`; `raza` exacta gana sobre
   `NULL`; a igual especificidad gana `vigente_desde` mayor.
2. Materializar es idempotente: correrlo N veces deja el mismo resultado.
3. Un ítem con registro de aplicación **nunca** se modifica ni se borra por materialización.
4. Un ítem creado a mano (`generado_automatico = false`) nunca se pisa.
5. Aplicar fuera de la franja **se permite** y **exige** motivo (ya vigente).
6. `Incumplido` = `diasDesviacion >= DiasUmbralIncumplido` de la empresa (14 por defecto). Sin cambio.
7. Sin plantilla efectiva ⇒ el lote queda sin cronograma automático (nunca se inventa uno).
8. Empresa efectiva = `farms.company_id` de la granja del lote. Ambigüedad ⇒ vacío, nunca datos de
   otra empresa.

## 6. Casos de prueba

- `VacunacionPlantillaCalculos`: resolución raza exacta vs comodín; empate por `vigente_desde`; sin
  plantilla ⇒ `null`; plantilla inactiva se ignora.
- `VacunacionMaterializadorCalculos`: qué ítems faltan / cuáles se actualizan / cuáles se preservan
  (aplicados y manuales), a partir de (plantilla, ítems del lote) — **puro, sin EF**.
- Integración: materializar 2× no duplica; editar plantilla no toca aplicados; lote de otra empresa
  no se toca.
- Regresión: con 0 plantillas el módulo se comporta **byte a byte** como hoy.
- Smoke doble: empresa sin plantillas (cero cambios visibles) y empresa con plantilla.
