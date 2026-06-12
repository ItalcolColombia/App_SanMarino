# Tracker — Merma en Liquidación Ecuador + Peso real por lote (ventas multi-lote)

**Plan:** [fase_de_desarrollo/merma_liquidacion_ecuador_plan.md](fase_de_desarrollo/merma_liquidacion_ecuador_plan.md)

> Estado: ✅ Desarrollo y validación local COMPLETOS. ⏸️ Pendiente solo el bloque "Datos prod" (D1-D4), que requiere OK explícito de Moisés.

## Backend

- [x] B1. `fn_indicadores_pollo_engorde.sql`: semántica NULL (merma cruda, marcador `merma_registrada`, 6 campos derivados NULL cuando no hay merma; aritmética idéntica para lotes con merma)
- [x] B2. Migración EF `20260611220237_UpdateFnIndicadoresPolloEngordeMermaNull` (CREATE OR REPLACE idempotente, Down() restaura el cuerpo previo) — aplicada en BD local
- [x] B3. `IndicadorEcuadorRow.cs`: campos merma/ajuste/total-cliente a nullable
- [x] B4. `IndicadorEcuadorDto.cs`: mismos campos nullable (default null)
- [x] B5. `IndicadorEcuadorService`: consolidado suma con `?? 0`; único consumidor afectado, build limpio
- [x] B6. `OrganizarPesoAsync` REDISEÑADO tras auditar la BD (ver hallazgo en el plan §2.4-bis):
  - agrupa por `factura_id` → `numero_despacho`+granja (sin fecha; hay despachos reales cuyas líneas cruzan fechas) → huérfanos
  - CLON GLOBAL (mismo bruto/tara en todas las líneas): ya prorrateado → solo normaliza `*_global`; sin prorratear (neto nulo o = global) → re-prorratea con `ProrratearPesoPorLinea`; prorrateado contra OTRO global → RevisionManual
  - PESO POR LÍNEA (pesaje propio por galpón/viaje): restaura `peso_neto = bruto − tara` (repara la corrupción del reproceso anterior) y `*_global` = suma del despacho
  - huérfanos sospechosos (granja+fecha+bruto+placa) → RevisionManual, sin tocar
  - dry-run con `KgAntes`/`KgDespues` por grupo
- [x] B7. `UpdateAsync` (Crud): re-prorratea líneas hermanas de la factura al editar peso/cantidades; recalcula `PesoNeto/PromedioPesoAve` en movimientos simples; valida bruto ≥ tara
- [x] B8. Script auditoría solo-lectura `backend/sql/audit_peso_individual_facturas_multilote.sql` (6 secciones: clones sin prorratear, peso propio corrupto, globales mal, huérfanos, resumen kg por lote)
- [x] B9. Tests xUnit: `MovimientoPolloEngordeCalculosTests` (6) + `IndicadorEcuadorCalculosTests` (9, con el ejemplo del correo) — 17/17 en verde

## Frontend (solo Ecuador)

- [x] F1. Matriz consolidada: filas nuevas (fecha alistamiento/encaset/liquidación, merma und/%, ajuste, % ajuste, merma kg, total kg cliente, días engorde) en el orden del reporte de Costos, `—` cuando null
- [x] F2. Tab individual por lote: mismas filas nuevas
- [x] F3. `liquidacionTotales()`: totales de merma solo sobre lotes con merma registrada; sin ninguna → null → `—`; agrega promedio de días de engorde
- [x] F4. Export Excel: celdas vacías (no 0) cuando merma null; fechas y días de engorde agregados al consolidado
- [x] F5. Ficha imprimible (`liquidacion-reporte`): bloque merma con `fmtONada` (vacío); helpers devuelven null sin merma
- [x] F6. Modal liquidación: sección "Merma (Costos)" con gate `!esPanama`
- [x] F7. Verificado: en Panamá `generarLiquidacion()` retorna temprano hacia `reportePanama` — la matriz Ecuador no es alcanzable

## Validación

- [x] V1. `dotnet build` — 0 errores, sin advertencias nuevas (las 6 preexistentes son de archivos no tocados)
- [x] V2. `dotnet test` — 17/17 en verde
- [x] V3. `yarn build` — OK (advertencia de presupuesto de bundle preexistente)
- [x] V4. BD local: fn con merma 5 und/10,66 kg → merma % ✓, ajuste = enc−vend−mort−merma ✓, % ajuste ✓, total cliente = kg − 10,66 ✓ (valores exactos verificados)
- [x] V5. BD local: lote sin merma → los 6 campos NULL ✓; kilos en pie y días de engorde con valor ✓; merma solo-kilos → unidades 0, registrada=true ✓ (datos de prueba revertidos)
- [x] V6. Prorrateo: 6 tests unitarios (suma exacta, residuo, 3 decimales) + simulación de la clasificación sobre los 837 grupos de la copia local: 36 líneas corruptas restauradas (+69.457 kg netos), 2 contradictorias a revisión manual, 0 corrupciones nuevas
- [x] V7. Panamá intacto: modal sin sección merma, reporte Panamá sin cambios
- [x] V8. Sin procesos huérfanos (no se levantaron servidores; la BD local ya estaba corriendo y se deja como estaba)

## Datos prod (requiere OK explícito de Moisés)

- [ ] D1. Correr `backend/sql/audit_peso_individual_facturas_multilote.sql` contra RDS prod (solo lectura). En la copia local (08-jun) ya arroja: 36 movimientos corruptos (lote 12 con −31.899 kg, lote 7 con −10.170 kg, …), 2 despachos contradictorios (143·granja 43, 311·granja 42) y 50 grupos con globales mal
- [ ] D2. `POST OrganizarPeso { DryRun: true, ReprocesarTodo: true }` (vía Swagger/endpoint en prod tras el deploy) → presentar KgAntes/KgDespues por despacho a Moisés
- [ ] D3. ⛔ OK explícito de Moisés
- [ ] D4. Aplicar (`DryRun: false, ReprocesarTodo: true`) y re-verificar la liquidación de los lotes afectados (12, 7, 55, 15, 5, 14, …)

## Para desplegar

1. Merge a `main` y push a `main-produccion` → el workflow construye y la migración de la fn se aplica sola al arrancar.
2. Verificación post-deploy obligatoria (sección 🚀 de CLAUDE.md): TaskDef + imagen reales en ECS.
3. Ejecutar D1-D4.
