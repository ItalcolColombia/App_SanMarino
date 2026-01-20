# 📊 Módulo de Reportes Técnicos

## 📋 Descripción General

El módulo de Reportes Técnicos permite generar reportes diarios y semanales por sublote y consolidados por lote. Estos reportes son elaborados por el Líder Técnico, revisados y enviados por el Director Técnico, y se envían consolidados semanalmente a las áreas de análisis.

## 🎯 Funcionalidades

### Tipos de Reportes

1. **Reporte Diario por Sublote**
   - Genera un reporte diario para un sublote específico (ej: Lote K326 A)
   - Incluye todas las variables diarias

2. **Reporte Diario Consolidado**
   - Genera un reporte diario consolidado para un lote completo (ej: Lote K326)
   - Consolida datos de todos los sublotes del lote

3. **Reporte Semanal por Sublote**
   - Genera un reporte semanal para un sublote específico
   - Consolida datos de la semana (7 días)

4. **Reporte Semanal Consolidado**
   - Genera un reporte semanal consolidado para un lote completo
   - **IMPORTANTE:** Solo consolida semanas completas (7 días) de todos los sublotes
   - Si un sublote tiene menos de 7 días en una semana, esa semana NO se consolida

## 📊 Variables Incluidas

- **Consumo**: Consumo diario y acumulado de alimento (kilos, bultos, gramos/ave)
- **Mortalidad**: Mortalidad diaria, porcentaje diario y acumulado
- **Ingresos de Alimentos**: Entradas de alimento a la granja
- **Consumos de Alimentos**: Consumo registrado diario
- **Traslados de Alimento**: Traslados de alimento entre granjas
- **Peso**: Peso actual, uniformidad, ganancia de peso, coeficiente de variación
- **Selección Ventas**: Aves retiradas para venta

## 🔧 Lógica de Consolidación Semanal

### Reglas de Consolidación:

1. **7 días = 1 semana**: La edad se calcula desde la fecha de encasetamiento
2. **Solo semanas completas**: Para consolidar, todos los sublotes deben tener 7 días completos en esa semana
3. **Ejemplo**:
   - Sublote A: Semana 1 completa (7 días) ✅
   - Sublote B: Semana 1 con solo 5 días ❌
   - **Resultado**: NO se consolida la semana 1
   
4. **Si un sublote no existe en una semana**: Se toma solo el sublote que existe (no el total del lote)

## 📁 Estructura de Archivos

```
backend/src/
├── ZooSanMarino.Application/
│   ├── DTOs/
│   │   └── ReporteTecnicoDto.cs
│   └── Interfaces/
│       └── IReporteTecnicoService.cs
├── ZooSanMarino.Infrastructure/
│   └── Services/
│       ├── ReporteTecnicoService.cs
│       └── ReporteTecnicoExcelService.cs
└── ZooSanMarino.API/
    └── Controllers/
        └── ReporteTecnicoController.cs
```

## 🚀 Endpoints de la API

### 1. Reporte Diario por Sublote
```
GET /api/ReporteTecnico/diario/sublote/{loteId}?fechaInicio={date}&fechaFin={date}
```

### 2. Reporte Diario Consolidado
```
GET /api/ReporteTecnico/diario/consolidado?loteNombre={nombre}&fechaInicio={date}&fechaFin={date}
```

### 3. Reporte Semanal por Sublote
```
GET /api/ReporteTecnico/semanal/sublote/{loteId}?semana={number}
```

### 4. Reporte Semanal Consolidado
```
GET /api/ReporteTecnico/semanal/consolidado?loteNombre={nombre}&semana={number}
```

### 5. Generar Reporte (Genérico)
```
POST /api/ReporteTecnico/generar
Body: GenerarReporteTecnicoRequestDto
```

### 6. Obtener Sublotes
```
GET /api/ReporteTecnico/sublotes?loteNombre={nombre}
```

### 7. Exportar a Excel (Diario)
```
POST /api/ReporteTecnico/exportar/excel/diario
Body: GenerarReporteTecnicoRequestDto
Returns: Excel file
```

### 8. Exportar a Excel (Semanal)
```
POST /api/ReporteTecnico/exportar/excel/semanal
Body: GenerarReporteTecnicoRequestDto
Returns: Excel file
```

## 📝 Ejemplos de Uso

### Ejemplo 1: Generar Reporte Diario para Sublote K326 A
```json
POST /api/ReporteTecnico/generar
{
  "loteId": 123,
  "incluirSemanales": false,
  "consolidarSublotes": false
}
```

### Ejemplo 2: Generar Reporte Diario Consolidado para Lote K326
```json
POST /api/ReporteTecnico/generar
{
  "loteNombre": "K326",
  "incluirSemanales": false,
  "consolidarSublotes": true
}
```

### Ejemplo 3: Exportar Reporte Semanal Consolidado a Excel
```json
POST /api/ReporteTecnico/exportar/excel/semanal
{
  "loteNombre": "K326",
  "incluirSemanales": true,
  "consolidarSublotes": true
}
```

## 📄 Formato de Archivos Excel

### Nombres de Archivo:
- **Diario Sublote**: `Lote_K326_A_Ross_AP_diario_20250115.xlsx`
- **Diario Consolidado**: `Lote_K326_General_Ross_AP_diario_20250115.xlsx`
- **Semanal Sublote**: `Lote_K326_A_Ross_AP_semanal_20250115.xlsx`
- **Semanal Consolidado**: `Lote_K326_General_Ross_AP_semanal_20250115.xlsx`

### Estructura del Excel:
- **Encabezado**: Información del lote (línea, raza, etapa, número de hembras, encasetamiento, galpón)
- **Tabla de Datos**: Columnas según el tipo de reporte (diario o semanal)
- **Formato**: Similar al Excel de ejemplo proporcionado

## 🔍 Identificación de Sublotes

Los sublotes se identifican por el nombre del lote:
- **Lote Base**: "K326"
- **Sublote A**: "K326 A"
- **Sublote B**: "K326 B"

El sistema extrae automáticamente el sublote del nombre del lote.

## ⚙️ Configuración

### Cálculo de Edad:
- **Edad en días**: Diferencia entre fecha de registro y fecha de encasetamiento
- **Edad en semanas**: `ceil(edadDias / 7)`

### Cálculo de Bultos:
- **Peso por bulto**: 40kg (configurable)
- **Bultos**: `kilos / 40`

### Consolidación Semanal:
- Solo se consolida si todos los sublotes tienen 7 días completos en esa semana
- Si un sublote no existe en una semana, se toma solo el sublote que existe

## 🛠️ Servicios Registrados

Los siguientes servicios están registrados en `Program.cs`:
- `IReporteTecnicoService` → `ReporteTecnicoService`
- `ReporteTecnicoExcelService`

## 📌 Notas Importantes

1. **Semanas Completas**: La consolidación semanal solo funciona si todos los sublotes tienen la semana completa (7 días)
2. **Datos de Alimentos**: Los ingresos y traslados de alimentos se obtienen de `FarmInventoryMovement`
3. **Etapas**: El sistema detecta automáticamente si el lote está en LEVANTE o PRODUCCIÓN
4. **Ganancia de Peso**: Se calcula comparando el peso actual con el peso del registro anterior

## 🔄 Flujo de Trabajo

1. **Líder Técnico**: Elabora el reporte diario
2. **Director Técnico**: Revisa y envía el reporte
3. **Consolidación Semanal**: Se envía consolidado semanal a áreas de análisis
4. **Exportación**: Los reportes se pueden exportar a Excel para distribución


