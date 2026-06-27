# Tracker — Verificador / Auditoría de Liquidación Pollo Engorde (Ecuador)

Plan: [fase_de_desarrollo/auditoria_liquidacion_engorde_plan.md](fase_de_desarrollo/auditoria_liquidacion_engorde_plan.md)
Diagnóstico base: [backend/documentacion/ANALISIS_DESCUADRE_LIQUIDACION_POLLO_ENGORDE_ECUADOR.md](backend/documentacion/ANALISIS_DESCUADRE_LIQUIDACION_POLLO_ENGORDE_ECUADOR.md)

> Contexto previo (ya hecho, en working tree sin commitear): fix C2 en `liquidacionTotales()` + fix de dato mov 102 en local. Pendiente deploy/tiquete real.

## 1. BD — `fn_auditoria_liquidacion_engorde` (núcleo) ✅
- [x] Crear `backend/sql/fn_auditoria_liquidacion_engorde.sql` (plpgsql, RETURNS jsonb)
- [x] Reconciliación sistema vs Excel por indicador (clase dato/definicion, tolerancia por decimales)
- [x] Detectores: MOV_SIN_PESO, ANULADO_ACTIVO, MERMA_NO_REGISTRADA, DESPACHO_MULTILOTE, AJUSTE_ALTO
- [x] Simulación de corrección (gap → corregido vs Excel + nota peso implícito)
- [x] Aplicar a local y validar con el Excel 2601 → encuentra mov 102, gap 5.923, simula cuadre exacto
- [x] Migración EF idempotente (CREATE OR REPLACE FUNCTION) — `AddFnAuditoriaLiquidacionEngorde`

## 2. Back (.NET) — delgado ✅
- [x] DTO `AuditoriaLiquidacionRequest`
- [x] `AuditoriaLiquidacionExcelParser` (ClosedXML, API) → dict claves canónicas (normaliza acentos)
- [x] Servicio `AuditarLiquidacionAsync`: serializa + llama fn + retorna jsonb passthrough
- [x] Endpoint `POST IndicadorEcuador/auditoria-liquidacion` (multipart)
- [x] `dotnet build` sin errores (0 errores)
- [x] Migración EF `AddFnAuditoriaLiquidacionEngorde` (idempotente, compila)

## 3. Front (Angular) — pinta ✅
- [x] Servicio: `auditarLiquidacion(scope, file)` (multipart) → resultado
- [x] `models/auditoria-liquidacion.model.ts` con los tipos del resultado
- [x] Modal `auditoria-liquidacion-modal` (upload + 3 secciones: reconciliación, hallazgos, simulación)
- [x] Botón "🔎 Verificar liquidación" en `indicador-ecuador-list` (toolbar de la liquidación)
- [x] `yarn build` sin errores (exit 0)

## 4. Cierre ✅
- [x] Parser probado de verdad contra `EJEMPLO_liquidacion_2601_correcto.xlsx` → 21 claves OK (ignora el 5923 extra)
- [x] Excel de ejemplo en `backend/documentacion/EJEMPLO_liquidacion_2601_correcto.xlsx`
- [x] Scratch limpiado

## 5. Robustez — Excel incompleto/erróneo ✅ (reportado por el usuario con Book1.xlsx)
- [x] Diagnóstico: `Book1.xlsx` es plantilla con fórmulas en error (#DIV/0!) y ceros → parser leyó esos valores rotos
- [x] Función: detector `EXCEL_INCOMPLETO` (crítico, se antepone) cuando enc/sac/producción del Excel ≤ 0
- [x] `resumen.excelValido` + simulación con `NULLIF(...,0)` (sin cuadre falso cuando Excel=0)
- [x] Front: banner rojo "Excel incompleto/incorrecto" cuando `excelValido=false` + tipo en el modelo
- [x] Migración regenerada con la fn actualizada · Infrastructure compila
- [x] Validado en local: archivo roto → EXCEL_INCOMPLETO; archivo bueno → cuadre intacto

## 6. Plantilla descargable ✅ (pedido del usuario)
- [x] `PLANTILLA_INDICADORES` (etiquetas canónicas) en el modelo — deben coincidir con el parser
- [x] Botón "⬇ Descargar plantilla (.xlsx)" en el modal (genera con SheetJS en el front, back queda delgado)
- [x] Plantilla en blanco enviada al usuario + en `backend/documentacion/plantilla_verificacion_liquidacion.xlsx`
- [x] `yarn build` sin errores (exit 0)

## 7. Aplicar corrección (gateado por permiso) — pedido del usuario ✅
- [x] `backend/sql/fn_aplicar_correccion_despachos_sin_peso.sql` (escribe peso_neto, auditado, transaccional)
- [x] Permiso `liquidacion.aplicar_correccion` (seed) + función → migración `20260627040000_AddAplicarCorreccionLiquidacion` (hecha a mano: SQL-only, sin cambio de modelo)
- [x] Back: interfaz + servicio `AplicarCorreccionSinPesoAsync` + endpoint `auditoria-liquidacion/aplicar` (chequea permiso → Forbid)
- [x] Front: botón gateado `*appHasPermission` + kg editable (default gap) + confirmación + re-verificar
- [x] Compila: Infrastructure OK; API compila (solo fallaba el COPY por backend corriendo PID 28908)
- [x] Validado fn en local: aplicar 5923→mov102, producción 251052, 2ª llamada rechazada, revertido
- [x] Permiso seedeado en local (id 57) para pruebas
- [x] `yarn build` exit 0

## Pendiente (decisión usuario)
- [ ] Subir el Excel REAL (calculado, con valores) o la PLANTILLA llena — Book1.xlsx era plantilla rota
- [ ] Reiniciar `yarn start` + hard refresh para tomar los cambios del modal
- [ ] Asignar el permiso `liquidacion.aplicar_correccion` al rol (pantalla Roles) para ver el botón
- [ ] Desplegar a prod (front + migración aplica la fn sola)
- [ ] mov 102 en local está en NULL (= prod) para que el verificador demuestre el hallazgo
