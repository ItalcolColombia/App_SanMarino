# Validación contra los informes técnicos de Verenice — lote S-369AB

**Fecha:** 07-ago-2026
**Insumos:** `INFORME TECNICO LEVANTE S-369AB.xlsm` · `INFORME PRODUCCION S-369AB.xlsx`
**Objeto:** comprobar qué del informe técnico llega íntegro a la aplicación, qué no tiene dónde
guardarse, y dejar la plataforma alineada para que **cualquier diferencia futura sea del usuario y no
de la herramienta**.

---

## 0. Nota de arranque: el `.xlsm` de levante viene corrupto

`INFORME TECNICO LEVANTE S-369AB.xlsm` está **truncado**: conserva las 206 entradas del ZIP pero le
falta el directorio central, así que Excel/openpyxl lo rechazan (`File is not a zip file`). Se
reconstruyó con `scripts/recover.py` (reinyecta el directorio a partir de los local headers). **El
archivo original no se puede abrir tal cual** — conviene volver a exportarlo desde el origen.

---

## 1. Resultado: la aplicación NO pierde el dato del informe

Comparación **día a día, por sublote y por métrica**, del informe contra la BD. No se usaron las
hojas `… general 369AB` porque **consolidan por EDAD y no por fecha**: sus fechas no sirven para
conciliar. Se usaron las hojas consolidadas de cada sublote, que sí llevan fecha real.

| Sublote | Días del informe | Métricas | Celdas comparadas | Diferencias |
|---|---:|---:|---:|---:|
| S-369A | 175 | 8 | 1.400 | **0** |
| S-369B | 175 | 8 | 1.400 | **1** |
| | | | **2.800** | **1** |

La única diferencia es **0,20 kg** de consumo de hembras el 26-feb-2026 (informe 1.132,90 · app
1.132,70). Métricas comparadas: mortalidad H/M, error de sexaje H/M, descartes/selección H/M y
consumo de alimento H/M.

**Conclusión: sobre lo que la plataforma sí captura, la carga es fiel.** Las diferencias que aparezcan
en una conciliación no vienen de que el dato se haya perdido.

---

## 2. Lo que sí genera diferencias: el corte de etapa está una semana antes

Los 7 últimos días que el informe llama **levante** la aplicación los tiene guardados como
**producción**:

| Sublote | Levante en la app | Producción en la app | El informe llama «levante» hasta |
|---|---|---|---|
| S-369A | 30-ago-2025 → 13-feb-2026 (168 d) | 14-feb-2026 → 31-jul-2026 | 20-feb-2026 |
| S-369B | 05-sep-2025 → 19-feb-2026 (168 d) | 20-feb-2026 → 30-jul-2026 | 26-feb-2026 |

El informe cierra levante en **175 días (25 semanas)**; en la app este lote se cerró en **168 días
(24 semanas)**. El dato está completo — está del otro lado de la raya.

**Impacto:** ~**17.332 kg** de alimento y **11 aves** de mortalidad cambian de etapa. Si costos compara
«levante contra levante», eso aparece como un faltante de levante y un sobrante de producción que en
el acumulado del ciclo **no existe**. Es exactamente la misma clase de hallazgo del lote K345, con una
diferencia importante a favor:

> **En S-369 el corte es limpio: 0 días duplicados.** En K345 (cargado por script, fuera del módulo
> de Migraciones Masivas) hay **14 días de julio presentes en las dos tablas** con el mismo consumo.
> El módulo de carga masiva hace bien el corte; el script no.

**Acción de proceso:** fijar con técnica y costos si levante son 24 o 25 semanas, y cerrar todos los
lotes con el mismo criterio.

---

## 3. Lo que el informe trae y la aplicación no tenía dónde guardar

### 3.1 Coeficiente de variación en levante — CORREGIDO

El informe de levante trae **C.V. por sexo, 25 valores** (uno por semana de pesaje). En la
aplicación:

- La tabla `seguimiento_diario_levante` **sí tiene** `cv_hembras` y `cv_machos`.
- `fn_reporte_semanal_levante_extras` y `fn_resumen_semanal_ra_pesadas_levante` **leen** esas columnas
  → la columna **«C.V.%»** del Reporte Técnico Semanal de levante sale de ahí.
- El **modal de levante SÍ los captura** (controles `cvH`/`cvM`, que el servicio mapea a
  `CvHembras`/`CvMachos` en `SeguimientoLoteLevanteService.Mapeos.cs:173`). El registro diario por
  pantalla nunca tuvo el problema.
- Lo que **no** los recibía era la **carga masiva**: la plantilla de levante no tenía la columna.

Resultado: en los lotes históricos —que entran por carga masiva, no por pantalla— la columna C.V.%
del reporte semanal salía vacía. Verificado en la BD: de 336 días de S-369, `cv_hembras` tenía
**0** valores.

Producción sí aceptaba estos campos desde `20260728130000`. **Levante nunca recibió el mismo
tratamiento** — asimetría, no decisión.

### 3.2 Agua y observaciones de pesaje en levante — CORREGIDO

Mismo caso: el **modal de levante sí captura** consumo de agua, pH, ORP y temperatura
(`SeguimientoLoteLevante.ConsumoAguaDiario/Ph/Orp/Temperatura`), y la **carga masiva los descartaba en
silencio** porque la plantilla no tenía esas columnas. Producción sí las tenía.

### 3.3 Lo que el informe trae y sigue sin tener destino (NO corregido)

| Bloque del informe | Estado | Comentario |
|---|---|---|
| **Incubadora**: huevo sentado, pollitos de primera/segunda/desecho, % nacimiento | Sin campo en la app | **Vacío en este informe** — Verenice no lo diligencia. No urge |
| **Otras incubadoras** (mismo bloque duplicado) | Sin campo en la app | Vacío también |
| Venta de huevo descarte / comercial / fértil (dentro del bloque incubadora) | Vacío en el informe | La app ya lo cubre por `traslados-huevos` y la pestaña de Movimientos de Huevo |
| Consumo de agua en producción | El informe lo trae **roto** (`#REF!` en las 160 filas) | Defecto del propio Excel, no de la app |
| `BACHE` (levante) | Columna vacía en el informe | Sin uso |
| `P-H` y `MX` (peso de huevo) | Constantes 50,4 y 0,021 | Son estándar/guía, no dato capturado |

---

## 4. Lo implementado en esta pasada

### 4.1 Reporte Contable — hoja RESUMEN con Selección

`ReporteContableExcelService.cs`. La hoja RESUMEN consolidaba mortalidad, traslados y ventas pero
**no selección**, aunque la hoja semanal sí la escribe. Ahora son 12 columnas, con **Selección
inmediatamente después de Mortalidad** (mismo orden que la sección AVES de la hoja semanal).

El escritor pasó a ser **data-driven** (una lista de columnas con su formato) en vez de indexar
celdas a mano: agregar una columna ya no obliga a reindexar el encabezado, los formatos y la fila de
totales por separado, que es donde vivía el riesgo de off-by-one. El acumulado se extrajo a
`ReporteContableResumenCalculos` (cálculo puro) con **6 tests xUnit**.

### 4.2 Reporte Contable — hoja MOVIMIENTOS HUEVOS en el Excel

La información existía (`GET /api/ReporteContable/movimientos-huevos` y la pestaña en pantalla) pero
**el Excel no la exportaba**. Ahora `GenerarExcel(reporte, movimientosHuevos = null)` agrega la hoja
después de RESUMEN, **espejo exacto de la pantalla**: `Día · Fecha · Lote ·` **PRODUCCIÓN**
(`POSTURA · HVTO FÉRTIL · HVO COMERCIAL · HUEVO DESECHO`) · **MOVIMIENTOS** (`ENTRADA · CAPTURA INFO ·
VENTA · SALIDA · TRASLADO A PLANTA · DESCARTE`), más fila de TOTALES tomada del DTO (no se recalcula).

El parámetro es **opcional** y solo se resuelve en fase Producción: el Excel de Levante sale byte a
byte como hoy.

### 4.3 Carga masiva de LEVANTE a paridad con producción

Plantilla de levante + `fn_migracion_seguimiento_levante` ahora aceptan: **Coef. Variación H**,
**Coef. Variación M**, **Observaciones Pesaje**, **Consumo Agua (L)**, **pH Agua** (0-14),
**ORP Agua (mV)** y **Temperatura Agua (°C)**.

- Van **al final** de la plantilla, para no correr las columnas que los operarios copian y pegan.
- Todas **opcionales**: un archivo ya generado importa exactamente igual que antes (verificado con la
  fn contra la BD: sin las claves nuevas, esas columnas quedan NULL y el resto es idéntico).
- Migración `20260807190000_FnMigracionLevantePesajeYAgua` (CREATE OR REPLACE, firma intacta, sin DDL
  de tablas) + espejo `backend/sql/fn_migracion_seguimiento.sql` actualizado en el mismo commit.

**Con esto, el C.V. del informe de Verenice ya tiene por dónde entrar y la columna C.V.% del reporte
semanal de levante deja de estar vacía en los lotes cargados por Excel.**

### 4.4 Corte de etapa: bloqueo del doble conteo

`CorteEtapaPosturaCalculos` (cálculo puro, 10 tests) + guard en las dos direcciones:
`SeguimientoLoteLevanteService.EnsureDiaSinAporteDeProduccionAsync` y
`ProduccionService.EnsureDiaSinAporteDeLevanteAsync`.

La regla **no** es «un día no puede tener dos filas» — el arrastre de huevos del levante crea
legítimamente una fila de producción de solo huevos para un día que ya tiene su levante. Lo que se
bloquea es que **las dos filas aporten consumo o bajas**, que es el doble conteo real del caso K345.
Filas de solo huevos, de solo pesaje o vacías no chocan.

Barrido de la BD: el traslape existe **únicamente en K345** (15 días); el resto de los lotes está
limpio, así que el guard no rompe nada existente.

### Validación

- `dotnet build`: 0 errores, 0 advertencias nuevas.
- `dotnet test`: **1.929 pasan**, 0 fallan (+15 nuevos: 6 del resumen contable, 9 del esquema de levante).
- Smoke del Excel real: RESUMEN con 12 columnas y totales correctos; hoja de huevo presente solo
  cuando hay movimientos.
- Smoke de la fn de levante contra el Postgres local, en transacción revertida: escribe cv/agua/obs y
  es retro-compatible con archivos sin esas columnas.

---

## 5. Pendientes (no implementados)

| # | Qué | Por qué importa |
|---|---|---|
| 1 | Corte levante/producción: **24 vs 25 semanas** | Mueve ~17 t entre etapas en una conciliación. Es decisión de técnica + costos, no de desarrollo |
| 2 | **Limpiar los 15 días traslapados de K345** | El guard impide nuevos, pero los existentes siguen ahí. Requiere criterio (¿cuál de las dos filas queda?) y OK explícito antes de tocar datos |
| 3 | Bloque de **incubadora/nacimientos** sin destino en el modelo | Hoy el informe no lo diligencia; si empieza a usarse, hay que modelarlo |
| 4 | Re-exportar `INFORME TECNICO LEVANTE S-369AB.xlsm` | El archivo está truncado y no abre sin reparación |

> **Corrección respecto de la primera versión de este documento:** se afirmó que el modal de levante
> no capturaba el C.V. Es **falso** — sí lo captura (controles `cvH`/`cvM`). El hueco estaba solo en
> la carga masiva, que es la vía por la que entran los lotes históricos.
