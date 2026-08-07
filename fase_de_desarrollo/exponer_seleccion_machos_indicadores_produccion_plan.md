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
