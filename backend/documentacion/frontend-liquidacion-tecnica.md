# 🎯 Requisitos Frontend - Sistema de Liquidación Técnica

## 📋 **Resumen del Sistema**

El sistema de **Liquidación Técnica** permite calcular y gestionar el cierre técnico de lotes de aves hasta la semana 25, comparando datos reales vs guía genética.

## 🔌 **Endpoints Disponibles**

### **Base URL:** `http://localhost:5002/api/LiquidacionTecnica`

---

## 📊 **1. Obtener Liquidación Técnica Simple**

### **GET** `/api/LiquidacionTecnica/{loteId}`

**Descripción:** Obtiene la liquidación técnica básica de un lote.

**Parámetros:**
- `loteId` (string, path): ID del lote
- `fechaHasta` (DateTime, query): Fecha límite para el cálculo

**Ejemplo de Request:**
```http
GET /api/LiquidacionTecnica/L001?fechaHasta=2024-10-02T23:59:59Z
Authorization: Bearer {token}
```

**Response 200:**
```json
{
  "loteId": "L001",
  "loteNombre": "Lote Cobb 500 - Granja Norte",
  "fechaEncaset": "2024-03-15T00:00:00Z",
  "raza": "Cobb 500",
  "anoTablaGenetica": 2024,
  "hembrasEncasetadas": 5000,
  "machosEncasetados": 500,
  "totalAvesEncasetadas": 5500,
  "porcentajeMortalidadHembras": 3.2,
  "porcentajeMortalidadMachos": 4.1,
  "porcentajeSeleccionHembras": 1.5,
  "porcentajeSeleccionMachos": 2.0,
  "porcentajeErrorSexajeHembras": 0.3,
  "porcentajeErrorSexajeMachos": 0.5,
  "porcentajeRetiroTotalHembras": 5.0,
  "porcentajeRetiroTotalMachos": 6.6,
  "porcentajeRetiroTotalGeneral": 5.2,
  "porcentajeRetiroGuia": 4.8,
  "consumoAlimentoRealGramos": 2850.5,
  "consumoAlimentoGuiaGramos": 2800.0,
  "porcentajeDiferenciaConsumo": 1.8,
  "pesoSemana25RealHembras": 2450.0,
  "pesoSemana25GuiaHembras": 2400.0,
  "porcentajeDiferenciaPesoHembras": 2.1,
  "uniformidadRealHembras": 85.5,
  "uniformidadGuiaHembras": 88.0,
  "porcentajeDiferenciaUniformidadHembras": -2.8
}
```

---

## 📈 **2. Obtener Liquidación Técnica Completa**

### **GET** `/api/LiquidacionTecnica/{loteId}/completa`

**Descripción:** Obtiene la liquidación técnica con detalles de seguimiento y guía genética.

**Parámetros:**
- `loteId` (string, path): ID del lote
- `fechaHasta` (DateTime, query): Fecha límite para el cálculo

**Ejemplo de Request:**
```http
GET /api/LiquidacionTecnica/L001/completa?fechaHasta=2024-10-02T23:59:59Z
Authorization: Bearer {token}
```

**Response 200:**
```json
{
  "resumen": {
    // ... mismo objeto que el endpoint simple
  },
  "detallesSeguimiento": [
    {
      "id": 1,
      "loteId": "L001",
      "semana": 1,
      "fechaRegistro": "2024-03-22T00:00:00Z",
      "mortalidadHembras": 10,
      "mortalidadMachos": 2,
      "seleccionHembras": 5,
      "seleccionMachos": 1,
      "consumoAlimento": 125.5,
      "pesoPromedioHembras": 180.0,
      "uniformidadHembras": 92.0,
      "observaciones": "Semana normal"
    }
    // ... más registros por semana
  ],
  "detallesGuiaGenetica": [
    {
      "id": 1,
      "anioGuia": "2024",
      "raza": "Cobb 500",
      "edad": "1",
      "mortSemH": "0.2",
      "retiroAcH": "0.2",
      "mortSemM": "0.3",
      "retiroAcM": "0.3",
      "consAcH": "125",
      "pesoH": "180",
      "uniformidad": "92"
      // ... más campos de la guía genética
    }
    // ... más registros por edad/semana
  ]
}
```

---

## 🧮 **3. Calcular Liquidación Técnica**

### **POST** `/api/LiquidacionTecnica/calcular`

**Descripción:** Calcula la liquidación técnica para un lote específico.

**Request Body:**
```json
{
  "loteId": "L001",
  "fechaHasta": "2024-10-02T23:59:59Z"
}
```

**Response 200:**
```json
{
  // ... mismo formato que GET simple
}
```

---

## ✅ **4. Validar Lote**

### **GET** `/api/LiquidacionTecnica/{loteId}/validar`

**Descripción:** Valida si un lote existe y tiene datos para liquidación.

**Response 200:**
```json
true
```

**Response 404:**
```json
false
```

---

## 🔍 **5. Validar Múltiples Lotes**

### **POST** `/api/LiquidacionTecnica/validar-multiples`

**Descripción:** Valida múltiples lotes de una vez.

**Request Body:**
```json
["L001", "L002", "L003", "L004"]
```

**Response 200:**
```json
{
  "L001": true,
  "L002": true,
  "L003": false,
  "L004": true
}
```

---

## 🎨 **Interfaces TypeScript Recomendadas**

### **Interfaces Principales:**

```typescript
// Liquidación Técnica Básica
export interface LiquidacionTecnicaDto {
  loteId: string;
  loteNombre: string;
  fechaEncaset?: Date;
  raza?: string;
  anoTablaGenetica?: number;
  hembrasEncasetadas: number;
  machosEncasetados: number;
  totalAvesEncasetadas: number;
  porcentajeMortalidadHembras: number;
  porcentajeMortalidadMachos: number;
  porcentajeSeleccionHembras: number;
  porcentajeSeleccionMachos: number;
  porcentajeErrorSexajeHembras: number;
  porcentajeErrorSexajeMachos: number;
  porcentajeRetiroTotalHembras: number;
  porcentajeRetiroTotalMachos: number;
  porcentajeRetiroTotalGeneral: number;
  porcentajeRetiroGuia?: number;
  consumoAlimentoRealGramos: number;
  consumoAlimentoGuiaGramos?: number;
  porcentajeDiferenciaConsumo?: number;
  pesoSemana25RealHembras?: number;
  pesoSemana25GuiaHembras?: number;
  porcentajeDiferenciaPesoHembras?: number;
  uniformidadRealHembras?: number;
  uniformidadGuiaHembras?: number;
  porcentajeDiferenciaUniformidadHembras?: number;
}

// Liquidación Técnica Completa
export interface LiquidacionTecnicaCompletaDto {
  resumen: LiquidacionTecnicaDto;
  detallesSeguimiento: SeguimientoLoteLevanteDto[];
  detallesGuiaGenetica: ProduccionAvicolaRawDto[];
}

// Request para cálculo
export interface LiquidacionTecnicaRequest {
  loteId: string;
  fechaHasta: Date;
}

// Seguimiento semanal
export interface SeguimientoLoteLevanteDto {
  id: number;
  loteId: string;
  semana: number;
  fechaRegistro: Date;
  mortalidadHembras?: number;
  mortalidadMachos?: number;
  seleccionHembras?: number;
  seleccionMachos?: number;
  errorSexajeHembras?: number;
  errorSexajeMachos?: number;
  consumoAlimento?: number;
  pesoPromedioHembras?: number;
  pesoPromedioMachos?: number;
  uniformidadHembras?: number;
  uniformidadMachos?: number;
  observaciones?: string;
}

// Guía genética
export interface ProduccionAvicolaRawDto {
  id: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  consAcH?: string;
  consAcM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  // ... más campos según necesidad
}
```

---

## 🛠️ **Servicio Angular Recomendado**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class LiquidacionTecnicaService {
  private baseUrl = 'http://localhost:5002/api/LiquidacionTecnica';

  constructor(private http: HttpClient) {}

  // Obtener liquidación simple
  getLiquidacionTecnica(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaDto> {
    const params = { fechaHasta: fechaHasta.toISOString() };
    return this.http.get<LiquidacionTecnicaDto>(`${this.baseUrl}/${loteId}`, { params });
  }

  // Obtener liquidación completa
  getLiquidacionCompleta(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaCompletaDto> {
    const params = { fechaHasta: fechaHasta.toISOString() };
    return this.http.get<LiquidacionTecnicaCompletaDto>(`${this.baseUrl}/${loteId}/completa`, { params });
  }

  // Calcular liquidación
  calcularLiquidacion(request: LiquidacionTecnicaRequest): Observable<LiquidacionTecnicaDto> {
    return this.http.post<LiquidacionTecnicaDto>(`${this.baseUrl}/calcular`, request);
  }

  // Validar lote
  validarLote(loteId: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/${loteId}/validar`);
  }

  // Validar múltiples lotes
  validarMultiplesLotes(loteIds: string[]): Observable<{[key: string]: boolean}> {
    return this.http.post<{[key: string]: boolean}>(`${this.baseUrl}/validar-multiples`, loteIds);
  }
}
```

---

## 🎨 **Componentes Sugeridos**

### **1. Selector de Lote**
```typescript
// liquidacion-selector.component.ts
export class LiquidacionSelectorComponent {
  loteId: string = '';
  fechaHasta: Date = new Date();
  
  onCalcular() {
    if (this.loteId) {
      // Navegar a vista de resultados o emitir evento
    }
  }
}
```

### **2. Vista de Resultados**
```typescript
// liquidacion-resultados.component.ts
export class LiquidacionResultadosComponent {
  liquidacion: LiquidacionTecnicaDto | null = null;
  loading = false;
  
  constructor(private liquidacionService: LiquidacionTecnicaService) {}
  
  cargarLiquidacion(loteId: string, fechaHasta: Date) {
    this.loading = true;
    this.liquidacionService.getLiquidacionTecnica(loteId, fechaHasta)
      .subscribe({
        next: (data) => {
          this.liquidacion = data;
          this.loading = false;
        },
        error: (error) => {
          console.error('Error al cargar liquidación:', error);
          this.loading = false;
        }
      });
  }
}
```

### **3. Tabla de Comparación**
```typescript
// liquidacion-comparacion.component.ts
export class LiquidacionComparacionComponent {
  @Input() liquidacion: LiquidacionTecnicaDto | null = null;
  
  get indicadores() {
    if (!this.liquidacion) return [];
    
    return [
      {
        concepto: 'Mortalidad Hembras',
        real: this.liquidacion.porcentajeMortalidadHembras,
        guia: null, // Se puede agregar si existe en la guía
        diferencia: null,
        unidad: '%'
      },
      {
        concepto: 'Consumo Alimento',
        real: this.liquidacion.consumoAlimentoRealGramos,
        guia: this.liquidacion.consumoAlimentoGuiaGramos,
        diferencia: this.liquidacion.porcentajeDiferenciaConsumo,
        unidad: 'gr'
      },
      {
        concepto: 'Peso Semana 25',
        real: this.liquidacion.pesoSemana25RealHembras,
        guia: this.liquidacion.pesoSemana25GuiaHembras,
        diferencia: this.liquidacion.porcentajeDiferenciaPesoHembras,
        unidad: 'gr'
      },
      {
        concepto: 'Uniformidad',
        real: this.liquidacion.uniformidadRealHembras,
        guia: this.liquidacion.uniformidadGuiaHembras,
        diferencia: this.liquidacion.porcentajeDiferenciaUniformidadHembras,
        unidad: '%'
      }
    ];
  }
}
```

---

## 📱 **Templates HTML Sugeridos**

### **Selector de Lote:**
```html
<!-- liquidacion-selector.component.html -->
<div class="card">
  <div class="card-header">
    <h5>Liquidación Técnica - Seleccionar Lote</h5>
  </div>
  <div class="card-body">
    <form (ngSubmit)="onCalcular()">
      <div class="row">
        <div class="col-md-6">
          <label for="loteId" class="form-label">Lote ID</label>
          <input 
            type="text" 
            class="form-control" 
            id="loteId" 
            [(ngModel)]="loteId" 
            name="loteId"
            placeholder="Ej: L001"
            required>
        </div>
        <div class="col-md-6">
          <label for="fechaHasta" class="form-label">Fecha Hasta</label>
          <input 
            type="date" 
            class="form-control" 
            id="fechaHasta" 
            [(ngModel)]="fechaHasta" 
            name="fechaHasta"
            required>
        </div>
      </div>
      <div class="mt-3">
        <button type="submit" class="btn btn-primary" [disabled]="!loteId">
          <i class="fas fa-calculator"></i> Calcular Liquidación
        </button>
      </div>
    </form>
  </div>
</div>
```

### **Tabla de Resultados:**
```html
<!-- liquidacion-resultados.component.html -->
<div class="card" *ngIf="liquidacion">
  <div class="card-header">
    <h5>Liquidación Técnica - {{ liquidacion.loteNombre }}</h5>
  </div>
  <div class="card-body">
    <!-- Información General -->
    <div class="row mb-4">
      <div class="col-md-3">
        <strong>Lote:</strong> {{ liquidacion.loteId }}
      </div>
      <div class="col-md-3">
        <strong>Raza:</strong> {{ liquidacion.raza }}
      </div>
      <div class="col-md-3">
        <strong>Fecha Encaset:</strong> {{ liquidacion.fechaEncaset | date }}
      </div>
      <div class="col-md-3">
        <strong>Total Aves:</strong> {{ liquidacion.totalAvesEncasetadas | number }}
      </div>
    </div>

    <!-- Tabla de Indicadores -->
    <table class="table table-striped">
      <thead>
        <tr>
          <th>Indicador</th>
          <th>Real</th>
          <th>Guía</th>
          <th>Diferencia</th>
          <th>Estado</th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td>Mortalidad Hembras</td>
          <td>{{ liquidacion.porcentajeMortalidadHembras | number:'1.2-2' }}%</td>
          <td>-</td>
          <td>-</td>
          <td><span class="badge bg-info">Info</span></td>
        </tr>
        <tr>
          <td>Consumo Alimento</td>
          <td>{{ liquidacion.consumoAlimentoRealGramos | number:'1.1-1' }}g</td>
          <td>{{ liquidacion.consumoAlimentoGuiaGramos | number:'1.1-1' }}g</td>
          <td>
            <span [class]="liquidacion.porcentajeDiferenciaConsumo > 0 ? 'text-danger' : 'text-success'">
              {{ liquidacion.porcentajeDiferenciaConsumo | number:'1.1-1' }}%
            </span>
          </td>
          <td>
            <span [class]="liquidacion.porcentajeDiferenciaConsumo > 5 ? 'badge bg-danger' : 'badge bg-success'">
              {{ liquidacion.porcentajeDiferenciaConsumo > 5 ? 'Alto' : 'Normal' }}
            </span>
          </td>
        </tr>
        <!-- Más filas según necesidad -->
      </tbody>
    </table>
  </div>
</div>

<!-- Loading -->
<div class="text-center" *ngIf="loading">
  <div class="spinner-border" role="status">
    <span class="visually-hidden">Cargando...</span>
  </div>
</div>
```

---

## 🔧 **Configuración de Rutas**

```typescript
// app-routing.module.ts
const routes: Routes = [
  {
    path: 'liquidacion-tecnica',
    children: [
      { path: '', component: LiquidacionSelectorComponent },
      { path: 'resultados/:loteId', component: LiquidacionResultadosComponent },
      { path: 'completa/:loteId', component: LiquidacionCompletaComponent }
    ]
  }
];
```

---

## 🎯 **Funcionalidades Recomendadas**

### **1. Dashboard Principal**
- Selector de lote con autocompletado
- Validación en tiempo real
- Botón de cálculo
- Historial de liquidaciones recientes

### **2. Vista de Resultados**
- Tabla comparativa Real vs Guía
- Gráficos de indicadores clave
- Alertas por valores fuera de rango
- Exportación a PDF/Excel

### **3. Vista Detallada**
- Seguimiento semanal en gráficos
- Tabla de guía genética
- Análisis de tendencias
- Recomendaciones automáticas

### **4. Funciones Adicionales**
- Comparación entre múltiples lotes
- Reportes por período
- Alertas automáticas
- Integración con otros módulos

---

## 🚨 **Manejo de Errores**

```typescript
// Errores comunes y manejo
export class ErrorHandler {
  static handleLiquidacionError(error: any): string {
    switch (error.status) {
      case 404:
        return 'Lote no encontrado o sin datos para liquidación';
      case 400:
        return 'Parámetros inválidos para el cálculo';
      case 500:
        return 'Error interno del servidor';
      default:
        return 'Error desconocido al calcular liquidación';
    }
  }
}
```

---

## ✅ **Checklist de Implementación**

### **Backend (✅ Completado)**
- [x] Endpoints de liquidación técnica
- [x] Cálculos automáticos
- [x] Validaciones de negocio
- [x] DTOs y modelos
- [x] Documentación API

### **Frontend (📋 Por Implementar)**
- [ ] Servicio Angular
- [ ] Interfaces TypeScript
- [ ] Componente selector
- [ ] Componente resultados
- [ ] Componente detallado
- [ ] Rutas y navegación
- [ ] Estilos y UX
- [ ] Manejo de errores
- [ ] Pruebas unitarias

---

## 🎉 **¡Todo Listo para el Frontend!**

Con esta documentación tienes **todo lo necesario** para implementar el CRUD completo de liquidación técnica en Angular. El backend está **100% funcional** y esperando las peticiones del frontend.

**¡Manos a la obra!** 🚀
