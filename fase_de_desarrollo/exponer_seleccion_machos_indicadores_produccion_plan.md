# Exponer `seleccion_machos` en indicadores semanales de PRODUCCIÓN (postura)

**Fecha:** 2026-08-07 · **Tipo:** exposición de contrato (sin cambio de aritmética)

## Problema

`seleccion_machos` es un callejón sin salida: se calcula en la BD y se descarta antes de llegar al front.

Cadena verificada:

| # | Eslabón | Estado |
|---|---|---|
| 1 | `fn_indicadores_produccion_postura` emite `seleccion_machos` (col. 15 de 69) | ✅ la agregó `20260806093256_SaldoProduccionDescuentaVentasYTraslados` |
| 2 | `IndicadorProduccionSemanalBdRow.SeleccionMachos` la materializa | ✅ ya existía |
| 3 | `IndicadorProduccionSemanalDto` | 🔴 **no tenía el campo** |
| 4 | `IndicadoresProduccionCalculos.MapRow` | 🔴 **no lo mapeaba** |
| 5 | Front (`features/lote-produccion/`) | 🔴 `grep seleccionMachos` = 0 resultados |

Comprobado por API antes del fix: `POST /api/Produccion/indicadores-semanales` con
`{"lotePosturaProduccionId":7}` responde 200 con 44 semanas y **sin** la clave `seleccionMachos`.

## Alcance: exponer, NO recalcular

La migración `20260806093256` ya metió la selección de machos en la aritmética, y sigue vigente en la
última versión de la fn (`20260807140000_UniformidadGuiaProduccionNull`). Verificado **contra la fn
realmente desplegada en la BD local** (`pg_get_functiondef`, no el espejo `.sql` — ver
[[espejo-sql-desincronizado-y-gate]]):

- Saldo de machos: `v_aves_m_act := GREATEST(0, v_aves_m_act - r_mort_m - r_sel_m ...)` ✅
- %Retiro semanal M: `(r_mort_m + r_sel_m) / v_aves_m_act * 100` ✅
- %Retiro acumulado M: `(v_cum_mort_m + v_cum_sel_m) / v_aves_m_ini * 100` ✅

⇒ Esta tarea **solo agrega el paso 3-4-5**. Cero cambios en SQL, cero cambios de aritmética.

## Enfoque

### Backend
- `IndicadorProduccionSemanalDto`: agregar `int SeleccionMachos` **dentro del bloque Selección**
  (posición 15, igual que en la fn y en el BdRow). Es un `record` posicional, pero el **único** sitio
  de construcción es `MapRow` (verificado con `grep "new IndicadorProduccionSemanalDto"`), así que
  insertar en medio es seguro: si se olvidara actualizar el mapeo, no compila por aridad.
- `IndicadoresProduccionCalculos.MapRow`: `r.SeleccionMachos` en la misma posición. Es `int` → `int`,
  sin conversión ni redondeo.

### Frontend — decisión del usuario: **tabla + Excel, solo el conteo**
La fn emite el **conteo**, no el porcentaje (`porcentaje_seleccion_hembras` existe;
`porcentaje_seleccion_machos` **no**). Se descartó calcular `%Sel M` en TypeScript: sería una segunda
implementación de un número que hoy vive en la BD, y el repo prohíbe dos fórmulas para el mismo
número. Si se quiere el %, va emitido desde la fn en otra tarea.

Criterio tomado del tab de **LEVANTE** (`features/lote-levante/pages/tabla-lista-indicadores/`): allí
la tabla semanal muestra la selección como **% combinado** y los **conteos por sexo** viajan en el
Excel — o sea, el conteo por sexo es dato de Excel/detalle, no de comparación con guía. Producción ya
se apartaba de eso (muestra `Sel H` + `%Sel H` en la tabla), así que se agrega `Sel M` al lado, que es
lo mínimo consistente con lo que la pantalla ya hace.

- `produccion.service.ts` → `IndicadorProduccionSemanalDto`: `seleccionMachos: number;`
- `tabla-lista-indicadores.component.html`: `<th>Sel M</th>` + `<td>{{ ind.seleccionMachos }}</td>`
  tras `%Sel H`.
- `tabla-lista-indicadores.component.ts` → `buildIndicadoresRows()`: `SeleccionM` tras `PorcSelH`.

**Hallazgo de paso:** el `colspan` del grupo «💀 Mortalidad / Selección» decía **8** con **10**
subcolumnas debajo (quedó desactualizado cuando se agregaron `Sel H`/`%Sel H`), lo que corría 2
columnas la fila superior de encabezados respecto de la inferior. Se corrige a **11** (10 previas + la
nueva). No hay `nth-child` en el SCSS ni colspan de detalle que dependan del número (usa `999`).

## Archivos

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Application/DTOs/Produccion/IndicadorProduccionSemanalDto.cs` | + `int SeleccionMachos` |
| `backend/src/ZooSanMarino.Application/Calculos/IndicadoresProduccionCalculos.cs` | + `r.SeleccionMachos` en `MapRow` |
| `backend/tests/ZooSanMarino.Application.Tests/IndicadoresProduccionCalculosTests.cs` | `SampleRow.SeleccionMachos = 3` + aserción |
| `frontend/.../lote-produccion/services/produccion.service.ts` | + `seleccionMachos: number` |
| `frontend/.../tabla-lista-indicadores/tabla-lista-indicadores.component.html` | + `<th>`/`<td>`, colspan 8→11 |
| `frontend/.../tabla-lista-indicadores/tabla-lista-indicadores.component.ts` | + `SeleccionM` en el Excel |

**BD/SQL: ninguno.** Sin migración.

## Casos de prueba

1. `MapRow` copia `SeleccionMachos` sin alterarlo (xUnit, valor ≠ 0 para que un mapeo faltante no pase
   como falso verde: el `SampleRow` tenía `SeleccionHembras = 0`).
2. Los demás campos siguen mapeando igual (los 5 tests previos del archivo deben seguir verdes: la
   inserción en medio del record posicional no corrió nada).
3. Smoke API: `POST /api/Produccion/indicadores-semanales` `{"lotePosturaProduccionId":7}` ⇒ 200, la
   clave `seleccionMachos` aparece en las 44 semanas.
4. `yarn build` del front sin errores nuevos (único warning aceptado: bundle budget preexistente).

---

# Fase 2 — `%Sel M` emitido desde la fn

La fase 1 dejó el conteo llegando al front pero sin porcentaje: la tabla mostraba «%Sel H» sin su par
porque la fn solo calculaba `porcentaje_seleccion_hembras`. Se resuelve donde corresponde —en la
BD, no replicando la fórmula en TypeScript.

## Migración `20260807180000_PorcentajeSeleccionMachosProduccion`

Cambio **aditivo** sobre `fn_indicadores_produccion_postura`. La firma cambia ⇒ `DROP + CREATE`
(Postgres no permite cambiar el tipo de retorno), idempotente por el `DROP ... IF EXISTS`; `Down()`
restituye la versión previa completa.

El SQL se generó **partiendo del cuerpo exacto de `20260807140000`** y aplicando 4 inserciones
puntuales, cada una con guard de ocurrencia única:

| # | Inserción |
|---|---|
| 1 | columna `porcentaje_seleccion_machos double precision` tras `seleccion_machos` (+ 2 líneas de comentario) |
| 2 | `r_porc_sel_m double precision;` junto a `r_porc_sel_h` |
| 3 | `r_porc_sel_m := CASE WHEN v_aves_m_act > 0 THEN r_sel_m::double precision / v_aves_m_act * 100 ELSE 0 END;` |
| 4 | `porcentaje_seleccion_machos := r_porc_sel_m;` en el bloque de salida |

**Verificado programáticamente:** quitando esas 6 líneas del cuerpo nuevo, el resultado es **byte a
byte** el cuerpo previo. Misma fórmula y mismo denominador que el de hembras (`v_aves_*_act`, el
saldo del sexo antes de descontar la semana).

## Gate de paridad

Receta de [[espejo-sql-desincronizado-y-gate]]: la versión nueva se desplegó primero con **otro
nombre** (`..._gate`) para no tocar la fn real que estaba usando el backend de otra sesión en `:5002`.

- Se materializó la salida de ambas versiones para **los 6 lotes de producción** de la BD local
  (tablas reales, no `TEMP`: la fn crea una `TEMP TABLE ON COMMIT DROP` ⇒ **una llamada por
  transacción**, así que dos llamadas en la misma consulta fallan con `relation "_seg" already exists`).
- Comparación `EXCEPT ALL` en **ambos sentidos** sobre las **69 columnas** previas: **0 diferencias**
  en 135 filas.
- ⚠️ Cobertura real: las 135 filas son todas de la **empresa 1**; los 2 lotes de la empresa 4 existen
  pero no producen filas (sin seguimiento cargado). El gate cubre una sola empresa **por falta de
  datos, no por diseño**.

## Resto de la cadena

- `IndicadorProduccionSemanalBdRow` + `IndicadorProduccionSemanalDto` (`decimal
  PorcentajeSeleccionMachos`, tras `SeleccionMachos`) + `MapRow` (`double`→`decimal`).
- Test: valor `≠ 0` en el `SampleRow` y aserción de conversión sin pérdida.
- Front: `porcentajeSeleccionMachos` en la interfaz, columna «%Sel M» tras «Sel M», `PorcSelM` en el
  Excel, colspan del grupo 11 → **12**.

## Verificación

- Gate de paridad: 0 diferencias (arriba).
- `dotnet build` 0 errores · `dotnet test` 1864+1 verdes · `ng build` OK.
- **La migración la aplicó EF sola** al arrancar el backend (`Database:RunMigrations=true`), que es
  el camino correcto: no se tocó `__EFMigrationsHistory` a mano. Antes se verificó que la única
  migración pendiente era ésta (los 4 archivos `*.Fn.cs` que aparecen como «pendientes» son
  `partial class` de migraciones ya aplicadas).
- Smoke `POST /api/Produccion/indicadores-semanales {lotePosturaProduccionId:7}` ⇒ 200, 44 semanas,
  las dos claves presentes en las 44 y el `%Sel M` coincidiendo con la fórmula en **44/44**.
  Semana 56: `porcentajeSeleccionMachos == retiroSemanalMachosReal` (2.1242) porque esa semana no
  hubo mortalidad de machos — confirma que el retiro es mortalidad + selección.
- Estructura de la tabla: 61 = 61 = 61 (grupos, subcolumnas, `<td>`).
