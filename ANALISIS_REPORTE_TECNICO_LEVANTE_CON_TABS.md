# 📊 ANÁLISIS: MÓDULO DE REPORTE TÉCNICO DE LEVANTE CON TABS

## 🎯 OBJETIVO

Analizar el módulo de **Reporte Técnico** y el módulo de **Seguimiento Diario de Levante** para entender su relación y proponer una estructura de tres tabs que generen:
1. **Reporte Diario Machos**
2. **Reporte Diario Hembras**
3. **Reporte Semanal** (que incluye todo)

---

## 📋 1. ESTRUCTURA DEL MÓDULO DE SEGUIMIENTO DIARIO DE LEVANTE

### 1.1. Entidad: `SeguimientoLoteLevante`

**Ubicación**: `backend/src/ZooSanMarino.Domain/Entities/SeguimientoLoteLevante.cs`

**Tabla BD**: `seguimiento_lote_levante`

### 1.2. Campos Disponibles

| Campo | Tipo | Descripción | Uso en Reportes |
|-------|------|-------------|-----------------|
| `Id` | `int` | ID del registro | Identificación |
| `LoteId` | `int` | ID del lote | Filtrado por lote |
| `FechaRegistro` | `DateTime` | Fecha del registro | Agrupación diaria/semanal |
| `MortalidadHembras` | `int` | Mortalidad diaria hembras | Reporte hembras, semanal |
| `MortalidadMachos` | `int` | Mortalidad diaria machos | Reporte machos, semanal |
| `SelH` | `int` | Selección/retiro hembras (puede ser negativo por traslados) | Reporte hembras, semanal |
| `SelM` | `int` | Selección/retiro machos (puede ser negativo por traslados) | Reporte machos, semanal |
| `ErrorSexajeHembras` | `int` | Errores de sexaje hembras | Reporte hembras, semanal |
| `ErrorSexajeMachos` | `int` | Errores de sexaje machos | Reporte machos, semanal |
| `ConsumoKgHembras` | `double` | Consumo alimento hembras (kg) | Reporte hembras, semanal |
| `ConsumoKgMachos` | `double?` | Consumo alimento machos (kg) | Reporte machos, semanal |
| `TipoAlimento` | `string` | Tipo de alimento utilizado | Todos los reportes |
| `PesoPromH` | `double?` | Peso promedio hembras | Reporte hembras, semanal |
| `PesoPromM` | `double?` | Peso promedio machos | Reporte machos, semanal |
| `UniformidadH` | `double?` | Uniformidad hembras | Reporte hembras, semanal |
| `UniformidadM` | `double?` | Uniformidad machos | Reporte machos, semanal |
| `CvH` | `double?` | Coeficiente variación hembras | Reporte hembras, semanal |
| `CvM` | `double?` | Coeficiente variación machos | Reporte machos, semanal |
| `KcalAlH` | `double?` | Kcal/kg alimento hembras | Reporte hembras, semanal |
| `ProtAlH` | `double?` | %Proteína alimento hembras | Reporte hembras, semanal |
| `KcalAveH` | `double?` | Kcal/ave/día hembras | Reporte hembras, semanal |
| `ProtAveH` | `double?` | Proteína/ave/día hembras | Reporte hembras, semanal |
| `Observaciones` | `string?` | Observaciones del día | Todos los reportes |

### 1.3. API Disponible

**Controlador**: `SeguimientoLoteLevanteController`

**Ruta Base**: `/api/SeguimientoLoteLevante`

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `GET` | `/api/SeguimientoLoteLevante/por-lote/{loteId}` | Obtiene todos los registros de un lote |
| `POST` | `/api/SeguimientoLoteLevante` | Crea un nuevo registro diario |
| `PUT` | `/api/SeguimientoLoteLevante/{id}` | Edita un registro diario |
| `DELETE` | `/api/SeguimientoLoteLevante/{id}` | Elimina un registro diario |

---

## 📊 2. ESTRUCTURA DEL MÓDULO DE REPORTE TÉCNICO

### 2.1. Servicio: `ReporteTecnicoService`

**Ubicación**: `backend/src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs`

### 2.2. Métodos Actuales

| Método | Descripción | Uso |
|--------|-------------|-----|
| `GenerarReporteDiarioSubloteAsync` | Genera reporte diario para un sublote | Reporte diario consolidado |
| `GenerarReporteDiarioConsolidadoAsync` | Genera reporte diario consolidado | Reporte diario consolidado |
| `GenerarReporteSemanalSubloteAsync` | Genera reporte semanal para un sublote | Reporte semanal |
| `GenerarReporteSemanalConsolidadoAsync` | Genera reporte semanal consolidado | Reporte semanal |
| `ObtenerDatosDiariosLevanteAsync` | Obtiene datos diarios desde seguimiento | Base para todos los reportes |
| `GenerarReporteLevanteCompletoAsync` | Genera reporte completo (25 semanas) | Reporte semanal completo |

### 2.3. DTOs Actuales

#### `ReporteTecnicoDiarioDto`
- Contiene datos diarios consolidados (hembras + machos)
- Incluye: mortalidad total, consumo total, aves actuales totales
- **Problema**: No separa datos de hembras y machos

#### `ReporteTecnicoSemanalDto`
- Contiene datos semanales consolidados
- Incluye: mortalidad semanal, consumo semanal, etc.
- **Problema**: No separa datos de hembras y machos

#### `ReporteTecnicoLevanteSemanalDto`
- Contiene datos semanales **separados por hembras y machos**
- Incluye todos los campos necesarios para reporte completo
- **✅ Este DTO ya tiene la estructura correcta**

---

## 🔗 3. RELACIÓN ENTRE MÓDULOS

### 3.1. Flujo de Datos

```
Seguimiento Diario Levante (BD)
         ↓
ReporteTecnicoService.ObtenerDatosDiariosLevanteAsync()
         ↓
Datos Diarios (ReporteTecnicoDiarioDto)
         ↓
ConsolidarSemanales() / GenerarReporteLevanteCompletoAsync()
         ↓
Datos Semanales (ReporteTecnicoSemanalDto / ReporteTecnicoLevanteSemanalDto)
         ↓
Frontend (Visualización)
```

### 3.2. Datos Disponibles para Reportes

**Desde `SeguimientoLoteLevante`:**
- ✅ Datos separados por hembras y machos
- ✅ Mortalidad diaria (hembras y machos)
- ✅ Selección/retiro (hembras y machos)
- ✅ Error de sexaje (hembras y machos)
- ✅ Consumo de alimento (hembras y machos)
- ✅ Peso promedio (hembras y machos)
- ✅ Uniformidad (hembras y machos)
- ✅ Coeficiente de variación (hembras y machos)
- ✅ Valores nutricionales (hembras y machos)

**Desde `Lote`:**
- ✅ Aves iniciales (hembras y machos)
- ✅ Fecha de encasetamiento
- ✅ Información del lote (raza, línea, granja, etc.)

---

## 🎨 4. PROPUESTA: ESTRUCTURA CON TABS

### 4.1. Estructura de Tabs Propuesta

```
┌─────────────────────────────────────────────────────────┐
│  Reporte Técnico de Levante                             │
├─────────────────────────────────────────────────────────┤
│  [Tab 1: Diario Machos] [Tab 2: Diario Hembras] [Tab 3: Semanal] │
└─────────────────────────────────────────────────────────┘
```

### 4.2. Tab 1: Reporte Diario Machos

**Objetivo**: Mostrar todos los datos diarios relacionados con **machos** del seguimiento diario.

**Datos a Mostrar** (por día):
- Fecha
- Edad (días y semanas)
- Saldo de machos actual
- Mortalidad machos (diaria y acumulada)
- Selección/retiro machos (diaria y acumulada)
- Error de sexaje machos (diario y acumulado)
- Consumo de alimento machos (kg diario y acumulado)
- Consumo por ave macho (gramos/día)
- Peso promedio machos
- Uniformidad machos
- Coeficiente de variación machos
- Valores nutricionales (Kcal, Proteína) para machos
- Observaciones

**Fuente de Datos**:
- `SeguimientoLoteLevante.MortalidadMachos`
- `SeguimientoLoteLevante.SelM`
- `SeguimientoLoteLevante.ErrorSexajeMachos`
- `SeguimientoLoteLevante.ConsumoKgMachos`
- `SeguimientoLoteLevante.PesoPromM`
- `SeguimientoLoteLevante.UniformidadM`
- `SeguimientoLoteLevante.CvM`
- `SeguimientoLoteLevante.KcalAlH` (mismo alimento, usar para machos)
- `SeguimientoLoteLevante.ProtAlH` (mismo alimento, usar para machos)

### 4.3. Tab 2: Reporte Diario Hembras

**Objetivo**: Mostrar todos los datos diarios relacionados con **hembras** del seguimiento diario.

**Datos a Mostrar** (por día):
- Fecha
- Edad (días y semanas)
- Saldo de hembras actual
- Mortalidad hembras (diaria y acumulada)
- Selección/retiro hembras (diaria y acumulada)
- Error de sexaje hembras (diario y acumulado)
- Consumo de alimento hembras (kg diario y acumulado)
- Consumo por ave hembra (gramos/día)
- Peso promedio hembras
- Uniformidad hembras
- Coeficiente de variación hembras
- Valores nutricionales (Kcal, Proteína) para hembras
- Observaciones

**Fuente de Datos**:
- `SeguimientoLoteLevante.MortalidadHembras`
- `SeguimientoLoteLevante.SelH`
- `SeguimientoLoteLevante.ErrorSexajeHembras`
- `SeguimientoLoteLevante.ConsumoKgHembras`
- `SeguimientoLoteLevante.PesoPromH`
- `SeguimientoLoteLevante.UniformidadH`
- `SeguimientoLoteLevante.CvH`
- `SeguimientoLoteLevante.KcalAlH`
- `SeguimientoLoteLevante.ProtAlH`
- `SeguimientoLoteLevante.KcalAveH`
- `SeguimientoLoteLevante.ProtAveH`

### 4.4. Tab 3: Reporte Semanal

**Objetivo**: Mostrar datos semanales consolidados que incluyen **todo** (hembras y machos).

**Datos a Mostrar** (por semana):
- Semana (1-25)
- Fecha inicio y fin de semana
- Edad (días y semanas)
- **HEMBRAS:**
  - Saldo hembras al inicio y fin de semana
  - Mortalidad hembras (semana y acumulada)
  - Selección hembras (semana y acumulada)
  - Error sexaje hembras (semana y acumulado)
  - Consumo hembras (kg semana y acumulado)
  - Consumo por ave hembra (gramos/día promedio)
  - Peso promedio hembras
  - Uniformidad promedio hembras
  - CV promedio hembras
  - Valores nutricionales hembras
- **MACHOS:**
  - Saldo machos al inicio y fin de semana
  - Mortalidad machos (semana y acumulada)
  - Selección machos (semana y acumulada)
  - Error sexaje machos (semana y acumulado)
  - Consumo machos (kg semana y acumulado)
  - Consumo por ave macho (gramos/día promedio)
  - Peso promedio machos
  - Uniformidad promedio machos
  - CV promedio machos
  - Valores nutricionales machos
- **COMPARACIÓN CON GUÍA GENÉTICA:**
  - Valores GUIA para hembras y machos
  - Diferencias con GUIA
  - Porcentajes de diferencia
- Observaciones consolidadas

**Fuente de Datos**:
- Ya existe `ReporteTecnicoLevanteSemanalDto` que contiene todos estos datos
- Usar `GenerarReporteLevanteCompletoAsync()` que ya genera estos datos

---

## 🛠️ 5. IMPLEMENTACIÓN PROPUESTA

### 5.1. Backend: Nuevos DTOs

#### 5.1.1. `ReporteTecnicoDiarioMachosDto`

```csharp
public class ReporteTecnicoDiarioMachosDto
{
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    public int SaldoMachos { get; set; }
    
    // Mortalidad
    public int MortalidadMachos { get; set; }
    public int MortalidadMachosAcumulada { get; set; }
    public decimal MortalidadMachosPorcentajeDiario { get; set; }
    public decimal MortalidadMachosPorcentajeAcumulado { get; set; }
    
    // Selección/Retiro
    public int SeleccionMachos { get; set; } // Solo valores positivos
    public int SeleccionMachosAcumulada { get; set; }
    public decimal SeleccionMachosPorcentajeDiario { get; set; }
    public decimal SeleccionMachosPorcentajeAcumulado { get; set; }
    
    // Traslados
    public int TrasladosMachos { get; set; } // Valores negativos en valor absoluto
    public int TrasladosMachosAcumulados { get; set; }
    
    // Error Sexaje
    public int ErrorSexajeMachos { get; set; }
    public int ErrorSexajeMachosAcumulado { get; set; }
    public decimal ErrorSexajeMachosPorcentajeDiario { get; set; }
    public decimal ErrorSexajeMachosPorcentajeAcumulado { get; set; }
    
    // Consumo
    public decimal ConsumoKgMachos { get; set; }
    public decimal ConsumoKgMachosAcumulado { get; set; }
    public decimal ConsumoGramosPorAveMachos { get; set; }
    
    // Peso y Uniformidad
    public decimal? PesoPromedioMachos { get; set; }
    public decimal? UniformidadMachos { get; set; }
    public decimal? CoeficienteVariacionMachos { get; set; }
    public decimal? GananciaPesoMachos { get; set; }
    
    // Valores Nutricionales
    public double? KcalAlMachos { get; set; }
    public double? ProtAlMachos { get; set; }
    public double? KcalAveMachos { get; set; }
    public double? ProtAveMachos { get; set; }
    
    // Observaciones
    public string? Observaciones { get; set; }
}
```

#### 5.1.2. `ReporteTecnicoDiarioHembrasDto`

```csharp
public class ReporteTecnicoDiarioHembrasDto
{
    public DateTime Fecha { get; set; }
    public int EdadDias { get; set; }
    public int EdadSemanas { get; set; }
    public int SaldoHembras { get; set; }
    
    // Mortalidad
    public int MortalidadHembras { get; set; }
    public int MortalidadHembrasAcumulada { get; set; }
    public decimal MortalidadHembrasPorcentajeDiario { get; set; }
    public decimal MortalidadHembrasPorcentajeAcumulado { get; set; }
    
    // Selección/Retiro
    public int SeleccionHembras { get; set; } // Solo valores positivos
    public int SeleccionHembrasAcumulada { get; set; }
    public decimal SeleccionHembrasPorcentajeDiario { get; set; }
    public decimal SeleccionHembrasPorcentajeAcumulado { get; set; }
    
    // Traslados
    public int TrasladosHembras { get; set; } // Valores negativos en valor absoluto
    public int TrasladosHembrasAcumulados { get; set; }
    
    // Error Sexaje
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeHembrasAcumulado { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeDiario { get; set; }
    public decimal ErrorSexajeHembrasPorcentajeAcumulado { get; set; }
    
    // Consumo
    public decimal ConsumoKgHembras { get; set; }
    public decimal ConsumoKgHembrasAcumulado { get; set; }
    public decimal ConsumoGramosPorAveHembras { get; set; }
    
    // Peso y Uniformidad
    public decimal? PesoPromedioHembras { get; set; }
    public decimal? UniformidadHembras { get; set; }
    public decimal? CoeficienteVariacionHembras { get; set; }
    public decimal? GananciaPesoHembras { get; set; }
    
    // Valores Nutricionales
    public double? KcalAlHembras { get; set; }
    public double? ProtAlHembras { get; set; }
    public double? KcalAveHembras { get; set; }
    public double? ProtAveHembras { get; set; }
    
    // Observaciones
    public string? Observaciones { get; set; }
}
```

#### 5.1.3. DTO Completo con Tabs

```csharp
public class ReporteTecnicoLevanteConTabsDto
{
    public ReporteTecnicoLoteInfoDto InformacionLote { get; set; } = new();
    
    // Tab 1: Diario Machos
    public List<ReporteTecnicoDiarioMachosDto> DatosDiariosMachos { get; set; } = new();
    
    // Tab 2: Diario Hembras
    public List<ReporteTecnicoDiarioHembrasDto> DatosDiariosHembras { get; set; } = new();
    
    // Tab 3: Semanal (ya existe ReporteTecnicoLevanteSemanalDto)
    public List<ReporteTecnicoLevanteSemanalDto> DatosSemanales { get; set; } = new();
    
    public bool EsConsolidado { get; set; }
    public List<string> SublotesIncluidos { get; set; } = new();
}
```

### 5.2. Backend: Nuevos Métodos en `ReporteTecnicoService`

#### 5.2.1. `GenerarReporteDiarioMachosAsync`

```csharp
public async Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
    int loteId,
    DateTime? fechaInicio = null,
    DateTime? fechaFin = null,
    CancellationToken ct = default)
{
    // Obtener datos diarios de levante
    var seguimientos = await ObtenerDatosDiariosLevanteAsync(loteId, fechaEncaset, fechaInicio, fechaFin, ct);
    
    // Filtrar solo semanas de levante (1-25)
    seguimientos = seguimientos.Where(d => d.EdadSemanas <= 25).ToList();
    
    // Obtener lote para aves iniciales
    var lote = await _ctx.Lotes.FirstOrDefaultAsync(l => l.LoteId == loteId, ct);
    var machosIniciales = lote?.MachosL ?? 0;
    
    // Procesar datos y calcular acumulados para machos
    var datosMachos = new List<ReporteTecnicoDiarioMachosDto>();
    var machosActuales = machosIniciales;
    var mortalidadAcumulada = 0;
    var seleccionAcumulada = 0;
    var errorSexajeAcumulado = 0;
    var consumoAcumulado = 0m;
    var trasladosAcumulados = 0;
    decimal? pesoAnterior = null;
    
    foreach (var seg in seguimientos)
    {
        // Calcular mortalidad
        var mortalidad = seg.MortalidadMachos;
        mortalidadAcumulada += mortalidad;
        machosActuales -= mortalidad;
        
        // Separar selección normal de traslados
        var selM = seg.SelM;
        var seleccionNormal = Math.Max(0, selM);
        var traslados = Math.Abs(Math.Min(0, selM));
        
        seleccionAcumulada += seleccionNormal;
        trasladosAcumulados += traslados;
        machosActuales -= seleccionNormal;
        machosActuales -= traslados;
        
        // Error sexaje
        var errorSexaje = seg.ErrorSexajeMachos;
        errorSexajeAcumulado += errorSexaje;
        
        // Consumo
        var consumo = (decimal)(seg.ConsumoKgMachos ?? 0);
        consumoAcumulado += consumo;
        var consumoGramosPorAve = machosActuales > 0 ? (consumo * 1000) / machosActuales : 0;
        
        // Peso
        var pesoActual = (decimal?)(seg.PesoPromM);
        var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
            ? pesoActual.Value - pesoAnterior.Value 
            : (decimal?)null;
        
        var dto = new ReporteTecnicoDiarioMachosDto
        {
            Fecha = seg.FechaRegistro,
            EdadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro),
            EdadSemanas = CalcularEdadSemanas(edadDias),
            SaldoMachos = machosActuales,
            MortalidadMachos = mortalidad,
            MortalidadMachosAcumulada = mortalidadAcumulada,
            MortalidadMachosPorcentajeDiario = machosActuales > 0 ? (decimal)mortalidad / machosActuales * 100 : 0,
            MortalidadMachosPorcentajeAcumulado = machosIniciales > 0 ? (decimal)mortalidadAcumulada / machosIniciales * 100 : 0,
            SeleccionMachos = seleccionNormal,
            SeleccionMachosAcumulada = seleccionAcumulada,
            SeleccionMachosPorcentajeDiario = machosActuales > 0 ? (decimal)seleccionNormal / machosActuales * 100 : 0,
            SeleccionMachosPorcentajeAcumulado = machosIniciales > 0 ? (decimal)seleccionAcumulada / machosIniciales * 100 : 0,
            TrasladosMachos = traslados,
            TrasladosMachosAcumulados = trasladosAcumulados,
            ErrorSexajeMachos = errorSexaje,
            ErrorSexajeMachosAcumulado = errorSexajeAcumulado,
            ErrorSexajeMachosPorcentajeDiario = machosActuales > 0 ? (decimal)errorSexaje / machosActuales * 100 : 0,
            ErrorSexajeMachosPorcentajeAcumulado = machosIniciales > 0 ? (decimal)errorSexajeAcumulado / machosIniciales * 100 : 0,
            ConsumoKgMachos = consumo,
            ConsumoKgMachosAcumulado = consumoAcumulado,
            ConsumoGramosPorAveMachos = consumoGramosPorAve,
            PesoPromedioMachos = pesoActual,
            UniformidadMachos = (decimal?)(seg.UniformidadM),
            CoeficienteVariacionMachos = (decimal?)(seg.CvM),
            GananciaPesoMachos = gananciaPeso,
            KcalAlMachos = seg.KcalAlH, // Mismo alimento
            ProtAlMachos = seg.ProtAlH, // Mismo alimento
            KcalAveMachos = machosActuales > 0 && seg.KcalAlH.HasValue 
                ? (seg.KcalAlH.Value * (double)consumo) / machosActuales 
                : null,
            ProtAveMachos = machosActuales > 0 && seg.ProtAlH.HasValue 
                ? (seg.ProtAlH.Value * (double)consumo) / machosActuales 
                : null,
            Observaciones = seg.Observaciones
        };
        
        if (pesoActual.HasValue)
            pesoAnterior = pesoActual;
        
        datosMachos.Add(dto);
    }
    
    return datosMachos;
}
```

#### 5.2.2. `GenerarReporteDiarioHembrasAsync`

Similar a `GenerarReporteDiarioMachosAsync` pero usando datos de hembras.

#### 5.2.3. `GenerarReporteLevanteConTabsAsync`

```csharp
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
    
    // Generar datos para cada tab
    var datosDiariosMachos = await GenerarReporteDiarioMachosAsync(loteId, fechaInicio, fechaFin, ct);
    var datosDiariosHembras = await GenerarReporteDiarioHembrasAsync(loteId, fechaInicio, fechaFin, ct);
    var datosSemanales = (await GenerarReporteLevanteCompletoAsync(loteId, consolidarSublotes, ct)).DatosSemanales;
    
    return new ReporteTecnicoLevanteConTabsDto
    {
        InformacionLote = infoLote,
        DatosDiariosMachos = datosDiariosMachos,
        DatosDiariosHembras = datosDiariosHembras,
        DatosSemanales = datosSemanales,
        EsConsolidado = consolidarSublotes,
        SublotesIncluidos = new List<string> { ExtraerSublote(lote.LoteNombre) ?? "Sin sublote" }
    };
}
```

### 5.3. Frontend: Componente con Tabs

#### 5.3.1. Estructura HTML

```html
<div class="reporte-levante-tabs">
  <!-- Tabs Navigation -->
  <div class="tabs-nav">
    <button 
      class="tab-button" 
      [class.active]="tabActivo === 'machos'"
      (click)="tabActivo = 'machos'">
      📊 Diario Machos
    </button>
    <button 
      class="tab-button" 
      [class.active]="tabActivo === 'hembras'"
      (click)="tabActivo = 'hembras'">
      📊 Diario Hembras
    </button>
    <button 
      class="tab-button" 
      [class.active]="tabActivo === 'semanal'"
      (click)="tabActivo = 'semanal'">
      📅 Semanal
    </button>
  </div>
  
  <!-- Tab Content: Diario Machos -->
  <div class="tab-content" *ngIf="tabActivo === 'machos'">
    <app-tabla-datos-diarios-machos 
      [datos]="reporte()?.datosDiariosMachos || []">
    </app-tabla-datos-diarios-machos>
  </div>
  
  <!-- Tab Content: Diario Hembras -->
  <div class="tab-content" *ngIf="tabActivo === 'hembras'">
    <app-tabla-datos-diarios-hembras 
      [datos]="reporte()?.datosDiariosHembras || []">
    </app-tabla-datos-diarios-hembras>
  </div>
  
  <!-- Tab Content: Semanal -->
  <div class="tab-content" *ngIf="tabActivo === 'semanal'">
    <app-tabla-levante-completa 
      [datos]="reporte()?.datosSemanales || []">
    </app-tabla-levante-completa>
  </div>
</div>
```

#### 5.3.2. Componente TypeScript

```typescript
export class ReporteTecnicoLevanteTabsComponent {
  tabActivo: 'machos' | 'hembras' | 'semanal' = 'machos';
  reporte = signal<ReporteTecnicoLevanteConTabsDto | null>(null);
  
  generarReporte(): void {
    this.reporteService.generarReporteLevanteConTabs(
      this.selectedLoteId,
      this.fechaInicio,
      this.fechaFin
    ).subscribe({
      next: (reporte) => {
        this.reporte.set(reporte);
      }
    });
  }
}
```

---

## 📋 6. RESUMEN DE DATOS DISPONIBLES

### 6.1. Datos del Seguimiento Diario que se Usarán

| Dato | Campo en Seguimiento | Usado en Tab |
|------|---------------------|--------------|
| Mortalidad Machos | `MortalidadMachos` | Diario Machos, Semanal |
| Mortalidad Hembras | `MortalidadHembras` | Diario Hembras, Semanal |
| Selección Machos | `SelM` | Diario Machos, Semanal |
| Selección Hembras | `SelH` | Diario Hembras, Semanal |
| Error Sexaje Machos | `ErrorSexajeMachos` | Diario Machos, Semanal |
| Error Sexaje Hembras | `ErrorSexajeHembras` | Diario Hembras, Semanal |
| Consumo Machos | `ConsumoKgMachos` | Diario Machos, Semanal |
| Consumo Hembras | `ConsumoKgHembras` | Diario Hembras, Semanal |
| Peso Machos | `PesoPromM` | Diario Machos, Semanal |
| Peso Hembras | `PesoPromH` | Diario Hembras, Semanal |
| Uniformidad Machos | `UniformidadM` | Diario Machos, Semanal |
| Uniformidad Hembras | `UniformidadH` | Diario Hembras, Semanal |
| CV Machos | `CvM` | Diario Machos, Semanal |
| CV Hembras | `CvH` | Diario Hembras, Semanal |
| Valores Nutricionales | `KcalAlH`, `ProtAlH` | Todos los tabs |
| Observaciones | `Observaciones` | Todos los tabs |

### 6.2. Cálculos Necesarios

**Para Reportes Diarios:**
- ✅ Saldo actual (hembras/machos) = Iniciales - Mortalidad - Selección - Traslados
- ✅ Acumulados (mortalidad, selección, error sexaje, consumo)
- ✅ Porcentajes (diario y acumulado)
- ✅ Consumo por ave (gramos/día)
- ✅ Ganancia de peso (diferencia con día anterior)

**Para Reporte Semanal:**
- ✅ Consolidación de datos diarios por semana
- ✅ Promedios semanales (peso, uniformidad, CV)
- ✅ Totales semanales (mortalidad, selección, consumo)
- ✅ Comparación con guía genética
- ✅ Cálculos de eficiencia (Kcal/ave, Prot/ave)

---

## ✅ 7. VENTAJAS DE ESTA ESTRUCTURA

1. **Separación Clara**: Cada tab muestra información específica (machos, hembras, o todo)
2. **Reutilización**: El reporte semanal ya existe y funciona correctamente
3. **Datos Completos**: Todos los datos del seguimiento diario están disponibles
4. **Flexibilidad**: Los usuarios pueden ver solo lo que necesitan
5. **Consistencia**: Usa la misma fuente de datos (Seguimiento Diario Levante)

---

## 🚀 8. PRÓXIMOS PASOS

1. ✅ **Análisis Completo** (Este documento)
2. ⏳ Crear DTOs nuevos (`ReporteTecnicoDiarioMachosDto`, `ReporteTecnicoDiarioHembrasDto`, `ReporteTecnicoLevanteConTabsDto`)
3. ⏳ Implementar métodos en `ReporteTecnicoService`:
   - `GenerarReporteDiarioMachosAsync`
   - `GenerarReporteDiarioHembrasAsync`
   - `GenerarReporteLevanteConTabsAsync`
4. ⏳ Crear endpoint en `ReporteTecnicoController`
5. ⏳ Crear componentes frontend:
   - `TablaDatosDiariosMachosComponent`
   - `TablaDatosDiariosHembrasComponent`
   - `ReporteTecnicoLevanteTabsComponent`
6. ⏳ Actualizar servicio frontend (`reporte-tecnico.service.ts`)
7. ⏳ Integrar en el componente principal (`reporte-tecnico-main.component.ts`)
8. ⏳ Pruebas y validación

---

## 📝 NOTAS IMPORTANTES

1. **Traslados**: Los traslados se registran como valores negativos en `SelH` y `SelM`. Deben separarse correctamente de la selección normal.

2. **Semanas de Levante**: Solo se deben mostrar datos de semanas 1-25 (levante). Las semanas 26+ son de producción.

3. **Cálculo de Saldos**: Los saldos actuales deben calcularse correctamente considerando:
   - Mortalidad (resta)
   - Selección normal (resta)
   - Traslados (resta)
   - Error de sexaje (no afecta saldo, solo es corrección)

4. **Valores Nutricionales**: Los valores de Kcal y Proteína del alimento son los mismos para hembras y machos (mismo tipo de alimento), pero el cálculo por ave es diferente porque depende del número de aves.

5. **Guía Genética**: El reporte semanal ya incluye comparación con guía genética. Los reportes diarios pueden no necesitarla, pero se puede agregar si es requerido.

---

**Documento creado**: {{ fecha_actual }}
**Versión**: 1.0
**Autor**: Análisis de Módulos
