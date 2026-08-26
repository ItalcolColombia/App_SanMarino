# Plan — Reportes ciegos a `metadata.huevoItems` (Santa Reyes)

## Contexto

Con `companies.clasificacion_huevo_por_items = true` (Santa Reyes, company_id 6), el desglose real
de huevo vive en `seguimiento_diario_produccion.metadata->'huevoItems'`. La grilla diaria de
producción y los indicadores semanales ya lo leen de forma dinámica (Primera/Pnc), verificado en
vivo (ver `tracker_estado.md` bloque X18.6).

Los **reportes** (contable, técnico de producción, diario de costos postura, técnico semanal) NO
leen `huevoItems`: siguen consumiendo `huevo_tot` (correcto, siempre coincide con la suma del
desglose) y las 11 columnas fijas legacy (`huevo_inc`, `huevo_limpio`…`huevo_otro`), que para estas
empresas quedan **siempre en 0**. Una auditoría de código (Agent Explore, 25-ago-2026) encontró 4
puntos concretos donde eso produce una pantalla rota o engañosa, no solo columnas vacías:

1. **Reporte Diario Costos Postura → pestaña Huevos**: dispara el banner *"N registro(s) donde
   fértil + comercial + inservible no suma el huevo total. Es un defecto del dato cargado"* para
   prácticamente el 100% de las filas de Santa Reyes — **confirmado en vivo** con el lote
   `SMOKE-SR-001` (4/4 registros). No es un defecto del dato: es que el reporte nunca puede cuadrar
   una partición que no calcula.
2. **Reporte Técnico Semanal → hoja "CLAS Huevo"**: única pantalla que expone el desglose de 11
   (10 agrupadas) columnas legacy completo, sin gatear ningún flag.
3. **Reporte Contable → Movimientos Huevos**: ya gatea 2 de 3 columnas rotas (`HVO COMERCIAL`,
   `HUEVO DESECHO`) tras el flag `clasificacionHuevoPorItems`; deja `HVTO FERTIL` sin gatear.
4. **Reporte Técnico Producción → Diario y Cuadro**: columnas derivadas de `huevo_inc`
   (Incubable/Cargado en Diario; HUEVOS INCUB/%DESCARTE/%ACUM INCUB/LAA/H.CARGA/H.CAR ACU en
   Cuadro) sin gatear. La pestaña "Clasificación" de este mismo reporte YA estaba bien oculta —
   patrón de referencia para el resto.

## Enfoque

**Frontend-only, mismo patrón ya usado con `ocultaMachosEnPostura` (barrido de machos, X18.4):**
ocultar/gatear detrás de `clasificacionHuevoPorItems` (leído de `ActiveCompanyConfigService`) lo
que sea matemáticamente ciego para estas empresas. El backend NO cambia: los DTOs siguen trayendo
0 en esas columnas, el frontend simplemente no las pinta. `huevo_tot`/`huevosTotales` (siempre
correcto) y todo lo que sale de `traslado_huevos` (ventas/traslados a planta) se mantienen en
los 4 reportes.

No se intenta reemplazar las columnas ocultas por un desglose Primera/Pnc (lo que sí hace la
grilla diaria): eso exigiría que cada reporte agregue `metadata.huevoItems`, que es un backend
nuevo por reporte — fuera de alcance de este pase, que es sobre no mostrar datos rotos/engañosos.

### 1. Reporte Diario Costos Postura (`reporte-diario-costos-postura`)
- Ocultar columnas "Huevo fértil"/"Huevo comercial"/"Huevo inservible" (header, filas, footer).
- `huevosDescuadrados` = 0 cuando el flag está ON (la partición nunca cuadra por diseño, no es el
  defecto de dato que el aviso describe) → el banner deja de dispararse solo.
- Excel (`construirHojasCostosPostura`/`hojaHuevos`): mismo gateo, con `clasificacionPorItems`
  como parámetro nuevo (mismo patrón que `ocultaMachos`), spread `[]`/`[valores]` en la MISMA
  posición en cabecera y datos para no desalinear.

### 2. Reporte Técnico Semanal (`reporte-tecnico-semanal`)
- Ocultar la pestaña "Clasificación" completa (botón + contenido) cuando el flag está ON — mismo
  tratamiento que ya tiene la pestaña "Clasificación" de Reporte Técnico Producción.
- Excel (`construirHojasProduccion`): no emitir las hojas "CLAS Gral"/"CLAS <lote>" cuando el flag
  está ON.

### 3. Reporte Contable (`reporte-contable` / `tabla-movimientos-huevos`)
- Extender el `@if (!clasificacionHuevoPorItems)` existente para que también cubra `HVTO FERTIL`
  (header, filas, footer) y ajustar el `colspan` del grupo "Producción" de 2→1.
- Excel: generado en backend (`exportarExcelCompleto`), fuera de alcance de este pase — igual que
  ya quedó documentado para el barrido de machos (W1.d).

### 4. Reporte Técnico Producción — Diario y Cuadro
- **Diario**: ocultar "INCUBABLE"/"CARGADO" (ambas derivan de `huevo_inc`).
- **Cuadro**: dentro de "HUEVO INCUBABLE" (colspan 5) ocultar HUEVOS INCUB/%DESCARTE/%ACUM
  INCUB/LAA (las 4 derivan de `huevo_inc`, verificado contra
  `ReporteTecnicoProduccionService.Cuadro.cs:179-192`); mantener STD ROSS (valor de guía, no
  depende de `huevo_inc`) → colspan pasa a 1. Dentro de "HUEVOS CARGADOS Y POLLITOS" (colspan 7)
  ocultar H.CARGA/H.CAR ACU (mismo origen); mantener V.HUEVO (sale de `traslado_huevos`) y los 4 de
  pollitos/eclosión (no dependen de huevo) → colspan pasa a 5.
- Excel: generado en backend (`exportarExcelCompleto`), fuera de alcance — mismo criterio que #3.

## Reglas de negocio / invariantes

- Con el flag OFF, cero cambios visibles en ningún reporte (todas las condiciones son
  `@if (!clasificacionHuevoPorItems)` sobre columnas que antes se pintaban siempre).
- `huevo_tot`/`huevosTotales`/`POSTURA` (el número real de cuadre) nunca se toca ni se oculta en
  ningún reporte.
- Datos que salen de `traslado_huevos` (ventas, traslados a planta, V.HUEVO) no dependen del flag
  y se mantienen siempre.
- Ningún backend cambia: mismos DTOs, mismas queries, mismo `huevo_inc`/columnas fijas devueltas.

## Casos de prueba

- `construirHojasCostosPostura`/`hojaHuevos`: header y filas con la misma cantidad de columnas en
  ambos estados del flag; con flag ON no aparecen fértil/comercial/inservible.
- `construirHojasProduccion` (semanal): con flag ON no se agregan hojas "CLAS *".
- Smoke en navegador con datos reales de Santa Reyes (`SMOKE-SR-001`): banner de Costos Postura no
  aparece, columnas ocultas en los 4 reportes, `huevo_tot`/totales de venta y traslado intactos.

## Validación

`yarn build` (0 errores) + `yarn test` + smoke visual en navegador con el flag ON (Santa Reyes) y
verificación de que con el flag OFF (Sanmarino/Demo/Panamá) ningún reporte cambia.
