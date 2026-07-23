# Plan — Migraciones Masivas: línea Seguimiento Reproductora Engorde + alineación Seguimiento Pollo Engorde

**Fecha:** 2026-07-23 · **Módulo:** Migraciones Masivas (backend `Services/Migracion/`, front `features/migraciones-masivas/`)

## Contexto / problema

El módulo de Migraciones Masivas tiene la línea Engorde con 3 tipos (`LotesPolloEngorde`, `SeguimientoPolloEngorde`, `VentaPolloEngorde`). **No existe** la línea de carga masiva del **seguimiento reproductora engorde** (primera semana, tabla `seguimiento_diario_lote_reproductora_aves_engorde`), y la línea `SeguimientoPolloEngorde` quedó desactualizada respecto a las validaciones agregadas al front/back en jul-2026:

- **Reproductora (nuevo en front/back):** confirmación por registro (`confirmado` gatea el cruce a pollo engorde), fecha acotada a **edad [1,7]** vs `fecha_encasetamiento` (`ReproductoraEngordeCalculos.EdadSeguimientoDias`/`EsEdadSeguimientoValida`), máximo **7 días** por lote reproductora, registro confirmado no editable.
- **Engorde (front `engorde-comun/modal-seguimiento-engorde`):** uniformidad 0–100, peso obligatorio en días 1–7 y múltiplos de 7 (bloquea guardar en el modal), Panamá captura Mixtas (se mapean a H con M=0, `mapearPanamaMixtoAHM`) y quintales `qqMixtas/qqHembras/qqMachos` (persisten en columnas `qq_*`, usadas por el Informe Semanal Panamá).

**Requerimiento del usuario:** (1) alinear ambas líneas de carga masiva con la lógica vigente del front; (2) en reproductora, los registros cargados por migración quedan **confirmados automáticamente** ("aceptación de una" → dispara el cruce a pollo engorde vía trigger); (3) plantilla descargable para cada línea.

## Enfoque arquitectónico

Mismo patrón que la línea Engorde existente: **la migración reutiliza los servicios vivos** (no duplica reglas de efecto): `ISeguimientoDiarioLoteReproductoraService.CreateAsync` (descuento inventario si aplica + anclaje mediodía UTC + validaciones de compañía/7 días/edad) y luego `ConfirmarAsync(id)` (idempotente; setea `confirmado/confirmado_at/confirmado_por` → dispara `trg_cruce_reproductora_engorde`). La validación de filas se hace ANTES en C# (dry-run reporta todo; el runner corta si hay errores salvo parcial opt-in), espejo de `MigracionService.SeguimientoEngorde.cs`.

**Selección de contexto sin cambios de UI:** el usuario selecciona el **lote engorde** con el filtro jerárquico existente; el Excel lleva una columna **"Reproductora"** que identifica el lote reproductora dentro del lote (por `ReproductoraId`, `CodigoReproductora` o `NombreLote`, normalizados). Así un solo archivo carga todas las reproductoras del lote.

## Archivos a crear/modificar

### Backend
| Archivo | Cambio |
|---|---|
| `Application/DTOs/Migracion/TipoMigracion.cs` | Enum + catálogo: nuevo `SeguimientoReproductoraEngorde` (fase 4, requiereLote=true, disponible=true). |
| `Application/Calculos/MigracionEsquemas.cs` | Esquema nuevo `SeguimientoReproductoraEngorde` (Reproductora + Fecha requeridas; Mort/Sel/Error H-M, Consumo H/M kg, Tipo Alimento, Peso H/M, Uniformidad H/M, CV H/M, Observaciones). Esquema `SeguimientoPolloEngorde`: + columnas opcionales Panamá `QQ Mixtas`, `QQ H`, `QQ M`. `Para()` + `TiposConEsquema` (9). |
| `Infrastructure/Services/Migracion/MigracionService.cs` | ctor: inyectar `ISeguimientoDiarioLoteReproductoraService` (ya registrado en DI). |
| `Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoReproductora.cs` (**nuevo**) | Elegibles (lotes engorde no cerrados **con** reproductoras), plantilla por lote (hoja Instrucciones lista reproductoras + rango de fechas edad 1–7 + días cargados/confirmados), parse/validación, runner Create+Confirmar. |
| `Infrastructure/Services/Migracion/Funciones/MigracionService.SeguimientoEngorde.cs` | Validaciones alineadas al front: uniformidad H/M ∈ [0,100] (Error), fecha < encaset (Error), fecha futura (Advertencia), peso faltante en día de pesaje obligatorio — edad 1–7 o múltiplo de 7 — (Advertencia), QQ Panamá → `QqMixtas/QqHembras/QqMachos` del request. Instrucciones de plantilla actualizadas (incluye regla Panamá mixtas→columnas H con M=0). |
| `Infrastructure/Services/Migracion/Funciones/MigracionService.Operaciones.cs` | 3 casos de dispatch para el tipo nuevo (elegibles / plantilla / procesar). |

### Reglas de validación de la línea Reproductora (espejo front + servicio)
1. Contexto: lote engorde seleccionado, existente, no cerrado.
2. "Reproductora": obligatoria; debe existir en el lote (match normalizado por ReproductoraId / CodigoReproductora / NombreLote).
3. Fecha: obligatoria, única por reproductora en el archivo; si ya existe en BD → fila **omitida** (idempotencia).
4. Edad = fecha − fecha_encasetamiento ∈ **[1,7]** cuando la reproductora tiene encaset (mismos mensajes que el backend).
5. Máx **7 días** por reproductora (existentes en BD + nuevos del archivo).
6. Mort/Sel/Error H-M: enteros ≥ 0 (vacío = 0). Consumo H/M: decimal ≥ 0 en kg. Peso/CV ≥ 0 opcionales. Uniformidad ∈ [0,100] opcional.
7. Importación real: `CreateAsync` + `ConfirmarAsync` por fila (orden fecha ascendente por reproductora). Fallo de confirmación se reporta como error de fila.

### Frontend
| Archivo | Cambio |
|---|---|
| `features/migraciones-masivas/models/migracion.model.ts` | Union `TipoMigracionCodigo` + `'SeguimientoReproductoraEngorde'`. |
| `features/migraciones-masivas/funciones/agrupar-tipo-migracion.funcion.ts` | Agregarlo a `TIPOS_POLLO_ENGORDE` (gating permiso `carga_masiva_pollo_engorde`). |

(El resto del front es data-driven vía `/api/Migracion/tipos` — sin más cambios.)

### BD
Sin cambios de schema: se reutilizan tablas/trigger existentes (`seguimiento_diario_lote_reproductora_aves_engorde`, `trg_cruce_reproductora_engorde`). Sin migraciones EF.

### Tests
- `backend/tests/ZooSanMarino.Application.Tests/MigracionEsquemasTests.cs`: los tests por `TiposConEsquema` cubren el esquema nuevo automáticamente; + test puntual: esquema reproductora exige "Reproductora" y "Fecha"; esquema engorde acepta archivos sin columnas QQ (compatibilidad hacia atrás).
- Cálculo puro de edad ya testeado en `ReproductoraEngordeCalculos` (sin cambios de aritmética).

## Casos de prueba (validación manual/build)
1. `dotnet build` 0 errores; `dotnet test` verde.
2. `yarn build` front 0 errores.
3. Plantillas: generar ejemplo .xlsx de ambas líneas para el usuario (mismos encabezados que el esquema).

## Ampliación (pedida durante el desarrollo)

1. **Columna "Unidad Consumo" (kg/qq) en ambas líneas de seguimiento:** opcional, default `kg`; con `qq` la carga convierte el Consumo H/M a kg (factor oficial 1 qq = 45.36 kg, redondeo a 3 decimales — mismo `toKg` del front). Texto distinto de kg/qq = error de fila. Cálculo puro en `MigracionCalculos.NormalizarUnidadConsumo` / `ConsumoAKilos` (+ tests).
2. **Fix consumo machos reproductora (backend, afecta también al modal):** en `CreateSeguimientoDiarioLoteReproductoraRequest.ToDto()`, `CalcularConsumoTotalAlimentos` devuelve `0` para lista vacía y ese `0` con `HasValue=true` bloqueaba el fallback de `ConsumoMachos` → el consumo de machos enviado como escalar (sin ítems de inventario) se perdía y la tabla solo mostraba el de hembras. Fix: total de ítems machos vacío ⇒ `null` + fallback con la condición del request de levante (`!HasValue || <= 0`). Tests en `CreateSeguimientoDiarioLoteReproductoraRequestTests`.

## Ampliación 2 (pedida durante el desarrollo) — solo Seguimiento Pollo Engorde

1. **Ubicación por NOMBRES con comparación case/acento-insensible:** columnas opcionales `Granja`, `Núcleo`, `Galpón`, `Lote`. Con `Lote`, la fila resuelve su lote engorde ABIERTO por nombre normalizado (`NormalizarClave`), acotado por granja/núcleo/galpón (nombre O código) si vienen; ambigüedad (nombres repetidos) → error pidiendo desambiguar; sin `Lote` la fila usa el lote seleccionado en pantalla (compatible hacia atrás). Permite cargar VARIOS lotes en un archivo; idempotencia pasa a ser por (lote, fecha).
2. **Select de alimentos + hasta 2 alimentos por sexo:** columnas `Alimento 1/2 H` + `Consumo Alimento 1/2 H` (ídem M). El alimento se resuelve contra los ítems ACTIVOS de concepto alimento del inventario unificado de la empresa (`ItemInventario`, por nombre o código, sin mayúsculas/acentos) y viaja como `ItemsHembras/ItemsMachos` con `itemInventarioEcuadorId` (camino 2 en todos los países) → al importar SÍ descuenta inventario (mismo efecto que el modal). La plantilla trae hoja **Referencias** (alimentos de la empresa + tabla de lotes abiertos con su ubicación) y **dropdowns** en `Tipo Alimento`, los 4 `Alimento N`, y `Lote`. Consumo directo `Consumo H/M (kg)` se mantiene para filas sin ítems (sin descuento); si vienen ambos, se ignora el directo con Advertencia. `Unidad Consumo` (kg/qq) aplica al directo Y a los alimentos.
3. Reproductora queda con UN solo alimento (texto), confirmado por el usuario.

## Verificación realizada

- `dotnet build` 0 err / 0 warn · `dotnet test` 642 verdes (641 Application + 1 Domain) · `yarn build` 0 err (solo warning de bundle budget preexistente).
- **Smoke E2E local** (backend :5002 Development, JWT + X-Secret-Up minteados, company 5, lote 110): `/tipos` expone el tipo nuevo; `/elegibles` devuelve 28 lotes; `/plantilla` descarga ambas plantillas; `/validar` dry-run reporta los 5 errores esperados (edad 0, reproductora inexistente, fecha repetida, unidad inválida, uniformidad >100) y valida el archivo limpio; `/importar` real insertó 3 filas **confirmadas** (confirmado=t, confirmado_por del token) con QQ convertido (1 qq→45.360, 0.5 qq→22.680) y consumo machos persistido; re-import → 3 omitidas (idempotente); sin cruce parcial (0 filas engorde). Datos de prueba eliminados y backend detenido.

## Decisiones/trade-offs
- **Peso obligatorio (engorde) como Advertencia, no Error:** el front lo bloquea al capturar el día a día, pero una carga histórica sin pesos no debe quedar brickeada; la advertencia informa sin frenar. Uniformidad >100 sí es Error (dato inválido).
- **QQ solo en engorde:** el modal reproductora convierte qq→kg en el cliente y NO envía `qq*`; la plantilla reproductora queda en kg (como su modal). El engorde sí persiste `qq_*` (informe Panamá) → columnas opcionales.
- **Confirmación automática SOLO en la línea de migración** (no cambia el servicio): el flujo manual del front sigue exigiendo el botón Confirmar.
