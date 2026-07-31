# Plan — Carga masiva Seguimiento Diario Levante: movimientos de aves + tab huevos fijo + ocultar estructura

**Fecha:** 2026-07-31
**Módulo:** Migraciones Masivas (`features/migraciones-masivas` + `Services/Migracion`) · Seguimiento Levante (`lote-levante` + `SeguimientoLoteLevanteService`)

## 0. Estado actual verificado (workflow 6 agentes, 192 lecturas)

La carga masiva de `SeguimientoLevante` **ya cubre** la mayor parte del pedido (commit `7846200`, 28-jul):

- **Esquema de 36 columnas** (`MigracionEsquemas.SeguimientoLevante`, L93-115): 15 históricas + Unidad Consumo + 8 slots de alimento de inventario + 11 categorías de huevo + Peso Huevo. Solo `Fecha` es requerida.
- **Plantilla de 4 hojas**: Datos, **Alimento**, Referencias, Instrucciones (`GenerarPlantillaSeguimientoAsync`, Historicos.cs:109-211).
- **Hoja Alimento** = mismo esquema de engorde (`AlimentoPostura => AlimentoEngorde`). Soporta Ingreso/Traslado/Recepción/Consumo delegando en `IInventarioGestionService`; simulación de balance (`SimularBalancePosturaAsync`) que **rechaza el archivo entero** si falta stock; idempotencia por `ClaveIdempotencia` contra `inventario_gestion_movimiento`.
- **Consumo del seguimiento** (slots Alimento 1/2 H-M) descuenta inventario con la referencia byte a byte del alta manual (`Seguimiento lote levante #{id} {fecha}`).
- **Huevos**: 11 categorías en la hoja Datos; `huevo_tot`/`huevo_inc` derivados en C#; doble gate (flag `captura_huevos_en_levante` + semana ≥ 14) como **Advertencia + cerar** (no bloquea la fila).
- **fn_migracion_seguimiento_levante** (vigente = `20260728140000`, idéntica al canónico `backend/sql/fn_migracion_seguimiento.sql`): merge sobre filas de traslado "limpias", dedup por fecha calendario, descuento **incremental** de aves.
- **Flujo UI**: paso 1 tipo → paso 2 empresa (header) + granja/núcleo/galpón (`HierarchicalFilter`) + lote (elegibles) → paso 3 plantilla/upload. **Un lote por archivo.**
- **Cierre de lote manual** posterior (cerrar levante → crea lote de producción) ya existe; fuera de alcance.

**Lo que NO existe** (confirmado): movimientos/traslados de aves en la carga masiva de levante (cero columnas, cero hojas), y el tab de huevos está gateado por semana 14.

## 1. Alcance (3 tareas)

### A. Ocultar visualmente los tipos de ESTRUCTURA (Granjas, Núcleos, Galpones)

Solo front (el backend y el historial quedan intactos):

- `funciones/agrupar-tipo-migracion.funcion.ts`: set `TIPOS_ESTRUCTURA: ReadonlySet<TipoMigracionCodigo>` + `esTipoEstructura()`.
- `migraciones-masivas-page.component.ts`: `computed` `tiposVisibles` que filtra; el paso 1 usa `[tipos]="tiposVisibles()"`.
- El historial (`<app-historial-migraciones [tipos]="tipos()">`) **sigue recibiendo la lista completa** para que las corridas viejas de Granjas/Núcleos/Galpones muestren su nombre legible.
- No se toca `Disponible` (eso solo deshabilita con "Próximamente", no oculta).

### B. Hoja nueva "Movimientos Aves" en la plantilla de SeguimientoLevante

**Decisión de diseño** (el usuario dejó abierta hoja vs columna): **hoja separada**, como la hoja Alimento — permite varios movimientos por día y columnas de contraparte propias sin ensanchar las 36 de Datos.

**Semántica (pedida por el usuario):**
- **Salida** = traslado de aves de ESTE lote hacia otro. Valida que el **lote destino exista** en la empresa (espejo `lote_postura_levante`). **NO acredita al destino** (el destino carga su propio Ingreso en su propio archivo) — evita doble conteo entre archivos.
- **Ingreso** = aves recibidas "en tránsito". Acredita a ESTE lote. **NO descuenta al origen**. Lote Origen opcional (informativo/cohorte).

**Esquema nuevo** `MigracionEsquemas.MovimientosAvesLevante`, hoja `"Movimientos Aves"`:

| # | Columna | Req | Alias | Opciones |
|---|---|---|---|---|
| 1 | Fecha | sí | — | — |
| 2 | Tipo | sí | `movimiento`, `tipo movimiento` | Salida, Ingreso |
| 3 | Hembras | no | `cantidad hembras`, `traslado hembras` | — |
| 4 | Machos | no | `cantidad machos`, `traslado machos` | — |
| 5 | Lote Contraparte | no* | `lote destino`, `lote origen`, `lote` | dropdown Referencias |
| 6 | Granja Contraparte | no | `granja destino`, `granja origen` | — |
| 7 | Observaciones | no | — | — |

\* Requerida **por fila** cuando Tipo=Salida (Error si falta/no existe/ambigua); opcional en Ingreso (no resoluble ⇒ Advertencia, se registra sin contraparte).

**Efectos al aplicar (espejo del camino `TrasladoAvesDesdeSegService`, pero UNILATERAL):**

Salida sobre el lote del archivo:
1. Fila de `seguimiento_diario_levante` de esa fecha (la que insertó la fn, o upsert si no existe): `traslado_salida_hembras += H`, `traslado_salida_machos += M`, `traslado_aves_salida += H+M`, `es_traslado=true`, `traslado_direccion='SALIDA'`, `traslado_lote_contraparte_id = <espejo destino>`, `traslado_granja_contraparte_id`, observaciones concatenadas.
2. `lote_postura_levante`: `levante_traslado_salida_hembras/machos += `, `aves_h_actual/aves_m_actual = max(0, actual − X)`.
3. `movimiento_aves` de **auditoría** en estado `Completado` (número `MGA-yyyyMMdd-…`, patrón TSD), **sin** pasar por `MovimientoAvesService.CreateAsync` (auto-procesa ⇒ doble conteo, regla §2 de la spec Fase 3) y **sin** tocar `inventario_aves` (paridad exacta con `TrasladoAvesDesdeSegService`, que tampoco lo toca).

Ingreso: espejo (ingreso_*, `+=` en aves actuales, dirección `'INGRESO'`, contraparte como origen). Si el Lote Origen resuelve y tiene `fecha_encaset` ⇒ cohorte en `lote_aves_cohortes` ligada al `MovimientoAvesId` (como el service vivo: si falta encaset NO bloquea, se omite).

**Validaciones:**
- Contraparte por nombre de lote (normalizado case/acento-insensible) o id, contra espejos LPL vivos de la empresa; ambigüedad ⇒ Error pidiendo id; solo etapa levante en v1 (cross-etapa fuera de alcance).
- Contraparte == lote del archivo ⇒ Error.
- H y M ≥ 0, H+M > 0.
- Saldo proyectado: `aves_actuales − Σ(mort+sel+err nuevas del archivo) − Σsalidas + Σingresos < 0` ⇒ **Advertencia** (no bloquea histórico; el descuento satura en 0 como la fn).
- Fecha contra encaset igual que Datos (anterior ⇒ Error, futura ⇒ Advertencia).

**Idempotencia:** antes de aplicar cada fila se busca en `movimiento_aves` un registro del mismo día (rango calendario, sin `.Date` en el WHERE) con mismas cantidades y el lote del archivo como origen (Salida) o destino (Ingreso). Existe ⇒ se omite y cuenta en `FilasOmitidas`. Reimportar el mismo archivo = 0 efectos.

**Orden en `EjecutarHistoricoPosturaAsync`:** los movimientos de aves se aplican **DESPUÉS de la fn** (las filas diarias ya existen y el upsert las extiende sin tocar mort/sel — mismo contrato del service vivo). Dry-run: valida y simula, no aplica. Fallos por fila se reportan como errores (patrón `AplicarMovimientosAlimentoAsync`), no se tragan.

**Plantilla:** hoja nueva solo en levante (`esLevante`); Referencias suma la lista de lotes levante de la empresa (nombre — para el dropdown de Lote Contraparte); Instrucciones documenta Salida/Ingreso unilateral + idempotencia. Producción ignora la hoja (como levante ignora "Huevos").

**Archivos:**
- `Application/Calculos/MigracionEsquemas.cs` (+esquema) y `MigracionMovimientosAvesCalculos.cs` (NUEVO, puro: `TryTipo` con sinónimos, clave de dedup, proyección de saldo).
- `Infrastructure/Services/Migracion/Funciones/MigracionService.MovimientosAves.cs` (NUEVO partial, namespace plano): `LeerHojaMovimientosAvesAsync` + `AplicarMovimientosAvesAsync`.
- `MigracionService.Historicos.cs`: leer la hoja en `ProcesarSeguimientoLevanteAsync`, plantilla, pasar la lista a `EjecutarHistoricoPosturaAsync`.
- Tests: `MigracionMovimientosAvesCalculosTests.cs` (NUEVO) + casos de esquema en `MigracionEsquemasTests.cs`.
- Sin DDL, sin cambios de front (la UI genérica ya sube el archivo).

### C. Tab «Huevos» de levante FIJO (cae el gate de semana 14; el flag por empresa se conserva)

Decisión del usuario: el tab deja de aparecer "a partir de la semana X" y queda fijo para capturar cuando sea el momento.

**Nueva regla de `PermiteHuevos`:** se permite salvo `fechaRegistro < fechaEncaset` (cuando hay encaset). Sin encaset ⇒ permitido (la única condición restante no se puede evaluar; mantener fail-closed dejaría un 400 sin remedio con el tab visible).

Backend:
- `HuevosLevanteCalculos`: quitar `SemanaMinimaHuevosLevante` y la condición de semana en `PermiteHuevos` (L110-115); `SemanaVida` se conserva (la usan otros).
- `SeguimientoLoteLevanteService.AplicarGateHuevosLevanteAsync` (L120-122): mensaje del throw pasa a hablar de fecha anterior al encaset.
- `MigracionService.Historicos.cs:279-285`: cae la rama de semana (la del flag queda); texto de Instrucciones (L201-202) actualizado.
- Tests `HuevosLevanteCalculosTests`: reescribir los 5 del gate (semana 13/14, constante, fail-closed sin encaset ⇒ ahora true, anterior al encaset ⇒ false).

Frontend:
- `semana-vida-levante.funcion.ts`: `permiteHuevosEnLevante` espeja la nueva regla; cae `SEMANA_MINIMA_HUEVOS_LEVANTE`.
- `modal-create-edit.component.ts` `recalcularVisibilidadHuevos` (L301-308): `mostrarTabHuevos = capturaHuevosEnLevante && permiteHuevosEnLevante(...)` (queda fijo con flag ON salvo fecha anterior al encaset).
- ⚠️ `construirPayloadHuevos` (L1139-1146): conservar la semántica «vacío ⇒ null» (no mandar 0 por defecto) para no romper `ConservarHuevosPrevios`.
- Columnas de la grilla/Excel ya se gatean SOLO por flag — sin cambios.

## 2. Riesgos y reglas que NO se rompen

- **Doble conteo**: jamás llamar `MovimientoAvesService.CreateAsync/Procesar` para históricos (auto-procesa). Los movimientos de la carga son unilaterales por diseño del usuario.
- **`inventario_aves` NO se toca** (el camino vivo desde seguimiento tampoco lo hace; el saldo real sale de `GetMortalidadResumenAsync` que lee acumulados LPL + filas diarias).
- Comparaciones de fecha contra timestamptz: rango calendario (patrón `FechasYaCargadasAsync`), nunca `.Date ==`.
- `seguimiento_diario_levante.lote_id` es **varchar** (lote base como string); `traslado_lote_contraparte_id` es id de **espejo** LPL.
- Refactor ≠ cambio de comportamiento: producción y engorde quedan byte a byte iguales.

## 3. Validación

- `dotnet build` 0/0 + `dotnet test` (nuevos calculos + esquemas + huevos) — toolchain: `~/.dotnet/dotnet.exe` (SDK 10), Node portable 22.23.1.
- `yarn build` 0 errores.
- Smoke API local (backend propio en puerto alterno, JWT + X-Secret-Up minteados, BD :5433): plantilla con 5 hojas · import con Salida+Ingreso → filas diarias extendidas + acumulados LPL + `movimiento_aves` auditoría · reimport = 0 · destino inexistente rechaza · huevos en semana < 14 ahora entran (flag ON) · flag OFF sigue cerando · BD restaurada.
