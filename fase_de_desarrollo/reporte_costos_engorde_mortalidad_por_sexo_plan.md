# Reporte Diario Costos engorde — mortalidad por sexo (H/M) por galpón

**Pedido (Ecuador, 22-sep-2026):** «en el reporte de costo ... que se visualice en mortalidad el sexo
como hembra y macho y que nos permita ver por galpón». Pantalla `/reporte-diario-costos-engorde`
(granja Kilometro 22, lote base 2604): hoy el bloque «Mortalidad + selección» muestra UNA columna por
galpón con H+M sumados.

## Enfoque

El dato por sexo ya existe fila a fila en `fn_seguimiento_diario_engorde`
(`mortalidad_hembras/machos`, `sel_h/sel_m`); `fn_reporte_diario_costos_engorde` lo SUMA antes de
agregar por galpón. Se agrega el desglose sin tocar ningún número existente:

- **fn v4** (`CREATE OR REPLACE`, `RETURNS TABLE` igual ⇒ sin `DROP`): el JSON `galpones` suma 6
  claves nuevas por galpón y día — `mortalidad_hembras`, `mortalidad_machos`, `seleccion_hembras`,
  `seleccion_machos`, `mort_sel_hembras`, `mort_sel_machos`. Las claves existentes y las columnas
  `consumo_total_kg/mort_sel_total/aves_vivas_total/alimentos` quedan byte a byte iguales.
  Invariante: `mort_sel_hembras + mort_sel_machos = mort_sel` (y lo mismo para mortalidad/selección).
- **Quién ve el desglose lo decide un flag que YA existe:** `companies.seguimiento_engorde_mixto`
  (ON solo ItalcolPanama). Panamá no maneja el engorde por sexo: su mortalidad mixta se guarda en la
  columna H (alias «Mort Mixta» → «Mort H»), así que partirla mostraría «H: 36 · M: 0», falso. Con
  el flag ON el reporte queda IDÉNTICO al de hoy. Nada de `if (pais == X)`.
  Decisión pura en `ReporteDiarioCostosEngordeCalculos.MuestraMortalidadPorSexo(mixto)` con test; el
  service solo resuelve el flag y lo publica en el DTO (`MortalidadPorSexo`).
- **Front:** con `mortalidadPorSexo` cada galpón del bloque se abre en **H | M | Total** (el Total es
  el número que ven hoy, para que nadie lo pierda); tooltip con mortalidad/selección de ese sexo.
  Footer igual. Sin el flag, la tabla es la de siempre. El Excel sigue el mismo layout (3.ª fila de
  encabezado solo con desglose).

## Archivos

**Backend**
- `backend/sql/fn_reporte_diario_costos_engorde.sql` — espejo v4.
- `Migrations/<ts>_ReporteCostosEngordeMortalidadPorSexo.cs` (+ `.Designer.cs` clonado, sin tocar el
  ModelSnapshot) — `Up()` = fn v4, `Down()` = fn v3 exacta.
- `Application/DTOs/ReporteDiarioCostosEngordeDtos.cs` — campos nuevos (con default) en
  `ReporteDiarioCostosGalponDiaDto`, `ReporteDiarioCostosGalponTotalDto` y `MortalidadPorSexo` en el
  reporte.
- `Application/Calculos/ReporteDiarioCostosEngordeCalculos.cs` — totales por sexo en
  `ConstruirTotales` + `MuestraMortalidadPorSexo`.
- `Infrastructure/Services/ReporteDiarioCostosEngordeService.cs` — lee el flag.
- `tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosEngordeCalculosTests.cs`.

**Frontend** (`features/reporte-diario-costos-engorde/`)
- `models/reporte-diario-costos.model.ts`, `pages/.../main.component.{ts,html}`,
  `funciones/construir-aoa-reporte-costos.funcion.ts` (+ spec nuevo).

## BD
Solo la fn (por migración, `CREATE OR REPLACE`). Sin DDL de tablas, sin flag nuevo, sin seeds.

## Casos de prueba
1. xUnit: totales por galpón suman H/M por separado; H+M = MortSel; flag mixto ⇒ sin desglose.
2. SQL en transacción revertida, TODAS las empresas/granjas: la salida v4 sin las claves nuevas ==
   salida v3 (0 diferencias); `mort_sel_hembras + mort_sel_machos = mort_sel` en todas las filas.
3. Smoke API: Kilometro 22 / lote base 2604 (Ecuador) ⇒ `mortalidadPorSexo=true` y cifras H/M;
   una granja de Panamá ⇒ `false` y tabla idéntica a la de hoy.
4. UI: Ecuador ve H | M | Total por galpón (también en el footer y el Excel); Panamá sin cambios.
