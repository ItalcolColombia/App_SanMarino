# Tracker — Migraciones Masivas: Seguimiento Reproductora Engorde (nueva línea) + alineación Seguimiento Pollo Engorde

**Plan:** [fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md](fase_de_desarrollo/migracion_masiva_seguimiento_engorde_reproductora_plan.md)

## Backend — línea nueva SeguimientoReproductoraEngorde
- [x] `TipoMigracion.cs`: enum `SeguimientoReproductoraEngorde` + entrada del catálogo (fase 4, requiereLote, disponible)
- [x] `MigracionEsquemas.cs`: esquema `SeguimientoReproductoraEngorde` (Reproductora + Fecha requeridas) + `Para()` + `TiposConEsquema` (9)
- [x] `MigracionService.cs` (ancla): inyectar `ISeguimientoDiarioLoteReproductoraService`
- [x] Nuevo partial `Funciones/MigracionService.SeguimientoReproductora.cs`:
  - [x] Elegibles: lotes engorde no cerrados con ≥1 lote reproductora
  - [x] Plantilla por lote (Instrucciones lista reproductoras + rango fechas edad 1–7 + cargados/confirmados)
  - [x] Parse/validación: reproductora existente (id/código/nombre, ambigüedad detectada), fecha única/idempotente, edad [1,7], máx 7 días, enteros ≥0, consumo ≥0, uniformidad 0–100, CV/peso ≥0
  - [x] Runner: `CreateAsync` + `ConfirmarAsync` por fila (**confirmación automática** → dispara cruce), parcial opt-in, auditoría central
- [x] `MigracionService.Operaciones.cs`: dispatch elegibles/plantilla/procesar del tipo nuevo

## Backend — alineación SeguimientoPolloEngorde
- [x] Uniformidad H/M ∈ [0,100] → Error · Peso H/M ≥ 0
- [x] Fecha anterior al encaset del lote → Error; fecha futura → Advertencia
- [x] Peso faltante en día de pesaje obligatorio (edad 1–7 o múltiplo de 7) → Advertencia
- [x] Columnas opcionales Panamá `QQ Mixtas` / `QQ H` / `QQ M` → `QqMixtas/QqHembras/QqMachos`
- [x] Instrucciones de plantilla actualizadas (incluye Panamá mixtas→columnas H con M=0)

## Ampliación pedida en curso
- [x] Columna `Unidad Consumo` (kg default / qq) en ambos esquemas + conversión ×45.36 (3 dec) en ambos parsers (`MigracionCalculos.NormalizarUnidadConsumo`/`ConsumoAKilos`)
- [x] Fix consumo machos reproductora: `ToDto()` ya no descarta `ConsumoMachos` escalar (total ítems vacío ⇒ null + fallback estilo levante)

## Ampliación 2 — solo Seguimiento Pollo Engorde
- [x] Columnas `Granja`/`Núcleo`/`Galpón`/`Lote` opcionales: lote por NOMBRE case/acento-insensible (multi-lote por archivo), ambigüedad detectada, fallback al lote de pantalla; idempotencia por (lote, fecha) en una consulta
- [x] Alimentos del inventario: `Alimento 1/2 H-M` + `Consumo Alimento 1/2 H-M` resueltos contra `ItemInventario` (concepto alimento, activo, por nombre/código) → `ItemsHembras/ItemsMachos` con `itemInventarioEcuadorId` (descuenta inventario al importar); consumo directo ignorado con Advertencia si vienen ítems
- [x] Plantilla: hoja Referencias (alimentos empresa + lotes abiertos con ubicación) + dropdowns en Tipo Alimento / Alimento 1-2 H-M / Lote; instrucciones completas
- [x] Smoke E2E 2 (dry-run): granja+lote en minúsculas resuelve, 2 alimentos H (nombre y código) + 1 M en qq validan, "LOTE 6" solo → ambiguo (2 coincidencias reales), lote inexistente / granja sin lote / alimento inexistente / alimento sin consumo / fecha pre-encaset → 6 errores esperados; archivo limpio → Validado con 1 omitida

## Ajuste 4 — plantilla reproductora simplificada (flujo con filtro)
- [x] La plantilla descargada de reproductora ya NO emite `Granja/Núcleo/Galpón/Lote` (el lote sale del filtro de pantalla); helper `PonerEncabezadosSin` conserva dropdowns con índices re-calculados
- [x] El backend sigue aceptando esas columnas (opcionales) para el flujo avanzado multi-lote — compat verificada: formato simple y formato ancho validan con ctx repro
- [x] Instrucciones: nota "Avanzado" para agregar las columnas si se cargan varios lotes

## Ampliación 3 — selector de lote elegible + reproductora en pantalla
- [x] **Fix "0 lotes"**: el paso 2 ya no usa el selector de lotes genérico del filtro jerárquico (listaba lotes base/postura); el lote se elige de `/api/Migracion/elegibles` del tipo seleccionado (engorde = `lote_ave_engorde`), refrescado por la cascada granja/núcleo/galpón
- [x] Selector "Reproductora (opcional)" (solo tipo reproductora): endpoint nuevo `GET /api/Migracion/reproductoras?loteId=` (nombre, id, encaset, cargados/confirmados); default "Todas — el Excel indica la reproductora"
- [x] `MigracionContextoDto.ReproductoraId` (+ form/query en controller): con reproductora elegida las filas pueden dejar la columna vacía; fila que nombra OTRA reproductora → error; sin selección la columna es obligatoria por fila
- [x] Reproductora también acepta ubicación por nombres (`Granja`/`Núcleo`/`Galpón`/`Lote` opcionales, case/acento-insensible, multi-lote por archivo) igual que engorde; "Reproductora" pasa a opcional a nivel encabezado
- [x] Plantilla reproductora: hoja Referencias (reproductoras del lote + lotes abiertos ubicados) + dropdowns en Reproductora y Lote; instrucciones según haya o no reproductora seleccionada
- [x] Smoke E2E 3 (:5299, sin tocar el backend del usuario en :5002): elegibles por galpón G0455 devuelve LOTE 6 (caso del screenshot que daba 0), `/reproductoras` lista 3 con avance 0/7, dry-runs: ctx repro + columna vacía → Validado; sin ctx → error por fila; mismatch → error con ambos nombres

## Frontend
- [x] `migracion.model.ts`: union `TipoMigracionCodigo` + `'SeguimientoReproductoraEngorde'`; `MigracionContexto.reproductoraId`; interfaz `ReproductoraElegible`
- [x] `agrupar-tipo-migracion.funcion.ts`: agregado a `TIPOS_POLLO_ENGORDE` (permiso `carga_masiva_pollo_engorde`)
- [x] `selector-tipo-migracion`: icono 🐣 del tipo nuevo (Record tipado exigía la clave)
- [x] `migracion.service.ts`: `getReproductoras(loteId)` + `reproductoraId` en params/form
- [x] Página: filtro jerárquico con `showLoteSelection=false` + select de lote (elegibles) + select de reproductora opcional; signals/OnPush; estilos `.selectores`

## Tests & validación
- [x] `MigracionEsquemasTests`: requeridas del esquema reproductora + compat engorde sin columnas QQ
- [x] `MigracionCalculosTests`: `NormalizarUnidadConsumo` + `ConsumoAKilos` (factor 45.36, redondeo 3 dec)
- [x] `CreateSeguimientoDiarioLoteReproductoraRequestTests`: fix machos (escalar, ítems, null, gramos, hembras)
- [x] `dotnet build` 0 err/0 warn + `dotnet test` 642 verdes
- [x] `yarn build` 0 errores (solo warning bundle budget preexistente)
- [x] Smoke E2E local (:5002): tipos/elegibles/plantillas/validar/importar/idempotencia — importación real quedó **confirmada** con QQ convertido y consumo machos persistido; datos de prueba limpiados, backend detenido
- [x] Plantillas .xlsx reales (generadas por el backend) entregadas al usuario
