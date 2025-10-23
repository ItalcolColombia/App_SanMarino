# 🧪 Ejemplos Prácticos - API Traslados de Aves

## 🎯 **Casos de Uso Reales para el Frontend**

---

## 📋 **1. Flujo Completo de Traslado**

### **Paso 1: Consultar Inventario Actual**
```http
GET /api/InventarioAves/lote/L001
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response Exitosa:**
```json
{
  "id": 1,
  "companyId": 1,
  "loteId": "L001",
  "granjaId": 1,
  "nucleoId": "N001",
  "galponId": "G001",
  "cantidadHembras": 4500,
  "cantidadMachos": 500,
  "fechaUltimoConteo": "2024-10-01T10:00:00Z",
  "createdAt": "2024-09-15T08:00:00Z",
  "updatedAt": "2024-10-01T10:00:00Z"
}
```

### **Paso 2: Realizar Traslado Rápido**
```http
POST /api/MovimientoAves/traslado-rapido
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "loteOrigenId": "L001",
  "loteDestinoId": "L002",
  "cantidadHembras": 100,
  "cantidadMachos": 10,
  "observaciones": "Traslado por capacidad del galpón"
}
```

**Response Exitosa:**
```json
{
  "success": true,
  "message": "Traslado realizado exitosamente",
  "movimientoId": 15,
  "inventarioOrigenActualizado": {
    "loteId": "L001",
    "cantidadHembras": 4400,
    "cantidadMachos": 490,
    "totalAves": 4890,
    "fechaActualizacion": "2024-10-02T14:05:00Z"
  },
  "inventarioDestinoActualizado": {
    "loteId": "L002",
    "cantidadHembras": 3900,
    "cantidadMachos": 210,
    "totalAves": 4110,
    "fechaActualizacion": "2024-10-02T14:05:00Z"
  }
}
```

### **Paso 3: Verificar Historial**
```http
GET /api/HistorialInventario/trazabilidad/L001
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:**
```json
{
  "loteId": "L001",
  "loteNombre": "Lote Cobb 500 - Granja Norte",
  "granjaActual": "Granja Norte",
  "nucleoActual": "Núcleo Principal",
  "galponActual": "Galpón A1",
  "inventarioActual": {
    "cantidadHembras": 4400,
    "cantidadMachos": 490,
    "totalAves": 4890
  },
  "eventos": [
    {
      "fecha": "2024-09-15T08:00:00Z",
      "tipoEvento": "Creación",
      "descripcion": "Inventario inicial creado",
      "cantidadHembrasAntes": 0,
      "cantidadMachosAntes": 0,
      "cantidadHembrasDespues": 5000,
      "cantidadMachosDespues": 500,
      "usuario": "admin@zoosanmarino.com"
    },
    {
      "fecha": "2024-10-02T14:05:00Z",
      "tipoEvento": "Movimiento",
      "descripcion": "Traslado a lote L002",
      "cantidadHembrasAntes": 4500,
      "cantidadMachosAntes": 500,
      "cantidadHembrasDespues": 4400,
      "cantidadMachosDespues": 490,
      "usuario": "operador@zoosanmarino.com",
      "referenciaMovimiento": "15",
      "observaciones": "Traslado por capacidad del galpón"
    }
  ],
  "estadisticas": {
    "totalMovimientos": 1,
    "totalAjustes": 0,
    "totalMortalidad": 0,
    "fechaPrimerRegistro": "2024-09-15T08:00:00Z",
    "fechaUltimoMovimiento": "2024-10-02T14:05:00Z"
  }
}
```

---

## 📊 **2. Gestión de Inventarios**

### **Búsqueda Avanzada de Inventarios**
```http
POST /api/InventarioAves/search
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "granjaId": 1,
  "nucleoId": "N001",
  "soloActivos": true,
  "fechaDesde": "2024-09-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "sortBy": "lote_id",
  "sortDesc": false,
  "page": 1,
  "pageSize": 10
}
```

**Response:**
```json
{
  "items": [
    {
      "id": 1,
      "companyId": 1,
      "loteId": "L001",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G001",
      "cantidadHembras": 4400,
      "cantidadMachos": 490,
      "fechaUltimoConteo": "2024-10-02T14:05:00Z",
      "createdAt": "2024-09-15T08:00:00Z",
      "updatedAt": "2024-10-02T14:05:00Z"
    },
    {
      "id": 2,
      "companyId": 1,
      "loteId": "L002",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G002",
      "cantidadHembras": 3900,
      "cantidadMachos": 210,
      "fechaUltimoConteo": "2024-10-02T14:05:00Z",
      "createdAt": "2024-09-10T09:00:00Z",
      "updatedAt": "2024-10-02T14:05:00Z"
    }
  ],
  "total": 15,
  "page": 1,
  "pageSize": 10
}
```

### **Crear Nuevo Inventario**
```http
POST /api/InventarioAves
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "loteId": "L003",
  "granjaId": 2,
  "nucleoId": "N002",
  "galponId": "G003",
  "cantidadHembras": 3500,
  "cantidadMachos": 300,
  "fechaUltimoConteo": "2024-10-02T16:00:00Z"
}
```

### **Ajustar Inventario**
```http
POST /api/InventarioAves/ajustar/L001
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "cantidadHembras": 4350,
  "cantidadMachos": 485,
  "tipoEvento": "Ajuste por conteo",
  "observaciones": "Conteo físico realizado - diferencias menores"
}
```

**Response:**
```json
{
  "success": true,
  "message": "Ajuste de inventario completado",
  "inventarioAnterior": {
    "cantidadHembras": 4400,
    "cantidadMachos": 490
  },
  "inventarioNuevo": {
    "cantidadHembras": 4350,
    "cantidadMachos": 485
  },
  "diferencia": {
    "hembras": -50,
    "machos": -5,
    "total": -55
  }
}
```

### **Obtener Resumen General**
```http
GET /api/InventarioAves/resumen
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:**
```json
{
  "totalLotes": 15,
  "totalHembras": 67500,
  "totalMachos": 7500,
  "totalAves": 75000,
  "resumenPorGranja": [
    {
      "granjaId": 1,
      "granjaNombre": "Granja Norte",
      "nucleoId": "N001",
      "nucleoNombre": "Núcleo Principal",
      "galponId": "G001",
      "galponNombre": "Galpón A1",
      "cantidadLotes": 3,
      "totalHembras": 13500,
      "totalMachos": 1500,
      "totalAves": 15000,
      "fechaUltimaActualizacion": "2024-10-02T14:05:00Z"
    },
    {
      "granjaId": 1,
      "granjaNombre": "Granja Norte",
      "nucleoId": "N001",
      "nucleoNombre": "Núcleo Principal",
      "galponId": "G002",
      "galponNombre": "Galpón A2",
      "cantidadLotes": 2,
      "totalHembras": 8000,
      "totalMachos": 800,
      "totalAves": 8800,
      "fechaUltimaActualizacion": "2024-10-02T14:05:00Z"
    }
  ],
  "ultimaActualizacion": "2024-10-02T16:30:00Z"
}
```

---

## 🚚 **3. Gestión de Movimientos**

### **Crear Movimiento Manual**
```http
POST /api/MovimientoAves
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "loteOrigenId": "L002",
  "loteDestinoId": "L003",
  "cantidadHembras": 200,
  "cantidadMachos": 20,
  "tipoMovimiento": "Traslado",
  "observaciones": "Redistribución por edad",
  "fechaMovimiento": "2024-10-02T15:30:00Z"
}
```

**Response 201:**
```json
{
  "id": 16,
  "companyId": 1,
  "loteOrigenId": "L002",
  "loteDestinoId": "L003",
  "cantidadHembras": 200,
  "cantidadMachos": 20,
  "tipoMovimiento": "Traslado",
  "observaciones": "Redistribución por edad",
  "fechaMovimiento": "2024-10-02T15:30:00Z",
  "createdAt": "2024-10-02T15:35:00Z",
  "updatedAt": null
}
```

### **Búsqueda de Movimientos**
```http
POST /api/MovimientoAves/search
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "tipoMovimiento": "Traslado",
  "fechaDesde": "2024-10-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "sortBy": "fecha_movimiento",
  "sortDesc": true,
  "page": 1,
  "pageSize": 20
}
```

**Response:**
```json
{
  "items": [
    {
      "id": 16,
      "companyId": 1,
      "loteOrigenId": "L002",
      "loteDestinoId": "L003",
      "cantidadHembras": 200,
      "cantidadMachos": 20,
      "tipoMovimiento": "Traslado",
      "observaciones": "Redistribución por edad",
      "fechaMovimiento": "2024-10-02T15:30:00Z",
      "createdAt": "2024-10-02T15:35:00Z",
      "updatedAt": null
    },
    {
      "id": 15,
      "companyId": 1,
      "loteOrigenId": "L001",
      "loteDestinoId": "L002",
      "cantidadHembras": 100,
      "cantidadMachos": 10,
      "tipoMovimiento": "Traslado",
      "observaciones": "Traslado por capacidad del galpón",
      "fechaMovimiento": "2024-10-02T14:00:00Z",
      "createdAt": "2024-10-02T14:05:00Z",
      "updatedAt": null
    }
  ],
  "total": 8,
  "page": 1,
  "pageSize": 20
}
```

### **Procesar Movimiento Pendiente**
```http
POST /api/MovimientoAves/16/procesar
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:**
```json
{
  "id": 16,
  "companyId": 1,
  "loteOrigenId": "L002",
  "loteDestinoId": "L003",
  "cantidadHembras": 200,
  "cantidadMachos": 20,
  "tipoMovimiento": "Traslado",
  "observaciones": "Redistribución por edad - PROCESADO",
  "fechaMovimiento": "2024-10-02T15:30:00Z",
  "createdAt": "2024-10-02T15:35:00Z",
  "updatedAt": "2024-10-02T16:00:00Z"
}
```

### **Cancelar Movimiento**
```http
POST /api/MovimientoAves/16/cancelar
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "motivoCancelacion": "Error en las cantidades registradas"
}
```

---

## 📚 **4. Historial y Trazabilidad**

### **Búsqueda en Historial**
```http
POST /api/HistorialInventario/search
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "loteId": "L001",
  "tipoCambio": "Movimiento",
  "fechaDesde": "2024-10-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "sortBy": "fecha_cambio",
  "sortDesc": true,
  "page": 1,
  "pageSize": 10
}
```

**Response:**
```json
{
  "items": [
    {
      "id": 3,
      "companyId": 1,
      "loteId": "L001",
      "cantidadHembrasAntes": 4400,
      "cantidadMachosAntes": 490,
      "cantidadHembrasDespues": 4350,
      "cantidadMachosDespues": 485,
      "tipoEvento": "Ajuste",
      "referenciaMovimientoId": null,
      "fechaRegistro": "2024-10-02T16:00:00Z",
      "createdAt": "2024-10-02T16:00:00Z",
      "updatedAt": null
    },
    {
      "id": 2,
      "companyId": 1,
      "loteId": "L001",
      "cantidadHembrasAntes": 4500,
      "cantidadMachosAntes": 500,
      "cantidadHembrasDespues": 4400,
      "cantidadMachosDespues": 490,
      "tipoEvento": "Movimiento",
      "referenciaMovimientoId": "15",
      "fechaRegistro": "2024-10-02T14:05:00Z",
      "createdAt": "2024-10-02T14:05:00Z",
      "updatedAt": null
    }
  ],
  "total": 3,
  "page": 1,
  "pageSize": 10
}
```

---

## 🎨 **5. Ejemplos de Uso en Angular**

### **Servicio Completo con Manejo de Estados:**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, BehaviorSubject } from 'rxjs';
import { catchError, tap, map } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class TrasladosAvesService {
  private inventarioUrl = 'http://localhost:5002/api/InventarioAves';
  private movimientoUrl = 'http://localhost:5002/api/MovimientoAves';
  private historialUrl = 'http://localhost:5002/api/HistorialInventario';

  // Estados reactivos
  private inventariosSubject = new BehaviorSubject<InventarioAvesDto[]>([]);
  private resumenSubject = new BehaviorSubject<ResumenGeneralInventario | null>(null);
  private loadingSubject = new BehaviorSubject<boolean>(false);

  public inventarios$ = this.inventariosSubject.asObservable();
  public resumen$ = this.resumenSubject.asObservable();
  public loading$ = this.loadingSubject.asObservable();

  constructor(private http: HttpClient) {}

  // =====================================================
  // MÉTODOS CON GESTIÓN DE ESTADO
  // =====================================================

  // Cargar inventarios con actualización de estado
  cargarInventarios(filtros?: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>> {
    this.loadingSubject.next(true);
    
    const request = filtros || {
      soloActivos: true,
      sortBy: 'lote_id',
      sortDesc: false,
      page: 1,
      pageSize: 50
    };

    return this.searchInventarios(request).pipe(
      tap(result => {
        this.inventariosSubject.next(result.items);
        this.loadingSubject.next(false);
      }),
      catchError(error => {
        this.loadingSubject.next(false);
        return this.handleError(error);
      })
    );
  }

  // Cargar resumen con actualización de estado
  cargarResumen(): Observable<ResumenGeneralInventario> {
    return this.getResumenInventario().pipe(
      tap(resumen => this.resumenSubject.next(resumen)),
      catchError(this.handleError)
    );
  }

  // Realizar traslado con actualización automática
  realizarTrasladoConActualizacion(request: TrasladoRapidoRequest): Observable<TrasladoRapidoResponse> {
    return this.trasladoRapido(request).pipe(
      tap(response => {
        if (response.success) {
          // Actualizar inventarios localmente
          this.actualizarInventariosLocales(response);
          // Recargar resumen
          this.cargarResumen().subscribe();
        }
      }),
      catchError(this.handleError)
    );
  }

  // =====================================================
  // MÉTODOS BÁSICOS DE API
  // =====================================================

  searchInventarios(request: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>> {
    return this.http.post<PagedResult<InventarioAvesDto>>(`${this.inventarioUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  getInventarioByLote(loteId: string): Observable<InventarioAvesDto> {
    return this.http.get<InventarioAvesDto>(`${this.inventarioUrl}/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  trasladoRapido(request: TrasladoRapidoRequest): Observable<TrasladoRapidoResponse> {
    return this.http.post<TrasladoRapidoResponse>(`${this.movimientoUrl}/traslado-rapido`, request)
      .pipe(catchError(this.handleError));
  }

  getResumenInventario(): Observable<ResumenGeneralInventario> {
    return this.http.get<ResumenGeneralInventario>(`${this.inventarioUrl}/resumen`)
      .pipe(catchError(this.handleError));
  }

  getTrazabilidadLote(loteId: string): Observable<TrazabilidadLoteDto> {
    return this.http.get<TrazabilidadLoteDto>(`${this.historialUrl}/trazabilidad/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MÉTODOS AUXILIARES
  // =====================================================

  // Validar traslado antes de ejecutar
  validarTrasladoCompleto(request: TrasladoRapidoRequest): Observable<ValidacionTrasladoDto> {
    return this.getInventarioByLote(request.loteOrigenId).pipe(
      map(inventarioOrigen => {
        const errores: string[] = [];
        const advertencias: string[] = [];

        // Validar disponibilidad
        if (inventarioOrigen.cantidadHembras < request.cantidadHembras) {
          errores.push(`Solo hay ${inventarioOrigen.cantidadHembras} hembras disponibles`);
        }

        if (inventarioOrigen.cantidadMachos < request.cantidadMachos) {
          errores.push(`Solo hay ${inventarioOrigen.cantidadMachos} machos disponibles`);
        }

        // Validar cantidades mínimas
        const totalTraslado = request.cantidadHembras + request.cantidadMachos;
        if (totalTraslado === 0) {
          errores.push('Debe especificar al menos una ave para trasladar');
        }

        // Advertencias
        const totalDisponible = inventarioOrigen.cantidadHembras + inventarioOrigen.cantidadMachos;
        const porcentajeTraslado = (totalTraslado / totalDisponible) * 100;
        
        if (porcentajeTraslado > 50) {
          advertencias.push(`Está trasladando el ${porcentajeTraslado.toFixed(1)}% del lote`);
        }

        return {
          esValido: errores.length === 0,
          errores,
          advertencias,
          inventarioOrigen: {
            disponibleHembras: inventarioOrigen.cantidadHembras,
            disponibleMachos: inventarioOrigen.cantidadMachos,
            totalDisponible: totalDisponible
          }
        };
      }),
      catchError(this.handleError)
    );
  }

  // Obtener estadísticas de movimientos
  getEstadisticasMovimientos(fechaDesde?: Date, fechaHasta?: Date): Observable<EstadisticasMovimientos> {
    const request: MovimientoAvesSearchRequest = {
      fechaDesde,
      fechaHasta,
      sortBy: 'fecha_movimiento',
      sortDesc: true,
      page: 1,
      pageSize: 1000
    };

    return this.searchMovimientos(request).pipe(
      map(result => this.calcularEstadisticas(result.items)),
      catchError(this.handleError)
    );
  }

  searchMovimientos(request: MovimientoAvesSearchRequest): Observable<PagedResult<MovimientoAvesDto>> {
    return this.http.post<PagedResult<MovimientoAvesDto>>(`${this.movimientoUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MÉTODOS PRIVADOS
  // =====================================================

  private actualizarInventariosLocales(response: TrasladoRapidoResponse) {
    const inventariosActuales = this.inventariosSubject.value;
    
    const inventariosActualizados = inventariosActuales.map(inv => {
      if (response.inventarioOrigenActualizado && inv.loteId === response.inventarioOrigenActualizado.loteId) {
        return {
          ...inv,
          cantidadHembras: response.inventarioOrigenActualizado.cantidadHembras,
          cantidadMachos: response.inventarioOrigenActualizado.cantidadMachos,
          fechaUltimoConteo: response.inventarioOrigenActualizado.fechaActualizacion,
          updatedAt: response.inventarioOrigenActualizado.fechaActualizacion
        };
      }
      
      if (response.inventarioDestinoActualizado && inv.loteId === response.inventarioDestinoActualizado.loteId) {
        return {
          ...inv,
          cantidadHembras: response.inventarioDestinoActualizado.cantidadHembras,
          cantidadMachos: response.inventarioDestinoActualizado.cantidadMachos,
          fechaUltimoConteo: response.inventarioDestinoActualizado.fechaActualizacion,
          updatedAt: response.inventarioDestinoActualizado.fechaActualizacion
        };
      }
      
      return inv;
    });

    this.inventariosSubject.next(inventariosActualizados);
  }

  private calcularEstadisticas(movimientos: MovimientoAvesDto[]): EstadisticasMovimientos {
    const movimientosPorTipo = movimientos.reduce((acc, mov) => {
      acc[mov.tipoMovimiento] = (acc[mov.tipoMovimiento] || 0) + 1;
      return acc;
    }, {} as { [tipo: string]: number });

    const totalAves = movimientos.reduce((acc, mov) => 
      acc + mov.cantidadHembras + mov.cantidadMachos, 0
    );

    return {
      totalMovimientos: movimientos.length,
      movimientosPorTipo,
      movimientosPorMes: this.agruparPorMes(movimientos),
      lotesConMasMovimientos: this.obtenerLotesConMasMovimientos(movimientos),
      promedioAvesPorMovimiento: movimientos.length > 0 ? totalAves / movimientos.length : 0,
      ultimoMovimiento: movimientos.length > 0 ? 
        new Date(Math.max(...movimientos.map(m => new Date(m.fechaMovimiento).getTime()))) : 
        new Date()
    };
  }

  private agruparPorMes(movimientos: MovimientoAvesDto[]): { mes: string; cantidad: number }[] {
    const grupos = movimientos.reduce((acc, mov) => {
      const fecha = new Date(mov.fechaMovimiento);
      const mes = `${fecha.getFullYear()}-${(fecha.getMonth() + 1).toString().padStart(2, '0')}`;
      acc[mes] = (acc[mes] || 0) + 1;
      return acc;
    }, {} as { [mes: string]: number });

    return Object.entries(grupos)
      .map(([mes, cantidad]) => ({ mes, cantidad }))
      .sort((a, b) => a.mes.localeCompare(b.mes));
  }

  private obtenerLotesConMasMovimientos(movimientos: MovimientoAvesDto[]): { loteId: string; cantidad: number }[] {
    const conteo = movimientos.reduce((acc, mov) => {
      acc[mov.loteOrigenId] = (acc[mov.loteOrigenId] || 0) + 1;
      acc[mov.loteDestinoId] = (acc[mov.loteDestinoId] || 0) + 1;
      return acc;
    }, {} as { [loteId: string]: number });

    return Object.entries(conteo)
      .map(([loteId, cantidad]) => ({ loteId, cantidad }))
      .sort((a, b) => b.cantidad - a.cantidad)
      .slice(0, 5);
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'Error desconocido';
    
    if (error.error instanceof ErrorEvent) {
      errorMessage = `Error: ${error.error.message}`;
    } else {
      switch (error.status) {
        case 400:
          errorMessage = 'Datos inválidos en la solicitud';
          break;
        case 401:
          errorMessage = 'No autorizado. Inicie sesión nuevamente';
          break;
        case 404:
          errorMessage = 'Recurso no encontrado';
          break;
        case 409:
          errorMessage = 'Conflicto: No hay suficientes aves para el traslado';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }
    
    console.error('Error en TrasladosAvesService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
```

### **Componente con Validaciones Avanzadas:**

```typescript
import { Component, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Subject, combineLatest } from 'rxjs';
import { takeUntil, debounceTime, distinctUntilChanged, switchMap } from 'rxjs/operators';

@Component({
  selector: 'app-traslado-avanzado',
  templateUrl: './traslado-avanzado.component.html'
})
export class TrasladoAvanzadoComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();
  
  form: FormGroup;
  inventarios: InventarioAvesDto[] = [];
  validacion: ValidacionTrasladoDto | null = null;
  loading = false;
  procesando = false;

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosAvesService
  ) {
    this.form = this.createForm();
  }

  ngOnInit() {
    this.setupFormValidation();
    this.cargarInventarios();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  createForm(): FormGroup {
    return this.fb.group({
      loteOrigenId: ['', Validators.required],
      loteDestinoId: ['', Validators.required],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      observaciones: ['']
    });
  }

  setupFormValidation() {
    // Validación en tiempo real cuando cambian los campos críticos
    combineLatest([
      this.form.get('loteOrigenId')!.valueChanges,
      this.form.get('loteDestinoId')!.valueChanges,
      this.form.get('cantidadHembras')!.valueChanges,
      this.form.get('cantidadMachos')!.valueChanges
    ]).pipe(
      takeUntil(this.destroy$),
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(([loteOrigen, loteDestino, hembras, machos]) => {
        if (loteOrigen && loteDestino && (hembras > 0 || machos > 0)) {
          return this.trasladosService.validarTrasladoCompleto({
            loteOrigenId: loteOrigen,
            loteDestinoId: loteDestino,
            cantidadHembras: hembras || 0,
            cantidadMachos: machos || 0
          });
        }
        return [];
      })
    ).subscribe(validacion => {
      this.validacion = validacion as ValidacionTrasladoDto;
    });
  }

  cargarInventarios() {
    this.loading = true;
    this.trasladosService.cargarInventarios({
      soloActivos: true,
      sortBy: 'lote_id',
      sortDesc: false,
      page: 1,
      pageSize: 100
    }).pipe(
      takeUntil(this.destroy$)
    ).subscribe({
      next: (result) => {
        this.inventarios = result.items;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error al cargar inventarios:', error);
        this.loading = false;
      }
    });
  }

  async onSubmit() {
    if (this.form.invalid || !this.validacion?.esValido) {
      this.form.markAllAsTouched();
      return;
    }

    this.procesando = true;

    try {
      const result = await this.trasladosService.realizarTrasladoConActualizacion(
        this.form.value
      ).toPromise();

      if (result.success) {
        // Mostrar mensaje de éxito
        alert(`Traslado realizado exitosamente. Movimiento ID: ${result.movimientoId}`);
        this.form.reset();
        this.validacion = null;
      } else {
        alert(`Error en el traslado: ${result.message}`);
      }
    } catch (error: any) {
      alert(`Error: ${error.message}`);
    } finally {
      this.procesando = false;
    }
  }

  // Getters para el template
  get lotesDisponibles(): InventarioAvesDto[] {
    return this.inventarios.filter(inv => 
      (inv.cantidadHembras + inv.cantidadMachos) > 0
    );
  }

  get totalATrasldar(): number {
    const hembras = this.form.get('cantidadHembras')?.value || 0;
    const machos = this.form.get('cantidadMachos')?.value || 0;
    return hembras + machos;
  }

  get puedeRealizarTraslado(): boolean {
    return this.form.valid && 
           this.validacion?.esValido === true && 
           this.totalATrasldar > 0;
  }

  // Métodos auxiliares
  getInventarioPorLote(loteId: string): InventarioAvesDto | undefined {
    return this.inventarios.find(inv => inv.loteId === loteId);
  }

  formatearCantidad(cantidad: number): string {
    return cantidad.toLocaleString('es-ES');
  }
}
```

---

## 📊 **6. Datos de Prueba Completos**

### **Inventarios de Ejemplo:**
```json
{
  "inventarios": [
    {
      "loteId": "L001",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G001",
      "cantidadHembras": 4350,
      "cantidadMachos": 485,
      "fechaUltimoConteo": "2024-10-02T16:00:00Z"
    },
    {
      "loteId": "L002",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G002",
      "cantidadHembras": 3700,
      "cantidadMachos": 190,
      "fechaUltimoConteo": "2024-10-02T15:30:00Z"
    },
    {
      "loteId": "L003",
      "granjaId": 2,
      "nucleoId": "N002",
      "galponId": "G003",
      "cantidadHembras": 3700,
      "cantidadMachos": 320,
      "fechaUltimoConteo": "2024-10-02T16:00:00Z"
    }
  ]
}
```

### **Tipos de Movimiento Comunes:**
- **Traslado**: Entre lotes de la misma empresa
- **Venta**: Salida por comercialización
- **Mortalidad**: Reducción por muerte natural
- **Selección**: Retiro por criterios genéticos
- **Ajuste**: Corrección por conteo físico

### **Estados de Movimiento:**
- **Pendiente**: Registrado pero no procesado
- **Procesado**: En proceso de ejecución
- **Completado**: Finalizado exitosamente
- **Cancelado**: Anulado por el usuario
- **Error**: Falló durante el procesamiento

---

## ✅ **Checklist de Implementación Frontend**

### **Funcionalidades Básicas (Prioridad Alta):**
- [ ] Dashboard con resumen de inventarios
- [ ] Lista de inventarios con filtros
- [ ] Formulario de traslado rápido
- [ ] Validación en tiempo real
- [ ] Lista de movimientos con estados

### **Funcionalidades Avanzadas (Prioridad Media):**
- [ ] Trazabilidad completa de lotes
- [ ] Ajuste de inventarios
- [ ] Estadísticas de movimientos
- [ ] Alertas por inventario bajo
- [ ] Exportación de reportes

### **Funcionalidades Premium (Prioridad Baja):**
- [ ] Gráficos de tendencias
- [ ] Predicción de inventarios
- [ ] Notificaciones automáticas
- [ ] Integración móvil
- [ ] Reportes automáticos

---

## 🎉 **¡Todo Listo para Implementar!**

Con estos ejemplos y documentación, el equipo de frontend tiene **todo lo necesario** para implementar un sistema completo y robusto de traslados de aves.

**El backend está 100% operativo y esperando las peticiones!** 🚀

**APIs funcionando en:** `http://localhost:5002/swagger`

**Documentación ubicada en:** `documentacion/ejemplos-api-traslados.md`
