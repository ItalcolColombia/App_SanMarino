# 🧮 Modal de Liquidación Técnica - Seguimiento Levante

## 📋 Resumen Ejecutivo

El modal de **Liquidación Técnica** permite calcular y visualizar métricas acumuladas del desempeño de un lote de levante hasta la **semana 25** (175 días). Compara el desempeño real del lote con los valores esperados según la **Guía Genética** de la raza.

---

## 🏗️ Arquitectura del Sistema

### **Frontend**
```
modal-liquidacion.component
    ├── liquidacion-tecnica.component (componente principal)
    └── liquidacion-comparacion.component (comparación con guía)
```

### **Backend**
```
LiquidacionTecnicaController
    └── LiquidacionTecnicaService
        ├── ObtenerLoteAsync()
        ├── ObtenerSeguimientosAsync()
        ├── ObtenerDatosGuiaAsync()
        ├── CalcularMetricasAcumuladas()
        └── CalcularDiferenciasConGuia()
```

---

## 🔄 Flujo de Datos

### **1. Inicio del Modal**
```typescript
// Usuario hace clic en "Liquidación técnica"
seguimiento-lote-levante-list.component → abreModalLiquidacion()

// Modal recibe inputs
<app-modal-liquidacion
  [isOpen]="liquidacionOpen"
  [loteId]="selectedLoteId"
  [loteNombre]="selectedLoteNombre">
</app-modal-liquidacion>
```

### **2. Componente Principal**
```typescript
liquidacion-tecnica.component:
  - Recibe @Input loteId y loteNombre
  - ngOnChanges() detecta cambios en loteId
  - Ejecuta cargarDatosLote() y cargarLiquidacion()
```

### **3. Carga de Datos del Lote**
```typescript
obtenerDatosCompletosLote(loteId) → GET /api/lotes/{loteId}

Retorna: LoteDto con:
  - LoteId, LoteNombre
  - FechaEncaset, Raza, AnoTablaGenetica
  - HembrasL, MachosL, TotalAvesIniciales
  - Granja, Núcleo, Galpón
```

### **4. Cálculo de Liquidación**
```typescript
getLiquidacionTecnica(loteId, fechaHasta) 
  → GET /api/LiquidacionTecnica/{loteId}?fechaHasta={fecha}

El backend procesa:
  1. Obtiene datos del lote
  2. Obtiene seguimientos hasta semana 25
  3. Obtiene datos de guía genética
  4. Calcula métricas acumuladas
   converte difereces con guía
```

---

## 🧮 Cálculos Realizados (Backend)

### **A. Métricas Acumuladas**

#### **1. Mortalidad**
```csharp
TotalMortalidadH = Sum(seguimientos.MortalidadHembras)
TotalMortalidadM = Sum(seguimientos.MortalidadMachos)

%MortalidadH = (TotalMortalidadH / HembrasIniciales) * 100
%MortalidadM = (TotalMortalidadM / MachosIniciales) * 100
```

#### **2. Selección**
```csharp
TotalSeleccionH = Sum(seguimientos.SelH)
TotalSeleccionM = Sum(seguimientos.SelM)

%SeleccionH = (TotalSeleccionH / HembrasIniciales) * 100
%SeleccionM = (TotalSeleccionM / MachosIniciales) * 100
```

#### **3. Error de Sexaje**
```csharp
TotalErrorH = Sum(seguimientos.ErrorSexajeHembras)
TotalErrorM = Sum(seguimientos.ErrorSexajeMachos)

%ErrorH = (TotalErrorH / HembrasIniciales) * 100
%ErrorM = (TotalErrorM / MachosIniciales) * 100
```

#### **4. Retiro Total**
```csharp
%RetiroH = %MortalidadH + %SeleccionH + %ErrorH
%RetiroM = %MortalidadM + %SeleccionM + %ErrorM
%RetiroGeneral = (TotalRetiros / TotalAvesIniciales) * 100
```

#### **5. Consumo**
```csharp
ConsumoTotalKg = Sum(seguimientos.ConsumoKgHembras + ConsumoKgMachos)
ConsumoTotalGramos = ConsumoTotalKg * 1000
```

#### **6. Peso y Uniformidad Final**
```csharp
// Toma el último registro disponible
PesoFinalH = ultimoSeguimiento.PesoPromH
PesoFinalM = ultimoSeguimiento.PesoPromM
UniformidadFinalH = ultimoSeguimiento.UniformidadH
UniformidadFinalM = ultimoSeguimiento.UniformidadM
```

### **B. Comparación con Guía Genética**

#### **1. Obtener Datos de Guía**
```csharp
DatosGuia = SELECT FROM ProduccionAvicolaRaw
WHERE Raza = lote.Raza 
  AND AnioGuia = lote.AnoTablaGenetica
  AND Edad = "175"  // Semana 25
```

#### **2. Calcular Diferencias Porcentuales**
```csharp
DiferenciaConsumo = ((ConsumoReal - ConsumoGuia) / ConsumoGuia) * 100
DiferenciaPesoH = ((PesoRealH - PesoGuiaH) / PesoGuiaH) * 100
DiferenciaUnifH = ((UnifRealH - UnifGuia) / UnifGuia) * 100
```

---

## 📊 Estructura de Datos

### **LiquidacionTecnicaDto**
```typescript
{
  // Identificación
  loteId: string
  loteNombre: string
  fechaEncaset: Date
  raza: string
  anoTablaGenetica: number
  
  // Iniciales
  hembrasEncasetadas: number
  machosEncasetados: number
  totalAvesEncasetadas: number
  
  // Mortalidad
  porcentajeMortalidadHembras: decimal
  porcentajeMortalidadMachos: decimal
  
  // Selección
  porcentajeSeleccionHembras: decimal
  porcentajeSeleccionMachos: decimal
  
  // Error Sexaje
  porcentajeErrorSexajeHembras: decimal
  porcentajeErrorSexajeMachos: decimal
  
  // Retiros
  porcentajeRetiroTotalHembras: decimal
  porcentajeRetiroTotalMachos: decimal
  porcentajeRetiroTotalGeneral: decimal
  porcentajeRetiroGuia: decimal
  
  // Consumo
  consumoAlimentoRealGramos: decimal
  consumoAlimentoGuiaGramos: decimal
  porcentajeDiferenciaConsumo: decimal
  
  // Peso Semana 25
  pesoSemana25RealHembras: decimal
  pesoSemana25RealMachos: decimal
  pesoSemana25GuiaHembras: decimal
  porcentajeDiferenciaPesoHembras: decimal
  
  // Uniformidad
  uniformidadRealHembras: decimal
  uniformidadRealMachos: decimal
  uniformidadGuiaHembras: decimal
  porcentajeDiferenciaUniformidadHembras: decimal
}
```

---

## 🎨 Interfaz de Usuario

### **1. Información del Lote**
- Código/Nombre
- Raza
- Año Guía Genética
- Granja, Núcleo, Galpón
- Fecha Encaset
- Edad Actual
- Total Aves Iniciales

### **2. Tabla Comparativa con Guía Genética**
| Concepto | Real | Guía | Diferencia | Estado |
|----------|------|------|------------|--------|
| Mortalidad H (%) | 5.2 | 4.0 | +1.2 | ⚠️ Alerta |
| Mortalidad M (%) | 4.8 | 4.0 | +0.8 | ✅ Buena |
| Consumo (g) | 4200 | 4500 | -300 | ✅ Óptimo |
| Peso H (g) | 2450 | 2500 | -50 | ✅ Óptimo |
| Uniformidad H (%) | 82 | 85 | -3 | ⚠️ Aceptable |

### **3. Gráficos (3 tipos)**
1. **Barras**: Indicadores Real vs Guía
2. **Torta**: Distribución de retiros (Vivas, Mort, Sel, Error)
3. **Líneas**: Evolución semanal de mortalidad, selección, consumo y peso

---

## 🔑 API Endpoints

### **GET /api/LiquidacionTecnica/{loteId}**
Calcula la liquidación técnica básica.

**Query Params:**
- `fechaHasta` (opcional): Fecha límite para el cálculo

**Response:**
```json
{
  "loteId": "123",
  "loteNombre": "LT-2024-001",
  "porcentajeMortalidadHembras": 5.2,
  "consumoAlimentoRealGramos": 4200,
  ...
}
```

### **GET /api/LiquidacionTecnica/{loteId}/completa**
Obtiene liquidación completa con detalles del seguimiento.

**Response:**
```json
{
  "liquidacion": { ... },
  "detalleSeguimiento": [
    {
      "fecha": "2024-01-15",
      "semana": 1,
      "mortalidadHembras": 10,
      ...
    }
  ],
  "datosGuia": { ... }
}
```

---

## 📝 Fórmulas Clave

### **Cálculo de Semana**
```csharp
Dias = (FechaRegistro - FechaEncaset).Days
Semana = (Dias / 7) + 1
```

### **Cálculo de Diferencia Porcentual**
```csharp
Diferencia = ((ValorReal - ValorGuia) / ValorGuia) * 100
```

### **Conversión a Gramos**
```csharp
ConsumoGramos = ConsumoKg * 1000
```

---

## ⚠️ Validaciones y Errores

### **Validaciones del Servicio**
1. Lote debe existir y pertenecer a la compañía
2. Lote debe tener registros de seguimiento
3. Raza y AñoTablaGenetica deben estar definidos para obtener guía
4. Seguimientos se filtran hasta semana 25 (175 días)

### **Mensajes de Error**
- `404`: Lote no encontrado
- `400`: Parámetros inválidos
- `500`: Error interno del servidor

---

## 🎯 Estado de Indicadores

### **Clases CSS según Diferencias**
- **verde** (`estado-bueno`): Diferencia ≤ 5% (pesos) o ≤ 10% (consumo)
- **amarillo** (`estado-alerta`): Diferencia entre 5-10% o 10-20%
- **rojo** (`estado-critico`): Diferencia > 10% o > 20%

---

## 📚 Archivos Relacionados

### **Frontend**
- `modal-liquidacion.component.ts/html`
- `liquidacion-tecnica.component.ts/html/scss`
- `liquidacion-tecnica.service.ts`
- `liquidacion-comparacion.service.ts`

### **Backend**
- `LiquidacionTecnicaController.cs`
- `LiquidacionTecnicaService.cs`
- `ILiquidacionTecnicaService.cs`
- `LiquidacionTecnicaDto.cs`

---

## 🔧 Mantenimiento

### **Agregar Nuevo Indicador**
1. Agregar campo al `LiquidacionTecnicaDto`
2. Actualizar cálculo en `CalcularMetricasAcumuladas()`
3. Agregar a tabla comparativa en template
4. Actualizar método `indicadores` del componente

### **Modificar Rango de Semanas**
Cambiar en `ObtenerSeguimientosAsync()`:
```csharp
var fechaMaxima = lote.FechaEncaset.Value.AddDays(175); // Actualmente semana 25
```

---

**Última actualización**: Octubre 2025




