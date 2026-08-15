# Reportes de postura ciegos a los lotes cargados como «Produccion»

**Síntoma reportado (14ago26, prod):** con la cascada MANGOS → Modulo I → galpón 2 → **S369** y la fase
**LEVANTE**, «Generar Reporte» devuelve:

> Error: No se encontraron lotes levante para LotePosturaBase 30.

El usuario tiene los datos cargados (168 seguimientos diarios por sublote) pero ningún reporte de
levante los ve.

---

## 1. Diagnóstico (verificado contra la BD, no supuesto)

El error sale de [`ReporteTecnicoService.cs:2633`](../backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs),
después del filtro de la línea 2620:

```csharp
.Where(lpl => lotesIds.Contains(lpl.LoteId)
           && lpl.CompanyId == _currentUser.CompanyId
           && lpl.DeletedAt == null
           && lpl.Etapa == "Levante");     // ← acá se cae
```

Datos reales del lote base 30 (S369, company 1, granja 12):

| lpl_id | lote | lote_id | etapa | estado_cierre | seguimientos levante | lote_postura_produccion |
|---|---|---|---|---|---|---|
| 34 | S369A | 142 | **Produccion** | Abierto | **168** | — |
| 35 | S369B | 143 | **Produccion** | Abierto | **168** | — |
| 36 | S369A | 144 | **Produccion** | Abierto | 0 | — |
| 37 | S369B | 145 | **Produccion** | Abierto | 0 | — |

Los cuatro tienen `etapa = 'Produccion'` **sin haber pasado nunca a producción**: no hay fila en
`lote_postura_produccion`, `estado_cierre` sigue `Abierto` y los datos diarios son de levante
(ago-2025 → feb-2026, 24 semanas).

### Por qué nacieron en «Produccion»

`FaseLoteCalculos.DerivarPorEdad` marca `Produccion` cuando pasaron ≥ 26 semanas desde el
encasetamiento. S369 se cargó el 12ago26 con encaset **2025-08-29** ⇒ ~50 semanas ⇒ nació
«Produccion» aunque su historia sea de levante. Es exactamente el caso que el propio doc-comment de
`FaseLoteCalculos` describe: *«un lote encasetado hace un año nace en Producción y los dos reportes
de levante lo filtran … así que el dato entra por carga masiva y el reporte no lo ve nunca»*. La
fase opcional al crear evita el problema **hacia adelante**, pero no arregla los reportes ni los
lotes ya cargados.

### Por qué el filtro es incorrecto en sí mismo

`lote_postura_levante.etapa` (y `lotes.fase`) **sólo** toma el valor `Produccion` en la derivación
por edad al crear el lote. El paso real levante → producción **no la actualiza**: crea una fila en
`lote_postura_produccion` (`LotePosturaLevanteService.cs:172` escribe `Etapa = "Produccion"` sobre la
entidad *de producción*, no sobre la de levante) y un lote hijo en `lotes`. Se comprueba con K345:
`lpl 1/2` siguen en `etapa='Levante'` **y ya tienen** `P-K345A`/`P-K345B` en producción con 301
seguimientos cada uno.

⇒ Una fila de `lote_postura_levante` **es** el registro de levante por definición. Filtrar por
`etapa == "Levante"` no separa levante de producción: sólo esconde los lotes cargados con historia.

### Mismo bug en el reporte semanal

[`ReporteTecnicoSemanalService.Levante.cs:25`](../backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoSemanal/Funciones/ReporteTecnicoSemanalService.Levante.cs)
filtra `l.Fase != "Produccion"` sobre `lotes`, con el mismo efecto.

### Alcance real del daño (BD local, refresco de prod)

| lote base | lpl con datos de levante ocultos |
|---|---|
| 30 · S369 | 34 (168 segs), 35 (168 segs) |
| 2 · A374 | 6 (45 segs), 7 (2 segs) — el reporte de A374 hoy muestra **sólo 2 de sus 4 galpones** |

---

## 2. Enfoque

**Arreglar el filtro, no los datos.** Un `UPDATE` a `etapa='Levante'` sobre S369 taparía el síntoma
de un lote y dejaría A374 mal y el próximo histórico igual de roto. El dueño del número es la
existencia de la fila de levante, no una etiqueta derivada de la edad.

Se conserva la exclusión del **lote hijo de producción** (`Fase == "Produccion" && LotePadreId != null`,
el patrón que ya usa `LoteService.cs:129`), que es el único registro que legítimamente no es levante.
Ese hijo, además, nace sin `LotePosturaBaseId`, así que ya quedaba fuera por el filtro de lote base.

### Archivos a modificar

| Archivo | Cambio |
|---|---|
| `Application/Calculos/FaseLoteCalculos.cs` | + `EsRegistroLevante(fase, lotePadreId)` — predicado puro, único dueño de la regla |
| `Infrastructure/Services/ReporteTecnicoService.cs` (~2620) | quitar `lpl.Etapa == "Levante"`; mensaje de error más honesto |
| `Infrastructure/Services/ReporteTecnicoSemanal/Funciones/ReporteTecnicoSemanalService.Levante.cs` (~25) | `l.Fase != "Produccion"` → excluir sólo el hijo de producción |
| `tests/ZooSanMarino.Application.Tests/FaseLoteCalculosTests.cs` | cobertura del predicado |

**Sin cambios de BD.** Ninguna migración.

---

## 3. Reglas de negocio

1. Toda fila de `lote_postura_levante` viva (`deleted_at IS NULL`) del lote base es levante,
   cualquiera sea su `etapa`.
2. El reporte de levante sigue recortado a **semanas 1-25** (`CalcularEdadSemanas <= 25`): un lote
   que ya lleva 50 semanas no inventa filas nuevas, sólo deja de estar oculto.
3. Un lote de `lotes` es levante salvo que sea el hijo de producción (`Fase == "Produccion"` **y**
   `LotePadreId != null`).
4. El scoping por empresa y por granja (`UserLocationScopeCalculos`) no se toca: sigue fail-closed.

---

## 4. Casos de prueba

**Unitarios (`FaseLoteCalculosTests`)**
- `EsRegistroLevante("Levante", null)` ⇒ true
- `EsRegistroLevante("Produccion", null)` ⇒ true (lote cargado con historia)
- `EsRegistroLevante("Produccion", 13)` ⇒ false (hijo de producción)
- `EsRegistroLevante("Levante", 13)` ⇒ true (sublote de levante, caso K345B)
- `EsRegistroLevante(null, null)` ⇒ true
- No cambia `Resolver`/`DerivarPorEdad`/`NormalizarFaseIndicada`.

**Smoke HTTP (backend local, JWT minteado, company 1)** — validar **cada** reporte de postura:

| # | Reporte | Endpoint | Esperado |
|---|---|---|---|
| 1 | Técnico levante (pantalla del ticket) | `POST /api/ReporteTecnico/levante/obtener` | S369 ⇒ 200 con 24 semanas |
| 2 | Técnico levante completo | `GET /api/ReporteTecnico/levante/completo/{loteId}` | 200 |
| 3 | Técnico levante con tabs | `GET /api/ReporteTecnico/levante/tabs/{loteId}` | 200 |
| 4 | Cascada de filtros levante | `GET /api/ReporteTecnico/levante/filter-data` | S369 presente |
| 5 | Técnico producción | `POST /api/ReporteTecnicoProduccion/obtener-tabs` | sin regresión |
| 6 | Semanal levante | `POST /api/ReporteTecnicoSemanal/levante` | S369 ⇒ 200 con sublotes |
| 7 | Semanal producción | `POST /api/ReporteTecnicoSemanal/produccion` | sin regresión |
| 8 | Semanal resumen / curva | `POST …/resumen`, `…/curva` | sin regresión |
| 9 | Diario de costos postura | `POST /api/ReporteDiarioCostosPostura/generar` | sin regresión |
| 10 | Contable | `GET /api/ReporteContable/generar` | fases Levante y Produccion |

**No regresión:** K345 (lote base 1, transición real a producción) y A374 (lote base 2) deben seguir
respondiendo 200; A374 pasa de 2 a 4 sublotes en levante — es la corrección, se documenta.

**Ciclo de vida:** backend levantado sólo para el smoke y apagado al terminar (`:5002` libre).

---

## 5. Resultado del smoke (backend local :5002, company 1, 14ago26)

`dotnet build` 0 errores / sin advertencias nuevas · `dotnet test` **2.486 pasados, 0 fallidos**.

### S369 · lote base 30 (el del ticket)

| # | Reporte | Antes | Ahora |
|---|---|---|---|
| 1 | `POST /ReporteTecnico/levante/obtener` (Semanal y Diario) | **error** | **200 · 24 semanas** |
| 2 | `GET /ReporteTecnico/levante/completo/34` | error | 200 |
| 3 | `GET /ReporteTecnico/levante/tabs/34` | error | 200 |
| 4 | `GET /ReporteTecnico/levante/filter-data` | 200 | 200 |
| 5 | `POST /ReporteTecnicoProduccion/obtener-tabs` | 404 | 404 «No hay lotes de producción para la base 30» — correcto: S369 nunca pasó a producción |
| 6 | `POST /ReporteTecnicoSemanal/levante` | 200 con **0 tabs** | 200 con **4 tabs** (142 y 143 con 24 semanas; 144 y 145 sin datos) |
| 7 | `POST /ReporteTecnicoSemanal/produccion` | 200 vacío | 200 vacío (no hay producción) |
| 9 | `POST /ReporteDiarioCostosPostura/generar` | 200 | 200 |
| 10 | `GET /ReporteContable/generar?…faseLote=Levante` | 200 | 200 |
| — | Excel `GET /ReporteTecnico/levante/exportar/excel/34` | error | 200 · 54 KB xlsx |

Muestra de la semana 24 de S369A: saldo H 19.018 / M 2.247, cons. ac. H 206.248 kg, peso H 3.029 g,
uniformidad 87,4 %, con su cruce contra la guía — el dato estaba entero, sólo estaba oculto.

### No regresión

| Lote base | Antes | Ahora |
|---|---|---|
| 1 · K345 (transición real a producción) | levante 2 tabs · 25 sem · producción OK | **idéntico** |
| 2 · A374 | levante **2 tabs** (galpones 1 y 2) | **4 tabs** — aparecen los galpones 3 y 4 (lotes 114 y 115), que estaban ocultos por el mismo motivo |

`resumen` levante/producción, `curva` levante/producción, `lotes-base`, `filtros-disponibles` y los
Excel de producción y contable: 200 sin cambios.

**Backend apagado; `:5002` libre.**

---

## 6. Hallazgo aparte (NO tocado)

`porcDifPesoH` / `porcDifPesoM` del reporte técnico de levante comparan el **peso real en gramos**
contra la **guía en kilos**, así que rinden porcentajes absurdos: S369A semana 1 → `104037,93`
(peso 151 g vs guía 0,145 kg). Es **previo e igual en todos los lotes** (K345 semana 1 → `109555,17`),
no lo introduce este cambio, y corregirlo mueve un número visible en pantalla. Queda documentado
para decidirlo aparte.
