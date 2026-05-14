# Fase 5 — Exportación de Reportes a Excel

**Módulo:** Reportes Técnicos · Descarga de Excel con TABs  
**Fecha de planificación:** 2026-05-07  
**Responsable de Fase 4:** Validación completada  
**Contexto:** Fases 1-4 completadas (LEVANTE y PRODUCCIÓN)

**⚠️ IMPORTANTE:** Esta Fase 5 aplica a **LEVANTE y PRODUCCIÓN**
- Botón único de descarga en ambas etapas
- Estructura de Excel diferente según etapa (automático)
- Nombre de archivo dinámico según filtros seleccionados

---

## Objetivo

Implementar funcionalidad de descarga de reportes en Excel con:
1. **Múltiples hojas (tabs)** correspondientes a cada TAB visible en la UI
2. **Hoja de información** al inicio con metadatos del lote
3. **Formato Real | Guía | Diferencia** con estilos (colores, semáforos)
4. **Nombre dinámico:** `FASE_LOTE_BASE_SUBLOTE_FECHA_HORA.xlsx`

---

## Especificación de Estructura Excel

### Hoja 1: "Información" (Obligatoria)

Metadatos generales del reporte en formato clave-valor:

```
┌─────────────────────────────────────┐
│ INFORMACIÓN DEL REPORTE             │
├─────────────────────────────────────┤
│ Fase/Etapa:        LEVANTE          │
│ Lote Base:         K345             │
│ Lote Sublote:      K345A            │
│ Granja:            Granja Principal │
│ Núcleo:            Núcleo 01        │
│ Fecha Inicio:      2026-01-15       │
│ Fecha Fin:         2026-05-07       │
│ Total Aves Inicio: 10,000           │
│ Periodicidad:      Diario           │
│ Fecha Descarga:    2026-05-07 14:30 │
└─────────────────────────────────────┘
```

**Contenido exacto según etapa:**

#### LEVANTE:
- Fase/Etapa: LEVANTE
- Lote Base: (LotePosturaBaseId.nombre)
- Lote Sublote: (LotePosturaLevante.nombre si existe)
- Granja: (nombre)
- Núcleo: (nombre)
- Fecha Inicio: (fecha_encasetamiento)
- Fecha Fin: (hoy)
- Total Aves Inicio: (numero_aves_inicio)
- Periodicidad: (selectedPeriodicidad)
- Fecha Descarga: (ahora)

#### PRODUCCIÓN:
- Fase/Etapa: PRODUCCIÓN
- Lote Base: (LotePosturaBaseId.nombre)
- Lote Sublote: (LotePosturaProduccion.nombre si existe)
- Granja: (nombre)
- Núcleo: (nombre)
- Fecha Inicio: (fecha_inicio_produccion)
- Fecha Fin: (hoy)
- Total Aves Inicio: (numero_aves_inicio)
- Periodicidad: (selectedPeriodicidad)
- Fecha Descarga: (ahora)

---

### Hojas 2+: Datos del Reporte (Variables según etapa)

#### LEVANTE — Hojas esperadas:

```
Hoja 2: "Real vs Guía" (semanal)
  └─ Tabla: Semana | Población | Mortalidad | ... | HTAA
             Real  │ Real      │ Real       │ ... │ Real
             Guía  │ Guía      │ Guía       │ ... │ Guía
             Dif   │ Dif%      │ Dif%       │ ... │ Dif%

Hoja 3: "Semanal" (si existe en UI)
  └─ Tabla: Semana | Datos agregados (sin Guía si no aplica)

Hoja 4: "Diario" (si existe en UI)
  └─ Tabla: Fecha | Datos diarios (sin Guía si no aplica)
```

#### PRODUCCIÓN — Hojas esperadas:

```
Hoja 2: "Diario/Galpón"
  └─ Tabla para CADA galpón (Galpón 14, 15, 16, ...)
     Fecha | Mortalidad H/M | Consumo | Huevos | ... | HTAA
     Real  │ Real           │ Real    │ Real   │ ... │ Real
     Guía  │ Guía           │ Guía    │ Guía   │ ... │ Guía
     Dif   │ Dif%           │ Dif%    │ Dif%   │ ... │ Dif%

Hoja 3: "Semanal/Galpón"
  └─ Tabla para CADA galpón (agregados semanales)

Hoja 4: "Diario/General"
  └─ Tabla consolidada (todos los galpones + TOTAL)

Hoja 5: "Semanal/General"
  └─ Tabla consolidada semanal
```

---

## Estructura de Nombre de Archivo

```
FASE_LOTE_BASE_SUBLOTE_FECHA_HORA.xlsx
```

**Componentes:**

| Componente | Valor Ejemplo | Notas |
|-----------|--------------|-------|
| FASE | `LEVANTE` o `PRODUCCION` | De selectedEtapa |
| LOTE_BASE | `K345` | LotePosturaBase.nombre |
| SUBLOTE | `K345A` o `null` | LotePosturaLevante.nombre (LEVANTE) o LotePosturaProduccion.nombre (PRODUCCIÓN). Si es null, se omite este segmento |
| FECHA | `20260507` | YYYYMMDD |
| HORA | `143022` | HHmmss |

**Ejemplos reales:**

```
LEVANTE_K345_K345A_20260507_143022.xlsx
  └─ LEVANTE, Lote Base K345, Sublote K345A, descargado 07/05/2026 a las 14:30:22

LEVANTE_K345_20260507_143022.xlsx
  └─ LEVANTE, Lote Base K345, SIN sublote específico

PRODUCCION_K345_K345A_20260507_143022.xlsx
  └─ PRODUCCIÓN, Lote Base K345, Lote K345A

PRODUCCION_K370_20260507_143022.xlsx
  └─ PRODUCCIÓN, Lote Base K370, Sin filtro de sublote
```

---

## Formato de Celdas

### Estructura de 3 Filas por Dato (Real | Guía | Diferencia)

```
┌──────────┬────────┬────────┬────────┬────────┬────────┐
│ Fecha    │ Mort.H │ Mort.M │ Consumo│ Huevos │ HTAA   │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ Real     │ Real   │ Real   │ Real   │ Real   │ Real   │
│ Guía     │ Guía   │ Guía   │ Guía   │ Guía   │ Guía   │
│ Dif%     │ Dif%   │ Dif%   │ Dif%   │ Dif%   │ Dif%   │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ 02/02/26 │ 2      │ 1      │ 43.5   │ 1200   │ 45.8   │
│          │ 2.1    │ 1.0    │ 43.0   │ 1180   │ 45.5   │
│          │ -4.8%  │ 0.0%   │ 1.2%   │ 1.7%   │ 0.7%   │
└──────────┴────────┴────────┴────────┴────────┴────────┘
```

### Estilos y Colores

**Colores por fila:**

```
Fila "Real":   Fondo blanco (#FFFFFF), fuente negra (normal)
Fila "Guía":   Fondo azul claro (#E3F2FD), fuente gris (#424242) — itálica/más tenue
Fila "Dif%":   Fondo según semáforo:
               🟢 Verde (#C8E6C9):   Dentro de rango (±5%)
               🟡 Amarillo (#FFF9C4): Fuera de rango (±5% a ±15%)
               🔴 Rojo (#FFCDD2):   Crítico (> ±15%)
```

**Semáforo (valores numéricos en "Dif%"):**

| Rango | Color | RGB | Interpretación |
|-------|-------|-----|-----------------|
| ±0% a ±5% | Verde | #C8E6C9 | Excelente |
| ±5% a ±15% | Amarillo | #FFF9C4 | Advertencia |
| > ±15% | Rojo | #FFCDD2 | Crítico |

**Headers:**

- **Fila de encabezado principal:** Fondo gris oscuro (#424242), fuente blanca, bold, 14pt
- **Fila "Real/Guía/Dif":** Fondo gris claro (#E0E0E0), fuente negra, 11pt

**Ancho de columnas:** Auto-ajustado al contenido

**Congelación:** Primera fila congelada (permite scrolling vertical)

---

## Implementación

### Backend — FASE 5B (Servicio de Exportación)

#### Ubicación: `Infrastructure/Services/ExportacionExcelService.cs`

**Interfaz:**

```csharp
public interface IExportacionExcelService
{
    Task<byte[]> ExportarReporteLevanteAsync(
        ReporteTecnicoLevanteCompletoDto reporte,
        LoteInfo loteInfo,
        string loteSubBuscado,
        CancellationToken ct);
    
    Task<byte[]> ExportarReporteProduccionAsync(
        ReporteTecnicoProduccionTabsDto reporte,
        string etapa,
        string loteBaseName,
        string loteSubloteName,
        CancellationToken ct);
}
```

**Implementación usa ClosedXML o EPPlus:**

```csharp
using ClosedXML.Excel;

public class ExportacionExcelService : IExportacionExcelService
{
    public async Task<byte[]> ExportarReporteLevanteAsync(
        ReporteTecnicoLevanteCompletoDto reporte,
        LoteInfo loteInfo,
        string loteSubBuscado,
        CancellationToken ct)
    {
        var wb = new XLWorkbook();
        
        // 1. Hoja "Información"
        var wsInfo = wb.Worksheets.Add("Información");
        _AgregarHojaInformacion(wsInfo, "LEVANTE", loteInfo, loteSubBuscado);
        
        // 2. Hojas de datos (Real vs Guía, Semanal, Diario, etc.)
        if (reporte.DatosSemanales?.Any() == true)
            _AgregarHojaRealVsGuiaSemanal(wb, reporte);
        
        if (reporte.DatosDiarios?.Any() == true)
            _AgregarHojaDiario(wb, reporte);
        
        // Guardar a memoria
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
```

#### Métodos principales:

```csharp
private void _AgregarHojaInformacion(IXLWorksheet ws, 
    string etapa, LoteInfo loteInfo, string loteSub)
{
    // Fila 1: "INFORMACIÓN DEL REPORTE" (header)
    var row = 1;
    ws.Cell(row, 1).Value = "INFORMACIÓN DEL REPORTE";
    ws.Range($"A{row}:B{row}").Merge();
    ws.Cell(row, 1).Style.Font.Bold = true;
    ws.Cell(row, 1).Style.Font.FontSize = 14;
    ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.DarkGray;
    ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
    
    // Fila 2+: Datos
    row = 2;
    _AgregarFilaInfo(ws, row++, "Fase/Etapa", etapa);
    _AgregarFilaInfo(ws, row++, "Lote Base", loteInfo.nombre);
    _AgregarFilaInfo(ws, row++, "Lote Sublote", loteSub ?? "—");
    _AgregarFilaInfo(ws, row++, "Granja", loteInfo.granjaNombre);
    // ... más datos
    
    // Auto-ajustar ancho
    ws.Columns().AdjustToContents();
}

private void _AgregarHojaRealVsGuiaSemanal(IXLWorkbook wb,
    ReporteTecnicoLevanteCompletoDto reporte)
{
    var ws = wb.Worksheets.Add("Real vs Guía");
    
    // Headers principales
    var col = 1;
    _AgregarHeaderPrincipal(ws, col++, "Semana", 1);
    _AgregarHeaderPrincipal(ws, col++, "Población", 3);
    _AgregarHeaderPrincipal(ws, col++, "Mortalidad", 3);
    // ... más columnas
    
    // Sub-headers (Real / Guía / Dif)
    col = 1;
    _AgregarSubheader(ws, col++, "Sem");
    _AgregarSubheader(ws, col++, "Real");
    _AgregarSubheader(ws, col++, "Guía");
    _AgregarSubheader(ws, col++, "Dif%");
    // ... más columnas
    
    // Datos
    var rowData = 3;
    foreach (var semana in reporte.DatosSemanales)
    {
        ws.Cell(rowData, 1).Value = semana.semana;
        ws.Cell(rowData + 1, 1).Value = "";
        ws.Cell(rowData + 2, 1).Value = "";
        
        // Datos reales (fila rowData)
        ws.Cell(rowData, 2).Value = semana.poblacion;
        ws.Cell(rowData, 2).Style.Fill.BackgroundColor = XLColor.White;
        
        // Datos guía (fila rowData+1)
        ws.Cell(rowData + 1, 2).Value = semana.poblacionGuia;
        ws.Cell(rowData + 1, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE3F2FD);
        ws.Cell(rowData + 1, 2).Style.Font.FontColor = XLColor.FromArgb(0x424242);
        
        // Diferencia (fila rowData+2) — CON SEMÁFORO
        var dif = CalcularDiferencia(semana.poblacion, semana.poblacionGuia);
        ws.Cell(rowData + 2, 2).Value = dif;
        ws.Cell(rowData + 2, 2).Style.Fill.BackgroundColor = 
            ObtenerColorSemaforo(dif);  // 🟢🟡🔴
        
        rowData += 3;
    }
}
```

#### Método helper para color de semáforo:

```csharp
private XLColor ObtenerColorSemaforo(double? diferencia)
{
    if (diferencia == null) return XLColor.White;
    
    var difAbs = Math.Abs(diferencia.Value);
    
    if (difAbs <= 5)
        return XLColor.FromArgb(0xC8E6C9);  // Verde
    else if (difAbs <= 15)
        return XLColor.FromArgb(0xFFF9C4);  // Amarillo
    else
        return XLColor.FromArgb(0xFFCDD2);  // Rojo
}
```

---

### Frontend — FASE 5F (Botón y Servicio HTTP)

#### Ubicación: `reporte-tecnico-main.component.ts`

**Agregar en template HTML (al lado del botón de filtros):**

```html
<div class="reporte-actions">
  <button 
    (click)="descargarExcel()"
    [disabled]="!reporteLevante() && !reporteProduccionTabs()"
    class="btn btn-primary btn-export">
    <i class="icon-download"></i> Descargar Excel
  </button>
</div>
```

**Método en componente:**

```typescript
descargarExcel(): void {
  if (this.filterSvc.selectedEtapa() === 'LEVANTE') {
    this._descargarExcelLevante();
  } else if (this.filterSvc.selectedEtapa() === 'PRODUCCION') {
    this._descargarExcelProduccion();
  }
}

private _descargarExcelLevante(): void {
  if (!this.reporteLevante()) return;
  
  const nombreArchivo = this._GenerarNombreArchivo(
    'LEVANTE',
    this.filterSvc.selectedLoteBaseId()?.toString(),
    this.filterSvc.selectedSubloteId()?.toString()
  );
  
  this.reporteService.exportarExcelLevante(
    this.reporteLevante()!,
    nombreArchivo
  ).subscribe({
    next: (blob) => {
      this._DescargarBlob(blob, `${nombreArchivo}.xlsx`);
    },
    error: (err) => {
      this.error = 'Error al descargar Excel: ' + err.message;
    }
  });
}

private _GenerarNombreArchivo(
  etapa: string,
  loteBase: string,
  sublote?: string
): string {
  const ahora = new Date();
  const fecha = ahora.toISOString().split('T')[0].replace(/-/g, '');
  const hora = ahora.toTimeString().slice(0, 6).replace(/:/g, '');
  
  let nombre = `${etapa}_${loteBase}`;
  if (sublote) nombre += `_${sublote}`;
  nombre += `_${fecha}_${hora}`;
  
  return nombre;
}

private _DescargarBlob(blob: Blob, filename: string): void {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  window.URL.revokeObjectURL(url);
}
```

#### Service HTTP:

```typescript
// En reporte-tecnico.service.ts

exportarExcelLevante(
  reporte: ReporteTecnicoLevanteCompletoDto,
  nombreArchivo: string
): Observable<Blob> {
  return this.http.post<Blob>(
    `${this.baseUrl}/levante/exportar-excel`,
    { reporte, nombreArchivo },
    { responseType: 'blob' as 'json' }
  );
}

exportarExcelProduccion(
  reporte: ReporteTecnicoProduccionTabsDto,
  nombreArchivo: string
): Observable<Blob> {
  return this.http.post<Blob>(
    `${this.baseUrl}/produccion/exportar-excel`,
    { reporte, nombreArchivo },
    { responseType: 'blob' as 'json' }
  );
}
```

---

### Endpoints Backend

**Archivo:** `API/Controllers/ReporteTecnicoController.cs` (LEVANTE)

```csharp
[HttpPost("levante/exportar-excel")]
public async Task<IActionResult> ExportarExcelLevante(
    [FromBody] ExportarExcelLevanteRequestDto request,
    CancellationToken ct)
{
    var bytes = await _exportService.ExportarReporteLevanteAsync(
        request.Reporte,
        request.LoteInfo,
        request.LoteSublote,
        ct);
    
    return File(bytes, 
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"{request.NombreArchivo}.xlsx");
}
```

**Archivo:** `API/Controllers/ReporteTecnicoProduccionController.cs` (PRODUCCIÓN)

```csharp
[HttpPost("exportar-excel")]
public async Task<IActionResult> ExportarExcelProduccion(
    [FromBody] ExportarExcelProduccionRequestDto request,
    CancellationToken ct)
{
    var bytes = await _exportService.ExportarReporteProduccionAsync(
        request.Reporte,
        "PRODUCCION",
        request.LoteBaseName,
        request.LoteSubloteName,
        ct);
    
    return File(bytes, 
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"{request.NombreArchivo}.xlsx");
}
```

---

## Checklist de Implementación

### Backend (Fase 5B)
- [ ] Crear `IExportacionExcelService` interface
- [ ] Implementar `ExportacionExcelService` con ClosedXML
- [ ] Método `ExportarReporteLevanteAsync()`
- [ ] Método `ExportarReporteProduccionAsync()`
- [ ] Crear DTOs: `ExportarExcelLevanteRequestDto`, `ExportarExcelProduccionRequestDto`
- [ ] Endpoints en controllers
- [ ] Registrar servicio en DI (Program.cs)

### Frontend (Fase 5F)
- [ ] Agregar botón "Descargar Excel" en template HTML
- [ ] Método `descargarExcel()` en componente
- [ ] Método helper `_GenerarNombreArchivo()`
- [ ] Método helper `_DescargarBlob()`
- [ ] Métodos `exportarExcelLevante()` y `exportarExcelProduccion()` en service HTTP
- [ ] Manejo de errores

### Testing
- [ ] Descargar Excel desde LEVANTE (verificar hojas y formato)
- [ ] Descargar Excel desde PRODUCCIÓN (verificar hojas por galpón)
- [ ] Verificar nombre de archivo dinámico
- [ ] Verificar colores de semáforo (verde/amarillo/rojo)
- [ ] Validar que no se modifique nada de LEVANTE cuando está activo PRODUCCIÓN

---

## Notas Técnicas

1. **Librería recomendada:** ClosedXML (open-source, sin dependencias)
   - Instalación: `dotnet add package ClosedXML`

2. **Response en frontend:** El blob se descarga automáticamente con `window.URL.createObjectURL()`

3. **Tamaño de archivo:** Reportes con 100 filas × 50 columnas generan ~200KB. Aceptable.

4. **Performance:** La generación de Excel se hace en backend (no en frontend) para no bloquear UI

5. **Localización:** Las fechas se formatean en el timezone del servidor

---

## Orden de Implementación Recomendado

1. **Backend first:** Implementar servicio de exportación (5-6 horas)
2. **Test Swagger:** Verificar que el endpoint retorna Excel válido
3. **Frontend:** Agregar botón y métodos (2-3 horas)
4. **Integración E2E:** Descargar desde UI y validar archivo

**Total: ~8-10 horas**

---

## Validaciones Críticas

✓ El botón se habilita SOLO si hay datos (`reporteLevante()` o `reporteProduccionTabs()`)  
✓ El nombre dinámico refleja los filtros seleccionados  
✓ Las hojas incluyen TODOS los datos visibles en UI (no hay truncado)  
✓ Los colores de semáforo se calculan igual que en frontend  
✓ La hoja "Información" es siempre la primera  
✓ No se modifica nada de LEVANTE cuando está en PRODUCCIÓN (y vice versa)

---

**Archivo de referencia:** `/fase_de_desarrollo/03_req_reportes_tabs.md`  
**Documento validación:** `/fase_de_desarrollo/04_validacion_campos_excel.md`
