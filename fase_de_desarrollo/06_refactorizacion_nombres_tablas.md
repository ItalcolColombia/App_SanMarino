# Refactorización — Nombres de Tablas en BD

**Objetivo:** Clarificar nombres de tablas para que sean semánticamente correctos y eviten ambigüedad.  
**Estado:** ✅ COMPLETA — 3 migraciones generadas, build 0 errores. Pendiente: aplicar migraciones a BD (requiere Docker activo o deploy).

---

## ⚠️ CORRECCIONES AL PLAN ORIGINAL (hallazgos del análisis de código)

Antes de ejecutar cualquier cambio, estos hallazgos corrigen supuestos erróneos del plan original:

### Hallazgo 1 — `SeguimientoDiario` es POLIMÓRFICA, no solo levante

La tabla `seguimiento_diario` tiene un campo `TipoSeguimiento` con valores:
- `'levante'`
- `'produccion'`
- `'reproductora'`
- `'engorde'`

Es una tabla **unificada** para múltiples tipos. Renombrarla a `seguimiento_diario_levante_reproductoras`
sería **semánticamente incorrecto** — también guarda datos de engorde.

**Decisión a tomar antes de ejecutar Tabla 1:**
- Opción A: Renombrar de todas formas (aceptar que el nombre no cubre engorde)
- Opción B: Mantener el nombre actual y documentar el campo `TipoSeguimiento`
- Opción C: Dividir en 2 tablas físicas separadas (mucho mayor impacto)

### Hallazgo 2 — La entidad activa de `produccion_diaria` es `SeguimientoProduccion`, no `ProduccionDiaria`

- `ProduccionDiaria` está **IGNORADA** en EF Core (`builder.Ignore<ProduccionDiaria>()`)
- La entidad real que mapea a `produccion_diaria` es **`SeguimientoProduccion`**
- El plan original propone renombrar la entidad ignorada → el cambio debe hacerse sobre `SeguimientoProduccion`

### Hallazgo 3 — `ProduccionAvicolaRaw` → rename es correcto y seguro

Esta tabla (`produccion_avicola_raw`) efectivamente es una guía genética, no datos reales.
El rename a `guia_genetica_sanmarino_colombia` está bien justificado.

---

## 📊 Mapeo Corregido: Tabla Antigua → Nueva

| # | Tabla actual en BD | Entidad EF activa | Tabla nueva propuesta | Entidad nueva propuesta | Riesgo |
|---|---|---|---|---|---|
| 1 | `seguimiento_diario` | `SeguimientoDiario` (polimórfica) | `seguimiento_diario_levante` *(ver nota)* | `SeguimientoDiario` *(sin cambio de clase)* | ALTO — 43 archivos, tabla polimórfica |
| 2 | `produccion_diaria` | `SeguimientoProduccion` (ACTIVA) | `seguimiento_diario_produccion_reproductoras` | `SeguimientoProduccion` *(sin cambio de clase)* | ALTO — 30 archivos |
| 3 | `produccion_avicola_raw` | `ProduccionAvicolaRaw` | `guia_genetica_sanmarino_colombia` | `GuiaGeneticaSanmarinoColombiaStandard` | MEDIO — 31 archivos |

> **Nota Tabla 1:** El rename a nivel de BD es solo `[Table]` attribute + migración.
> No es necesario renombrar la clase C# si la tabla polimórfica se mantiene unificada.

---

## 📁 TABLA 1 — `seguimiento_diario` → `seguimiento_diario_levante`

**Entidad activa:** `SeguimientoDiario` (polimórfica con campo `TipoSeguimiento`)  
**Archivos a tocar: 32**

### Dominio
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Domain/Entities/SeguimientoDiario.cs` | Cambiar atributo `[Table("seguimiento_diario")]` → `[Table("seguimiento_diario_levante")]` |
| `backend/src/ZooSanMarino.Domain/Entities/SeguimientoDiarioAvesEngorde.cs` | Verificar si hereda/referencia la tabla (puede ser tabla separada) |
| `backend/src/ZooSanMarino.Domain/Entities/SeguimientoDiarioLoteReproductoraAvesEngorde.cs` | Verificar si hereda/referencia la tabla |

### Persistencia — Configuraciones EF
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioConfiguration.cs` | Actualizar `.ToTable("seguimiento_diario")` → `.ToTable("seguimiento_diario_levante")` |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioAvesEngordeConfiguration.cs` | Verificar referencia |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoDiarioLoteReproductoraAvesEngordeConfiguration.cs` | Verificar referencia |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/ZooSanMarinoContext.cs` | Actualizar DbSet: `DbSet<SeguimientoDiario> SeguimientoDiario` (nombre del DbSet puede quedar igual o cambiarse a `SeguimientosDiariosLevante`) |

### Servicios que leen/escriben en `seguimiento_diario`
| Archivo | Tipo de cambio |
|---------|---------------|
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioService.cs` | No cambia código — EF resuelve la tabla por configuración |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraFilterDataService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/DisponibilidadLoteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/IndicadoresProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionCierreLoteLevanteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaEcuadorService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LotePosturaLevanteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LoteSeguimientoService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LoteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoLoteLevanteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/TrasladoHuevosService.cs` | No cambia código |

> **Por qué los servicios no cambian:** EF Core resuelve el nombre de tabla en tiempo de compilación desde la configuración.
> Los servicios solo usan el DbSet — si el DbSet apunta a la entidad correcta, el SQL generado usa la tabla nueva automáticamente.

### Migración EF necesaria
```bash
dotnet ef migrations add RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevante \
  --project ../ZooSanMarino.Infrastructure \
  --startup-project . \
  --context ZooSanMarinoContext
```

Contenido esperado de la migración:
```csharp
migrationBuilder.RenameTable(
    name: "seguimiento_diario",
    newName: "seguimiento_diario_levante");
```

### ⚠️ Decisión pendiente
**¿Esta tabla también guarda datos de `TipoSeguimiento = 'engorde'`?**
Si sí → el nombre `seguimiento_diario_levante` es impreciso.
Verificar en BD antes de ejecutar:
```sql
SELECT tipo_seguimiento, COUNT(*) 
FROM seguimiento_diario 
GROUP BY tipo_seguimiento;
```

---

## 📁 TABLA 2 — `produccion_diaria` → `seguimiento_diario_produccion_reproductoras`

**Entidad activa:** `SeguimientoProduccion` (NO es `ProduccionDiaria` que está ignorada)  
**Archivos a tocar: 22**

### Dominio
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Domain/Entities/SeguimientoProduccion.cs` | Cambiar `[Table("produccion_diaria")]` → `[Table("seguimiento_diario_produccion_reproductoras")]` |
| `backend/src/ZooSanMarino.Domain/Entities/ProduccionDiaria.cs` | ⚠️ Está IGNORADA en EF — verificar si debe eliminarse o mantenerse |
| `backend/src/ZooSanMarino.Domain/Entities/ProduccionDiariaLote.cs` | Verificar si referencia la tabla directamente |

### Persistencia — Configuraciones EF
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SeguimientoProduccionConfiguration.cs` | Actualizar `.ToTable("produccion_diaria")` → `.ToTable("seguimiento_diario_produccion_reproductoras")` |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionDiariaConfiguration.cs` | ⚠️ Ignorada — verificar si puede eliminarse |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/ZooSanMarinoContext.cs` | Actualizar DbSet de `SeguimientoProduccion` (el DbSet `ProduccionDiaria` ya está comentado) |

### Servicios que leen/escriben en `produccion_diaria`
| Archivo | Tipo de cambio |
|---------|---------------|
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoProduccionService.cs` | No cambia código — EF resuelve |
| `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionDiariaService.cs` | ⚠️ Usa entidad ignorada `ProduccionDiaria` — evaluar si mantener o migrar a `SeguimientoProduccion` |
| `backend/src/ZooSanMarino.Infrastructure/Services/EspejoHuevoProduccionSyncService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/IndicadoresProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/LotePosturaLevanteService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/MovimientoAvesService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs` | No cambia código |
| `backend/src/ZooSanMarino.Infrastructure/Services/TrasladoHuevosService.cs` | No cambia código |

### Controllers que exponen endpoints de esta tabla
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.API/Controllers/SeguimientoProduccionController.cs` | Sin cambio en código |
| `backend/src/ZooSanMarino.API/Controllers/ProduccionDiariaController.cs` | ⚠️ Usa entidad ignorada — evaluar |
| `backend/src/ZooSanMarino.API/Controllers/ReporteTecnicoProduccionController.cs` | Sin cambio |
| `backend/src/ZooSanMarino.API/Controllers/MovimientoAvesController.cs` | Sin cambio |
| `backend/src/ZooSanMarino.API/Controllers/TrasladoNavigationController.cs` | Sin cambio |
| `backend/src/ZooSanMarino.API/Controllers/TrasladosController.cs` | Sin cambio |
| `backend/src/ZooSanMarino.API/Controllers/DashboardController.cs` | Sin cambio |

### Migración EF necesaria
```bash
dotnet ef migrations add RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras \
  --project ../ZooSanMarino.Infrastructure \
  --startup-project . \
  --context ZooSanMarinoContext
```

Contenido esperado:
```csharp
migrationBuilder.RenameTable(
    name: "produccion_diaria",
    newName: "seguimiento_diario_produccion_reproductoras");
```

### ⚠️ Problema pendiente: `ProduccionDiariaService` y `ProduccionDiariaController`

Estos dos archivos usan la entidad `ProduccionDiaria` que está **ignorada en EF**.
Necesitamos entender antes de ejecutar: ¿cómo leen datos hoy? ¿usan raw SQL o una abstracción?
```
Verificar en ProduccionDiariaService.cs cómo accede a produccion_diaria
```

---

## 📁 TABLA 3 — `produccion_avicola_raw` → `guia_genetica_sanmarino_colombia`

**Entidad activa:** `ProduccionAvicolaRaw`  
**Archivos a tocar: 23**  
**Estado:** ✅ Rename semánticamente correcto y justificado

### Dominio
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Domain/Entities/ProduccionAvicolaRaw.cs` | Renombrar clase → `GuiaGeneticaSanmarinoColombiaStandard` + cambiar `[Table]` attribute |

### Persistencia — Configuraciones EF
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ProduccionAvicolaRawConfiguration.cs` | Renombrar archivo + clase + actualizar `.ToTable("guia_genetica_sanmarino_colombia")` |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/ZooSanMarinoContext.cs` | Cambiar DbSet: `DbSet<GuiaGeneticaSanmarinoColombiaStandard> GuiasGeneticasSanmarinoColombiaStandard` |

### Interfaces (renombrar + actualizar tipo)
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Application/Interfaces/IProduccionAvicolaRawService.cs` | Renombrar → `IGuiaGeneticaService.cs`, actualizar tipo de retorno |
| `backend/src/ZooSanMarino.Application/Interfaces/IExcelImportService.cs` | Actualizar parámetros que referencian `ProduccionAvicolaRaw` |
| `backend/src/ZooSanMarino.Application/Interfaces/ILiquidacionTecnicaComparacionService.cs` | Actualizar referencias |

### DTOs (renombrar o añadir alias)
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Application/DTOs/ProduccionAvicolaRawDto.cs` | Renombrar clase → `GuiaGeneticaDto` |
| `backend/src/ZooSanMarino.Application/DTOs/ProduccionAvicolaRawFilterOptionsDto.cs` | Renombrar clase → `GuiaGeneticaFilterOptionsDto` |
| `backend/src/ZooSanMarino.Application/DTOs/ExcelImportDto.cs` | Actualizar referencias internas |
| `backend/src/ZooSanMarino.Application/DTOs/ReporteDiarioGalponDto.cs` | Verificar si solo usa nombre en comentarios |

### Servicios
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.Infrastructure/Services/ProduccionAvicolaRawService.cs` | Renombrar → `GuiaGeneticaService.cs`, actualizar clase, referencias a entidad y DTO |
| `backend/src/ZooSanMarino.Infrastructure/Services/ExcelImportService.cs` | Actualizar tipo `ProduccionAvicolaRaw` → `GuiaGeneticaSanmarinoColombiaStandard` |
| `backend/src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaService.cs` | ⚠️ Ya existe este archivo — verificar si hay colisión de nombres |
| `backend/src/ZooSanMarino.Infrastructure/Services/IndicadoresProduccionService.cs` | Actualizar referencias a entidad |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionCierreLoteLevanteService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaComparacionService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaEcuadorService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaProduccionService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LoteAveEngordeService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/LoteService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs` | Actualizar referencias |
| `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs` | Actualizar referencias |

### Controllers
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.API/Controllers/ProduccionAvicolaRawController.cs` | Renombrar → `GuiaGeneticaController.cs`, actualizar clase e inyección de servicio |
| `backend/src/ZooSanMarino.API/Controllers/ExcelImportController.cs` | Actualizar referencias al tipo |
| `backend/src/ZooSanMarino.API/Controllers/LiquidacionTecnicaComparacionController.cs` | Actualizar referencias |

### DI en Program.cs
| Archivo | Cambio requerido |
|---------|-----------------|
| `backend/src/ZooSanMarino.API/Program.cs` | Cambiar: `AddScoped<IProduccionAvicolaRawService, ProduccionAvicolaRawService>()` → `AddScoped<IGuiaGeneticaService, GuiaGeneticaService>()` |

> **⚠️ Colisión con `GuiaGeneticaService.cs` existente:**
> Ya existe un archivo `GuiaGeneticaService.cs` en Infrastructure/Services.
> Verificar su contenido antes de ejecutar este rename — puede necesitar merge o un nombre alternativo.

### Migración EF necesaria
```bash
dotnet ef migrations add RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica \
  --project ../ZooSanMarino.Infrastructure \
  --startup-project . \
  --context ZooSanMarinoContext
```

Contenido esperado:
```csharp
migrationBuilder.RenameTable(
    name: "produccion_avicola_raw",
    newName: "guia_genetica_sanmarino_colombia");
```

---

## 🔍 Preguntas de validación ANTES de ejecutar cada tabla

### Para Tabla 1 (`seguimiento_diario`)
```sql
-- ¿Qué tipos de seguimiento existen realmente en BD?
SELECT tipo_seguimiento, COUNT(*) 
FROM seguimiento_diario 
GROUP BY tipo_seguimiento;
-- Si hay 'engorde', el rename a "levante" es impreciso
```

### Para Tabla 2 (`produccion_diaria`)
```sql
-- Confirmar que produccion_diaria es la tabla correcta
SELECT COUNT(*) FROM produccion_diaria;
-- Ver qué FK usa más
SELECT lote_postura_produccion_id IS NOT NULL as usa_lpp, COUNT(*)
FROM produccion_diaria
GROUP BY usa_lpp;
```

```bash
# ¿ProduccionDiariaService usa EF o raw SQL?
grep -n "produccion_diaria\|_context\|FromSql\|ExecuteSql" \
  backend/src/ZooSanMarino.Infrastructure/Services/ProduccionDiariaService.cs
```

### Para Tabla 3 (`produccion_avicola_raw`)
```bash
# ¿Qué hace GuiaGeneticaService.cs ya existente?
cat backend/src/ZooSanMarino.Infrastructure/Services/GuiaGeneticaService.cs | head -50
```

---

## 📋 Orden de ejecución recomendado

| Orden | Tabla | Razón |
|-------|-------|-------|
| 1° | `produccion_avicola_raw` → `guia_genetica_sanmarino_colombia` | Rename más limpio, sin ambigüedades de polimorfismo |
| 2° | `produccion_diaria` → `seguimiento_diario_produccion_reproductoras` | Requiere resolver duda de `ProduccionDiariaService` primero |
| 3° | `seguimiento_diario` → `seguimiento_diario_levante` | Último — mayor impacto, requiere validar datos por `TipoSeguimiento` |

---

## 📊 Resumen de archivos por tabla

| Tabla | Dominio | Configuración EF | Servicios | Controllers | DTOs | Interfaces | Total |
|-------|---------|-----------------|-----------|-------------|------|------------|-------|
| `seguimiento_diario` | 3 | 4 | 21 | 4 | 4 | 3 | **39** |
| `produccion_diaria` | 3 | 3 | 13 | 7 | 5 | 4 | **35** |
| `produccion_avicola_raw` | 1 | 2 | 13 | 3 | 4 | 3 | **26** |
| **TOTAL** | | | | | | | **~100** |

> Los servicios en tablas 1 y 2 en su mayoría **no necesitan cambios de código**
> (EF resuelve la tabla desde configuración). Solo se tocan Entidad + Configuración EF + Migración.

---

## ✅ Checklist de implementación (por tabla)

### Tabla 3 — `produccion_avicola_raw` ✅ COMPLETA (2026-05-07)
- [x] Verificar contenido de `GuiaGeneticaService.cs` existente — colisión descartada (es servicio de consulta, no CRUD)
- [x] Actualizar `.ToTable("guia_genetica_sanmarino_colombia")` en `ProduccionAvicolaRawConfiguration.cs`
- [x] Actualizar índices: `ix_produccion_avicola_raw_*` → `ix_guia_genetica_sanmarino_colombia_*`
- [x] Migración EF creada: `20260507174154_RenameTable_ProduccionAvicolaRaw_to_GuiaGenetica`
- [x] `dotnet build` → 0 errores
- [ ] Aplicar migración a BD (pendiente Docker/deploy)
- **Nota:** Clases C# (`ProduccionAvicolaRaw`, servicios, DTOs) se mantienen sin renombrar para evitar colisión con `GuiaGeneticaService` existente

### Tabla 2 — `produccion_diaria` ✅ COMPLETA (2026-05-07)
- [x] Actualizar `.ToTable("seguimiento_diario_produccion_reproductoras")` en `SeguimientoProduccionConfiguration.cs`
- [x] Actualizar índice: `ix_produccion_diaria_lote_postura_produccion_id` → `ix_seguimiento_diario_produccion_reproductoras_lpp_id`
- [x] Migración EF creada: `20260507181055_RenameTable_ProduccionDiaria_to_SeguimientoDiarioProduccionReproductoras`
- [x] `dotnet build` → 0 errores
- [ ] Aplicar migración a BD (pendiente Docker/deploy)
- **Nota:** Entidad activa era `SeguimientoProduccion` (no `ProduccionDiaria` que está ignorada en EF) — sin cambio de clases

### Tabla 1 — `seguimiento_diario` ✅ COMPLETA (2026-05-07)
- [x] Validado: `seguimiento_diario_aves_engorde` es tabla separada → rename a `levante_reproductoras` es semánticamente correcto
- [x] Actualizar `.ToTable("seguimiento_diario_levante_reproductoras", "public")` en `SeguimientoDiarioConfiguration.cs`
- [x] Migración EF creada: `RenameTable_SeguimientoDiario_to_SeguimientoDiarioLevanteReproductoras`
- [x] `dotnet build` → 0 errores
- [ ] Aplicar migración a BD (pendiente Docker/deploy)

---

**Última actualización:** 2026-05-07 — Las 3 migraciones generadas y build backend ✅ 0 errores.  
**Próximo paso:** Levantar Docker + `dotnet ef database update` para aplicar las 3 migraciones a BD local. En producción aplicar en deploy.
