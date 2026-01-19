# 📊 ANÁLISIS COMPLETO: IMPLEMENTACIÓN DE REPORTE TÉCNICO DE LEVANTE CON TABS

## 🎯 OBJETIVO GENERAL

Modificar el módulo de **Reporte Técnico de Levante** para implementar una estructura con **tres tabs** que permita visualizar:
1. **Tab 1: Reporte Diario Machos** - Datos diarios específicos de machos
2. **Tab 2: Reporte Diario Hembras** - Datos diarios específicos de hembras  
3. **Tab 3: Reporte Semanal** - Datos semanales consolidados (hembras + machos + guía genética)

---

## 📋 1. ANÁLISIS DE LA ESTRUCTURA ACTUAL

### 1.1. Backend - Servicios y DTOs Existentes

#### ✅ **Servicio: `ReporteTecnicoService`**
**Ubicación**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs`

**Métodos Clave Existentes:**
- `ObtenerDatosDiariosLevanteAsync()` - ✅ Obtiene datos diarios desde `SeguimientoLoteLevante`
- `GenerarReporteLevanteCompletoAsync()` - ✅ Genera reporte semanal completo (25 semanas)
- `ConsolidarSemanales()` - ✅ Consolida datos diarios en semanas

**Análisis:**
- ✅ Ya tiene acceso a datos separados por hembras/machos
- ✅ Ya calcula acumulados correctamente
- ✅ Ya maneja traslados (valores negativos en SelH/SelM)
- ⚠️ **Falta**: Métodos específicos para reportes diarios separados (machos/hembras)

#### ✅ **DTOs Existentes:**

**`ReporteTecnicoDiarioDto`** (Actual - Consolidado)
- Contiene datos totales (hembras + machos juntos)
- **Problema**: No separa por sexo
- **Uso actual**: Reporte diario estándar

**`ReporteTecnicoLevanteSemanalDto`** (✅ Perfecto)
- Contiene datos semanales **separados por hembras y machos**
- Incluye todos los campos necesarios
- Incluye comparación con guía genética
- **✅ Este DTO ya está completo y funcional**

### 1.2. Frontend - Componentes Existentes

#### ✅ **Componente Principal: `ReporteTecnicoMainComponent`**
**Ubicación**: `frontend/src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/`

**Estado Actual:**
- ✅ Tiene tabs para alternar entre Levante y Producción
- ✅ Tiene toggle para modo "Completo" (reporte semanal completo)
- ✅ Maneja filtros (granja, núcleo, galpón, lote)
- ✅ Maneja fechas y tipo de reporte
- ⚠️ **Falta**: Tabs internos para Diario Machos / Diario Hembras / Semanal

#### ✅ **Componentes de Tabla Existentes:**

**`TablaDatosDiariosComponent`**
- Muestra datos diarios consolidados (hembras + machos)
- **Uso**: Reporte diario estándar actual
- **Modificación necesaria**: Crear versiones separadas para machos y hembras

**`TablaLevanteCompletaComponent`**
- Muestra datos semanales completos (estructura Excel)
- **✅ Este componente ya funciona perfectamente para el Tab 3**

### 1.3. API - Controlador Existente

#### ✅ **`ReporteTecnicoController`**
**Ubicación**: `backend/src/ZooSanMarino.API/Controllers/ReporteTecnicoController.cs`

**Endpoints Existentes:**
- `GET /api/ReporteTecnico/diario/sublote/{loteId}` - Reporte diario sublote
- `GET /api/ReporteTecnico/diario/consolidado` - Reporte diario consolidado
- `GET /api/ReporteTecnico/semanal/sublote/{loteId}` - Reporte semanal sublote
- `GET /api/ReporteTecnico/semanal/consolidado` - Reporte semanal consolidado
- `GET /api/ReporteTecnico/levante/completo/{loteId}` - Reporte completo (25 semanas)
- `POST /api/ReporteTecnico/generar` - Genera reporte según parámetros

**Análisis:**
- ✅ Estructura de endpoints bien definida
- ⚠️ **Falta**: Endpoint específico para reporte con tabs

---

## 🛠️ 2. IMPLEMENTACIÓN DETALLADA - BACKEND

### 2.1. Nuevos DTOs a Crear

#### 2.1.1. `ReporteTecnicoDiarioMachosDto.cs`

**Ubicación**: `backend/src/ZooSanMarino.Application/DTOs/ReporteTecnicoDiarioMachosDto.cs`

```csharp
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para datos diarios de reporte técnico específico de MACHOS
/// </summary>
public class ReporteTecnicoDiarioMachosDto
{
    // ========== IDENTIFICACIÓN ==========
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    
    // ========== SALDO ACTUAL ==========
    public int SaldoMachos { get; set; } // Saldo actual de machos
    
    // ========== MORTALIDAD ==========
    public int MortalidadMachos { get; set; } // Mortalidad diaria
    public int MortalidadMachosAcumulada { get; set; } // Mortalidad acumulada desde inicio
    public decimal MortalidadMachosPorcentajeDiario { get; set; } // % sobre aves antes de mortalidad
    public decimal MortalidadMachosPorcentajeAcumulado { get; set; } // % sobre aves iniciales
    
    // ========== SELECCIÓN/RETIRO (Valores Positivos) ==========
    public int SeleccionMachos { get; set; } // Solo valores positivos de SelM
    public int SeleccionMachosAcumulada { get; set; }
    public decimal SeleccionMachosPorcentajeDiario { get; set; }
    public decimal SeleccionMachosPorcentajeAcumulado { get; set; }
    
    // ========== TRASLADOS (Valores Negativos en Valor Absoluto) ==========
    public int TrasladosMachos { get; set; } // Valores negativos de SelM en valor absoluto
    public int TrasladosMachosAcumulados { get; set; }
    
    // ========== ERROR DE SEXAJE ==========
    public int ErrorSexajeMachos { get; set; } // Error sexaje diario
    public int ErrorSexajeMachosAcumulado { get; set; } // Error sexaje acumulado
    public decimal ErrorSexajeMachosPorcentajeDiario { get; set; }
    public decimal ErrorSexajeMachosPorcentajeAcumulado { get; set; }
    
    // ========== CONSUMO DE ALIMENTO ==========
    public decimal ConsumoKgMachos { get; set; } // Consumo diario en kg
    public decimal ConsumoKgMachosAcumulado { get; set; } // Consumo acumulado en kg
    public decimal ConsumoGramosPorAveMachos { get; set; } // Gramos por ave por día
    
    // ========== PESO Y UNIFORMIDAD ==========
    public decimal? PesoPromedioMachos { get; set; } // Peso promedio en kg
    public decimal? UniformidadMachos { get; set; } // Uniformidad (%)
    public decimal? CoeficienteVariacionMachos { get; set; } // Coeficiente de variación (%)
    public decimal? GananciaPesoMachos { get; set; } // Ganancia de peso vs día anterior
    
    // ========== VALORES NUTRICIONALES ==========
    public double? KcalAlMachos { get; set; } // Kcal por kg de alimento
    public double? ProtAlMachos { get; set; } // % Proteína en alimento
    public double? KcalAveMachos { get; set; } // Kcal por ave por día
    public double? ProtAveMachos { get; set; } // Proteína por ave por día
    
    // ========== OBSERVACIONES ==========
    public string? Observaciones { get; set; }
}
```

#### 2.1.2. `ReporteTecnicoDiarioHembrasDto.cs`

**Ubicación**: `backend/src/ZooSanMarino.Application/DTOs/ReporteTecnicoDiarioHembrasDto.cs`

```csharp
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para datos diarios de reporte técnico específico de HEMBRAS
/// </summary>
public class ReporteTecnicoDiarioHembrasDto
{
    // ========== IDENTIFICACIÓN ==========
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    
    // ========== SALDO ACTUAL ==========
    public int SaldoHembras { get; set; } // Saldo actual de hembras
    
    // ========== MORTALIDAD ==========
    public int MortalidadHembras { get; set; }
    public int MortalidadHembrasAcumulada { get; set; }
    public decimal MortalidadHembrasPorcentajeDiario { get; set; }
    public decimal MortalidadHembrasPorcentajeAcumulado { get; set; }
    
    // ========== SELECCIÓN/RETIRO ==========
    public int SeleccionHembras { get; set; } // Solo valores positivos de SelH
    public int SeleccionHembrasAcumulada { get; set; }
    public decimal SeleccionHembrasPorcentajeDiario { get; set; }
    public decimal SeleccionHembrasPorcentajeAcumulado { get; set; }
    
    // ========== TRASLADOS ==========
    public int TrasladosHembras { get; set; } // Valores negativos de SelH en valor absoluto
    public int TrasladosHembrasAcumulados { get; set; }
    
    // ========== ERROR DE SEXAJE ==========
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeHembrasAcumulado { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeDiario { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeAcumulado { get; set; }
    
    // ========== CONSUMO DE ALIMENTO ==========
    public decimal ConsumoKgHembras { get; set; }
    public decimal ConsumoKgHembrasAcumulado { get; set; }
    public decimal ConsumoGramosPorAveHembras { get; set; }
    
    // ========== PESO Y UNIFORMIDAD ==========
    public decimal? PesoPromedioHembras { get; set; }
    public decimal? UniformidadHembras { get; set; }
    public decimal? CoeficienteVariacionHembras { get; set; }
    public decimal? GananciaPesoHembras { get; set; }
    
    // ========== VALORES NUTRICIONALES ==========
    public double? KcalAlHembras { get; set; }
    public double? ProtAlHembras { get; set; }
    public double? KcalAveHembras { get; set; } // Puede venir del seguimiento o calcularse
    public double? ProtAveHembras { get; set; } // Puede venir del seguimiento o calcularse
    
    // ========== OBSERVACIONES ==========
    public string? Observaciones { get; set; }
}
```

#### 2.1.3. `ReporteTecnicoLevanteConTabsDto.cs`

**Ubicación**: `backend/src/ZooSanMarino.Application/DTOs/ReporteTecnicoLevanteConTabsDto.cs`

```csharp
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO completo para reporte técnico de Levante con estructura de tabs
/// Incluye datos diarios separados (machos y hembras) y datos semanales completos
/// </summary>
public class ReporteTecnicoLevanteConTabsDto
{
    public ReporteTecnicoLoteInfoDto InformacionLote { get; set; } = new();
    
    // Tab 1: Diario Machos
    public List<ReporteTecnicoDiarioMachosDto> DatosDiariosMachos { get; set; } = new();
    
    // Tab 2: Diario Hembras
    public List<ReporteTecnicoDiarioHembrasDto> DatosDiariosHembras { get; set; } = new();
    
    // Tab 3: Semanal (reutiliza DTO existente)
    public List<ReporteTecnicoLevanteSemanalDto> DatosSemanales { get; set; } = new();
    
    public bool EsConsolidado { get; set; }
    public List<string> SublotesIncluidos { get; set; } = new();
}
```

### 2.2. Nuevos Métodos en `ReporteTecnicoService`

#### 2.2.1. `GenerarReporteDiarioMachosAsync`

**Implementación Completa:**

```csharp
/// <summary>
/// Genera reporte diario específico de MACHOS desde el seguimiento diario de levante
/// </summary>
public async Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
    int loteId,
    DateTime? fechaInicio = null,
    DateTime? fechaFin = null,
    CancellationToken ct = default)
{
    // Obtener lote para información inicial
    var lote = await _ctx.Lotes
        .AsNoTracking()
        .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);
    
    if (lote == null)
        throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");
    
    if (!lote.FechaEncaset.HasValue)
        throw new InvalidOperationException($"El lote {loteId} no tiene fecha de encaset");
    
    var machosIniciales = lote.MachosL ?? 0;
    
    // Obtener todos los registros de seguimiento (para cálculos acumulados correctos)
    var queryTodos = _ctx.SeguimientoLoteLevante
        .AsNoTracking()
        .Where(s => s.LoteId == loteId)
        .OrderBy(s => s.FechaRegistro);
    
    var todosSeguimientos = await queryTodos.ToListAsync(ct);
    
    // Filtrar solo semanas de levante (1-25)
    todosSeguimientos = todosSeguimientos.Where(seg =>
    {
        var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
        var edadSemanas = CalcularEdadSemanas(edadDias);
        return edadSemanas <= 25;
    }).ToList();
    
    // Aplicar filtros de fecha
    var queryFiltrado = todosSeguimientos.AsQueryable();
    if (fechaInicio.HasValue)
        queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);
    if (fechaFin.HasValue)
        queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);
    
    var seguimientos = queryFiltrado.ToList();
    
    // Variables acumuladas
    var datosMachos = new List<ReporteTecnicoDiarioMachosDto>();
    var mortalidadAcumulada = 0;
    var seleccionAcumulada = 0;
    var trasladosAcumulados = 0;
    var errorSexajeAcumulado = 0;
    var consumoAcumulado = 0m;
    decimal? pesoAnterior = null;
    
    // Procesar cada registro diario
    foreach (var seg in seguimientos)
    {
        var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
        var edadSemanas = CalcularEdadSemanas(edadDias);
        
        // Calcular acumulados hasta esta fecha (incluyendo todos los registros anteriores)
        var registrosHastaFecha = todosSeguimientos
            .Where(s => s.FechaRegistro <= seg.FechaRegistro)
            .ToList();
        
        // Calcular saldo actual de machos
        var machosActuales = machosIniciales;
        var machosAntesMortalidad = machosIniciales; // Para calcular % mortalidad diario correctamente
        
        foreach (var reg in registrosHastaFecha)
        {
            // Guardar aves antes de aplicar mortalidad del registro actual
            if (reg.Id == seg.Id)
            {
                machosAntesMortalidad = machosActuales;
            }
            
            // Aplicar mortalidad
            machosActuales -= reg.MortalidadMachos;
            
            // Separar selección normal de traslados
            var selM = reg.SelM;
            var seleccionNormal = Math.Max(0, selM);
            var traslados = Math.Abs(Math.Min(0, selM));
            
            machosActuales -= seleccionNormal;
            machosActuales -= traslados;
            
            // Error de sexaje no afecta el saldo (solo es corrección)
        }
        
        // Calcular valores del día actual
        var mortalidad = seg.MortalidadMachos;
        mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadMachos);
        
        var selM = seg.SelM;
        var seleccionNormal = Math.Max(0, selM);
        var traslados = Math.Abs(Math.Min(0, selM));
        seleccionAcumulada = registrosHastaFecha.Sum(s => Math.Max(0, s.SelM));
        trasladosAcumulados = registrosHastaFecha.Sum(s => Math.Abs(Math.Min(0, s.SelM)));
        
        var errorSexaje = seg.ErrorSexajeMachos;
        errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeMachos);
        
        var consumo = (decimal)(seg.ConsumoKgMachos ?? 0);
        consumoAcumulado = registrosHastaFecha.Sum(s => (decimal)(s.ConsumoKgMachos ?? 0));
        var consumoGramosPorAve = machosActuales > 0 ? (consumo * 1000) / machosActuales : 0;
        
        // Peso y ganancia
        var pesoActual = (decimal?)(seg.PesoPromM);
        var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
            ? pesoActual.Value - pesoAnterior.Value 
            : (decimal?)null;
        
        // Valores nutricionales
        var kcalAl = seg.KcalAlH; // Mismo alimento para machos y hembras
        var protAl = seg.ProtAlH;
        var kcalAve = machosActuales > 0 && kcalAl.HasValue 
            ? (kcalAl.Value * (double)consumo) / machosActuales 
            : (double?)null;
        var protAve = machosActuales > 0 && protAl.HasValue 
            ? (protAl.Value * (double)consumo) / machosActuales 
            : (double?)null;
        
        var dto = new ReporteTecnicoDiarioMachosDto
        {
            Fecha = seg.FechaRegistro,
            EdadDias = edadDias,
            EdadSemanas = edadSemanas,
            SaldoMachos = machosActuales,
            MortalidadMachos = mortalidad,
            MortalidadMachosAcumulada = mortalidadAcumulada,
            MortalidadMachosPorcentajeDiario = machosAntesMortalidad > 0 
                ? (decimal)mortalidad / machosAntesMortalidad * 100 
                : 0,
            MortalidadMachosPorcentajeAcumulado = machosIniciales > 0 
                ? (decimal)mortalidadAcumulada / machosIniciales * 100 
                : 0,
            SeleccionMachos = seleccionNormal,
            SeleccionMachosAcumulada = seleccionAcumulada,
            SeleccionMachosPorcentajeDiario = machosActuales > 0 
                ? (decimal)seleccionNormal / machosActuales * 100 
                : 0,
            SeleccionMachosPorcentajeAcumulado = machosIniciales > 0 
                ? (decimal)seleccionAcumulada / machosIniciales * 100 
                : 0,
            TrasladosMachos = traslados,
            TrasladosMachosAcumulados = trasladosAcumulados,
            ErrorSexajeMachos = errorSexaje,
            ErrorSexajeMachosAcumulado = errorSexajeAcumulado,
            ErrorSexajeMachosPorcentajeDiario = machosActuales > 0 
                ? (decimal)errorSexaje / machosActuales * 100 
                : 0,
            ErrorSexajeMachosPorcentajeAcumulado = machosIniciales > 0 
                ? (decimal)errorSexajeAcumulado / machosIniciales * 100 
                : 0,
            ConsumoKgMachos = consumo,
            ConsumoKgMachosAcumulado = consumoAcumulado,
            ConsumoGramosPorAveMachos = consumoGramosPorAve,
            PesoPromedioMachos = pesoActual,
            UniformidadMachos = (decimal?)(seg.UniformidadM),
            CoeficienteVariacionMachos = (decimal?)(seg.CvM),
            GananciaPesoMachos = gananciaPeso,
            KcalAlMachos = kcalAl,
            ProtAlMachos = protAl,
            KcalAveMachos = kcalAve,
            ProtAveMachos = protAve,
            Observaciones = seg.Observaciones
        };
        
        if (pesoActual.HasValue)
            pesoAnterior = pesoActual;
        
        datosMachos.Add(dto);
    }
    
    return datosMachos;
}
```

#### 2.2.2. `GenerarReporteDiarioHembrasAsync`

Similar a `GenerarReporteDiarioMachosAsync` pero usando datos de hembras:
- `MortalidadHembras` en lugar de `MortalidadMachos`
- `SelH` en lugar de `SelM`
- `ErrorSexajeHembras` en lugar de `ErrorSexajeMachos`
- `ConsumoKgHembras` en lugar de `ConsumoKgMachos`
- `PesoPromH` en lugar de `PesoPromM`
- `UniformidadH` en lugar de `UniformidadM`
- `CvH` en lugar de `CvM`
- `HembrasL` en lugar de `MachosL` para aves iniciales
- `KcalAveH` y `ProtAveH` pueden venir directamente del seguimiento o calcularse

#### 2.2.3. `GenerarReporteLevanteConTabsAsync`

```csharp
/// <summary>
/// Genera reporte técnico de Levante con estructura de tabs
/// Incluye datos diarios separados (machos y hembras) y datos semanales completos
/// </summary>
public async Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
    int loteId,
    DateTime? fechaInicio = null,
    DateTime? fechaFin = null,
    bool consolidarSublotes = false,
    CancellationToken ct = default)
{
    var lote = await _ctx.Lotes
        .AsNoTracking()
        .Include(l => l.Farm)
        .Include(l => l.Nucleo)
        .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);
    
    if (lote == null)
        throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");
    
    var infoLote = MapearInformacionLote(lote);
    var sublote = ExtraerSublote(lote.LoteNombre);
    infoLote.Sublote = sublote;
    infoLote.Etapa = "LEVANTE";
    
    // Generar datos para cada tab
    var datosDiariosMachos = await GenerarReporteDiarioMachosAsync(loteId, fechaInicio, fechaFin, ct);
    var datosDiariosHembras = await GenerarReporteDiarioHembrasAsync(loteId, fechaInicio, fechaFin, ct);
    
    // Reutilizar método existente para datos semanales
    var reporteCompleto = await GenerarReporteLevanteCompletoAsync(loteId, consolidarSublotes, ct);
    
    return new ReporteTecnicoLevanteConTabsDto
    {
        InformacionLote = infoLote,
        DatosDiariosMachos = datosDiariosMachos,
        DatosDiariosHembras = datosDiariosHembras,
        DatosSemanales = reporteCompleto.DatosSemanales,
        EsConsolidado = consolidarSublotes,
        SublotesIncluidos = consolidarSublotes 
            ? reporteCompleto.SublotesIncluidos 
            : new List<string> { sublote ?? "Sin sublote" }
    };
}
```

### 2.3. Actualizar Interfaz `IReporteTecnicoService`

**Ubicación**: `backend/src/ZooSanMarino.Application/Interfaces/IReporteTecnicoService.cs`

```csharp
public interface IReporteTecnicoService
{
    // ... métodos existentes ...
    
    // Nuevos métodos para tabs
    Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);
    
    Task<List<ReporteTecnicoDiarioHembrasDto>> GenerarReporteDiarioHembrasAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default);
    
    Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        bool consolidarSublotes = false,
        CancellationToken ct = default);
}
```

### 2.4. Nuevo Endpoint en `ReporteTecnicoController`

```csharp
/// <summary>
/// Genera reporte técnico de Levante con estructura de tabs
/// Incluye datos diarios separados (machos y hembras) y datos semanales completos
/// </summary>
[HttpGet("levante/tabs/{loteId}")]
public async Task<ActionResult<ReporteTecnicoLevanteConTabsDto>> GetReporteLevanteConTabs(
    int loteId,
    [FromQuery] DateTime? fechaInicio = null,
    [FromQuery] DateTime? fechaFin = null,
    [FromQuery] bool consolidarSublotes = false,
    CancellationToken ct = default)
{
    try
    {
        var reporte = await _service.GenerarReporteLevanteConTabsAsync(
            loteId, 
            fechaInicio, 
            fechaFin, 
            consolidarSublotes, 
            ct);
        return Ok(reporte);
    }
    catch (InvalidOperationException ex)
    {
        return NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error al generar reporte técnico con tabs para lote {LoteId}", loteId);
        return StatusCode(500, new { message = "Error interno del servidor" });
    }
}
```

---

## 🎨 3. IMPLEMENTACIÓN DETALLADA - FRONTEND

### 3.1. Actualizar Servicio Frontend

**Ubicación**: `frontend/src/app/features/reportes-tecnicos/services/reporte-tecnico.service.ts`

#### 3.1.1. Agregar Interfaces TypeScript

```typescript
// Interfaces para nuevos DTOs
export interface ReporteTecnicoDiarioMachosDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  saldoMachos: number;
  mortalidadMachos: number;
  mortalidadMachosAcumulada: number;
  mortalidadMachosPorcentajeDiario: number;
  mortalidadMachosPorcentajeAcumulado: number;
  seleccionMachos: number;
  seleccionMachosAcumulada: number;
  seleccionMachosPorcentajeDiario: number;
  seleccionMachosPorcentajeAcumulado: number;
  trasladosMachos: number;
  trasladosMachosAcumulados: number;
  errorSexajeMachos: number;
  errorSexajeMachosAcumulado: number;
  errorSexajeMachosPorcentajeDiario: number;
  errorSexajeMachosPorcentajeAcumulado: number;
  consumoKgMachos: number;
  consumoKgMachosAcumulado: number;
  consumoGramosPorAveMachos: number;
  pesoPromedioMachos?: number | null;
  uniformidadMachos?: number | null;
  coeficienteVariacionMachos?: number | null;
  gananciaPesoMachos?: number | null;
  kcalAlMachos?: number | null;
  protAlMachos?: number | null;
  kcalAveMachos?: number | null;
  protAveMachos?: number | null;
  observaciones?: string | null;
}

export interface ReporteTecnicoDiarioHembrasDto {
  fecha: string;
  edadDias: number;
  edadSemanas: number;
  saldoHembras: number;
  mortalidadHembras: number;
  mortalidadHembrasAcumulada: number;
  mortalidadHembrasPorcentajeDiario: number;
  mortalidadHembrasPorcentajeAcumulado: number;
  seleccionHembras: number;
  seleccionHembrasAcumulada: number;
  seleccionHembrasPorcentajeDiario: number;
  seleccionHembrasPorcentajeAcumulado: number;
  trasladosHembras: number;
  trasladosHembrasAcumulados: number;
  errorSexajeHembras: number;
  errorSexajeHembrasAcumulado: number;
  errorSexajeHembrasPorcentajeDiario: number;
  errorSexajeHembrasPorcentajeAcumulado: number;
  consumoKgHembras: number;
  consumoKgHembrasAcumulado: number;
  consumoGramosPorAveHembras: number;
  pesoPromedioHembras?: number | null;
  uniformidadHembras?: number | null;
  coeficienteVariacionHembras?: number | null;
  gananciaPesoHembras?: number | null;
  kcalAlHembras?: number | null;
  protAlHembras?: number | null;
  kcalAveHembras?: number | null;
  protAveHembras?: number | null;
  observaciones?: string | null;
}

export interface ReporteTecnicoLevanteConTabsDto {
  informacionLote: ReporteTecnicoLoteInfoDto;
  datosDiariosMachos: ReporteTecnicoDiarioMachosDto[];
  datosDiariosHembras: ReporteTecnicoDiarioHembrasDto[];
  datosSemanales: ReporteTecnicoLevanteSemanalDto[];
  esConsolidado: boolean;
  sublotesIncluidos: string[];
}
```

#### 3.1.2. Agregar Método en el Servicio

```typescript
generarReporteLevanteConTabs(
  loteId: number,
  fechaInicio?: string,
  fechaFin?: string,
  consolidarSublotes: boolean = false
): Observable<ReporteTecnicoLevanteConTabsDto> {
  const params = new HttpParams()
    .set('loteId', loteId.toString())
    .set('consolidarSublotes', consolidarSublotes.toString())
    .appendIf(fechaInicio, 'fechaInicio', fechaInicio!)
    .appendIf(fechaFin, 'fechaFin', fechaFin!);
  
  return this.http.get<ReporteTecnicoLevanteConTabsDto>(
    `${this.apiUrl}/levante/tabs/${loteId}`,
    { params }
  );
}
```

### 3.2. Crear Componentes de Tabla

#### 3.2.1. `TablaDatosDiariosMachosComponent`

**Ubicación**: `frontend/src/app/features/reportes-tecnicos/components/tabla-datos-diarios-machos/`

**Archivo**: `tabla-datos-diarios-machos.component.ts`

```typescript
import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReporteTecnicoDiarioMachosDto } from '../../services/reporte-tecnico.service';

@Component({
  selector: 'app-tabla-datos-diarios-machos',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-datos-diarios-machos.component.html',
  styleUrls: ['./tabla-datos-diarios-machos.component.scss']
})
export class TablaDatosDiariosMachosComponent {
  @Input() datos: ReporteTecnicoDiarioMachosDto[] = [];

  formatNumber(value: number | null | undefined, decimals: number = 2): string {
    if (value === null || value === undefined) return '-';
    return value.toFixed(decimals);
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: 'short', 
      year: '2-digit' 
    });
  }

  getMortalidadClass(mortalidad: number): string {
    if (mortalidad > 10) return 'highlight-red';
    if (mortalidad > 5) return 'highlight-orange';
    if (mortalidad > 0) return 'highlight-yellow';
    return '';
  }

  getPorcentajeClass(porcentaje: number): string {
    if (porcentaje > 1) return 'highlight-red';
    if (porcentaje > 0.5) return 'highlight-orange';
    return '';
  }
}
```

**Archivo**: `tabla-datos-diarios-machos.component.html`

```html
<div class="tabla-container">
  <div class="table-responsive">
    <table class="tabla-datos">
      <thead>
        <tr>
          <th rowspan="2">FECHA</th>
          <th rowspan="2">EDAD (Días)</th>
          <th rowspan="2">EDAD (Sem.)</th>
          <th rowspan="2">SALDO MACHOS</th>
          <th colspan="4">MORTALIDAD</th>
          <th colspan="4">SELECCIÓN/RETIRO</th>
          <th colspan="2">TRASLADOS</th>
          <th colspan="4">ERROR SEXAJE</th>
          <th colspan="4">CONSUMO ALIMENTO</th>
          <th colspan="4">PESO Y UNIFORMIDAD</th>
          <th colspan="4">VALORES NUTRICIONALES</th>
          <th rowspan="2">OBSERVACIONES</th>
        </tr>
        <tr>
          <!-- Mortalidad -->
          <th>Diaria</th>
          <th>Acum.</th>
          <th>% Diario</th>
          <th>% Acum.</th>
          <!-- Selección -->
          <th>Diaria</th>
          <th>Acum.</th>
          <th>% Diario</th>
          <th>% Acum.</th>
          <!-- Traslados -->
          <th>Diario</th>
          <th>Acum.</th>
          <!-- Error Sexaje -->
          <th>Diario</th>
          <th>Acum.</th>
          <th>% Diario</th>
          <th>% Acum.</th>
          <!-- Consumo -->
          <th>Kg Diario</th>
          <th>Kg Acum.</th>
          <th>Gr/Ave/Día</th>
          <th>Acum. Gr/Ave</th>
          <!-- Peso -->
          <th>Peso (kg)</th>
          <th>Uniformidad</th>
          <th>CV (%)</th>
          <th>Ganancia</th>
          <!-- Nutricionales -->
          <th>Kcal/kg Al</th>
          <th>% Prot Al</th>
          <th>Kcal/Ave</th>
          <th>Prot/Ave</th>
        </tr>
      </thead>
      <tbody>
        <tr *ngFor="let dato of datos; trackBy: trackByFecha">
          <td>{{ formatDate(dato.fecha) }}</td>
          <td>{{ dato.edadDias }}</td>
          <td>{{ dato.edadSemanas }}</td>
          <td class="text-right"><strong>{{ dato.saldoMachos }}</strong></td>
          
          <!-- Mortalidad -->
          <td [class]="getMortalidadClass(dato.mortalidadMachos)" class="text-right">
            {{ dato.mortalidadMachos }}
          </td>
          <td class="text-right">{{ dato.mortalidadMachosAcumulada }}</td>
          <td [class]="getPorcentajeClass(dato.mortalidadMachosPorcentajeDiario)" class="text-right">
            {{ formatNumber(dato.mortalidadMachosPorcentajeDiario) }}%
          </td>
          <td class="text-right">{{ formatNumber(dato.mortalidadMachosPorcentajeAcumulado) }}%</td>
          
          <!-- Selección -->
          <td class="text-right">{{ dato.seleccionMachos }}</td>
          <td class="text-right">{{ dato.seleccionMachosAcumulada }}</td>
          <td class="text-right">{{ formatNumber(dato.seleccionMachosPorcentajeDiario) }}%</td>
          <td class="text-right">{{ formatNumber(dato.seleccionMachosPorcentajeAcumulado) }}%</td>
          
          <!-- Traslados -->
          <td class="text-right">{{ dato.trasladosMachos }}</td>
          <td class="text-right">{{ dato.trasladosMachosAcumulados }}</td>
          
          <!-- Error Sexaje -->
          <td class="text-right">{{ dato.errorSexajeMachos }}</td>
          <td class="text-right">{{ dato.errorSexajeMachosAcumulado }}</td>
          <td class="text-right">{{ formatNumber(dato.errorSexajeMachosPorcentajeDiario) }}%</td>
          <td class="text-right">{{ formatNumber(dato.errorSexajeMachosPorcentajeAcumulado) }}%</td>
          
          <!-- Consumo -->
          <td class="text-right">{{ formatNumber(dato.consumoKgMachos, 2) }}</td>
          <td class="text-right">{{ formatNumber(dato.consumoKgMachosAcumulado, 2) }}</td>
          <td class="text-right">{{ formatNumber(dato.consumoGramosPorAveMachos, 1) }}</td>
          <td class="text-right">
            {{ formatNumber((dato.consumoKgMachosAcumulado * 1000) / (dato.saldoMachos || 1), 1) }}
          </td>
          
          <!-- Peso -->
          <td class="text-right">{{ formatNumber(dato.pesoPromedioMachos, 3) }}</td>
          <td class="text-right">{{ formatNumber(dato.uniformidadMachos, 1) }}</td>
          <td class="text-right">{{ formatNumber(dato.coeficienteVariacionMachos, 1) }}</td>
          <td class="text-right">{{ formatNumber(dato.gananciaPesoMachos, 3) }}</td>
          
          <!-- Nutricionales -->
          <td class="text-right">{{ formatNumber(dato.kcalAlMachos, 0) }}</td>
          <td class="text-right">{{ formatNumber(dato.protAlMachos, 2) }}</td>
          <td class="text-right">{{ formatNumber(dato.kcalAveMachos, 1) }}</td>
          <td class="text-right">{{ formatNumber(dato.protAveMachos, 3) }}</td>
          
          <!-- Observaciones -->
          <td class="text-left">{{ dato.observaciones || '-' }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</div>
```

**Archivo**: `tabla-datos-diarios-machos.component.scss`

```scss
.tabla-container {
  overflow-x: auto;
  margin: 1rem 0;
}

.table-responsive {
  width: 100%;
  overflow-x: auto;
}

.tabla-datos {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.875rem;
  
  th {
    background-color: #f5f5f5;
    border: 1px solid #ddd;
    padding: 0.5rem;
    text-align: center;
    font-weight: 600;
    position: sticky;
    top: 0;
    z-index: 10;
  }
  
  td {
    border: 1px solid #ddd;
    padding: 0.5rem;
    text-align: right;
  }
  
  .text-left {
    text-align: left;
  }
  
  .text-right {
    text-align: right;
  }
  
  .highlight-red {
    background-color: #ffebee;
    color: #c62828;
    font-weight: 600;
  }
  
  .highlight-orange {
    background-color: #fff3e0;
    color: #e65100;
  }
  
  .highlight-yellow {
    background-color: #fffde7;
    color: #f57f17;
  }
  
  tbody tr:hover {
    background-color: #f5f5f5;
  }
}
```

#### 3.2.2. `TablaDatosDiariosHembrasComponent`

Similar a `TablaDatosDiariosMachosComponent` pero usando `ReporteTecnicoDiarioHembrasDto` y campos de hembras.

### 3.3. Modificar Componente Principal

**Ubicación**: `frontend/src/app/features/reportes-tecnicos/pages/reporte-tecnico-main/`

#### 3.3.1. Actualizar TypeScript

```typescript
// Agregar al componente existente
export class ReporteTecnicoMainComponent implements OnInit, OnDestroy {
  // ... código existente ...
  
  // Nuevo estado para tabs internos
  tabLevanteActivo: 'machos' | 'hembras' | 'semanal' = 'machos';
  reporteLevanteConTabs = signal<ReporteTecnicoLevanteConTabsDto | null>(null);
  
  // Método para generar reporte con tabs
  generarReporteLevanteConTabs(): void {
    if (!this.selectedLoteId) {
      this.error = 'Debe seleccionar un lote';
      return;
    }
    
    this.loading.set(true);
    this.error = null;
    
    this.reporteService.generarReporteLevanteConTabs(
      this.selectedLoteId,
      this.fechaInicio || undefined,
      this.fechaFin || undefined,
      this.tipoConsolidacion === 'consolidado'
    )
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (reporte) => {
          this.reporteLevanteConTabs.set(reporte);
          this.reporte.set(null);
          this.reporteProduccion.set(null);
          this.reporteLevanteCompleto.set(null);
          this.error = null;
        },
        error: (err) => {
          console.error('Error al generar reporte con tabs:', err);
          this.error = err.error?.message || 'Error al generar el reporte';
          this.reporteLevanteConTabs.set(null);
        }
      });
  }
}
```

#### 3.3.2. Actualizar HTML

```html
<!-- Agregar después de los tabs de Levante/Producción -->
<div class="ux-tabs-internos" *ngIf="tabActivo === 'levante' && reporteLevanteConTabs()">
  <input 
    type="radio" 
    id="tab-machos" 
    name="tab-levante" 
    [checked]="tabLevanteActivo === 'machos'"
    (change)="tabLevanteActivo = 'machos'">
  <label for="tab-machos" class="ux-tab">📊 Diario Machos</label>
  
  <input 
    type="radio" 
    id="tab-hembras" 
    name="tab-levante" 
    [checked]="tabLevanteActivo === 'hembras'"
    (change)="tabLevanteActivo = 'hembras'">
  <label for="tab-hembras" class="ux-tab">📊 Diario Hembras</label>
  
  <input 
    type="radio" 
    id="tab-semanal" 
    name="tab-levante" 
    [checked]="tabLevanteActivo === 'semanal'"
    (change)="tabLevanteActivo = 'semanal'">
  <label for="tab-semanal" class="ux-tab">📅 Semanal</label>
</div>

<!-- Contenido de tabs -->
<section class="ux-card" *ngIf="reporteLevanteConTabs() && tabActivo === 'levante'">
  <div class="reporte-header">
    <h2>
      Reporte Técnico de Levante - {{ reporteLevanteConTabs()!.informacionLote.loteNombre }}
      <span *ngIf="reporteLevanteConTabs()!.informacionLote.sublote" class="chip chip--info">
        {{ reporteLevanteConTabs()!.informacionLote.sublote }}
      </span>
    </h2>
  </div>
  
  <!-- Tab 1: Diario Machos -->
  <div *ngIf="tabLevanteActivo === 'machos'">
    <h3>Reporte Diario - Machos</h3>
    <app-tabla-datos-diarios-machos 
      [datos]="reporteLevanteConTabs()!.datosDiariosMachos">
    </app-tabla-datos-diarios-machos>
  </div>
  
  <!-- Tab 2: Diario Hembras -->
  <div *ngIf="tabLevanteActivo === 'hembras'">
    <h3>Reporte Diario - Hembras</h3>
    <app-tabla-datos-diarios-hembras 
      [datos]="reporteLevanteConTabs()!.datosDiariosHembras">
    </app-tabla-datos-diarios-hembras>
  </div>
  
  <!-- Tab 3: Semanal -->
  <div *ngIf="tabLevanteActivo === 'semanal'">
    <h3>Reporte Semanal Completo</h3>
    <app-tabla-levante-completa 
      [datos]="reporteLevanteConTabs()!.datosSemanales">
    </app-tabla-levante-completa>
  </div>
</section>
```

---

## 📝 4. PLAN DE IMPLEMENTACIÓN PASO A PASO

### Fase 1: Backend - DTOs y Métodos Base
1. ✅ Crear `ReporteTecnicoDiarioMachosDto.cs`
2. ✅ Crear `ReporteTecnicoDiarioHembrasDto.cs`
3. ✅ Crear `ReporteTecnicoLevanteConTabsDto.cs`
4. ✅ Implementar `GenerarReporteDiarioMachosAsync()`
5. ✅ Implementar `GenerarReporteDiarioHembrasAsync()`
6. ✅ Implementar `GenerarReporteLevanteConTabsAsync()`
7. ✅ Actualizar `IReporteTecnicoService`
8. ✅ Agregar endpoint en `ReporteTecnicoController`

### Fase 2: Frontend - Servicios y Interfaces
1. ✅ Agregar interfaces TypeScript en `reporte-tecnico.service.ts`
2. ✅ Agregar método `generarReporteLevanteConTabs()`

### Fase 3: Frontend - Componentes de Tabla
1. ✅ Crear `TablaDatosDiariosMachosComponent`
2. ✅ Crear `TablaDatosDiariosHembrasComponent`
3. ✅ Crear estilos SCSS para ambos componentes

### Fase 4: Frontend - Integración
1. ✅ Modificar `ReporteTecnicoMainComponent` (TypeScript)
2. ✅ Modificar `ReporteTecnicoMainComponent` (HTML)
3. ✅ Agregar estilos para tabs internos

### Fase 5: Pruebas y Validación
1. ⏳ Probar endpoint backend
2. ⏳ Probar generación de reportes diarios (machos y hembras)
3. ⏳ Probar integración con reporte semanal
4. ⏳ Validar cálculos acumulados
5. ⏳ Validar manejo de traslados
6. ⏳ Validar filtros de fecha
7. ⏳ Validar UI/UX

---

## ⚠️ 5. CONSIDERACIONES TÉCNICAS IMPORTANTES

### 5.1. Cálculo de Saldos
- **Importante**: Los saldos deben calcularse correctamente considerando:
  - Mortalidad (resta)
  - Selección normal (resta)
  - Traslados (resta)
  - Error de sexaje (NO afecta saldo, solo es corrección)

### 5.2. Manejo de Traslados
- Los traslados se registran como valores **negativos** en `SelH` y `SelM`
- Deben separarse de la selección normal usando:
  - `seleccionNormal = Math.Max(0, selH)` (solo valores positivos)
  - `traslados = Math.Abs(Math.Min(0, selH))` (valores negativos en absoluto)

### 5.3. Porcentajes de Mortalidad Diaria
- El porcentaje de mortalidad diaria debe calcularse sobre las aves **ANTES** de aplicar la mortalidad del día
- Fórmula: `%MortalidadDiaria = (MortalidadDiaria / AvesAntesMortalidad) * 100`

### 5.4. Valores Nutricionales
- Los valores de Kcal y Proteína del alimento son los mismos para hembras y machos (mismo tipo de alimento)
- El cálculo por ave es diferente porque depende del número de aves:
  - `KcalAve = (KcalAl * ConsumoKg) / NumeroAves`
  - `ProtAve = (ProtAl * ConsumoKg) / NumeroAves`

### 5.5. Filtrado por Semanas de Levante
- Solo mostrar datos de semanas 1-25 (levante)
- Las semanas 26+ son de producción y no deben aparecer en este reporte

### 5.6. Rendimiento
- Los reportes diarios pueden tener muchos registros
- Considerar paginación o virtualización en el frontend si hay muchos datos
- Optimizar consultas en el backend para evitar cargar datos innecesarios

---

## 📊 6. ESTRUCTURA DE ARCHIVOS FINAL

```
backend/
├── src/
│   ├── ZooSanMarino.Application/
│   │   ├── DTOs/
│   │   │   ├── ReporteTecnicoDiarioMachosDto.cs          [NUEVO]
│   │   │   ├── ReporteTecnicoDiarioHembrasDto.cs          [NUEVO]
│   │   │   ├── ReporteTecnicoLevanteConTabsDto.cs        [NUEVO]
│   │   │   └── ReporteTecnicoLevanteCompletoDto.cs        [EXISTENTE]
│   │   └── Interfaces/
│   │       └── IReporteTecnicoService.cs                  [MODIFICAR]
│   ├── ZooSanMarino.Infrastructure/
│   │   └── Services/
│   │       └── ReporteTecnicoService.cs                   [MODIFICAR]
│   └── ZooSanMarino.API/
│       └── Controllers/
│           └── ReporteTecnicoController.cs                 [MODIFICAR]

frontend/
└── src/
    └── app/
        └── features/
            └── reportes-tecnicos/
                ├── components/
                │   ├── tabla-datos-diarios-machos/         [NUEVO]
                │   │   ├── tabla-datos-diarios-machos.component.ts
                │   │   ├── tabla-datos-diarios-machos.component.html
                │   │   └── tabla-datos-diarios-machos.component.scss
                │   ├── tabla-datos-diarios-hembras/        [NUEVO]
                │   │   ├── tabla-datos-diarios-hembras.component.ts
                │   │   ├── tabla-datos-diarios-hembras.component.html
                │   │   └── tabla-datos-diarios-hembras.component.scss
                │   └── tabla-levante-completa/             [EXISTENTE - REUTILIZAR]
                ├── pages/
                │   └── reporte-tecnico-main/              [MODIFICAR]
                │       ├── reporte-tecnico-main.component.ts
                │       ├── reporte-tecnico-main.component.html
                │       └── reporte-tecnico-main.component.scss
                └── services/
                    └── reporte-tecnico.service.ts          [MODIFICAR]
```

---

## ✅ 7. CHECKLIST DE IMPLEMENTACIÓN

### Backend
- [ ] Crear DTOs nuevos
- [ ] Implementar `GenerarReporteDiarioMachosAsync()`
- [ ] Implementar `GenerarReporteDiarioHembrasAsync()`
- [ ] Implementar `GenerarReporteLevanteConTabsAsync()`
- [ ] Actualizar interfaz `IReporteTecnicoService`
- [ ] Agregar endpoint en controller
- [ ] Probar endpoints con Postman/Swagger

### Frontend
- [ ] Agregar interfaces TypeScript
- [ ] Agregar método en servicio
- [ ] Crear componente `TablaDatosDiariosMachosComponent`
- [ ] Crear componente `TablaDatosDiariosHembrasComponent`
- [ ] Modificar componente principal (TypeScript)
- [ ] Modificar componente principal (HTML)
- [ ] Agregar estilos para tabs internos
- [ ] Probar integración completa

### Pruebas
- [ ] Validar cálculos de saldos
- [ ] Validar manejo de traslados
- [ ] Validar porcentajes de mortalidad
- [ ] Validar acumulados
- [ ] Validar filtros de fecha
- [ ] Validar UI/UX
- [ ] Probar con datos reales

---

**Documento creado**: Análisis Completo de Implementación
**Versión**: 2.0
**Estado**: Listo para implementación

---

## 📌 NOTA FINAL

Este documento proporciona una guía completa para implementar el módulo de Reporte Técnico de Levante con tabs. Todos los componentes están detallados con código completo y consideraciones técnicas importantes.

**Esperando**: El primer tab específico que el usuario necesita para adaptar la implementación a sus requisitos exactos.
