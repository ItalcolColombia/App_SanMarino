# Rename neutro de módulos transversales — quitar el prefijo de país al código que no es de un país

**Fecha:** 2026-09-02
**Alcance elegido por el usuario:** máximo — rótulos visibles + símbolos CLR/TS + **rename físico de BD**
(la «Fase C» que se difirió en julio-2026), sobre los **4** módulos identificados.
**Antecedente:** `fase_de_desarrollo/inventario_rename_neutro_plan.md` (julio-2026) dejó el catálogo de
inventario neutro en CLR/TS y difirió la BD. Este plan cierra esa fase y extiende el criterio a los otros tres.

---

## 1. El problema, en una frase

Hay módulos que **nacieron para un país y hoy los usan varias empresas y varios países**, pero conservan el
nombre del país de origen en la pantalla, en las clases del backend y en las tablas. El nombre miente, y ya
costó bugs reales: la doble validación de engorde se rompió porque alguien creyó que
`SeguimientoAvesEngordeEcuador` era un camino de Ecuador (memoria `engorde-front-pega-al-controller-ecuador`).

---

## 2. Auditoría — medida, no asumida (2026-09-02)

### 2.1 Qué es transversal y el nombre miente

| Módulo | Evidencia de que es transversal | Estado del nombre |
|---|---|---|
| **Ítems de inventario** | Tabla compartida EC/PA/CO; el propio seed dice «(Ecuador/Panama)» | Backend **ya neutro** desde julio (`ItemInventarioController`, entidad `ItemInventario`). Faltan rótulos, ruta SPA y **BD** |
| **Seguimiento aves engorde** | El único formulario postea a `/api/SeguimientoAvesEngordeEcuador` para **EC, PA y CO**; los 2 services escriben la **misma** tabla `seguimiento_diario_aves_engorde` | Controller + service + interfaz + entidad con «Ecuador» |
| **Guía genética** | La entidad tiene `PaisId` y su propio doc dice «por empresa + raza + año»; la Ross 308 AP de Panamá vive ahí | Header/detalle/service/controller/módulo front/menú con «Ecuador» |
| **Indicador** | El módulo front `indicador-ecuador/` contiene `liquidacion-reporte-panama/` y `corridas-panama.funcion.ts` | Controller + service + módulo front + menú con «Ecuador». **Sin tablas propias** (servicio de cálculo) |

### 2.2 Qué NO se toca (el país es real, no un resto histórico)

- `PuentePanama` / `sincronizacion-panama` — integración con un ERP **externo** panameño.
- `MovimientoPolloEngordePanama` — venta con peso de báscula diferido; flujo distinto de verdad.
- `ColombiaInventarioConsumo` — inventario unificado de Colombia (país 1), multi-empresa pero país-específico.
- `ReporteIndicadorPanama` — reporte propio de Panamá.
- `is-ecuador.pipe`, `show-if-ecuador*.directive`, `isEcuadorOrPanama` — gating de país; decisión **cerrada**
  en julio-2026 («no re-litigar»).
- **`ModuloSeguimiento.ENGORDE_EC`** — es un **dato persistido** (`OrigenModulo` de las reservas), no un símbolo.
  Renombrarlo huerfaniza reservas vivas. Se colapsa con `Canonico()`, como ya se hace.

### 2.3 Superficie real en la BD (consultada contra `sanmarinoapplocal`, 2026-09-02)

**Tablas a renombrar**

| Hoy | Neutro | Existe |
|---|---|---|
| `item_inventario_ecuador` | `item_inventario` | sí |
| `guia_genetica_ecuador_header` | `guia_genetica_header` | sí |
| `guia_genetica_ecuador_detalle` | `guia_genetica_detalle` | sí |
| `seguimiento_diario_aves_engorde_ecuador` | — | **NO EXISTE** (ver §2.4) |

**Columnas a renombrar** — `item_inventario_ecuador_id` → `item_inventario_id` en **5 tablas vivas**:
`inventario_gasto_detalle`, `inventario_gestion_movimiento`, `inventario_gestion_stock`,
`lote_registro_historico_unificado`, `seguimiento_reserva_alimento`.
Y `guia_genetica_ecuador_detalle.guia_genetica_ecuador_header_id` → `guia_genetica_header_id`.
⛔ **No se toca** `_backup_consumos_duplicados_validacion_20260831` (respaldo congelado; renombrarlo
falsifica el respaldo).

**Índices y constraints con el nombre viejo (13):** `item_inventario_ecuador_pkey`,
`ix_item_inventario_ecuador_{company_id,pais_id,tipo_item}`, `uq_item_inv_ecuador_company_pais_codigo`,
`fk_item_inv_ecuador_{company,pais}`, `fk_igm_item_inventario_ecuador`, `fk_igs_item_inventario_ecuador`,
`guia_genetica_ecuador_header_pkey`, `guia_genetica_ecuador_detalle_pkey`,
`ix_guia_genetica_ecuador_header_company_id_pais_id_raza_anio_g`.
(Los `_backup_rename_lote_engorde_ecuador_*` son de otra cosa y no se tocan.)

**🔴 13 funciones se rompen con el rename** (Postgres guarda su *texto*, no el OID):
`fn_congelar_liquidacion_engorde` (plpgsql), `fn_informe_semanal_pollo_engorde`,
`fn_inventario_gastos_existencias`, `fn_inventario_gastos_search`, `fn_reporte_diario_costos_engorde`,
`fn_reporte_diario_costos_postura`, `fn_vacunacion_cronograma_lote`, `fn_vacunacion_cumplimiento_detalle`,
`fn_vacunacion_filter_data`, `fn_vacunacion_pendientes`, y **3 triggers plpgsql**:
`trg_lote_hist_desde_inventario_gestion`, `trg_lote_hist_desde_movimiento_pollo_engorde`, `trg_sync_tombstone`.
Las 13 se recrean **en la misma migración** que el rename, o la app queda rota entre un paso y otro.

**1 vista:** `vw_indicadores_diarios_engorde` (consumida por **Power BI**, externo).
✅ Las vistas de Postgres se ligan por OID: **sobreviven solas** al rename y su definición se reescribe sola.
🔴 **Pero su columna de salida** `guia_genetica_ecuador_header_id` sale de un alias explícito
(`gh.id AS guia_genetica_ecuador_header_id`, línea 183). **Ese alias NO se toca**: si cambia, Power BI se rompe
en silencio. La vista se deja tal cual — es la única pieza donde el nombre viejo se conserva **a propósito**.

### 2.4 Hallazgo: el «módulo Ecuador» de seguimiento es un fantasma

`SeguimientoDiarioAvesEngordeEcuador` (entidad + Configuration + `DbSet`) **no lo usa nadie**: el service vivo
resuelve todo contra `_ctx.SeguimientoDiarioAvesEngorde` (9 usos) y la tabla que mapea, **no existe en la BD**
(`to_regclass` → NULL) aunque `20260517104629_SplitSeguimientoDiarioAvesEngordeByCountry` figure aplicada.
El split por país se abandonó y nadie limpió el rastro.

⇒ Para este módulo **no hay rename de tabla: hay una eliminación**. Es la parte de mayor valor y menor riesgo
de todo el trabajo: borra la mentira de raíz en vez de maquillarla.
(`seguimiento_diario_aves_engorde_panama` **sí existe** y no se toca.)

---

## 3. Lo que queda FUERA, dicho explícito

- **La clave de wire/DTO `itemInventarioEcuadorId` no se renombra.** Es contrato con el front **y con la cola
  offline de la PWA**: hay dispositivos con filas encoladas que la llevan adentro. Renombrarla haría fallar la
  sincronización de todo lo capturado sin red. Las propiedades CLR/TS sí quedan neutras, espejando el nombre
  viejo con `[JsonPropertyName]`. Migrar el wire es una entrega propia, con ventana y versionado.
- **Las claves jsonb persistidas** (`itemInventarioEcuadorId` / `catalogItemId` dentro de `metadata`) tampoco:
  son datos históricos, no esquema.
- **`ENGORDE_EC`** como valor de `OrigenModulo` (§2.2).
- **La vista `vw_indicadores_diarios_engorde`** y su alias de salida (§2.3).
- **El deploy.** Las migraciones se aplican solas al arrancar; el merge a `main-produccion` va con OK aparte (§7).

---

## 4. Fases

### Fase A — Rótulos visibles (riesgo cero, resuelve lo que se ve)
1. `item-inventario-list.component.html:5` — «Ítems de inventario (Ecuador)» → «Ítems de inventario».
2. `gestion-inventario-page.component.html:1353` (+ comentario `:1321`) — misma tarjeta.
3. Labels «Raza (guía Ecuador)» / «Año Tabla Genética (guía Ecuador)» en el alta de lote engorde
   → «Raza (guía genética)» / «Año Tabla Genética». ⚠️ `lote-engorde-list.component.ts:942` y su
   **spec `:116`** comparan el string exacto — se actualizan juntos o el test rompe.
4. Migración data-only: `menus.name` «Guía genética Ecuador» → «Guía genética», «Indicador Ecuador» →
   «Indicador de engorde»; descripción del catálogo de ítems sin «(Ecuador/Panama)».
   Localizar **por `route`**, nunca por id (ids difieren local↔prod), e idempotente
   (`WHERE ... IS DISTINCT FROM`).

### Fase B — Símbolos CLR/TS (sin DDL, wire estable)
5. **Borrar el fantasma** (§2.4): entidad + Configuration + `DbSet` de `SeguimientoDiarioAvesEngordeEcuador`.
6. `SeguimientoAvesEngordeEcuadorService`/`ISeguimientoAvesEngordeEcuadorService`/carpeta `Funciones/` →
   `SeguimientoAvesEngordeService…` — **choca con el service neutro que ya existe**: se resuelve nombrando
   al vivo por lo que hace (`SeguimientoAvesEngordeDiarioService`) y evaluando si el otro sigue teniendo
   dueño. Decisión documentada en el tracker antes de tocar.
7. Controller: `[Route]` doble — neutra + la vieja como alias; el front migra a la neutra.
8. `GuiaGeneticaEcuador*` → `GuiaGenetica*` (entidades, config, DTOs, service, controller, DbSet, navegación).
   Técnica de julio: token con **negative lookahead** para no tocar el escalar FK hasta la Fase C.
9. `IndicadorEcuador*` → `IndicadorEngorde*` (controller/service/interfaz/DTOs/`Calculos`).
10. Front: carpetas `features/indicador-ecuador/` → `indicador-engorde/`,
    `config/guia-genetica-ecuador/` → `config/guia-genetica/`; rutas SPA neutras + **redirect** de la vieja;
    menú en BD apuntado a la ruta nueva por migración data-only.
11. `cd backend && dotnet build` + `dotnet test`; `cd frontend && yarn build` + `ng test`.

### Fase C — BD (DDL, la parte cara)
12. Una migración EF **idempotente** que, en una sola transacción:
    a. `ALTER TABLE … RENAME TO` (3 tablas) — con guarda `to_regclass` para ser re-ejecutable.
    b. `ALTER TABLE … RENAME COLUMN` (6 columnas en 6 tablas).
    c. `ALTER INDEX/CONSTRAINT … RENAME TO` (13 objetos).
    d. **`CREATE OR REPLACE` de las 13 funciones** con el cuerpo actualizado (§2.3).
13. Espejos en `backend/sql/` actualizados en el **mismo commit** (regla «el `.sql` es el espejo, la migración
    el vehículo») + gate `node backend/scripts/verificar-sql-llega-por-migracion.js`.
14. `ToTable`/`HasColumnName` de las Configurations apuntando a los nombres nuevos; escalares CLR
    `ItemInventarioEcuadorId` → `ItemInventarioId` **con `[JsonPropertyName("itemInventarioEcuadorId")]`** (§3).
15. ⛔ **Los `.sql` operativos de una sola vez** (`migracion_*`, `backfill_*`, `fix_*`, `fase*`, `verificar_*`)
    **no se reescriben**: son el registro de lo que se hizo. Reescribirlos falsifica la historia.

---

## 5. Reglas de negocio que no se pueden mover

- **Refactor ≠ cambio de comportamiento.** Ningún número, mensaje ni redondeo cambia. Los rótulos de UI sí
  cambian: es exactamente lo pedido.
- **El histórico unificado se anula, nunca se abandona**: 2 de las 13 funciones son los triggers que lo llenan.
  Recrearlas mal deja el saldo contando filas que debería ignorar.
- **Una sola fórmula por número**: no se aprovecha el rename para «mejorar» ninguna función. Sólo cambia el
  identificador que nombran.

---

## 6. Casos de prueba

| # | Caso | Cómo se verifica |
|---|---|---|
| 1 | La pantalla ya no dice «(Ecuador)» | Captura de `config/item-inventario` y de la tarjeta en Gestión de Inventario |
| 2 | El menú dice «Guía genética» / «Indicador de engorde» | Login real, sidebar |
| 3 | Ruta SPA vieja sigue entrando | Navegar `config/item-inventario-ecuador` ⇒ redirige, no 404 |
| 4 | Ruta HTTP vieja sigue respondiendo | `GET /api/item-inventario-ecuador` y `/api/SeguimientoAvesEngordeEcuador` ⇒ 200 |
| 5 | Borrar el fantasma no cambia nada | `dotnet test` verde + POST de seguimiento de engorde en EC y PA ⇒ misma fila |
| 6 | **Las 13 funciones siguen dando el mismo número** | Congelar salida **antes** del rename, comparar fila a fila **después**, en las 3 empresas (gate multipaís de CLAUDE.md) |
| 7 | Power BI no se rompe | `\d vw_indicadores_diarios_engorde` ⇒ la columna de salida sigue llamándose `guia_genetica_ecuador_header_id` |
| 8 | El cuadre no se movió | `backend/sql/verificar_cuadre_alimento_engorde.sql` antes/después ⇒ mismas 2 señales |
| 9 | La cola offline de la PWA sigue sincronizando | Payload encolado con `itemInventarioEcuadorId` ⇒ 200 |
| 10 | Migración idempotente | Correrla dos veces sobre la misma BD ⇒ sin error |

---

## 7. Riesgo de despliegue — leer antes de mergear la Fase C

**ECS hace rollback silencioso.** Si la tarea nueva no pasa el health check 3 veces, ECS vuelve a la TaskDef
anterior **con la BD ya renombrada** ⇒ el binario viejo mapea columnas que no existen y **todo el inventario
deja de funcionar**, sin que el CLI diga otra cosa que «completado».

Por eso la Fase C:
1. va en un **deploy propio**, sin nada más adentro;
2. en horario de baja operación;
3. con la verificación post-deploy obligatoria de CLAUDE.md §🚀 (qué TaskDef corre / qué imagen tiene);
4. y con **plan de vuelta atrás escrito**: la migración inversa es simétrica (`RENAME` al revés + las 13
   funciones viejas), y se deja preparada **antes** de desplegar, no después.

Mitigación evaluada y **descartada**: vistas de compatibilidad con el nombre viejo. Son auto-actualizables
para `item_inventario`, pero no resuelven las 5 tablas donde cambia la **columna**, así que darían una falsa
sensación de red.

---

## 8. Orden de entrega

A (rótulos) → B (símbolos, sin DDL) → C (BD). Cada fase es un commit propio y validable.
**A y B se pueden desplegar con cualquier release. C exige su propio deploy y OK explícito.**
