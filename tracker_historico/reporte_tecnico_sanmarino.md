# Memoria de Desarrollo - Reporte Técnico San Marino
> **ARCHIVADO:** 2026-05-08 — Feature completa. Todas las fases (1–5 + Refactorización BD) COMPLETAS ✅

---

## Fase 1 — Backend Levante: COMPLETA ✅

### Histórico de Tareas
- [x] **1.1 Estructura de Filtros:** DTOs y lógica de cascada (Granja → Lote Seguimiento + Periodicidad).
      → `ObtenerReporteLevanteRequestDto` (LotePosturaBaseId, LoteLevanteId?, FiltroPeriodicidad, FechaInicio?, FechaFin?)
- [x] **1.2 Extracción de Datos Base:** Navegación lote_postura_base → lotes → lote_postura_levante.
      → Implementado en `ObtenerReporteLevanteAsync` con validaciones de companyId y deleted_at.
- [x] **1.3 Lógica de Consolidado:** Agrupación de lotes hijos (suma de mortalidades, recálculo de porcentajes).
      → `GenerarSemanalesConsolidados` + `GenerarDiariosConsolidados`.
- [x] **1.4 Lógica Individual:** Filtro por LoteLevanteId opcional.
- [x] **1.5 Agrupación Temporal:** Switch Diario/Semanal. Semanal → semanas 1-25 relativas a fecha_encaset.
- [x] **1.6 Cruce Genético (Semanas 1-25):** Join con `ProduccionAvicolaRaw` por Raza/Año/Semana.
      → Carga `guiasRaw` + `guiasGenetica` y puebla campos GUIA: PorcMortHGUIA, DifMortH, RetiroHGUIA, ConsAcGrHGUIA/MGUIA, GrAveDiaGUIAH/M, IncrConsHGUIA/M, PorcDifConsH, DifConsM, PesoHGUIA/MGUIA, PorcDifPesoH/M, UnifHGUIA/M, DifConsAcH/M.
- [x] **1.7 Exposición de API:** Endpoint `POST /api/reportetecnico/levante/obtener`.
      → Acepta `ObtenerReporteLevanteRequestDto`. Retorna `ReporteTecnicoLevanteCompletoDto`.

### Archivos Modificados — Fase 1
| Archivo | Cambio |
|---------|--------|
| `Application/DTOs/ObtenerReporteLevanteRequestDto.cs` | CREADO |
| `Application/DTOs/ReporteTecnicoLevanteCompletoDto.cs` | MODIFICADO — Agregado `DatosDiarios` |
| `Application/Interfaces/IReporteTecnicoService.cs` | MODIFICADO — Firma `ObtenerReporteLevanteAsync` |
| `Infrastructure/Services/ReporteTecnicoService.cs` | MODIFICADO — Cruce genético + consolidado |
| `API/Controllers/ReporteTecnicoController.cs` | MODIFICADO — Endpoint `POST /levante/obtener` |

---

## Fase 2 — Frontend Levante: COMPLETA ✅

### Histórico de Tareas

- [x] **2.1 Tipado Estricto / Service Integration:**
      → Interfaces TypeScript: `ObtenerReporteLevanteRequestDto`, `ReporteTecnicoDiarioLevanteDto`, `ReporteTecnicoLevanteSemanalDto` (80+ campos GUIA).
      → `ReporteTecnicoLevanteCompletoDto` actualizado con `datosDiarios`.
      → Método `obtenerReporteLevante()` en `ReporteTecnicoService`.

- [x] **2.2 Conexión API / Lógica de Filtros:**
      → Tab "Real vs Guía" consume `reporteLevanteConTabs()?.datosSemanales`.
      → Método `obtenerReporteLevante()` preparado para consumir `POST /levante/obtener`.

- [x] **2.3 View Engine / Tabla Dinámica (Real vs Guía):**
      → Tab 5 con tabla doble-cabecera estilo Excel: Semana + Fecha sticky, 28+ columnas.
      → Doble `thead`: fila-1 grupos (Población/Mortalidad/Pesos/Consumo), fila-2 métricas (Real/Guía/Dif).
      → Angular `@if`/`@for` blocks + `@let`.

- [x] **2.4 Formateo de Datos / Semáforo:**
      → Método `semaforo(value, tipo)` con clases `cell-success`/`cell-danger`/`cell-warning`.
      → Pipes: `date:'dd/MM/yy'`, `number:'1.2-2'`, `number:'1.3-3'`.
      → SCSS: sticky thead, colores por grupo (verde/azul/guía), semáforo RGB.

- [x] **2.5 Servicio Dedicado de Filtros:**
      → `ReporteTecnicoLevanteFilterService` (Signals puros).
      → Computed: `nucleosFiltrados`, `galponesFiltrados`, `lotesFiltrados` (solo lotes padre).
      → Expone `selectedLotePosturaBaseId` para `POST /levante/obtener`.
      → Backend: `LoteFilterItemDto` + `int? LotePosturaBaseId`.

- [x] **2.6 Corrección Lote Base en dropdown:**
      → Dropdown "Lote Base" mostraba sublotes — corregido.
      → Backend: `LoteReproductoraFilterDataDto` añade campo `LotesBase: IEnumerable<LoteBaseFilterItemDto>`.
      → Frontend: computed `lotesPaso4` cross-referencia `d.lotes` con `d.lotesBase`.

- [x] **2.7 Filtro de Fase/Etapa (Selector Maestro LEVANTE / PRODUCCIÓN):**
      → Signal `selectedEtapa` en `ReporteTecnicoLevanteFilterService`.
      → `setEtapa()` resetea toda la cascada e invalida caché.
      → `_cargarDatos()` selecciona URL según etapa.
      → `_normalizeProduccion()` mapea `SeguimientoProduccionFilterDataDto` a `LevanteFilterData`.

- [x] **2.8 Reestructuración de UI — Jerarquía visual:**
      → Eliminados números de paso, nombres de tablas de BD, hints técnicos.
      → Todos los `style="..."` inline movidos a clases SCSS.
      → Placeholders simplificados. Build Angular: ✅ 0 errores.

### Archivos Modificados — Fase 2
| Archivo | Cambio |
|---------|--------|
| `frontend/.../reporte-tecnico.service.ts` | Interfaces DTOs + `obtenerReporteLevante()` |
| `frontend/.../reporte-tecnico-main.component.ts` | Tab 5, método `semaforo()`, `tabLevanteActivo` default |
| `frontend/.../reporte-tecnico-main.component.html` | Tab 5 tabla doble cabecera + semáforo |
| `frontend/.../reporte-tecnico-main.component.scss` | Estilos tabla Excel, sticky, semáforo |
| `Application/DTOs/LoteReproductoraFilterDataDto.cs` | Añadido `LoteBaseFilterItemDto` + `LotesBase` |
| `Infrastructure/Services/ReporteTecnicoLevanteFilterDataService.cs` | Reescrito: retorna `LotesBase` reales |
| `Infrastructure/Services/LoteLevanteFilterDataService.cs` | `LotesBase: Array.Empty<>()` |
| `Infrastructure/Services/LoteReproductoraFilterDataService.cs` | `LotesBase: Array.Empty<>()` |
| `Infrastructure/Services/SeguimientoAvesEngordeFilterDataService.cs` | `LotesBase: Array.Empty<>()` |
| `frontend/.../services/reporte-tecnico-levante-filter.service.ts` | `selectedEtapa`, `FiltroLoteItem`, `lotesPaso4`, `setEtapa` |
| `frontend/.../reporte-tecnico-main.component.ts` | `onEtapaChange()`, `_generarReporteLevante()`, guards |
| `frontend/.../reporte-tecnico-main.component.html` | Radio group FASE + pasos condicionales |

---

## Fase 3 — Reporte Técnico de PRODUCCIÓN: COMPLETA ✅

**Plan completo:** `fase_de_desarrollo/02_req_reporte_produccion.md`

### Contexto técnico validado
- **Tabla real de seguimiento diario:** `public.produccion_diaria` — entidad EF Core: `SeguimientoProduccion`
- **FK de enlace:** `lote_postura_produccion_id` (nullable) en `produccion_diaria`
- **Cadena de navegación:** `LotePosturaBase → Lote → LotePosturaLevante → LotePosturaProduccion → produccion_diaria.lote_postura_produccion_id`
- **Servicio existente:** `ReporteTecnicoProduccionService.ObtenerSeguimientosProduccionPorLPPAsync()`

### Estado de Tareas

#### Sub-fase A — Backend: Filter-data con LoteBase ✅
- [x] **3.A1** `LoteProduccionFilterDataService`: cadena LPP→LPL→Lote→LPB; retorna `LoteReproductoraFilterDataDto`
- [x] **3.A2** `ILoteProduccionFilterDataService`: firma cambiada a `Task<LoteReproductoraFilterDataDto>`
- [x] **3.A3** `ReporteTecnicoProduccionController` `GET /filter-data`: tipo actualizado

#### Sub-fase B — Backend: Nuevo endpoint /obtener ✅
- [x] **3.B1** `ObtenerReporteProduccionRequestDto` creado
- [x] **3.B2** `ObtenerReporteProduccionAsync()` en `IReporteTecnicoProduccionService`
- [x] **3.B3** Implementado en `ReporteTecnicoProduccionService`
- [x] **3.B4** `ReporteTecnicoProduccionController`: `POST /obtener` agregado

#### Sub-fase C — Frontend: Filter Service ✅
- [x] **3.C1** Eliminado `_normalizeProduccion()` — PRODUCCIÓN retorna `LevanteFilterData` directamente
- [x] **3.C2** `sublotesFiltrados` computed aplica en ambas etapas

#### Sub-fase D — Frontend: Componente Principal ✅
- [x] **3.D1** Signal `reporteProduccion` + `tabProduccionActivo`
- [x] **3.D2** `_generarReporteProduccion()` implementado
- [x] **3.D3** `generarReporte()` despacha según etapa
- [x] **3.D4** Guards en `limpiarReporte()`, `puedeGenerarReporte()`, `exportarExcel()`
- [x] **3.D5** Interfaces PRODUCCIÓN + `obtenerReporteProduccion()` en service

#### Sub-fase E — Frontend: HTML ✅
- [x] **3.E1–3.E4** Labels dinámicos, periodicidad en PRODUCCIÓN, sección resultado completa

### Archivos Modificados — Fase 3
| Archivo | Cambio |
|---------|--------|
| `Infrastructure/Services/LoteProduccionFilterDataService.cs` | Reescrito: cadena LPP→LPL→Lote→LPB |
| `Application/Interfaces/ILoteProduccionFilterDataService.cs` | Firma actualizada |
| `Application/DTOs/ObtenerReporteProduccionRequestDto.cs` | CREADO |
| `Application/Interfaces/IReporteTecnicoProduccionService.cs` | `ObtenerReporteProduccionAsync()` |
| `Infrastructure/Services/ReporteTecnicoProduccionService.cs` | Nuevo método + `ObtenerSeguimientosDesdePDAsync` |
| `API/Controllers/ReporteTecnicoProduccionController.cs` | `/filter-data` actualizado + `/obtener` |
| `frontend/.../reporte-tecnico-levante-filter.service.ts` | Normalización eliminada |
| `frontend/.../reporte-tecnico.service.ts` | DTOs PRODUCCIÓN + `obtenerReporteProduccion()` |
| `frontend/.../reporte-tecnico-main.component.ts` | Signal + métodos de producción |
| `frontend/.../reporte-tecnico-main.component.html` | Tipo/Sublote/Periodicidad dual + resultado PRODUCCIÓN |

---

## Fase 4 — Reportes con TABs (SOLO PRODUCCIÓN): COMPLETA ✅

**Documentos:** `fase_de_desarrollo/03_req_reportes_tabs.md`

**⚠️ IMPORTANTE:** SOLO para ETAPA PRODUCCIÓN — LEVANTE no fue tocado.

### Arquitectura
```
reporte-tecnico-main.component
├─ @if (LEVANTE) → [Componentes Fases 1-3]
└─ @if (PRODUCCION) → <app-reportes-tabs>
    ├─ <app-reporte-diario-galpon>
    ├─ <app-reporte-semanal-galpon>
    ├─ <app-reporte-general-diario>
    └─ <app-reporte-general-semanal>
```

### Tareas completadas
- [✅] **G1** 5 DTOs backend (ReporteDiario/SemanalGalpon, ReporteGeneral*, ReporteTabs)
- [✅] **G2** `ObtenerReporteProduccionTabsAsync()` — desglose por galpón + GUIA genética
- [✅] **G3** `POST /obtener-tabs` en controller
- [✅] **H1** `ReportesTabsComponent` padre — tabs + selector de galpón
- [✅] **H2** 4 componentes tabla hijo
- [✅] **H3** Templates + estilos (sticky header, semáforo, Real/Guía)
- [✅] **I1–I3** Integración en componente principal
- [✅] **J1** `obtenerReporteProduccionTabs()` en service

**Build:** Backend ✅ 0 errores. Angular ✅ 0 errores.

---

## Fase 5 — Exportación de Reportes a Excel: COMPLETA ✅

**Módulo:** Reportes Técnicos · Descarga Excel con TABs  
**Aplica:** LEVANTE y PRODUCCIÓN

### Estructura Excel
- **LEVANTE:** Información + Real vs Guía + Semanal + Diario
- **PRODUCCIÓN:** Información + Diario/Galpón + Semanal/Galpón + Diario/General + Semanal/General

### Nombre de archivo dinámico
```
LEVANTE_K345_K345A_20260507_143022.xlsx
PRODUCCION_K370_20260507_143022.xlsx
```

### Tareas completadas

#### Backend ✅
- [x] `IExportacionExcelService` + `ExportacionExcelService` (EPPlus)
- [x] DTOs: `ExportarExcelLevanteRequestDto`, `ExportarExcelProduccionTabsRequestDto`, `ExportarExcelMetaDto`
- [x] `POST /ReporteTecnico/levante/exportar-excel`
- [x] `POST /ReporteTecnicoProduccion/exportar-excel-tabs`
- [x] DI registrado en `Program.cs`

#### Frontend ✅
- [x] Botón "Descargar Excel" habilitado solo cuando hay datos (`puedeDescargarExcel()`)
- [x] `exportarExcel()` despacha a `_exportarLevante()` o `_exportarProduccion()`
- [x] `_buildMeta()`, `_nombreArchivo()`, `_descargarBlob()`
- [x] `exportarExcelLevanteNuevo()` + `exportarExcelProduccionTabs()` en service

#### Testing (pendiente validación con datos reales)
- [ ] Descargar Excel desde LEVANTE (verificar hojas y formato)
- [ ] Descargar Excel desde PRODUCCIÓN (verificar hojas por galpón)
- [ ] Verificar nombres de archivo dinámicos
- [ ] Validar colores de semáforo (verde/amarillo/rojo)

---

## Refactorización — Nombres de Tablas en BD: COMPLETA ✅

**Archivo de especificación:** `fase_de_desarrollo/06_refactorizacion_nombres_tablas.md`

### Renombres ejecutados

| Tabla Antigua | Tabla Nueva | Migración EF |
|---------------|-------------|--------------|
| `produccion_avicola_raw` | `guia_genetica_sanmarino_colombia` | `20260507174154_RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica` |
| `produccion_diaria` | `seguimiento_diario_produccion_reproductoras` | `20260507181055_RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras` |
| `seguimiento_diario` | `seguimiento_diario_levante_reproductoras` | `20260508030155_RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevanteReproductoras` |

### Migración de datos (menu)
- `20260508053425_UpdateMenu_ReporteTecnico_GenericLabel`: renombra label del menú id=19 a `'Reporte Técnico Sanmarino'`

### Lecciones aprendidas
- **PK naming:** EF genera `pk_table_name`, PostgreSQL default es `table_name_pkey` — siempre verificar en BD antes de `DropPrimaryKey`
- **Unique indexes:** Los creados con `CREATE UNIQUE INDEX` solo están en `pg_indexes`, NO en `pg_constraint`. Usar `ALTER INDEX IF EXISTS ... RENAME TO` (nunca `ALTER TABLE RENAME CONSTRAINT`)
- **DDL transaccional:** Fallo en cualquier migración del batch revierte todo — corregir todas antes de reintentar

### Tareas completadas
- [x] `.ToTable()` actualizado en los 3 archivos de configuración EF
- [x] 3 migraciones de renombre de tablas creadas y aplicadas
- [x] 1 migración de dato (menu label) creada y aplicada
- [x] Build backend: 0 errores
- [x] `dotnet run` sin errores — migraciones aplicadas al DB local

**Última actualización:** 2026-05-08 — **FEATURE COMPLETA. Archivado.**
