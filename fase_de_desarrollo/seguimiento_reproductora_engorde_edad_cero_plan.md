# Seguimiento reproductora engorde — el día del encasetamiento cuenta como DÍA 1

**Fecha:** 2026-07-24 · **Reporte del usuario:** en la carga masiva de seguimiento reproductora
(pollo engorde), una reproductora con encasetamiento 16/07/2026 rechazaba la fila con fecha
16/07/2026 ("la fecha no puede ser anterior al día siguiente del encasetamiento"). Regla de
negocio correcta (aclarada por el usuario a mitad de la tarea): **el día del encasetamiento ES
el DÍA 1 de la semana de recogida** — la semana va del día 1 (encaset) al día 7 (encaset+6).
Lo inválido es una fecha ANTERIOR al encasetamiento (p. ej. el 15/07).

## Contexto / causa raíz

- El fix del 22-jul-2026 acotó la fecha del seguimiento a **edad ∈ [1, 7]** (edad = días de
  calendario desde el encaset) porque `fn_cruce_reproductora_a_engorde` solo consolidaba edades
  1..7: un registro en edad 0 nunca cruzaba a pollo engorde, así que se rechazaba.
- Con la numeración de negocio (día 1 = encaset ⇒ edad 0), el cruce **debe consolidar también la
  edad 0**, o crearíamos registros confirmados que jamás sincronizan (el bug original).

## Enfoque implementado

- **Validación de fecha (front + back + carga masiva): edad ∈ [0, 7].** La edad 7 se **tolera**
  para que los lotes que arrancaron al día siguiente del encaset (numeración previa, días 1..7 =
  edades 1..7) puedan completar su semana; los nuevos arrancan el mismo día (días 1..7 = edades
  0..6). Tope de 7 registros y cierre a 7 confirmados: sin cambios.
- **`fn_cruce_reproductora_a_engorde` consolida edades 0..7** y las aves vivas de la edad d
  descuentan retiros de edades `[0, d)`. La fila de cruce edad 0 se fecha en `fecha_encaset + 0`.
  El texto de observaciones ya no dice "(día d)" (la numeración técnica difiere de la de negocio).
- **Sin cambios de modelo EF** (solo función SQL + textos + validaciones).

## Archivos modificados

### Backend
1. `Application/Calculos/ReproductoraEngordeCalculos.cs` — `EsEdadSeguimientoValida`: `edad >= 0`
   + doc con la numeración día 1 = encaset y la tolerancia de edad 7.
2. `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` — Create y Update:
   `edad < 0` + mensajes ("no puede ser anterior a la fecha de encasetamiento (el día del
   encasetamiento es el día 1)").
3. `Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoReproductora.cs` —
   misma condición/mensaje; plantilla: "el día del encasetamiento es el DÍA 1 y la semana va del
   día 1 al 7"; rango por reproductora "día 1 = <encaset> (encasetamiento), día 7 = <encaset+6>".
4. `backend/sql/fn_cruce_reproductora_a_engorde.sql` — loop `FOR d IN 0..7`; retiros `[0, d)`;
   observaciones sin número de día; comentarios con ambas numeraciones.
5. Migración `20260724100000_CruceReproductoraEngordeEdadCero` (a mano + Designer clonado del
   último, ModelSnapshot intacto): Up = fn nueva + recálculo dirigido (`PERFORM fn_cruce…`) de
   lotes con registros confirmados en edad 0 (backfill histórico); Down = fn previa 1..7 +
   DELETE de cruces edad 0 + mismo recálculo. Idempotente; la aplica el deploy (RunMigrations).
6. Tests `ReproductoraEngordeCalculosTests.cs` — edad 0/1/6/7 válidas, 8 y −1 inválidas; caso
   del escenario reportado (encaset 16/07: registro 16/07 válido, 15/07 inválido).

### Frontend
7. `modal-seguimiento-reproductora.component.ts/.html` — `minFechaYmd` = fecha de encasetamiento;
   mensajes/hints con "día 1 = día del encasetamiento" (máx sigue en encaset+7 por tolerancia).
8. `seguimiento-diario-lote-reproductora-list.component.ts/.html` — fecha sugerida del PRIMER
   registro = día del encasetamiento (antes +1); columna "Edad" pasa a **"Día"** mostrando
   `edad + 1` (día 1 = encaset; aplica también a registros históricos, que ahora se leen con la
   numeración de negocio).
9. `aves-engorde/funciones/construir-bloques-reproductora.funcion.ts` — acepta edades 0..7; la
   ventana de 7 filas (días 1..7) arranca en edad 0 si el lote registró el día del encaset y en
   edad 1 si no (lotes históricos: salida idéntica). Conversión del día 1 sigue dividiendo por
   el peso de llegada.

### Verificado sin cambios
- `fn_seguimiento_diario_engorde` soporta día 0 (`edad_dia = GREATEST(0, …)`, semana 1, eventos
  `>= fecha_encaset` inclusive). Front migraciones-masivas sin textos de rango. Resumen VPI
  (peso 7 días = 7ª fila) intacto.

## Validación ejecutada
- `dotnet build` Infrastructure: 0 errores, 0 warnings · `dotnet test`: **643 pasando**.
- `yarn build` front: OK (solo warning de bundle budget preexistente).
- Smoke SQL local (BEGIN…ROLLBACK, lote 109 / repro 5, encaset 2026-01-14): insertado registro
  confirmado el 14/01 → aparece cruce edad 0 fecha 14/01 (mort 3/2, consumos y alimento
  correctos) y las edades 1..7 se regeneran idénticas. ROLLBACK verificado.
