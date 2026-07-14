# Resultado — Mejoras del módulo Migraciones Masivas (Postura + Engorde)

**Fecha de cierre:** 2026-07-13
**Plan:** [migraciones_masivas_mejoras_plan.md](migraciones_masivas_mejoras_plan.md) · **Tracker:** [tracker_estado.md](../tracker_estado.md)
**Módulo:** `backend/src/ZooSanMarino.Infrastructure/Services/Migracion/` + `frontend/src/app/features/migraciones-masivas/`
**Ejecución:** agentes — Fable 5 (gestión, criterios, validación integral), Sonnet (implementación backend F1-F2 y frontend F3), Haiku (investigación F0) — 2026-07-13.

---

## 1. Resumen ejecutivo

El módulo de Migraciones Masivas (Postura + Engorde) ya funcionaba, pero acumulaba deuda en tres frentes: encabezados de columnas duplicados entre plantillas y parseo (7 archivos distintos), un archivo corrupto o mal nombrado devolvía un 500 genérico, y una columna borrada producía cientos de errores confusos en vez de uno claro. Además, la ejecución de importaciones no daba feedback real (sin duración, sin distinguir filas omitidas, historial sin UI) y el front no confirmaba antes de importar ni validaba el archivo del lado cliente.

Se atacó en tres ejes, **sin cambiar comportamiento de negocio** (mismas reglas, mismos mensajes, mismas plantillas visibles, misma aritmética): (1) **centralización** con un esquema único por tipo de migración que alimenta tanto la plantilla como la validación; (2) **validación robusta de archivos** (extensión/firma, hoja, encabezados faltantes/duplicados/desconocidos, tope de filas, cap de errores con severidad); (3) **mejor ejecución y feedback** (duración real, filas omitidas, importación parcial opt-in, auditoría de dry-runs, historial paginado con UI, exportación de errores a Excel). De paso se corrigió un bug real: `LotesPolloEngorde` no validaba duplicados contra BD, solo dentro del archivo.

Resultado: backend con 0 errores/0 warnings y 303 tests verdes (35 nuevos); frontend con `yarn build` limpio; validación end-to-end (API real + navegador) confirmó los tres ejes funcionando, incluyendo el bug fix de engorde.

---

## 2. Qué cambió — Backend

### Archivos nuevos
| Archivo | Contenido |
|---|---|
| `backend/src/ZooSanMarino.Application/DTOs/Migracion/MigracionEsquema.cs` | `record ColumnaEsquema(Titulo, Requerida, Alias?, Opciones?)` + `record EsquemaMigracion(Hoja, Columnas, MaxFilas)` — el contrato del esquema único. |
| `backend/src/ZooSanMarino.Application/Calculos/MigracionEsquemas.cs` | Los **8 esquemas transcritos exactos** del código previo (Granjas, Núcleos, Galpones, SeguimientoLevante, SeguimientoProducción, LotesPolloEngorde, SeguimientoPolloEngorde, VentaPolloEngorde) + `Para(TipoMigracion)`. Fuente única para plantilla y validación. |
| `backend/src/ZooSanMarino.Application/Calculos/MigracionEsquemaCalculos.cs` | Cálculo puro y testeable: `ValidarEncabezados(esquema, headers)` → faltantes/desconocidos; `LimitarErrores(errores, max)` → cap + total real. |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260713220110_AddMigracionMasivaMetricas.cs` (+ `.Designer.cs`) | Migración EF idempotente: agrega `filas_omitidas`, `duracion_ms`, `fue_dry_run` a `migracion_masiva`. |
| `backend/tests/ZooSanMarino.Application.Tests/MigracionEsquemasTests.cs` | 35 tests nuevos xUnit sobre los esquemas y los cálculos puros. |

### Archivos modificados
`MigracionController.cs` · `MigracionDtos.cs` · `IMigracionRepository.cs` · `IMigracionService.cs` · `MigracionMasiva.cs` (entidad) · `ZooSanMarinoContextModelSnapshot.cs` · `MigracionMasivaConfiguration.cs` · `MigracionService.Comun.cs` · `MigracionService.Estructura.cs` · `MigracionService.EstructuraEngorde.cs` · `MigracionService.Historicos.cs` · `MigracionService.Operaciones.cs` · `MigracionService.Plantillas.cs` · `MigracionService.SeguimientoEngorde.cs` · `MigracionService.VentaEngorde.cs` · `MigracionRepository.cs` · `MigracionService.cs`.

### Detalle por eje

**Esquema único (eje 1):** `MigracionEsquema.cs` + `MigracionEsquemas.cs` (8 tipos transcritos) + `MigracionEsquemaCalculos.cs` (puro) reemplazan los encabezados hardcodeados duplicados entre `Plantillas.cs` y cada parser. Las plantillas ahora se generan con `PonerEncabezados(ws, esquema)` + dropdowns inline desde `Opciones` — **salida visible idéntica** (mismos títulos y orden). Los helpers de coerción (`EnteroNoNeg`, `DecimalNoNeg`, `DobleOpc`, antes duplicados en `Historicos.cs` y `SeguimientoEngorde.cs`) quedaron centralizados en `Comun.cs`.

**Validación robusta de archivos (eje 2):** `Comun.cs` incorpora `ValidarArchivo` (extensión `.xlsx` + firma ZIP `PK` → 400 con mensaje claro en vez de 500) y `LeerDatosConEsquema`: la hoja `Datos` ya no cae en fallback silencioso a la primera hoja disponible; encabezados duplicados generan advertencia; cada columna requerida faltante genera **1 error claro** en vez de la cascada de N errores por fila; columnas desconocidas generan advertencia; tope de 5000 filas por archivo; cap de 300 errores reportados con el total real aparte. Se agregó severidad `Error`/`Advertencia` a `MigracionErrorDto` (las advertencias no bloquean). Se corrigieron los builders de resultado para que sean coherentes (`ResultadoFallido` ya no queda en un estado inconsistente).

**Ejecución y feedback (eje 3):** duración medida con `Stopwatch` en `ProcesarAsync`; `FilasOmitidas` contabilizado en `SeguimientoPolloEngorde` (fechas ya existentes que hoy se saltan con `continue`); importación parcial **opt-in** vía `PermitirParcial` en el form (default `false` = all-or-nothing intacto; con `true` inserta solo filas válidas bajo el estado nuevo `ProcesadoParcial`); auditoría centralizada en `ProcesarAsync` que ahora registra también los dry-runs (columna `fue_dry_run`); historial paginado (`MigracionHistorialPagedDto`, filtro `incluirValidaciones`, `pageSize` clamp 1..100); endpoint nuevo `GET historial/{id}/errores`. **Bug fix:** `LotesPolloEngorde` ahora valida duplicado contra BD (antes solo comparaba dentro del mismo archivo, por lo que reimportar el mismo archivo rompía en la inserción).

**Base de datos:** migración EF `AddMigracionMasivaMetricas` (idempotente, `ADD COLUMN IF NOT EXISTS`) aplicada en local (:5433); sin tocar funciones plpgsql ni el historial de migraciones previo.

---

## 3. Qué cambió — Frontend

### Archivos nuevos
- `frontend/src/app/features/migraciones-masivas/funciones/` (patrón del repo, funciones puras + `README.md`):
  - `validar-archivo-cliente.funcion.ts` — validación pura de extensión/tamaño/vacío antes de subir.
  - `construir-resumen-resultado.funcion.ts` — arma las tarjetas de resumen a partir del resultado.
  - `exportar-errores-excel.funcion.ts` — delega en el helper compartido `shared/utils/excel/exportar-tabla-excel.funcion.ts` (no reimplementa `XLSX.write*`).
- `frontend/src/app/features/migraciones-masivas/components/historial-migraciones/historial-migraciones.component.ts` — tabla paginada de historial (fecha, tipo, archivo, badge de estado + dry-run, contadores, duración; "Ver errores" reusa el reporte de errores); autorefresco tras importar.

### Archivos modificados
`panel-plantilla-upload.component.ts` · `reporte-errores-migracion.component.ts` · `models/migracion.model.ts` · `migraciones-masivas-page.component.html` · `migraciones-masivas-page.component.ts` · `services/migracion.service.ts` · `shared/utils/format.ts` (nueva función `fechaHoraCorta`).

### Detalle por eje

- **Panel de carga:** validación cliente al elegir archivo (.xlsx, ≤10MB) vía `validar-archivo-cliente.funcion.ts`; **gate de importación** — el botón Importar solo se habilita después de validar OK el mismo archivo (cambiar de archivo resetea el gate); si la validación devuelve errores, se exige tildar el checkbox "Importar solo las filas válidas…" (`PermitirParcial`) para poder continuar; confirmación con `ConfirmDialogService.ask` antes de importar (modal "Confirmar importación", menciona archivo/tipo/si es parcial); mensajes HTTP específicos para 401/413/timeout/500 en vez de un genérico; tarjetas de resumen (totales/procesadas/omitidas/errores/advertencias/duración) armadas con `construir-resumen-resultado.funcion.ts`; badge de estado.
- **Reporte de errores:** badges por severidad (Error/Advertencia), render limitado con "Mostrar más", botón "Exportar errores (.xlsx)" vía el helper compartido, nota del cap del backend (300 de N).
- **Historial:** componente nuevo en la página, tabla paginada con badge "Validación" para los dry-runs, "Ver errores" reutilizando el componente de reporte, se refresca solo tras cada importación.
- Modelos y servicio (`migracion.model.ts`, `migracion.service.ts`) actualizados al contrato nuevo (severidad, omitidas, duración, totalErrores, historial paginado).
- Todo implementado con signals + `OnPush`; sin getters de template que alocan arrays/objetos nuevos por ciclo (evita el bug NG0103 ya conocido en el repo).

---

## 4. Evidencia de validación

- **Backend:** `dotnet build` → 0 errores, 0 warnings. `dotnet test` → **303 tests pasados** (302 en `ZooSanMarino.Application.Tests`, de los cuales 35 son nuevos de `MigracionEsquemasTests`, + 1 en Domain), 0 fallos. Migración verificada idempotente: al volver a correr `dotnet ef database update`, resultado "No migrations were applied".
- **Frontend:** `yarn build` → 0 errores; único warning: bundle budget preexistente (1.85 MB vs 1.50 MB en un módulo lazy, no introducido por este trabajo).
- **Smoke API** (backend real en `:5002`, BD local `:5433`, usuarios de prueba de las dos líneas — postura y engorde):
  - Columna "Nombre" eliminada del archivo → **1 error claro** ("La columna 'Nombre' es obligatoria y no está en el archivo.") en vez de N errores confusos.
  - Archivo basura renombrado a `.xlsx` → **400** "El archivo debe ser un Excel .xlsx válido…" (antes devolvía 500).
  - Filas con granja inexistente / duplicado → errores con fila, columna, valor y severidad.
  - `PermitirParcial=true` → resultado `ProcesadoParcial`, insertando solo las filas válidas (1 de 3 en la corrida de prueba).
  - Flujo válido → `Validado` → `Procesado`.
  - Historial paginado refleja los dry-runs auditados (`fueDryRun`), la duración y `tieneErrores`; `GET historial/{id}/errores` devuelve los errores guardados.
  - Engorde (empresa Ecuador): dry-run detecta "El lote ya existe en la empresa." (**bug fix verificado**) y raza/año fuera de guía genética.
- **Visual** (navegador, front real en `:4200` con login):
  - Botón Importar deshabilitado hasta validar.
  - Validación en vivo mostró las tarjetas de resumen (2 totales / 0 procesadas / 2 con error / 0 advertencias / duración) y el badge "Validación · ConErrores".
  - El checkbox de importación parcial habilita el botón Importar.
  - Modal "Confirmar importación — Se importará 'mini_invalido.xlsx' como Núcleos. Se omitirán las filas con error." (se canceló a propósito, sin ejecutar la importación).
  - Reporte de errores con columna Severidad y botón "Exportar errores (.xlsx)".
  - Historial con estados/badges/paginación ("Página 1 de 1 (8 registros)") y duración en formato legible.
  - Consola del navegador sin errores.
  - **Nota:** los screenshots del pane no se pudieron capturar por timeout del capturador del entorno; la verificación visual se hizo por lectura de DOM/texto, no por imagen.

---

## 5. Decisiones tomadas

- Registrar también los **dry-runs** (validaciones sin importar) en el historial de auditoría, no solo las importaciones reales.
- Cap de **300 errores** reportados y tope de **5000 filas** por archivo, para no devolver respuestas ni `ErroresJson` desmedidos.
- Separar los builders `ResultadoConErrores` / `ResultadoOk` para no poder reportar éxito sin haber insertado nada.
- El tope de filas se mide sobre el **rango físico de la hoja** antes de materializar los datos en memoria (evita cargar archivos gigantes solo para descartarlos después).
- `TieneErrores` se deriva de `ErroresJson is not null` (sin duplicar el flag en otro lado).
- El gate de importación en el front se deriva **100% de computed signals**, sin `effect()`.
- El historial queda siempre visible al pie de la página (no es un tab oculto).
- El shape del endpoint de historial cambió a paginado — se aceptó el cambio porque la UI de historial no existía antes de este desarrollo (no había nada que romper).

---

## 6. Datos de prueba que quedaron en la BD local

Limpiables si molestan (BD local `sanmarinoapplocal` en `:5433`, compartida entre branches/worktrees):
- Núcleos `QA2` ("Nucleo QA 2") y `QAMIG13` ("Nucleo QA Migracion 2026-07-13") en la granja ABRUZZO (empresa 1).
- Alrededor de 10 corridas de prueba en la tabla `migracion_masiva` (incluye dry-runs auditados).
- En la línea de **engorde no se insertó nada** — todas las corridas de prueba fueron dry-run.

---

## 7. Pendientes / fuera de alcance

- `FilasOmitidas` queda en `0` para `SeguimientoLevante`, `SeguimientoProduccion` y `VentaPolloEngorde`: las funciones plpgsql que procesan esos tipos no devuelven ese dato hoy, y no se modificaron funciones SQL en este desarrollo (fuera de alcance del plan). Documentado como limitación conocida, no como bug.
- **Fase 3 de Postura** (Ventas/Movimientos) sigue pendiente, tal como estaba antes de este trabajo — spec en [migraciones_masivas_fase3_spec.md](migraciones_masivas_fase3_spec.md).
- **No se hizo commit.** Los cambios quedan en el working tree para revisión del usuario antes de confirmar.

---

## 8. Cómo probar a mano

1. Levantar el entorno: `make dev-back` (o `dotnet run --launch-profile "Development"` desde `backend/src/ZooSanMarino.API`) + `make dev-front` (o `yarn start` desde `frontend`). BD local Docker en `:5433` debe estar arriba.
2. Entrar a la app (`:4200`) con un usuario de la empresa/país que corresponda y navegar al módulo **Migraciones Masivas**.
3. Elegir un tipo (p. ej. Núcleos) y **descargar la plantilla** — confirmar que títulos/orden de columnas son los de siempre.
4. Completar la plantilla (o romper una columna a propósito) y subirla: **Validar** primero.
   - Con una columna requerida faltante → debe aparecer 1 error claro, no una cascada.
   - Con filas inválidas → tarjetas de resumen + reporte con severidad por fila.
5. Con errores presentes, tildar "Importar solo las filas válidas…" y confirmar en el modal → revisar que el resultado sea `ProcesadoParcial` y que solo las filas válidas queden insertadas.
6. Repetir con un archivo 100% válido → `Procesado`.
7. Revisar el **historial** al pie de la página: que aparezcan tanto las validaciones (dry-run) como las importaciones reales, con duración y contadores; abrir "Ver errores" de alguna corrida con errores.
8. Probar un archivo basura renombrado a `.xlsx` → debe devolver 400 con mensaje claro, no un error genérico.

---

## Fases completadas

- [x] **F0** — Investigación y plan (Haiku)
- [x] **F1** — Backend núcleo: esquema único + validación robusta (Sonnet A)
- [x] **F2** — Backend ejecución: contadores, auditoría, historial (Sonnet A)
- [x] **F3** — Frontend: panel, reporte, historial UI (Sonnet B)
- [x] **F4** — Validación integral: build + tests + smoke API + visual (Fable 5)
- [x] **F5** — Cierre: este documento + tracker + memoria
