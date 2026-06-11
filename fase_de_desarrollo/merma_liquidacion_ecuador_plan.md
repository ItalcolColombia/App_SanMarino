# Plan — Merma en Liquidación Ecuador + Peso real por lote en ventas multi-lote

> **Requerimiento fuente:** correo "REVISION DE REQUERIMIENTOS APLICATIVO ITALGRANJAS-ECUADOR"
> (Génesis Parrales / Lady Karina Rojas, hilo 22-may → 10-jun-2026, PDF `requerimiento merme.pdf`).
> **Alcance:** SOLO mermas + corrección de peso individual por lote. El tema **sobrante de aves
> queda FUERA** (congelado por Lady Karina hasta sesión de Teams, semana del 22-jun).
> **País:** SOLO Ecuador. Panamá no se toca (ni el modal de liquidación ni su reporte).

---

## 1. Contexto y diagnóstico

### Reclamo del cliente (10-jun)
El campo de merma al liquidar el lote **existe**, pero los datos **no se reflejan en la
"Liquidación Técnica Pollo Engorde"** (la matriz en pantalla que Costos consulta y fotografía),
ni los campos de ajuste y porcentajes.

### Campos y fórmulas solicitadas (validadas contra el ejemplo km 61 lote 02)

| Campo | Ejemplo | Fórmula / origen |
|---|---|---|
| Merma (unidades) | 5 | Digitado por Costos antes de liquidar |
| Merma (kilos) | 10,66 | Digitado por Costos antes de liquidar |
| Merma (%) | 0,01 | merma_und / aves vendidas × 100 |
| Ajuste en Aves | −8 | encasetadas − vendidas − mortalidad − merma_und |
| Porcentaje de ajuste | −0,02 | ajuste / encasetadas × 100 |
| Producción kilo en pie | — | kg carne (peso neto individual de despachos) |
| Total kilos despachados a cliente | 135.080 | producción kilo en pie − merma kilos |
| Fechas alistamiento / encasetamiento / liquidación | — | datos del lote |
| Días de engorde | 51 | fecha cierre − fecha encaset |

**Regla acordada con Moisés:** si el lote NO tiene merma registrada, los 6 campos derivados
(merma und, merma kg, merma %, ajuste en aves, % ajuste, total kg a cliente) se muestran
**VACÍOS** (`—`), no en 0. Producción kilo en pie y días de engorde siempre se muestran.

### Estado del código (auditoría 11-jun-2026)

**Ya está bien (no tocar la lógica):**
- Columnas `merma_*` en `lote_ave_engorde` (migración `20260530020432` desplegada).
- `fn_indicadores_pollo_engorde` calcula todos los campos con las fórmulas correctas.
- Captura de merma en `modal-liquidacion-lote-engorde` (guardar sin cerrar + al cerrar).
- Prorrateo de peso global→individual al crear venta por granja (`CreateVentaGranjaDespachoAsync`
  + `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea` + preview front `prorateo-peso.funcion`).
- Ficha imprimible (`liquidacion-reporte`) y Excel ya tienen filas de merma.

**Gaps a corregir:**

| # | Gap | Dónde |
|---|---|---|
| G1 | La matriz en pantalla (consolidado + tab por lote) NO tiene filas de merma/ajuste/fechas/días/total a cliente | `indicador-ecuador-list.component.html` líneas ~556-810 |
| G2 | Sin semántica de vacío: `COALESCE(...,0)` en la fn y `?? 0` en DTOs/front | fn SQL, `IndicadorEcuadorRow/Dto`, matriz, ficha, Excel |
| G3 | La sección "Merma (Costos)" del modal se muestra también en Panamá (`*ngIf="resumen"` sin gate) | `modal-liquidacion-lote-engorde.component.html:335` |
| G4 | Despachos multi-lote históricos tienen `peso_neto` = neto GLOBAL clonado por línea → la liquidación por lote trae el peso de toda la salida | datos en `movimiento_pollo_engorde` |
| G5 | `OrganizarPesoAsync` (reproceso) agrupa solo por `numero_despacho` (ignora `factura_id`, puede mezclar despachos homónimos), deja con peso global los multi-lote sin número, no puebla `peso_bruto_real/peso_tara_real` y reparte sin el ajuste de residuo oficial | `MovimientoPolloEngordeService.OrganizarPeso.cs` |
| G6 | `UpdateAsync` permite editar `PesoBruto/PesoTara/cantidades` sin recalcular `PesoNeto/PromedioPesoAve` ni re-prorratear las líneas hermanas de la factura | `MovimientoPolloEngordeService.Crud.cs:388-426` |

---

## 2. Cambios Backend

### 2.1 `fn_indicadores_pollo_engorde` — semántica NULL (G2)
Archivo: `backend/sql/fn_indicadores_pollo_engorde.sql` (+ migración EF nueva).

- En el CTE `lote`: conservar `l.merma_unidades` y `l.merma_kilos` **sin COALESCE** y agregar
  marcador `merma_registrada = (l.merma_unidades IS NOT NULL OR l.merma_kilos IS NOT NULL)`.
- Salida (mismos nombres y tipos, ahora nullable):
  - `merma_unidades` → valor crudo (NULL si no registrada).
  - `merma_kilos` → valor crudo (NULL si no registrada).
  - `merma_porcentaje` → NULL si `merma_registrada = false`; si no, fórmula actual con `COALESCE(merma_unidades,0)`.
  - `ajuste_aves` y `porcentaje_ajuste` → NULL si `merma_registrada = false`; si no, fórmula actual.
  - `total_kilos_despachados_cliente` → NULL si `merma_registrada = false`; si no, `kg_carne − COALESCE(merma_kilos,0)`.
  - `produccion_kilo_en_pie`, `dias_engorde`, fechas → sin cambio (siempre con valor).
- La aritmética de los lotes CON merma queda **idéntica** (mismas fórmulas, mismo `ROUND(...,6)`).

### 2.2 Migración EF idempotente
`dotnet ef migrations add UpdateFnIndicadoresPolloEngordeMermaNull ...`
- `Up()` = `CREATE OR REPLACE FUNCTION` con el cuerpo nuevo (espejo exacto del `.sql`).
- `Down()` = cuerpo anterior. Patrón de `20260601160325_FixFnIndicadoresPolloEngordeRoundOverflow`.
- Se aplica sola en el deploy (`Database__RunMigrations=true`). Probar local con `dotnet ef database update`.

### 2.3 DTOs nullable (G2)
- `IndicadorEcuadorRow.cs`: `int? MermaUnidades`, `decimal? MermaKilos`, `decimal? MermaPorcentaje`,
  `int? AjusteAves`, `decimal? PorcentajeAjuste`, `decimal? TotalKilosDespachadosCliente`.
- `IndicadorEcuadorDto.cs`: mismos campos a nullable con default `null`.
- `IndicadorEcuadorService.CalcularConsolidadoAsync`: sumar con `?? 0` (totales no cambian de tipo).
- Revisar TODOS los consumidores que compilen contra estos campos (grep `MermaUnidades|AjusteAves|
  PorcentajeAjuste|TotalKilosDespachadosCliente` en backend) y ajustar con `?? 0` donde aplique.

### 2.4 `OrganizarPesoAsync` corregido (G5)
Archivo: `Funciones/MovimientoPolloEngordeService.OrganizarPeso.cs`.

- **Agrupación nueva (en orden):**
  1. `factura_id` (no nulo) — agrupador confiable de despachos nuevos.
  2. Sin factura: (`numero_despacho` no vacío, `granja_origen_id`, `fecha_movimiento::date`) —
     evita mezclar despachos homónimos de granjas/fechas distintas.
  3. Resto: huérfanos. Multi-línea imposible de detectar con certeza → quedan en el reporte
     dry-run como "revisión manual" con una heurística informativa (misma granja + fecha +
     peso_bruto + placa) SIN auto-corregir.
- **Reparto:** usar `MovimientoPolloEngordeCalculos.ProrratearPesoPorLinea` (misma aritmética,
  redondeo a 3 decimales y residuo a la línea con más aves que el flujo de creación).
- **Persistir:** `PesoBrutoGlobal/PesoTaraGlobal/PesoNetoGlobal` + `PesoBrutoReal/PesoTaraReal/
  PesoNeto/PromedioPesoAve` por línea.
- **Dry-run:** mantener; agregar por grupo `KgAntes` (suma peso_neto actual) vs `KgDespues`
  para ver el sobreconteo que se corrige.

### 2.5 `UpdateAsync` re-prorratea (G6)
Archivo: `Funciones/MovimientoPolloEngordeService.Crud.cs`.

- Si cambian `PesoBruto/PesoTara/Cantidad*` y el movimiento tiene `FacturaId` con hermanas:
  recargar las líneas de la factura y re-prorratear todas (los `PesoBruto/PesoTara` editados son
  el GLOBAL del camión → propagar a hermanas + globals + individuales) dentro de la transacción.
- Si es movimiento simple: recalcular `PesoNeto = bruto − tara` y `PromedioPesoAve`.

### 2.6 Auditoría + backfill de datos prod (G4)
- Script de SOLO LECTURA `backend/sql/audit_peso_individual_facturas_multilote.sql`:
  grupos multi-línea (por factura_id / numero_despacho+granja+fecha) donde
  `peso_neto` de cada línea ≈ `peso_bruto − peso_tara` (= global clonado) → lista de facturas,
  lotes y kg sobrecontados en liquidación.
- **Backfill = ejecutar `OrganizarPeso` corregido** con `DryRun=true` → presentar resultado a
  Moisés → **OK explícito** → aplicar (`DryRun=false`). ⛔ Sin confirmación no se toca prod.

### 2.7 Tests
`backend/tests/ZooSanMarino.Application.Tests/MovimientoPolloEngordeCalculosTests.cs` (nuevo, xUnit):
- Prorrateo 2-3 líneas: suma de individuales == global, residuo al lote con más aves, 3 decimales.
- Caso del correo como sanity (merma 5/0,01% · ajuste −8/−0,02% · 10,66 kg → 135.080 total cliente)
  sobre las fórmulas (helper puro si se extrae, o test de la fn vía SQL local).

---

## 3. Cambios Frontend (solo flujo Ecuador)

### 3.1 Matriz "Liquidación Técnica Pollo Engorde" (G1)
Archivo: `indicador-ecuador-list.component.html` (consolidado ~556-675 y tab por lote ~696-806).

Filas nuevas, en el orden del reporte de Costos:

| Posición | Fila | Celda lote | Celda TOTAL |
|---|---|---|---|
| Tras "Granja" | Fecha alistamiento | fecha o `—` | `—` |
| ídem | Fecha encasetamiento | fecha | `—` |
| ídem | Fecha liquidación | fecha o `—` | `—` |
| Tras "Mortalidad (%)" | Merma (unidades) | valor o `—` | suma de registradas o `—` |
| ídem | Merma (%) | valor o `—` | tot merma und / tot vendidas × 100 o `—` |
| ídem | Ajuste en Aves | valor o `—` | suma o `—` |
| ídem | Porcentaje de ajuste (%) | valor o `—` | tot ajuste / tot encasetadas × 100 o `—` |
| Tras "Kg carne pollo" | Merma (kilos) | valor o `—` | suma o `—` |
| ídem | Total kilos despachados a cliente | valor o `—` | suma o `—` |
| Tras "Edad (días, ciclo)" | Días de engorde | valor | promedio de lotes con valor |

- El tab individual por lote replica las mismas filas (ya tiene fecha encaset/cierre; se agrega
  alistamiento y liquidación).
- Regla de vacío: campo `null/undefined` → `—`. NO usar `?? 0`.

### 3.2 TS del listado (G1/G2)
Archivo: `indicador-ecuador-list.component.ts`.
- `liquidacionTotales()`: totales de merma/ajuste/total-cliente calculados SOLO sobre lotes con
  merma registrada; si ninguno la tiene → `null` (la matriz muestra `—`).
- Export Excel (filas ~954-1008): celdas vacías `''` en lugar de 0 cuando el campo es null.
- Helper de formato `fmtONada(v, dec)` (valor o `—`) — si crece, extraer a
  `features/indicador-ecuador/funciones/` según la convención CLEAN CODE del repo.

### 3.3 Ficha imprimible (G2)
Archivos: `liquidacion-reporte.component.ts/html`.
- `ajusteAves()`, `totalKilosDespachadosCliente()` y los bindings `?? 0` de merma → devolver/mostrar
  vacío (`—`) cuando la merma no está registrada. El resto idéntico.

### 3.4 Modal de liquidación (G3)
Archivo: `modal-liquidacion-lote-engorde.component.html:335`.
- Gate de país: `*ngIf="resumen && !esPanama"` en la sección "Merma (Costos)" (el getter
  `esPanama` ya existe en el componente).

### 3.5 Verificación de alcance país
- Confirmar que la matriz de liquidación pollo solo se alcanza en flujo Ecuador (en Panamá la
  página deriva a `app-liquidacion-reporte-panama`). Si fuera alcanzable, condicionar las filas
  nuevas con `!esPanama`.

---

## 4. Reglas de negocio (resumen ejecutable)

1. Merma la digita Costos (unidades y kilos) **antes** de dar clic en liquidar lote; no afecta el
   registro de producción diario.
2. Merma (%) = merma_und / aves vendidas × 100.
3. Ajuste en Aves = encasetadas − vendidas − mortalidad − merma_und (negativo ⇒ sobrante).
4. Porcentaje de ajuste = ajuste / encasetadas × 100.
5. Total kilos despachados a cliente = producción kilo en pie − merma kilos.
6. Sin merma registrada ⇒ los 6 campos derivados van **vacíos** en pantalla, ficha y Excel.
7. Kg por lote en liquidación = `peso_neto` INDIVIDUAL prorrateado de cada movimiento
   (nunca el global de la factura). El global vive en `peso_*_global` para referencia de factura.

## 5. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | Lote cerrado CON merma (und=5, kg=10,66; enc/vend/mort del ejemplo) | Merma % 0,01 · Ajuste −8 · % ajuste −0,02 · total cliente = kilos en pie − 10,66 |
| T2 | Lote cerrado SIN merma | Las 6 celdas en `—` (pantalla, ficha, Excel); kilos en pie y días de engorde con valor |
| T3 | Lote con merma solo en kilos (und null) | Merma kg con valor; el resto del bloque calculado con und=0 (registrada=true) |
| T4 | Venta granja 3 lotes, peso global 12.000/2.000 | suma de `peso_neto` individuales == 10.000 exacto; residuo en el lote con más aves |
| T5 | `OrganizarPeso` dry-run sobre facturas legacy | KgAntes = n×global, KgDespues = global; sin tocar BD |
| T6 | Editar peso bruto de una línea de factura (Pendiente) | Re-prorrateo de todas las hermanas; suma == nuevo global |
| T7 | Liquidación consolidada multi-galpón | TOTAL kg = suma individuales (no n×global); merma totales solo de lotes registrados |
| T8 | Panamá: modal liquidación y reporte | SIN sección de merma; reporte Panamá intacto |

## 6. Validación final

- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.
- `cd frontend && yarn build`.
- Local (`make up`): aplicar migración, T1-T8 manuales clave, luego `make down` (sin procesos vivos).
- Backfill prod: dry-run → revisión de Moisés → OK explícito → aplicar → re-verificar liquidación
  del lote afectado.

## 7. Fuera de alcance

- Sobrante de aves (R2): congelado hasta sesión Teams (~22-jun). La infraestructura existente
  (`PermitirSobrante`, `aves_sobrante`) no se toca.
- Reportes/módulos Panamá.
- Cambios de comportamiento en el registro diario de producción.
