# Plan — Carga masiva de Postura (Levante + Producción): alimento con inventario real, huevos completos y validaciones a paridad con el alta manual

**Fecha:** 2026-07-28
**Módulo:** Migraciones Masivas → líneas `SeguimientoLevante` y `SeguimientoProduccion` (Postura)
**Tracker:** [`tracker_estado.md`](../tracker_estado.md) (bloque al final)

---

## 1. Objetivo

Que cargar el histórico de Levante y Producción por Excel deje el sistema **en el mismo estado** que si
esos días se hubieran digitado uno por uno en el modal diario. Hoy no es así: la carga masiva escribe
consumos que **nadie descuenta del inventario**, no hay forma de cargar las entradas de alimento y el
Excel de Producción no captura los huevos que el modal sí pide.

Alcance cerrado con el usuario (2026-07-28):

| # | Decisión | Elegido |
|---|---|---|
| D1 | Manejo del alimento | **Paridad total con Engorde**: hoja `Alimento` (ingresos/traslados/recepciones/consumos sueltos) + columnas `Alimento 1/2 H-M` en la hoja `Datos` que **descuentan stock real**, + simulación de balance que rechaza el archivo si no alcanza |
| D2 | Nivel del stock | **Flag efectivo `granja ?? empresa`** (`AlimentoNivelResolver`). Hoy Sanmarino y Santa Reyes = nivel GRANJA |
| D3 | Huevos en Producción | **Todo ahora**: las 11 categorías (Sanmarino) **y** `huevoItems` por ítem del catálogo (Santa Reyes), gateado por `clasificacion_huevo_por_items` |
| D4 | Multi-lote | **Un lote por archivo** (sale del selector de pantalla, como hoy) |

---

## 2. Estado actual verificado (2026-07-28)

### 2.1 Flujo vigente

| Paso | Levante | Producción |
|---|---|---|
| Elegibilidad | lote con `lote_postura_levante` vivo | LPL `Cerrado` + liquidado + LPP vivo |
| Parseo | `MigracionService.Historicos.cs` → `ProcesarSeguimientoLevanteAsync` | → `ProcesarSeguimientoProduccionAsync` |
| Inserción | `fn_migracion_seguimiento_levante(company, usuario text, rows jsonb)` | `fn_migracion_seguimiento_produccion(company, usuario int, rows jsonb)` |
| Idempotencia | `NOT EXISTS` por `lote_id` + `fecha::date` | idem por `lote_id` + `fecha_registro::date` |
| Merge | completa filas `es_traslado=true` sin datos manuales | idem |
| Aves | descuento **incremental** sobre `aves_*_actual` | idem |
| Inventario | **ninguno** | **ninguno** |

### 2.2 Qué hace el alta manual que la carga masiva NO hace

| Efecto | Alta manual | Carga masiva hoy |
|---|---|---|
| Descontar alimento del inventario | Sí. Colombia: `ValidarStockConsumoAsync` **antes** de persistir + `AplicarConsumoAsync` en la misma transacción (si falta stock, no se guarda). EC/PA: `RegistrarConsumoAsync` por ítem, tolerante | **No** |
| Guardar el desglose por alimento (`metadata.itemsHembras/itemsMachos`) | Sí | **No** (solo `tipo_alimento` texto libre) |
| Capturar las 11 categorías de huevo | Sí (Producción) | **No** (solo Total e Incubable) |
| `huevoItems` (Santa Reyes) | Sí (`ProduccionService` + `HuevoItemsCalculos`) | **No** |
| Validar `Etapa ∈ [1,3]` | Sí (`[Range(1,3)]` en el DTO) | **No** (acepta cualquier entero; la fn hace `COALESCE(etapa,1)`) |
| Rechazar fecha anterior al encasetamiento | Sí | **No** |
| Unidad de consumo kg/qq | Sí | **No** (asume kg) |
| Reportar filas omitidas por idempotencia | — | **Miente**: `FilasOmitidas` siempre 0 (`EjecutarHistoricoAsync` lo fija en 0) |

### 2.3 Datos de plataforma verificados en BD local (`sanmarinoapplocal:5433`)

- `companies.maneja_alimento_por_galpon`: Sanmarino `f`, Santa Reyes `f`, Demo `f`, ItalcolEcuador `t`, ItalcolPanama `t`
  ⇒ **postura Colombia opera a nivel GRANJA** (`inventario_gestion_stock` con `nucleo_id`/`galpon_id` NULL), confirmado también en los datos: las granjas 1/3/4/5/20 de Sanmarino tienen stock solo a nivel granja.
- `companies.clasificacion_huevo_por_items`: solo Santa Reyes `t`.
- `companies.captura_huevos_en_levante`: solo Sanmarino `t`.
- `seguimiento_diario_levante` y `seguimiento_diario_produccion` **ya tienen** `metadata jsonb` y las 11 columnas `huevo_*`.
  ⇒ **cero migraciones de schema**: alcanza con `CREATE OR REPLACE` de las dos funciones.

### 2.4 Restricción dura descubierta en `InventarioGestionService`

`RegistrarIngresoAsync` / `RegistrarTrasladoAsync` **resuelven el nivel solos** por el flag efectivo y
**lanzan** si el request no lo respeta:

- nivel galpón + alimento sin núcleo/galpón → *"Para ítem tipo alimento debe indicar Núcleo y Galpón."*
- nivel granja + núcleo/galpón informados → *"Esta granja maneja el alimento a nivel granja (no use Núcleo/Galpón)."*

⇒ La ubicación por defecto de la hoja `Alimento` en postura **no puede ser "la del lote"** tal cual (el lote
trae núcleo y galpón). Hay que **anular núcleo/galpón cuando la granja es nivel granja**. Esto es lo que
distingue esta implementación de la de engorde, donde el galpón siempre viaja.

Para el CONSUMO hay dos métodos distintos y no son intercambiables:
- nivel galpón → `RegistrarConsumoAsync` (exige galpón)
- nivel granja → `RegistrarConsumoNivelGranjaAsync` (Colombia; el que usa `ColombiaInventarioConsumoService`)

---

## 3. Enfoque arquitectónico

### 3.1 Se CONSERVA la inserción por función plpgsql

No se migra a "insertar fila por fila con el servicio" (lo que sí hizo la línea de engorde). Motivo: las dos
funciones ya tienen validados el **merge sobre filas de traslado**, la **idempotencia por fecha calendario** y
el **descuento incremental de aves** — tres fixes documentados (`20260714022321` y el gemelo de producción)
que costaron dos rondas de smoke. Reemplazarlos sería cambio de comportamiento, no refactor.

Lo nuevo se agrega **de forma aditiva**:

- Las funciones se recrean con `CREATE OR REPLACE` **manteniendo la firma** (`company, usuario, rows jsonb`
  → `integer`). Solo se amplía el `jsonb_to_recordset` con las claves nuevas y los `INSERT`/`UPDATE` con las
  columnas nuevas. Sin `DROP FUNCTION` ⇒ patrón de migración `20260714022321`.
- El **inventario lo mueve C#**, delegando en los mismos servicios que usa el alta manual. La función SQL
  sigue sin saber nada de inventario (su cabecera lo declara y se mantiene cierto).

### 3.2 Orden de ejecución del import (espejo del engorde)

```
1. Leer hoja "Datos"      (esquema del tipo)
2. Leer hoja "Alimento"   (OPCIONAL — sin ella, todo se comporta como hoy)
3. Leer hoja "Huevos"     (OPCIONAL — solo Producción, gateada por flag de empresa)
4. Resolver nivel efectivo de la granja del lote  → posición de stock (granja | granja+núcleo+galpón)
5. Resolver ítems de alimento por nombre/código   (concepto alimento, activo, empresa efectiva)
6. Simular balance: stock actual + entradas del archivo − salidas del archivo
      salidas = consumos por ítem del seguimiento + consumos sueltos de la hoja + orígenes de traslado
   ├─ falta stock  → Error que RECHAZA el archivo entero, con el faltante exacto
   └─ ok           → saldo proyectado por posición como Advertencia (informativo)
7. dry-run  → corta acá
8. import   → a) aplicar movimientos de la hoja "Alimento" (idempotentes, en orden de fecha)
              b) invocar fn_migracion_seguimiento_* (seguimiento + aves)
              c) descontar el consumo por ítem de las filas REALMENTE insertadas/mergeadas
```

El paso 6 se ejecuta **también en dry-run**: es el único momento en que el usuario puede comparar el saldo
proyectado contra su planilla antes de tocar nada.

### 3.3 Resolución de la referencia del consumo (punto fino)

El alta manual usa `"Seguimiento lote levante #{id} {fecha}"` / `"Seguimiento producción #{id} {fecha}"` como
referencia del movimiento de inventario. La función SQL devuelve un **conteo**, no los ids.

**Solución elegida** (sin tocar la firma): después de invocar la fn, C# consulta los ids por
`(lote_id, fecha::date IN (...))` sobre la tabla correspondiente y arma la referencia con el mismo formato.
Una sola query por corrida.

Se descarta cambiar el `RETURNS` de la función a `TABLE(...)`: obliga a `DROP FUNCTION`, rompe el
`SqlQueryRaw<int>` y no aporta nada que la query posterior no dé.

### 3.4 Idempotencia del descuento (bug latente que se corrige de paso)

Antes de invocar la fn, C# consulta **qué (lote, fecha) ya existen**. Esas filas:
- la fn las omite (no las inserta) — comportamiento actual;
- C# **no descuenta** su consumo — evita el doble descuento al reimportar el mismo archivo;
- se cuentan en `FilasOmitidas`, que hoy siempre reporta 0 para postura.

Las filas de traslado que la fn va a **mergear** SÍ cuentan como procesadas y SÍ descuentan (es su primera
carga de datos manuales).

---

## 4. Archivos

### 4.1 Backend — cálculo puro (`Application/Calculos/`)

| Archivo | Acción | Contenido |
|---|---|---|
| `MigracionPosturaCalculos.cs` | **NUEVO** | `PosicionStockDeLote(nivelGranja, farmId, nucleoId, galponId)` · `EtapaValida(int?)` · `TotalHuevosDesdeCategorias(...)` · `ConsumoDescuentaInventario(itemsH, itemsM, consumoDirectoH, consumoDirectoM)` · `NormalizarUbicacionSegunNivel(UbicacionAlimento, bool nivelGranja)` |
| `MigracionAlimentoCalculos.cs` | reusar sin cambios | `TryMovimiento`, `TryOrigenIngreso`, `Simular`, `Proyectar`, `Acumular`, `ClaveIdempotencia` |
| `MigracionEsquemas.cs` | modificar | Ampliar `SeguimientoLevante` y `SeguimientoProduccion`; agregar `AlimentoPostura` y `HuevosPostura` |

### 4.2 Backend — servicio (`Infrastructure/Services/Migracion/Funciones/`)

| Archivo | Acción | Contenido |
|---|---|---|
| `MigracionService.Historicos.cs` | modificar | Parseo ampliado, orden de ejecución §3.2, omitidas reales, plantilla con hojas nuevas |
| `MigracionService.AlimentoPostura.cs` | **NUEVO** | Hoja `Alimento` de postura: lee, resuelve el nivel efectivo, aplica movimientos delegando en `IInventarioGestionService` (variante granja o galpón según el flag). Reusa `CargarUbicacionesEmpresaAsync`, `CargarAlimentosEmpresaAsync`, `ClavesMovimientosExistentesAsync`, `CargarStockPosicionesAsync` del partial de engorde |
| `MigracionService.HuevosPostura.cs` | **NUEVO** | Hoja `Huevos`: valida contra el catálogo huevo de la empresa (reusa la validación de `ProduccionService`), gate por `clasificacion_huevo_por_items` fail-closed, arma el jsonb `huevoItems` por fecha |
| `MigracionService.cs` (ancla) | modificar | Inyectar `IColombiaInventarioConsumoService?` (opcional, mismo patrón que `IInventarioGestionService?`) |

### 4.3 Backend — SQL

| Archivo | Acción |
|---|---|
| `backend/sql/fn_migracion_seguimiento.sql` | modificar (fuente canónica): ambas funciones aceptan `metadata`, las 11 categorías y `huevo_items` |
| `Migrations/<ts>_FnMigracionSeguimientoPosturaAlimentoYHuevos.cs` | **NUEVO** — `CREATE OR REPLACE` de las dos funciones, firma intacta. Sin DDL de tablas |

### 4.4 Backend — tests (`tests/ZooSanMarino.Application.Tests/`)

| Archivo | Acción |
|---|---|
| `MigracionPosturaCalculosTests.cs` | **NUEVO** |
| `MigracionEsquemasTests.cs` | ampliar (retro-compatibilidad de encabezados viejos) |

### 4.5 Frontend

Sin cambios estructurales: tipos, plantillas y reporte de errores/advertencias son genéricos y ya vienen del
backend. Se revisa que `construir-resumen-resultado.funcion.ts` muestre `FilasOmitidas` (ahora dejará de ser 0).

---

## 5. Esquemas nuevos

### 5.1 `SeguimientoLevante` — 15 → 30 columnas

Se conservan las 15 actuales (mismo título, mismo orden ⇒ los archivos viejos siguen siendo válidos) y se agregan:

| Columna | Req. | Nota |
|---|---|---|
| `Unidad Consumo` | no | `kg` (default) / `qq` (×45,36). Alias `unidad`, `unidad de consumo` |
| `Alimento 1 H` · `Consumo Alimento 1 H` | no | ítem del inventario (nombre o código) + kg |
| `Alimento 2 H` · `Consumo Alimento 2 H` | no | idem |
| `Alimento 1 M` · `Consumo Alimento 1 M` | no | idem |
| `Alimento 2 M` · `Consumo Alimento 2 M` | no | idem |
| 11 categorías de huevo + `Peso Huevo (g)` | no | **solo** si la empresa tiene `captura_huevos_en_levante` y la fila está en semana ≥ 14 (cierra el pendiente P2 del tracker) |

### 5.2 `SeguimientoProduccion` — 12 → 28 columnas

| Columna | Req. | Nota |
|---|---|---|
| (las 12 actuales) | — | sin cambios de título ni orden |
| `Unidad Consumo` | no | `kg` / `qq` |
| `Alimento 1/2 H-M` + `Consumo Alimento 1/2 H-M` | no | 8 columnas, igual que Levante |
| `Huevo Limpio`, `Tratado`, `Sucio`, `Deforme`, `Blanco`, `Doble Yema`, `Piso`, `Pequeño`, `Roto`, `Desecho`, `Otro` | no | las 11 categorías del modal |

### 5.3 Hoja `Alimento` (nueva, opcional) — ambas líneas

Mismas columnas que `MigracionEsquemas.AlimentoEngorde` (`Fecha`, `Movimiento`, `Alimento`, `Cantidad`,
`Unidad`, `Granja/Núcleo/Galpón`, `Granja/Núcleo/Galpón Origen`, `Origen`, `Referencia`, `Observaciones`).

**Diferencia con engorde:** en una granja de **nivel granja** las columnas `Núcleo`/`Galpón` se ignoran con
Advertencia explícita (*"la granja X maneja el alimento a nivel granja; se ignora el galpón indicado"*) en
vez de propagarlas y hacer que `RegistrarIngresoAsync` lance.

### 5.4 Hoja `Huevos` (nueva, opcional) — solo Producción

| Columna | Req. |
|---|---|
| `Fecha` | sí |
| `Ítem` (nombre o código del catálogo huevo de la empresa) | sí |
| `Cantidad` | sí |

Se eligió hoja aparte y no columnas fijas porque el desglose es **variable por empresa** (Santa Reyes tiene 21
ítems) y `MigracionEsquemas` es un esquema estático que debe seguir siendo la fuente única.

---

## 6. Reglas de negocio

### R1 — Consumo por ítem vs consumo directo (espejo de engorde)
- Fila **con** `Alimento 1/2` → el consumo sale de esos ítems y **descuenta inventario**. Si además trae
  `Consumo H/M (kg)` > 0, se ignora con **Advertencia**.
- Fila **sin** `Alimento 1/2` → `Consumo H/M (kg)` es consumo directo, **no descuenta** (comportamiento actual, byte a byte).

### R2 — Nivel del stock
Posición = `granja` si `AlimentoNivelResolver.ManejaPorGalpon(farm, company) == false`, si no `granja+núcleo+galpón` del lote.
Se resuelve **una vez por corrida** (un lote por archivo ⇒ una granja).

### R3 — Descuento del consumo del seguimiento
Delega en el mismo camino que el alta manual, sin reimplementar:
- Colombia (`ModeloBNivelGranja`) → `IColombiaInventarioConsumoService.ValidarStockConsumoAsync` + `AplicarConsumoAsync`
- Ecuador/Panamá (`ModeloB`) → `IInventarioGestionService.RegistrarConsumoAsync` con núcleo/galpón del lote

### R4 — Balance y rechazo
La simulación agrega **todas** las salidas del archivo (consumo del seguimiento + consumos sueltos +
orígenes de traslado) contra stock actual + entradas del archivo. Cualquier posición negativa ⇒ **el archivo
se rechaza entero**, nunca parcialmente: un import a medias deja el galpón peor que no importar.
Excepción: con `PermitirParcial` explícito se aplica la regla existente del módulo.

### R5 — Idempotencia
- Seguimiento: `(lote, fecha)` ya existente ⇒ omitida, sin descuento (§3.4).
- Alimento: `ClaveIdempotencia(movimiento, ubicación, ítem, fecha, cantidad, referencia)` contra el histórico
  de `inventario_gestion_movimientos`. Un `Consumo` sin `Referencia` genera Advertencia (dos consumos iguales
  del mismo día se tomarían por repetidos).

### R6 — Huevos en Producción
- Sin hoja `Huevos` → las 11 categorías de la hoja `Datos` (o Total/Incubable si no vienen). Comportamiento
  actual conservado cuando ninguna columna nueva está presente.
- Con hoja `Huevos` → gate por `clasificacion_huevo_por_items` de la **empresa dueña de la granja del lote**
  (fail-closed, patrón `ResolverCompanyIdDeGranjaAsync`). Flag OFF + hoja presente ⇒ **Error** explicando que
  la empresa no usa clasificación por ítems. Flag ON ⇒ `huevo_tot` = suma, `huevo_inc` y las 11 columnas = 0,
  desglose en `metadata.huevoItems` (regla ya existente de `HuevoItemsCalculos`).
- Las dos fuentes en la misma fecha ⇒ **Error** (no se adivina cuál gana).

### R7 — Huevos en Levante
Solo con `captura_huevos_en_levante` ON **y** semana de vida ≥ 14 (`HuevosLevanteCalculos`). Fuera de eso,
las columnas presentes se ignoran con Advertencia. El gate va en C# porque la fn no conoce `fecha_encaset`.

### R8 — Validaciones de fila nuevas
| Regla | Severidad |
|---|---|
| Fecha anterior a `fecha_encaset` del lote | Error |
| Fecha futura | Advertencia |
| `Etapa` fuera de [1,3] (Producción) | Error |
| Unidad de consumo distinta de kg/qq | Error |
| Alimento inexistente / ambiguo en el inventario de la empresa | Error |
| Mortalidad + selección del día > aves vivas a esa fecha | Advertencia (no bloquea histórico) |
| Consumo directo ignorado por traer ítems | Advertencia |

### R9 — Lo que NO cambia
Merge sobre filas de traslado, descuento incremental de aves, dedup por fecha calendario, contrato de
Producción (`cons_kg_h` = total, `cons_kg_m` = 0 cuando no hay desglose por ítem), elegibilidad, un lote por
archivo. Un Excel con las columnas viejas y sin hojas nuevas debe producir **exactamente** el mismo resultado
que hoy — es el criterio de aceptación #1.

---

## 7. Casos de prueba

### 7.1 Puros (xUnit, gate CI)
1. `PosicionStockDeLote` con flag granja ⇒ núcleo/galpón null; con flag galpón ⇒ los del lote.
2. `NormalizarUbicacionSegunNivel` anula núcleo/galpón en nivel granja y los conserva en nivel galpón.
3. `EtapaValida`: 1, 2, 3 ok; 0, 4, null ⇒ según regla (null ⇒ default 1, resto Error).
4. `ConsumoDescuentaInventario`: con ítems ⇒ true; solo consumo directo ⇒ false; ambos ⇒ true + advertencia.
5. `Simular` con entradas fechadas después del consumo ⇒ no falta stock (ya cubierto en engorde; se agrega el caso nivel granja).
6. Retro-compatibilidad de esquemas: los encabezados viejos de Levante (15) y Producción (12) validan sin faltantes.
7. `TotalHuevosDesdeCategorias` == suma; con `huevoItems` presentes ⇒ 11 columnas en 0.

### 7.2 Smoke API local (JWT + `X-Secret-Up` minteados, BD `:5433`)
| # | Caso | Esperado |
|---|---|---|
| 1 | Excel viejo de Levante (15 columnas), lote Sanmarino | idéntico a la corrida previa: mismas filas, mismas aves, inventario **sin tocar** |
| 2 | Levante con `Alimento 1 H` + consumo, stock suficiente | filas insertadas + `inventario_gestion_stock` descontado a **nivel granja** + movimiento con referencia `Seguimiento lote levante #id fecha` |
| 3 | Igual al #2 con stock insuficiente | **400 / archivo rechazado entero**, 0 filas insertadas, inventario intacto, mensaje con faltante exacto |
| 4 | Hoja `Alimento` con ingreso que cubre el consumo del #3 | pasa: entrada aplicada + consumo descontado, saldo final = esperado |
| 5 | Reimportar el archivo del #4 | 0 insertadas, `FilasOmitidas` = N (≠ 0), inventario **sin doble descuento** |
| 6 | Hoja `Alimento` con `Núcleo`/`Galpón` en granja de nivel granja | Advertencia + movimiento aplicado a nivel granja (no excepción) |
| 7 | Producción con las 11 categorías | columnas persistidas, `huevo_tot` coherente |
| 8 | Producción con hoja `Huevos` en Santa Reyes (flag ON) | `metadata.huevoItems` escrito, `huevo_tot` = suma, 11 columnas y `huevo_inc` en 0 |
| 9 | Misma hoja `Huevos` en Sanmarino (flag OFF) | Error explícito, nada insertado |
| 10 | Fecha < encasetamiento · `Etapa` = 5 · unidad `lb` · alimento inexistente | 4 errores de fila, ninguno insertado |
| 11 | Fila sobre una fecha con registro "solo traslado" | merge (no fila nueva) + descuento aplicado una sola vez |
| 12 | Levante con huevos, empresa Sanmarino, semana ≥ 14 | 13 columnas persistidas; semana < 14 ⇒ Advertencia e ignoradas |

### 7.3 Smoke UI (dev server, sesión inyectada)
- Descargar plantilla de Levante y de Producción: hojas `Datos`, `Alimento`, (`Huevos` en Producción),
  `Referencias` (alimentos + ítems huevo de la empresa) e `Instrucciones`.
- Dry-run mostrando el bloque de saldo proyectado por posición.
- Import real y verificación del stock en la pantalla de Gestión de Inventario.
- Abrir y cerrar el panel dos veces (checklist de change detection).

### 7.4 Cierre
- `dotnet build` 0 errores / 0 advertencias nuevas · `dotnet test` verde · `yarn build` 0 errores.
- BD local restaurada al estado original; backend y dev server detenidos (sin procesos huérfanos).

---

## 8. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Doble descuento al reimportar | §3.4: las fechas ya existentes se excluyen del descuento **antes** de invocar la fn |
| Import a medias que descuadra el galpón | R4: cualquier faltante rechaza el archivo entero; los movimientos de alimento se aplican **antes** que el seguimiento y son idempotentes |
| Romper archivos ya en uso por el cliente | Todas las columnas nuevas son `Requerida: false` y las hojas nuevas opcionales; test dedicado de retro-compatibilidad (7.1 #6) y smoke #1 |
| `RegistrarIngresoAsync` lanzando por núcleo/galpón en nivel granja | §2.4 + R2 + smoke #6 |
| Referencia del consumo sin id de seguimiento | §3.3: query posterior por `(lote, fecha)` |
| Fuga entre empresas | Empresa efectiva por `farms.company_id` de la granja del lote (patrón `ResolverCompanyIdDeGranjaAsync`), fail-closed |
| Sesiones paralelas tocando `fn_migracion_seguimiento.sql` | `CREATE OR REPLACE` sin `DROP`; la migración no altera tablas |

---

## 9. Fases

| Fase | Contenido | Gate |
|---|---|---|
| **F1** | `MigracionPosturaCalculos` + tests + ampliación de `MigracionEsquemas` + tests de retro-compatibilidad | `dotnet test` verde |
| **F2** | `fn_migracion_seguimiento.sql` v2 (metadata, 11 categorías, huevo_items) + migración `CREATE OR REPLACE` + aplicación local | `dotnet ef migrations list` sin pendientes |
| **F3** | `MigracionService.AlimentoPostura.cs` + orden de ejecución + omitidas reales + descuento del consumo | `dotnet build` limpio |
| **F4** | `MigracionService.HuevosPostura.cs` + gate por flag + plantillas (hojas nuevas + Referencias + Instrucciones) | `dotnet build` limpio |
| **F5** | Smoke API (7.2) + smoke UI (7.3) + restauración de BD | 12/12 casos verdes |
| **F6** | Commit acotado a esta tarea | — |
