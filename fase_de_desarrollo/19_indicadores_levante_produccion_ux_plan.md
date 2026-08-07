# Tab «Indicadores» de Levante y Producción — validación contra la guía genética + unificación UX

## 0 · Qué pidió el usuario

1. Validar que el tab **Indicadores** de *Seguimiento Diario Levante* y *Seguimiento Diario
   Producción* **cuadre con la guía genética** y que los cálculos estén bien.
2. **Unificar el diseño/UX**: el de producción está distinto al de levante.
3. **Quitar el tab «Reporte semanal»** de levante y producción, porque no corresponde ahí.

---

## 1 · Validación contra la guía genética (HECHA, con datos reales)

Banco de pruebas: lote **S-369** recién reconstruido en la granja 44 (Agroavicola Sanmarino),
guía **AP 2026**, levante 24 semanas y producción 24 semanas.

### 1.1 Levante — ✅ sin hallazgos

Comparadas las 4 columnas de guía del endpoint `GET /api/SeguimientoLoteLevante/por-lote/{id}/indicadores`
contra `guia_genetica_sanmarino_colombia` (edad = semana de vida):

| Columna | Resultado |
|---|---|
| `consumoTablaHembras` vs `gr_ave_dia_h` | **24/24 exactas** |
| `pesoTablaHembras` vs `peso_h` | **24/24 exactas** |
| `mortTablaHembras` vs `mort_sem_h` | **24/24 exactas** |
| `unifTabla` vs `uniformidad` | **24/24 exactas** |

### 1.2 Producción — ✅ casi todo bien, 1 defecto real

Endpoint `POST /api/Produccion/indicadores-semanales` → `fn_indicadores_produccion_postura`.
La `semana` de producción **es la edad**, así que mapea directo contra la guía.

Verificadas correctas (24/24): `porcentajeProduccionGuia`, `consumoGuiaHembras/Machos`,
`mortalidadGuiaHembras/Machos`, `huevosTotalesGuia`, `huevosIncubablesGuia`, `pesoHuevoGuia`,
`retiroAcumuladoHembrasGuia/MachosGuia` y `pesoGuiaHembras/Machos` (la fn divide /1000 porque la
guía guarda gramos y el indicador trabaja en kg — la diferencia % da bien).

Dos cosas que **parecían** bugs y no lo son, y conviene dejar escritas para no “arreglarlas”:

- **Semana 25 tiene DOS filas en la guía** (`25` de levante y `25P` de producción). La fn usa
  correctamente la `25P`. Cualquier verificación que lea la fila `25` va a reportar un falso
  positivo.
- El **% de producción** sale de `eficienciaProduccion`, con denominador de **aves vivas
  corrientes**, no del promedio inicio/fin de la semana. Difiere <0,2 % del promedio y es
  internamente consistente.

**🔴 DEFECTO — `uniformidadGuia` siempre 0.** La guía **no trae uniformidad para edades de
producción** (solo 25 de sus 98 filas la tienen, todas de levante). La fn la lee bien como NULL
pero después la pisa:

```sql
g_unif := COALESCE(g_unif, 0);   -- línea ~777 de fn_indicadores_produccion_postura
```

Consecuencia: la columna «Uniformidad Guía» muestra **0 en las 24 semanas**, que se lee como
«la guía exige 0 % de uniformidad» en vez de «no hay dato», y `diferencia_uniformidad` se calcula
contra ese 0. Lo mismo aplica a `g_peso_h/m` cuando la guía viene vacía (`COALESCE(...,0)/1000`).

El `COALESCE` es **deliberado** (replica el `ParseDouble ⇒ 0` de una implementación vieja en C#), así
que el arreglo tiene que ser explícito y medido, no un cambio al pasar.

**Arreglo:** que las columnas de guía sin dato viajen **NULL** y la UI pinte «—».
Alcance: `g_unif`, `g_peso_h`, `g_peso_m`. Se dejan como están `g_cons_*`, `g_mort_*` y
`g_retiro_ac_*` porque la guía **sí** trae esos valores en todas las edades de producción y
cambiarlos movería números sin necesidad.

---

## 2 · Diferencias de UX entre los dos tabs

| | Levante | Producción |
|---|---|---|
| Encabezado | título + hint de fuente de guía | título + subtítulo, **con `style=` inline** |
| Chips de contexto (Regional/Granja/Módulo/Sub Lote) | ✅ | ❌ |
| Modal «📐 Fórmulas» | ✅ | ❌ |
| Descargar Excel | ✅ | ✅ |
| Leyenda de desvío vs guía (Óptimo/Aceptable/Atención) | ❌ | ✅ |
| Estado *cargando* | ❌ | ✅ |
| Estado *error* + reintentar | ❌ | ✅ |
| Estado *vacío* | ✅ (fila de tabla) | ✅ |
| Resumen acumulado (cards) | ✅ | ❌ |
| Columnas | 23, cabecera de 2 niveles | **52 + 5 sticky**, cabecera de 2 niveles con emojis |

O sea: **cada uno tiene la mitad de lo bueno**. La unificación consiste en que los dos tengan el
conjunto completo, con la misma estructura visual y los tokens del sistema de diseño.

---

## 3 · Enfoque

Sin componente compartido nuevo por ahora: los dos tabs comparten **estructura y estilos**, no
lógica (los datos y las columnas son irreconciliablemente distintos). Se unifica con:

1. **`shared/styles/indicadores-tab.scss`** — parcial con los tokens y bloques comunes:
   `.ind-header`, `.ind-chips`, `.ind-legend`, `.ind-state` (loading/error/empty), `.ind-table`,
   `.ind-summary`. Colores desde las variables de `theme-italfoods.scss` — **prohibido hardcodear**.
2. Cada componente adopta esas clases y **agrega lo que le falta** (ver tabla §2).
3. Se conservan los métodos públicos que usan las plantillas; solo cambia el marcado y el SCSS.

**Refactor ≠ cambio de comportamiento:** ninguna fórmula ni redondeo se toca, salvo el defecto
§1.2 que es el objetivo explícito.

---

## 4 · Archivos

**Backend**
- `backend/sql/fn_indicadores_produccion_postura.sql` — `g_unif`, `g_peso_h`, `g_peso_m` → NULL.
- Migración nueva (data-only, Designer clonado, idempotente).
- `tests/ZooSanMarino.Application.Tests/` — test del mapeo NULL.

**Frontend**
- `shared/styles/indicadores-tab.scss` (nuevo).
- `lote-levante/pages/tabla-lista-indicadores/*` — agregar loading/error, leyenda.
- `lote-produccion/components/tabla-lista-indicadores/*` — agregar chips, Fórmulas, resumen
  acumulado; sacar los `style=` inline; renombrar «Eficiencia» a «% Producción».
- `lote-levante/pages/tabs-principal/*` — **eliminar** el tab «🗓️ Reporte semana» (marcado, rama
  `@if`, estado `reporteSemana`, `buildReporteSemanaFilas`, `exportReporteSemanaExcel`, la interfaz
  `ReporteSemanaFila` y el SCSS que quede huérfano).

---

## 5 · Casos de prueba

1. Guía: las 4 columnas de levante siguen 24/24 exactas (regresión).
2. Guía producción: las 24/24 de las columnas que ya estaban bien **no se mueven**.
3. `uniformidadGuia` pasa de `0` a `null` en las 24 semanas; la UI muestra «—».
4. `pesoGuiaHembras/Machos` no cambian (la guía sí los trae) — verificación de no-regresión.
5. Gate multipaís de la fn: comparación fila a fila contra la versión previa en todas las
   empresas; solo deben cambiar las columnas de §1.2.
6. El tab «Reporte semana» desaparece y los otros tres siguen navegando.
7. `yarn build` sin errores nuevos; `dotnet build` + `dotnet test` verdes.
