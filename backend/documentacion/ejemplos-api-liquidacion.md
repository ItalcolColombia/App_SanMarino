# 🧪 Ejemplos Prácticos - API Liquidación Técnica

## 🎯 **Casos de Uso Reales para el Frontend**

---

## 📋 **1. Flujo Completo de Liquidación**

### **Paso 1: Validar Lote**
```http
GET /api/LiquidacionTecnica/L001/validar
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response Exitosa:**
```json
true
```

**Response Error:**
```json
false
```

### **Paso 2: Obtener Liquidación Simple**
```http
GET /api/LiquidacionTecnica/L001?fechaHasta=2024-10-02T23:59:59Z
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response Completa:**
```json
{
  "loteId": "L001",
  "loteNombre": "Lote Cobb 500 - Granja Norte",
  "fechaEncaset": "2024-03-15T08:00:00Z",
  "raza": "Cobb 500",
  "anoTablaGenetica": 2024,
  "hembrasEncasetadas": 4800,
  "machosEncasetados": 200,
  "totalAvesEncasetadas": 5000,
  "porcentajeMortalidadHembras": 3.25,
  "porcentajeMortalidadMachos": 4.15,
  "porcentajeSeleccionHembras": 1.48,
  "porcentajeSeleccionMachos": 2.05,
  "porcentajeErrorSexajeHembras": 0.32,
  "porcentajeErrorSexajeMachos": 0.48,
  "porcentajeRetiroTotalHembras": 5.05,
  "porcentajeRetiroTotalMachos": 6.68,
  "porcentajeRetiroTotalGeneral": 5.24,
  "porcentajeRetiroGuia": 4.80,
  "consumoAlimentoRealGramos": 2847.5,
  "consumoAlimentoGuiaGramos": 2800.0,
  "porcentajeDiferenciaConsumo": 1.70,
  "pesoSemana25RealHembras": 2445.8,
  "pesoSemana25GuiaHembras": 2400.0,
  "porcentajeDiferenciaPesoHembras": 1.91,
  "uniformidadRealHembras": 85.2,
  "uniformidadGuiaHembras": 88.0,
  "porcentajeDiferenciaUniformidadHembras": -3.18
}
```

---

## 📊 **2. Liquidación Completa con Detalles**

### **Request:**
```http
GET /api/LiquidacionTecnica/L001/completa?fechaHasta=2024-10-02T23:59:59Z
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### **Response Completa:**
```json
{
  "resumen": {
    "loteId": "L001",
    "loteNombre": "Lote Cobb 500 - Granja Norte",
    "fechaEncaset": "2024-03-15T08:00:00Z",
    "raza": "Cobb 500",
    "anoTablaGenetica": 2024,
    "hembrasEncasetadas": 4800,
    "machosEncasetados": 200,
    "totalAvesEncasetadas": 5000,
    "porcentajeMortalidadHembras": 3.25,
    "porcentajeMortalidadMachos": 4.15,
    "porcentajeSeleccionHembras": 1.48,
    "porcentajeSeleccionMachos": 2.05,
    "porcentajeErrorSexajeHembras": 0.32,
    "porcentajeErrorSexajeMachos": 0.48,
    "porcentajeRetiroTotalHembras": 5.05,
    "porcentajeRetiroTotalMachos": 6.68,
    "porcentajeRetiroTotalGeneral": 5.24,
    "porcentajeRetiroGuia": 4.80,
    "consumoAlimentoRealGramos": 2847.5,
    "consumoAlimentoGuiaGramos": 2800.0,
    "porcentajeDiferenciaConsumo": 1.70,
    "pesoSemana25RealHembras": 2445.8,
    "pesoSemana25GuiaHembras": 2400.0,
    "porcentajeDiferenciaPesoHembras": 1.91,
    "uniformidadRealHembras": 85.2,
    "uniformidadGuiaHembras": 88.0,
    "porcentajeDiferenciaUniformidadHembras": -3.18
  },
  "detallesSeguimiento": [
    {
      "id": 1,
      "loteId": "L001",
      "semana": 1,
      "fechaRegistro": "2024-03-22T10:00:00Z",
      "mortalidadHembras": 12,
      "mortalidadMachos": 2,
      "seleccionHembras": 5,
      "seleccionMachos": 1,
      "errorSexajeHembras": 1,
      "errorSexajeMachos": 0,
      "consumoAlimento": 125.8,
      "pesoPromedioHembras": 182.5,
      "pesoPromedioMachos": 185.2,
      "uniformidadHembras": 92.1,
      "uniformidadMachos": 89.5,
      "observaciones": "Primera semana normal, buena adaptación"
    },
    {
      "id": 2,
      "loteId": "L001",
      "semana": 2,
      "fechaRegistro": "2024-03-29T10:00:00Z",
      "mortalidadHembras": 8,
      "mortalidadMachos": 1,
      "seleccionHembras": 3,
      "seleccionMachos": 0,
      "errorSexajeHembras": 0,
      "errorSexajeMachos": 1,
      "consumoAlimento": 245.2,
      "pesoPromedioHembras": 385.8,
      "pesoPromedioMachos": 395.1,
      "uniformidadHembras": 90.8,
      "uniformidadMachos": 88.2,
      "observaciones": "Desarrollo dentro de parámetros esperados"
    }
    // ... más semanas hasta la 25
  ],
  "detallesGuiaGenetica": [
    {
      "id": 1,
      "companyId": 1,
      "anioGuia": "2024",
      "raza": "Cobb 500",
      "edad": "1",
      "mortSemH": "0.25",
      "retiroAcH": "0.25",
      "mortSemM": "0.40",
      "retiroAcM": "0.40",
      "consAcH": "125",
      "consAcM": "130",
      "grAveDiaH": "18.0",
      "grAveDiaM": "18.5",
      "pesoH": "180",
      "pesoM": "185",
      "uniformidad": "92",
      "hTotalAa": "0",
      "prodPorcentaje": "0",
      "hIncAa": "0",
      "aprovSem": "0",
      "pesoHuevo": "0",
      "masaHuevo": "0",
      "grasaPorcentaje": "0",
      "nacimPorcentaje": "0",
      "pollitoAa": "0",
      "kcalAveDiaH": "0",
      "kcalAveDiaM": "0",
      "aprovAc": "0",
      "grHuevoT": "0",
      "grHuevoInc": "0",
      "grPollito": "0",
      "valor1000": "0",
      "valor150": "0",
      "apareo": "0",
      "pesoMh": "0"
    }
    // ... más registros por edad
  ]
}
```

---

## 🧮 **3. Cálculo Mediante POST**

### **Request:**
```http
POST /api/LiquidacionTecnica/calcular
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "loteId": "L002",
  "fechaHasta": "2024-09-30T23:59:59Z"
}
```

### **Response:**
```json
{
  "loteId": "L002",
  "loteNombre": "Lote Ross 308 - Granja Sur",
  "fechaEncaset": "2024-02-20T08:00:00Z",
  "raza": "Ross 308",
  "anoTablaGenetica": 2024,
  "hembrasEncasetadas": 5200,
  "machosEncasetados": 300,
  "totalAvesEncasetadas": 5500,
  "porcentajeMortalidadHembras": 2.98,
  "porcentajeMortalidadMachos": 3.85,
  "porcentajeSeleccionHembras": 1.25,
  "porcentajeSeleccionMachos": 1.88,
  "porcentajeErrorSexajeHembras": 0.28,
  "porcentajeErrorSexajeMachos": 0.42,
  "porcentajeRetiroTotalHembras": 4.51,
  "porcentajeRetiroTotalMachos": 6.15,
  "porcentajeRetiroTotalGeneral": 4.68,
  "porcentajeRetiroGuia": 4.50,
  "consumoAlimentoRealGramos": 2785.2,
  "consumoAlimentoGuiaGramos": 2750.0,
  "porcentajeDiferenciaConsumo": 1.28,
  "pesoSemana25RealHembras": 2380.5,
  "pesoSemana25GuiaHembras": 2350.0,
  "porcentajeDiferenciaPesoHembras": 1.30,
  "uniformidadRealHembras": 87.8,
  "uniformidadGuiaHembras": 89.0,
  "porcentajeDiferenciaUniformidadHembras": -1.35
}
```

---

## ✅ **4. Validación Múltiple**

### **Request:**
```http
POST /api/LiquidacionTecnica/validar-multiples
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

["L001", "L002", "L003", "L999", "L004"]
```

### **Response:**
```json
{
  "L001": true,
  "L002": true,
  "L003": false,
  "L999": false,
  "L004": true
}
```

---

## 🚨 **5. Manejo de Errores**

### **Lote No Encontrado (404):**
```http
GET /api/LiquidacionTecnica/L999?fechaHasta=2024-10-02T23:59:59Z
```

**Response:**
```json
{
  "error": "Lote 'L999' no encontrado o sin datos para la fecha."
}
```

### **Parámetros Inválidos (400):**
```http
POST /api/LiquidacionTecnica/calcular
Content-Type: application/json

{
  "loteId": "",
  "fechaHasta": "fecha-invalida"
}
```

**Response:**
```json
{
  "error": "Parámetros inválidos",
  "details": {
    "loteId": ["El campo LoteId es requerido"],
    "fechaHasta": ["Formato de fecha inválido"]
  }
}
```

### **Error Interno (500):**
```json
{
  "error": "Error interno del servidor",
  "message": "Error al calcular liquidación técnica"
}
```

---

## 🎨 **6. Ejemplos de Uso en Angular**

### **Servicio Completo:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class LiquidacionTecnicaService {
  private baseUrl = 'http://localhost:5002/api/LiquidacionTecnica';

  constructor(private http: HttpClient) {}

  // Obtener liquidación simple
  getLiquidacionTecnica(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaDto> {
    const params = { fechaHasta: fechaHasta.toISOString() };
    return this.http.get<LiquidacionTecnicaDto>(`${this.baseUrl}/${loteId}`, { params })
      .pipe(
        catchError(this.handleError)
      );
  }

  // Obtener liquidación completa
  getLiquidacionCompleta(loteId: string, fechaHasta: Date): Observable<LiquidacionTecnicaCompletaDto> {
    const params = { fechaHasta: fechaHasta.toISOString() };
    return this.http.get<LiquidacionTecnicaCompletaDto>(`${this.baseUrl}/${loteId}/completa`, { params })
      .pipe(
        catchError(this.handleError)
      );
  }

  // Calcular liquidación
  calcularLiquidacion(request: LiquidacionTecnicaRequest): Observable<LiquidacionTecnicaDto> {
    return this.http.post<LiquidacionTecnicaDto>(`${this.baseUrl}/calcular`, request)
      .pipe(
        catchError(this.handleError)
      );
  }

  // Validar lote
  validarLote(loteId: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/${loteId}/validar`)
      .pipe(
        catchError((error) => {
          if (error.status === 404) {
            return [false];
          }
          return this.handleError(error);
        })
      );
  }

  // Validar múltiples lotes
  validarMultiplesLotes(loteIds: string[]): Observable<{[key: string]: boolean}> {
    return this.http.post<{[key: string]: boolean}>(`${this.baseUrl}/validar-multiples`, loteIds)
      .pipe(
        catchError(this.handleError)
      );
  }

  // Manejo de errores
  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Error desconocido';
    
    if (error.error instanceof ErrorEvent) {
      // Error del lado del cliente
      errorMessage = `Error: ${error.error.message}`;
    } else {
      // Error del lado del servidor
      switch (error.status) {
        case 400:
          errorMessage = 'Parámetros inválidos para el cálculo';
          break;
        case 401:
          errorMessage = 'No autorizado. Inicie sesión nuevamente';
          break;
        case 404:
          errorMessage = 'Lote no encontrado o sin datos para liquidación';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }
    
    console.error('Error en LiquidacionTecnicaService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
```

### **Componente de Ejemplo:**
```typescript
import { Component, OnInit } from '@angular/core';
import { LiquidacionTecnicaService } from './liquidacion-tecnica.service';

@Component({
  selector: 'app-liquidacion-tecnica',
  templateUrl: './liquidacion-tecnica.component.html'
})
export class LiquidacionTecnicaComponent implements OnInit {
  loteId: string = '';
  fechaHasta: Date = new Date();
  liquidacion: LiquidacionTecnicaDto | null = null;
  loading = false;
  error: string | null = null;

  constructor(private liquidacionService: LiquidacionTecnicaService) {}

  ngOnInit() {
    // Establecer fecha por defecto (hoy)
    this.fechaHasta = new Date();
  }

  async onCalcular() {
    if (!this.loteId.trim()) {
      this.error = 'Por favor ingrese un ID de lote';
      return;
    }

    this.loading = true;
    this.error = null;
    this.liquidacion = null;

    try {
      // Primero validar el lote
      const esValido = await this.liquidacionService.validarLote(this.loteId).toPromise();
      
      if (!esValido) {
        this.error = 'El lote no existe o no tiene datos suficientes';
        this.loading = false;
        return;
      }

      // Obtener la liquidación
      this.liquidacion = await this.liquidacionService
        .getLiquidacionTecnica(this.loteId, this.fechaHasta)
        .toPromise();

    } catch (error: any) {
      this.error = error.message || 'Error al calcular liquidación';
    } finally {
      this.loading = false;
    }
  }

  // Métodos auxiliares para la vista
  getEstadoIndicador(real: number, guia: number | null, tolerancia: number = 5): string {
    if (!guia) return 'info';
    
    const diferencia = Math.abs(((real - guia) / guia) * 100);
    
    if (diferencia <= tolerancia) return 'success';
    if (diferencia <= tolerancia * 2) return 'warning';
    return 'danger';
  }

  formatearPorcentaje(valor: number | null): string {
    return valor ? `${valor.toFixed(2)}%` : '-';
  }

  formatearPeso(valor: number | null): string {
    return valor ? `${valor.toFixed(1)}g` : '-';
  }
}
```

---

## 📊 **7. Datos de Prueba**

### **Lotes Disponibles para Pruebas:**
```json
{
  "lotesDisponibles": [
    {
      "loteId": "L001",
      "nombre": "Lote Cobb 500 - Granja Norte",
      "raza": "Cobb 500",
      "fechaEncaset": "2024-03-15",
      "estado": "Activo",
      "semanaActual": 25
    },
    {
      "loteId": "L002", 
      "nombre": "Lote Ross 308 - Granja Sur",
      "raza": "Ross 308",
      "fechaEncaset": "2024-02-20",
      "estado": "Activo",
      "semanaActual": 28
    },
    {
      "loteId": "L003",
      "nombre": "Lote Cobb 500 - Granja Este",
      "raza": "Cobb 500", 
      "fechaEncaset": "2024-04-01",
      "estado": "Liquidado",
      "semanaActual": 25
    }
  ]
}
```

### **Rangos Esperados:**
```json
{
  "rangosNormales": {
    "mortalidadHembras": { "min": 2.0, "max": 5.0 },
    "mortalidadMachos": { "min": 3.0, "max": 6.0 },
    "consumoAlimento": { "min": 2700, "max": 2900 },
    "pesoSemana25": { "min": 2300, "max": 2500 },
    "uniformidad": { "min": 85, "max": 92 },
    "retiroTotal": { "min": 4.0, "max": 6.0 }
  }
}
```

---

## 🎯 **8. Checklist de Implementación Frontend**

### **Funcionalidades Básicas:**
- [ ] Selector de lote con validación
- [ ] Selector de fecha hasta
- [ ] Botón calcular con loading
- [ ] Tabla de resultados básica
- [ ] Manejo de errores

### **Funcionalidades Avanzadas:**
- [ ] Autocompletado de lotes
- [ ] Validación en tiempo real
- [ ] Gráficos comparativos
- [ ] Exportación de datos
- [ ] Historial de consultas

### **UX/UI:**
- [ ] Diseño responsive
- [ ] Indicadores visuales de estado
- [ ] Tooltips explicativos
- [ ] Animaciones de carga
- [ ] Mensajes de éxito/error

### **Optimización:**
- [ ] Cache de resultados
- [ ] Lazy loading de componentes
- [ ] Paginación de datos
- [ ] Filtros avanzados
- [ ] Búsqueda rápida

---

## ✅ **¡Todo Listo para Implementar!**

Con estos ejemplos y documentación, el equipo de frontend tiene **todo lo necesario** para implementar un sistema completo y funcional de liquidación técnica.

**El backend está 100% operativo y esperando las peticiones!** 🚀
