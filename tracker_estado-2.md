# Tracker — Merma en Liquidación Ecuador + Peso real por lote (ventas multi-lote)

**Plan:** [fase_de_desarrollo/merma_liquidacion_ecuador_plan.md](fase_de_desarrollo/merma_liquidacion_ecuador_plan.md)

> Estado: ⏸️ Esperando OK de Moisés sobre el plan antes de escribir código.

## Backend

- [ ] B1. `fn_indicadores_pollo_engorde.sql`: semántica NULL (merma_unidades/kilos crudos, marcador `merma_registrada`, 6 campos derivados NULL cuando no hay merma; aritmética idéntica para lotes con merma)
- [ ] B2. Migración EF `UpdateFnIndicadoresPolloEngordeMermaNull` (CREATE OR REPLACE idempotente, Down() con cuerpo anterior)
- [ ] B3. `IndicadorEcuadorRow.cs`: campos merma/ajuste/total-cliente a nullable
- [ ] B4. `IndicadorEcuadorDto.cs`: mismos campos nullable (default null)
- [ ] B5. `IndicadorEcuadorService`: consolidado suma con `?? 0`; revisar y ajustar todos los consumidores backend de los campos ahora nullable (grep + build)
- [ ] B6. `OrganizarPesoAsync`: agrupar por `factura_id` → (`numero_despacho`+granja+fecha) → huérfanos solo a reporte; usar `ProrratearPesoPorLinea`; poblar `peso_*_real` + globals; dry-run con KgAntes/KgDespues
- [ ] B7. `UpdateAsync` (Crud): re-prorratear líneas hermanas de la factura al editar peso/cantidades; recalcular `PesoNeto/PromedioPesoAve` en movimientos simples
- [ ] B8. Script auditoría solo-lectura `backend/sql/audit_peso_individual_facturas_multilote.sql`
- [ ] B9. Tests xUnit `MovimientoPolloEngordeCalculosTests.cs` (prorrateo: suma exacta, residuo, 3 decimales) + sanity de fórmulas de merma con el ejemplo del correo

## Frontend (solo Ecuador)

- [ ] F1. Matriz consolidada: filas nuevas (fechas alistamiento/encaset/liquidación, merma und/%/kg, ajuste, % ajuste, total kg cliente, días engorde) en el orden del reporte de Costos, con `—` cuando null
- [ ] F2. Tab individual por lote: mismas filas nuevas
- [ ] F3. `liquidacionTotales()`: totales solo sobre lotes con merma registrada; `null` → `—`
- [ ] F4. Export Excel: celdas vacías (no 0) cuando el campo es null
- [ ] F5. Ficha imprimible (`liquidacion-reporte`): quitar `?? 0` del bloque merma; mostrar vacío
- [ ] F6. Modal liquidación: gate `!esPanama` en sección "Merma (Costos)"
- [ ] F7. Verificar que la matriz no es alcanzable en flujo Panamá (si lo es, condicionar filas nuevas)

## Validación

- [ ] V1. `cd backend && dotnet build` (0 errores, sin advertencias nuevas)
- [ ] V2. `dotnet test` en verde
- [ ] V3. `cd frontend && yarn build` en verde
- [ ] V4. Local (`make up` + `dotnet ef database update`): T1 lote con merma (0,01% / −8 / −0,02%)
- [ ] V5. Local: T2 lote sin merma → campos vacíos en pantalla, ficha y Excel
- [ ] V6. Local: T4/T6 prorrateo crear/editar — suma individuales == global
- [ ] V7. T8 Panamá intacto (modal sin merma, reporte Panamá igual)
- [ ] V8. `make down` — sin procesos vivos

## Datos prod (requiere OK explícito)

- [ ] D1. Correr auditoría de facturas multi-lote con peso global clonado (solo lectura)
- [ ] D2. `OrganizarPeso` DryRun=true → presentar KgAntes/KgDespues a Moisés
- [ ] D3. ⛔ OK explícito de Moisés
- [ ] D4. Aplicar backfill (DryRun=false) y re-verificar liquidación de los lotes afectados
