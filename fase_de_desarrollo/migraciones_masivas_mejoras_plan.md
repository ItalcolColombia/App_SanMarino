# Plan — Mejoras del módulo Migraciones Masivas (Postura + Engorde)

**Fecha:** 2026-07-13 · **Modelo de gestión:** Fable 5 (plan/criterios/validación) + agentes Sonnet (implementación) + Haiku (investigación).
**Módulo:** `backend/src/ZooSanMarino.Infrastructure/Services/Migracion/` + `frontend/src/app/features/migraciones-masivas/`.
**Planes previos:** `migraciones_masivas_plan.md` (fundación+F1+F2), `migraciones_masivas_engorde_plan.md` (línea engorde), `migraciones_masivas_fase3_spec.md` (ventas postura, NO se toca aquí).

## 1. Objetivo

Mejorar el módulo existente en tres ejes, **sin cambiar el comportamiento de negocio actual** (mismas reglas, mismos mensajes de negocio, mismas plantillas visibles, misma aritmética):

1. **Centralizar la lógica**: hoy los encabezados de columnas están duplicados entre generación de plantillas y parseo/validación (7 archivos), y los helpers de coerción están repetidos (`EnteroNoNeg`, `DecimalNoNeg`, `DobleOpc` en `Historicos.cs` y `SeguimientoEngorde.cs`). → **Esquema único por tipo** (fuente única para plantilla + validación) + helpers centralizados.
2. **Robustecer la validación de archivos**: hoy un archivo corrupto/no-xlsx da 500 genérico; una columna borrada/renombrada produce N errores confusos ("La granja es obligatoria" × 500) en vez de 1 claro; no hay límite de filas ni de errores; encabezados duplicados se ignoran en silencio.
3. **Mejorar la ejecución de importaciones**: contadores reales (procesadas/omitidas/duración), auditoría también de validaciones, historial paginado con UI, descarga/exportación de errores, confirmación antes de importar, e importación parcial **opt-in**.

## 2. Diagnóstico (evidencia)

- Encabezados hardcodeados 2 veces: `Plantillas.cs` (`PonerEncabezados(ws, "Granja", "Código Núcleo", ...)`) vs parsers (`Celda(fila, "codigo nucleo", "codigo")`). Divergencia silenciosa posible.
- `LeerDatos` (Comun.cs:35-68): sin validación de paquete (EPPlus lanza → 500), fallback silencioso a primera hoja, encabezados duplicados ignorados en silencio, sin tope de filas (10MB ≈ cientos de miles de filas en RAM).
- `MigracionErrorDto` sin severidad; sin cap de errores (10k errores → respuesta y `ErroresJson` gigantes).
- `MigracionController`: solo valida `Length > 0`; sin chequeo de extensión/firma.
- Contadores: no distingue filas **omitidas** por idempotencia (SeguimientoPolloEngorde salta fechas existentes con `continue` sin contarlas).
- **Bug real**: `LotesPolloEngorde` no valida duplicado contra BD (solo dentro del archivo) → reimportar el mismo archivo revienta en la inserción (EstructuraEngorde.cs:~109).
- Historial: `Take(200)` sin paginación; **sin UI en el front** (`getHistorial()` nunca se usa); errores guardados en `ErroresJson` irrecuperables desde la UI.
- Front: importar sin confirmación (`ConfirmDialogService` no usado), sin validación cliente de archivo (solo `accept=".xlsx,.xls"`), se puede importar sin validar antes, reporte sin límite de render ni export a Excel, mensajes HTTP genéricos, sin carpeta `funciones/`.
- Validar→Importar reparsea el archivo (aceptado: es el diseño seguro; importar SIEMPRE revalida).

## 3. Enfoque arquitectónico

### 3.1 Esquema único por tipo (Application, puro)
- **`Application/DTOs/Migracion/MigracionEsquema.cs`** (nuevo):
  - `record ColumnaEsquema(string Titulo, bool Requerida, string[]? Alias = null, string[]? Opciones = null)` — `Titulo` es el encabezado canónico de la plantilla; `Requerida` = la **columna** debe existir en el archivo (campos obligatorios por fila); `Alias` = claves normalizadas alternativas aceptadas al parsear (las que hoy acepta `Celda(...)`); `Opciones` = valores para dropdown inline (p.ej. Estado A/I).
  - `record EsquemaMigracion(string Hoja, IReadOnlyList<ColumnaEsquema> Columnas, int MaxFilas = 5000)`.
- **`Application/Calculos/MigracionEsquemas.cs`** (nuevo, `static class`): los 8 esquemas (Granjas, Nucleos, Galpones, SeguimientoLevante, SeguimientoProduccion, LotesPolloEngorde, SeguimientoPolloEngorde, VentaPolloEngorde) + `Para(TipoMigracion)`. **Los títulos, el orden y los alias se transcriben EXACTOS del código actual** (títulos de `PonerEncabezados`/plantillas; alias de las llamadas `Celda(...)`) → la plantilla generada queda idéntica y el parseo acepta lo mismo que hoy.
- **`Application/Calculos/MigracionEsquemaCalculos.cs`** (nuevo, puro, testeable):
  - `ValidarEncabezados(esquema, headersNormalizados)` → `(FaltantesRequeridos[], Desconocidos[])` (faltante = ni título ni alias presentes; desconocido = header que no matchea ninguna columna).
  - `LimitarErrores(errores, max)` → `(lista capada, total real)`.

### 3.2 Lectura robusta de archivo (Infrastructure, `Comun.cs`)
- `ValidarArchivo(IFormFile)`: extensión `.xlsx`, tamaño > 0, firma ZIP (`PK`) → mensaje claro (400 vía `InvalidOperationException`, ya mapeada en el controller).
- `LeerDatosConEsquema(stream, esquema, errores)`:
  - `new ExcelPackage` en try/catch → error fila 0 "El archivo no es un .xlsx válido o está dañado."
  - Hoja: `Datos` normalizada; si no está y el libro tiene UNA sola hoja, usarla; si hay varias → error claro (se elimina el fallback silencioso).
  - Encabezados duplicados → advertencia (hoy se ignoran en silencio).
  - `ValidarEncabezados`: cada columna requerida faltante → **1 error** (fila 0) "La columna '{Titulo}' es obligatoria y no está en el archivo."; desconocida → advertencia "La columna '{h}' no corresponde a la plantilla y será ignorada." Si faltan requeridas → NO se procesan filas (se evita la cascada de N errores confusos).
  - Tope `MaxFilas` (5000) → error claro y no procesa.
- Helpers de coerción **centralizados** en `Comun.cs`: mover ahí `EnteroNoNeg`, `DecimalNoNeg`, `DobleOpc` (hoy duplicados en `Historicos.cs:228-253` y `SeguimientoEngorde.cs`), junto a los existentes `EnteroNoNegNull`/`FechaOpc`.
- Constantes: `MaxFilasPorArchivo = 5000`, `MaxErroresReportados = 300`.

### 3.3 Severidad + cap de errores (contrato aditivo)
- `MigracionErrorDto` → `record(int Fila, string Columna, string? Valor, string Mensaje, string Severidad = "Error")` ("Error" | "Advertencia"). Aditivo: call sites existentes compilan igual.
- Las **advertencias no bloquean** la importación; `FilasError` cuenta solo severidad Error.
- Cap: respuestas y `ErroresJson` guardan como máximo 300 entradas + entrada meta ("Se muestran los primeros 300 de N."); `MigracionResultDto.TotalErrores` lleva el total real.
- `MigracionResultDto` → campos aditivos con default: `int FilasOmitidas = 0, long DuracionMs = 0, int TotalErrores = 0`.

### 3.4 Plantillas desde el esquema
- `PonerEncabezados(ws, esquema)` + dropdowns inline desde `Opciones`. Las hojas Referencias/Instrucciones se mantienen como están (contenido idéntico). **Salida visible sin cambios** (mismos títulos y orden).

### 3.5 Ejecución de importación
- `DuracionMs`: `Stopwatch` en `ProcesarAsync` (Operaciones.cs) + `result with { DuracionMs = ... }`.
- `FilasOmitidas`: SeguimientoPolloEngorde cuenta los `continue` por fecha ya existente en BD. Para los tipos plpgsql (SegLevante/SegProduccion/VentaEngorde): solo si la función ya devuelve el dato (no se modifican funciones SQL en este plan; si no lo devuelve, queda 0 y se documenta).
- **Importación parcial (opt-in)**: `permitirParcial=false` por defecto (comportamiento actual all-or-nothing intacto). Con `true`: inserta solo filas válidas, estado `ProcesadoParcial`, contadores reales. Viaja en el form (`MigracionUploadForm.PermitirParcial`) → `ImportarAsync(tipo, file, contexto, permitirParcial, ct)`. Todas las filas de todos los tipos son independientes entre sí (verificado), por lo que es seguro.
- **Auditoría también de dry-runs**: `RegistrarAuditoriaAsync` en TODAS las corridas; nueva columna `fue_dry_run`. Estados: `Validado`/`ConErrores` (dry) · `Procesado`/`ProcesadoParcial`/`ConErrores`/`Fallido` (real).
- **Bug fix LotesPolloEngorde**: validar duplicado contra BD (nombre de lote normalizado ya existente en la empresa → error de fila, mismo patrón que Granjas).

### 3.6 BD (única migración, idempotente)
Tabla `migracion_masiva` + 3 columnas (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`):
- `filas_omitidas int NOT NULL DEFAULT 0`
- `duracion_ms bigint NULL`
- `fue_dry_run boolean NOT NULL DEFAULT false`
Migración EF `AddMigracionMasivaMetricas` (Up con `Sql(...)` idempotente). Sin cambios a funciones plpgsql. Sin tocar el historial EF previo.

### 3.7 Historial (API + UI)
- `GET api/Migracion/historial?tipo&page&pageSize&incluirValidaciones` → `MigracionHistorialPagedDto(Items, Total, Page, PageSize)` (page=1, pageSize=20, máx 100). `MigracionHistorialDto` + `FilasOmitidas, DuracionMs, FueDryRun, TieneErrores`. (Cambio de shape aceptado: la UI de historial no existía.)
- `GET api/Migracion/historial/{id}/errores` → `MigracionErrorDto[]` desde `ErroresJson` (404 si no es de la empresa activa).

### 3.8 Frontend
- **`funciones/`** (nueva, patrón del repo + README): `validar-archivo-cliente.funcion.ts` (pura: extensión/tamaño/vacío → mensaje o null), `exportar-errores-excel.funcion.ts` (delega en `shared/utils/excel/exportar-tabla-excel.funcion.ts` → `exportarObjetosExcel`), `construir-resumen-resultado.funcion.ts` (resultado → tarjetas).
- **panel-plantilla-upload**: validación cliente al elegir archivo (.xlsx, ≤10MB); **gate de importación** (importar se habilita solo tras validar OK el mismo archivo; cambiar el archivo resetea); **confirmación** con `ConfirmDialogService.ask` antes de importar; checkbox "Importar filas válidas aunque haya errores" (permitirParcial, visible solo tras una validación con errores); mensajes HTTP específicos (401/413/0-timeout/500); resumen con tarjetas (totales/procesadas/omitidas/errores/advertencias/duración).
- **reporte-errores**: badges por severidad, render limitado (primeros 200 + "mostrar más"), botón "Exportar errores (.xlsx)", nota del cap del back, `aria-label`/roles básicos.
- **historial**: nuevo componente en la página (tabla paginada: fecha, tipo, archivo, badge estado + dry-run, contadores, duración; "ver errores" → `/historial/{id}/errores` reusando el reporte + export).
- **models** actualizados (severidad, omitidas, duración, totalErrores, paged historial). Todo signals + OnPush (gotcha CD zona-escape del repo); sin getters que alocan por ciclo; `takeUntilDestroyed` en subscribes.

## 4. Reglas de preservación (vinculantes para los agentes)

1. Títulos, orden y alias de columnas: EXACTOS a los actuales (transcribir del código, no inventar).
2. Mensajes de error de negocio existentes: intactos (los nuevos errores de archivo/encabezado/cap son adicionales).
3. All-or-nothing sigue siendo el default; parcial solo con opt-in explícito.
4. Namespace plano `ZooSanMarino.Infrastructure.Services`; partial classes; interfaz solo en el ancla; no editar `.csproj`.
5. DTOs: cambios aditivos con defaults (no romper llamadas existentes).
6. Migración EF idempotente; no insertar a mano en `__EFMigrationsHistory`; no `ef database update` contra prod.
7. Front: primitivas obligatorias (`ToastService`, `ConfirmDialogService`, helpers Excel compartidos); prohibido `alert/confirm` nativos y `XLSX.write*` inline.
8. Tests: los 31 de `MigracionCalculosTests` siguen verdes; los nuevos van en `backend/tests/ZooSanMarino.Application.Tests/`.

## 5. Fases y asignación

| Fase | Contenido | Ejecuta |
|---|---|---|
| **F1 — Back núcleo** | Esquemas + calculos puros + DTO severidad/cap + `Comun.cs` robusto + plantillas desde esquema + refactor de los 8 procesadores + fix duplicados LotesPolloEngorde + tests xUnit | Agente Sonnet A |
| **F2 — Back ejecución** | Duración + omitidas + parcial opt-in + auditoría dry-run + entidad/config/migración EF + historial paginado + endpoint errores por id | Agente Sonnet A (continúa) |
| **F3 — Front** | models + funciones/ + panel (validación cliente, gate, confirm, parcial, resumen) + reporte (severidad, cap, export) + historial UI | Agente Sonnet B |
| **F4 — Validación integral** | build+tests back/front; `make dev-back`+`make dev-front`; smoke API con credenciales de prueba (postura y engorde); visual con navegador (bajo consumo); sin procesos huérfanos | Fable 5 |
| **F5 — Cierre** | `migraciones_masivas_mejoras_resultado.md` + tracker + memoria | Agente C + Fable 5 |

## 6. Casos de prueba

**Unitarios (xUnit, `MigracionEsquemasTests.cs`):**
- Cada esquema: títulos normalizados únicos, ≥1 columna requerida, hoja "Datos".
- `ValidarEncabezados`: todas presentes → sin faltantes; falta requerida → la reporta; alias satisface al título; header desconocido → en Desconocidos; orden distinto no afecta.
- `LimitarErrores`: N≤max intacto; N>max capado con total real.
- Los 31 tests existentes de `MigracionCalculos` sin cambios y verdes.

**Smoke E2E (F4, dev local :5002/:4200, BD :5433):**
1. Login postura → `GET tipos` → `GET plantilla?tipo=Nucleos` → construir xlsx de prueba:
   a. inválido (columna "Nombre" eliminada; granja inexistente; fila duplicada) → `validar` devuelve error de columna faltante (1, no N), errores de fila con severidad, advertencias por columnas extra.
   b. válido (1 núcleo QA con granja real) → `validar` OK → `importar` → `Procesado`, contadores y duración; `historial` paginado lo refleja; `/historial/{id}/errores` responde.
2. Archivo basura (.txt renombrado / corrupto) → 400 con mensaje claro (no 500).
3. Login engorde (Ecuador) → tipos engorde visibles → `plantilla?tipo=LotesPolloEngorde` → `validar` dry-run: lote duplicado contra BD → error (bug fix verificado); raza/año inválidos → error.
4. Front visual: página migraciones (selector, panel, resumen, reporte con severidades, historial); consola sin errores.

## 7. Riesgos y mitigaciones

- **Regresión de plantillas/parseo** → esquemas transcritos del código actual + build/tests + smoke con plantilla generada real.
- **Shape historial cambia** → la UI de historial no existía; el front se actualiza en F3 (mismo PR conceptual).
- **BD compartida entre worktrees** → migración solo ADD COLUMN IF NOT EXISTS (no rompe otras ramas).
- **Import real en E2E** → solo 1 núcleo QA en postura (BD local de desarrollo); engorde solo dry-run.
- **Procesos** → `make down`/kill de los dev servers al cerrar F4.
