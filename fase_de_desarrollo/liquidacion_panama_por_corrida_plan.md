# Plan — Liquidación Panamá por CORRIDA (tab Pollo Engorde del módulo Indicador)

## Contexto y hallazgo raíz

- El módulo `indicador-ecuador` (front) renderiza "Indicador Panamá" cuando la empresa activa es Panamá.
  El tab **Pollo Engorde** (liquidación técnica) está amarrado a la convención **Ecuador**:
  - Año/Corrida = prefijo `YYCC` del `lote_nombre` (ej. `2601…`) → en Panamá no aplica.
  - Generar exige **un lote específico** → `GET /ReporteIndicadorPanama/{loteId}` (1 lote = 1 reporte).
- **Dato real Panamá (BD):** `lote_ave_engorde.lote_nombre` **ES el número de corrida** (ej. `94`, `85`, `59`)
  y se repite en ~4 galpones de la misma granja (una fila por galpón, encasetadas con días de diferencia).
  Ej.: corrida `94` granja 89 → lotes 163/173/184/194 (galpones PA-85…PA-88). Existe el caso `85-2`
  (segundo encasetamiento del galpón en la corrida): se trata como corrida aparte (match exacto).
- El tab **Indicadores Generales** ya trae datos de Panamá (cálculo genérico) → **no se toca**.
- La liquidación Panamá vive en `liquidacion_lote_engorde_panama` (1 fila/lote, 6 insumos digitados) y el
  reporte lo arma `fn_reporte_indicadores_panama(p_lote_id)` (insumos + agregados de
  `fn_seguimiento_diario_engorde`). **No hay cambios de BD en este plan** (ni migraciones ni DDL).

## Objetivo

Cuando el país activo es **Panamá**, el tab Pollo Engorde debe permitir **buscar por corrida**:
elegir granja → corrida → "Generar liquidación" trae el reporte de **todos los galpones de la corrida**
(uno por lote) + un **consolidado de la corrida**, señalando los galpones que aún no tienen liquidación
registrada. El flujo Ecuador queda **intacto** (refactor ≠ cambio de comportamiento).

## Enfoque arquitectónico

- **Backend nuevo endpoint** `GET api/ReporteIndicadorPanama/por-corrida?granjaId=&corrida=&nucleoId=&galponId=`
  → el backend resuelve los lotes de la corrida **en la BD** (company activa + granja + `lote_nombre = corrida`,
  `deleted_at IS NULL`) y ejecuta la fn por lote (≤ ~4 lotes por corrida; secuencial, DbContext).
- **Consolidado de corrida** = matemática **pura** en `Application/Calculos/ReporteIndicadorPanamaCalculos.cs`
  replicando las fórmulas EXACTAS de la fn sobre los insumos sumados (mismos guards de división por cero):
  - Sumas: metros², aves final granja, producción kilo en pie, aves beneficiadas, aves encasetadas,
    consumo (qq → kg = qq × 45.36), total selección, total muertas, faltante/sobrante.
  - `diasEngorde` / `diasEnGranja` consolidados = **promedio ponderado por aves encasetadas** (redondeado a entero).
  - Derivados con las mismas fórmulas de la fn: pesoPromedio = prodKiloPie/avesBenef; mortalidad% =
    muertas/encasetadas×100; selección% análogo; conversión = consumoKg/prodKiloPie; consumoAve =
    consumoKg/avesBenef; mortTotal = mort+sel; supervivencia = 100−mortTotal; EA = (pesoProm×2.2046)/díasEng;
    EEF = prodKiloPie×superv/(díasEng×conv)×100; EEF-2 = pesoProm×superv/(díasEng×conv)×100;
    aves/m² = avesBenef/m²; kilos/m² = prodKiloPie/m²; productividad = EA/conv; faltanteSobra = avesFinal−avesBenef.
  - Propiedad de identidad: consolidar UN solo lote reproduce sus propios derivados.
- **Front**: rama `esPanama` en los filtros del tab Pollo Engorde — reemplaza el trío Año/Corrida/Prefijo
  (Ecuador) por un select **Corrida** poblado con los `lote_nombre` distintos del alcance en cascada
  (granja/núcleo/galpón + filtro de fecha de encaset). Lote específico sigue siendo opcional y manda si se elige.

## Archivos a crear / modificar

### Backend
| Archivo | Acción |
|---|---|
| `Application/DTOs/ReporteCorridaPanamaDto.cs` | **Nuevo**: `ReporteCorridaPanamaDto` (corrida, granjaId, items, lotesSinLiquidacion, consolidado), `ReporteCorridaPanamaItemDto` (loteAveEngordeId, loteNombre, galponId, fechaEncaset, reporte), `LoteCorridaPanamaResumenDto`. |
| `Application/Calculos/ReporteIndicadorPanamaCalculos.cs` | **Nuevo**: `ConsolidarCorrida(...)` puro (static), fórmulas espejo de la fn. |
| `Application/Interfaces/IReporteIndicadorPanamaService.cs` | + `GetReportePorCorridaAsync(granjaId, corrida, nucleoId?, galponId?, ct)`. |
| `Infrastructure/Services/ReporteIndicadorPanamaService.cs` | Inyectar `ICurrentUser`; implementar el método (query lotes por company+granja+nombre → fn por lote → armar DTO + consolidado). Null si la corrida no existe en la granja. |
| `API/Controllers/ReporteIndicadorPanamaController.cs` | + `GET por-corrida` (validaciones, 404 si no hay lotes de esa corrida). |
| `tests/ZooSanMarino.Application.Tests/ReporteIndicadorPanamaCalculosTests.cs` | **Nuevo**: identidad 1 lote, suma multi-lote, ponderación de días, guards de cero, lista vacía → null. |

### Frontend (`features/indicador-ecuador/`)
| Archivo | Acción |
|---|---|
| `funciones/corridas-panama.funcion.ts` | **Nuevo** (puras): `corridasDisponiblesPanama(lotes)` (distinct `loteNombre`, orden numérico desc) y `filtrarLotesPorCorridaPanama(lotes, corrida)` (match exacto; corrida null ⇒ misma referencia — estabilidad CD). |
| `services/indicador-ecuador.service.ts` | + interfaces TS espejo del DTO + `getReporteCorridaPanama(granjaId, corrida, nucleoId?, galponId?)`. |
| `pages/indicador-ecuador-list/indicador-ecuador-list.component.ts` | Estado nuevo (`selectedCorridaPanama`, `corridasPanama: string[]` **campo memoizado** —NG0103—, `reporteCorridaPanama`, `mostrarReporteCorridaPanama`); recálculo de corridas en `applyPeCascade` y en los hooks del filtro de encaset; rama Panamá de `generarLiquidacionPolloEngorde()`: lote elegido → flujo actual; si no, corrida → endpoint nuevo; si nada → error claro. Resets en `onPeGranjaChange`/`limpiarFiltros`. |
| `pages/.../indicador-ecuador-list.component.html` | En el bloque Año/Corrida/Prefijo: `@if (!esPanama)` mantiene el trío Ecuador; `@if (esPanama)` muestra select **Corrida** (+hint). Ocultar "Estado de lote" en Panamá (no participa del flujo). Sección de resultado nueva para el reporte por corrida. |
| `components/liquidacion-reporte-corrida-panama/` | **Nuevo** wrapper: tabs Consolidado + un tab por galpón (reusa `app-liquidacion-reporte-panama` por ítem), alerta con galpones sin liquidación, botón cerrar. |

## Fix adicional descubierto en la validación (endpoint por-lote roto)

Al probar con datos reales, el endpoint **existente** `GET /ReporteIndicadorPanama/{loteId}` también
fallaba (500): la fn devuelve numerics sin redondear y los derivados encadenados (p. ej. `eef_dos =
437.99925694900682723601469605167954990`, 39 dígitos) **no caben en `System.Decimal`** → Npgsql lanza
Overflow al leer. Fix sin DDL: el `SELECT` del servicio enumera las columnas casteadas a
`numeric(18,6)` (la UI muestra 2 decimales; nada visible cambia). Esto arregla el por-lote y el
por-corrida a la vez.

## Reglas de negocio

1. Corrida = match **exacto** de `lote_nombre` (trim) dentro de la granja (los números se repiten entre granjas).
2. Alcance del endpoint acotado por `CompanyId` del usuario activo (multi-empresa fail-closed).
3. Lote sin fila en `liquidacion_lote_engorde_panama` → va a `lotesSinLiquidacion` (el front lo lista como aviso);
   el consolidado se arma SOLO con los lotes que sí tienen liquidación.
4. 0 lotes con liquidación → items vacío + consolidado null (front: aviso "la corrida no tiene liquidaciones registradas").
5. Lote específico seleccionado tiene prioridad sobre la corrida (comportamiento actual conservado).
6. Ecuador: cero cambios de comportamiento (mismo wire, misma UI).

## Casos de prueba

- **xUnit (cálculo puro):** (a) consolidar 1 lote ≡ sus derivados (tolerancia decimal); (b) 2 lotes: sumas y
  ponderación de días correctas; (c) divisiones por cero → 0 como la fn; (d) lista vacía → null.
- **Endpoint (local, corrida 92 GR DAYLAND — 4 galpones con ~40 seguimientos):** sembrar los 6 insumos por lote
  (POST /liquidar o SQL), llamar `por-corrida` y verificar: items por galpón == salida de la fn por lote;
  consolidado == cálculo a mano; galpón sin liquidación aparece en `lotesSinLiquidacion`; corrida inexistente → 404;
  granja de otra company → 404.
- **Front:** `yarn build` limpio; en el navegador (empresa Panamá): granja → corrida → Generar muestra tabs por
  galpón + consolidado; con lote específico sigue el reporte individual; en Ecuador la pantalla no cambia.
