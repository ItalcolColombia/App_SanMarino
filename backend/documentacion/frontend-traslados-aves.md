# 🐔 Requisitos Frontend - Sistema de Traslados de Aves

## 📋 **Resumen del Sistema**

El sistema de **Traslados de Aves** permite gestionar el inventario de aves y realizar movimientos entre diferentes lotes, granjas, núcleos y galpones. Incluye funcionalidades para registrar traslados, actualizar inventarios automáticamente y mantener un historial completo de todos los movimientos.

## 🔌 **Endpoints Disponibles**

### **Base URL:** `http://localhost:5002/api`

---

## 📊 **1. Gestión de Inventario de Aves**

### **GET** `/api/InventarioAves/{id}`

**Descripción:** Obtiene un inventario específico por ID.

**Ejemplo de Request:**
```http
GET /api/InventarioAves/1
Authorization: Bearer {token}
```

**Response 200:**
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

### **GET** `/api/InventarioAves/lote/{loteId}`

**Descripción:** Obtiene el inventario actual de un lote específico.

**Ejemplo de Request:**
```http
GET /api/InventarioAves/lote/L001
Authorization: Bearer {token}
```

### **POST** `/api/InventarioAves/search`

**Descripción:** Búsqueda avanzada de inventarios con filtros y paginación.

**Request Body:**
```json
{
  "loteId": "L001",
  "granjaId": 1,
  "nucleoId": "N001",
  "galponId": "G001",
  "estado": "activo",
  "fechaDesde": "2024-09-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "soloActivos": true,
  "sortBy": "lote_id",
  "sortDesc": false,
  "page": 1,
  "pageSize": 20
}
```

**Response 200:**
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
      "cantidadHembras": 4500,
      "cantidadMachos": 500,
      "fechaUltimoConteo": "2024-10-01T10:00:00Z",
      "createdAt": "2024-09-15T08:00:00Z",
      "updatedAt": "2024-10-01T10:00:00Z"
    }
  ],
  "total": 25,
  "page": 1,
  "pageSize": 20
}
```

### **POST** `/api/InventarioAves`

**Descripción:** Crea un nuevo registro de inventario.

**Request Body:**
```json
{
  "loteId": "L002",
  "granjaId": 2,
  "nucleoId": "N002",
  "galponId": "G002",
  "cantidadHembras": 3800,
  "cantidadMachos": 200,
  "fechaUltimoConteo": "2024-10-02T09:00:00Z"
}
```

### **PUT** `/api/InventarioAves/{id}`

**Descripción:** Actualiza un inventario existente.

### **DELETE** `/api/InventarioAves/{id}`

**Descripción:** Elimina un inventario (soft delete).

### **POST** `/api/InventarioAves/ajustar/{loteId}`

**Descripción:** Ajusta las cantidades de aves en un lote específico.

**Request Body:**
```json
{
  "cantidadHembras": 4400,
  "cantidadMachos": 480,
  "tipoEvento": "Ajuste por conteo",
  "observaciones": "Conteo físico realizado"
}
```

### **GET** `/api/InventarioAves/resumen`

**Descripción:** Obtiene un resumen del inventario por granja, núcleo y galpón.

**Response 200:**
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
      "fechaUltimaActualizacion": "2024-10-02T10:00:00Z"
    }
  ]
}
```

---

## 🚚 **2. Gestión de Movimientos de Aves**

### **POST** `/api/MovimientoAves`

**Descripción:** Registra un nuevo movimiento de aves.

**Request Body:**
```json
{
  "loteOrigenId": "L001",
  "loteDestinoId": "L002",
  "cantidadHembras": 100,
  "cantidadMachos": 10,
  "tipoMovimiento": "Traslado",
  "observaciones": "Traslado por capacidad del galpón",
  "fechaMovimiento": "2024-10-02T14:00:00Z"
}
```

**Response 201:**
```json
{
  "id": 1,
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
```

### **GET** `/api/MovimientoAves/{id}`

**Descripción:** Obtiene un movimiento específico por ID.

### **POST** `/api/MovimientoAves/search`

**Descripción:** Búsqueda avanzada de movimientos.

**Request Body:**
```json
{
  "numeroMovimiento": "MOV-001",
  "tipoMovimiento": "Traslado",
  "estado": "Completado",
  "loteOrigenId": "L001",
  "loteDestinoId": "L002",
  "granjaOrigenId": 1,
  "granjaDestinoId": 2,
  "fechaDesde": "2024-10-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "usuarioMovimientoId": 1,
  "sortBy": "fecha_movimiento",
  "sortDesc": true,
  "page": 1,
  "pageSize": 20
}
```

### **POST** `/api/MovimientoAves/traslado-rapido`

**Descripción:** Realiza un traslado rápido entre lotes con validaciones automáticas.

**Request Body:**
```json
{
  "loteOrigenId": "L001",
  "loteDestinoId": "L002",
  "cantidadHembras": 50,
  "cantidadMachos": 5,
  "observaciones": "Traslado de emergencia"
}
```

**Response 200:**
```json
{
  "success": true,
  "message": "Traslado realizado exitosamente",
  "movimientoId": 15,
  "inventarioOrigenActualizado": {
    "loteId": "L001",
    "cantidadHembras": 4450,
    "cantidadMachos": 495
  },
  "inventarioDestinoActualizado": {
    "loteId": "L002",
    "cantidadHembras": 3850,
    "cantidadMachos": 205
  }
}
```

### **POST** `/api/MovimientoAves/{id}/procesar`

**Descripción:** Procesa un movimiento pendiente.

### **POST** `/api/MovimientoAves/{id}/cancelar`

**Descripción:** Cancela un movimiento.

**Request Body:**
```json
{
  "motivoCancelacion": "Error en las cantidades registradas"
}
```

---

## 📚 **3. Historial de Inventario**

### **GET** `/api/HistorialInventario/{id}`

**Descripción:** Obtiene un registro específico del historial.

### **POST** `/api/HistorialInventario/search`

**Descripción:** Búsqueda en el historial de cambios.

**Request Body:**
```json
{
  "inventarioId": 1,
  "loteId": "L001",
  "tipoCambio": "Movimiento",
  "movimientoId": 15,
  "granjaId": 1,
  "nucleoId": "N001",
  "galponId": "G001",
  "fechaDesde": "2024-10-01T00:00:00Z",
  "fechaHasta": "2024-10-02T23:59:59Z",
  "usuarioCambioId": 1,
  "sortBy": "fecha_cambio",
  "sortDesc": true,
  "page": 1,
  "pageSize": 20
}
```

**Response 200:**
```json
{
  "items": [
    {
      "id": 1,
      "companyId": 1,
      "loteId": "L001",
      "cantidadHembrasAntes": 4500,
      "cantidadMachosAntes": 500,
      "cantidadHembrasDespues": 4450,
      "cantidadMachosDespues": 495,
      "tipoEvento": "Movimiento",
      "referenciaMovimientoId": "15",
      "fechaRegistro": "2024-10-02T14:05:00Z",
      "createdAt": "2024-10-02T14:05:00Z",
      "updatedAt": null
    }
  ],
  "total": 50,
  "page": 1,
  "pageSize": 20
}
```

### **GET** `/api/HistorialInventario/trazabilidad/{loteId}`

**Descripción:** Obtiene la trazabilidad completa de un lote.

**Response 200:**
```json
{
  "loteId": "L001",
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
      "cantidadHembrasDespues": 4450,
      "cantidadMachosDespues": 495,
      "usuario": "operador@zoosanmarino.com",
      "referenciaMovimiento": "15"
    }
  ]
}
```

---

## 🎨 **Interfaces TypeScript**

### **Interfaces Principales:**

```typescript
// Inventario de Aves
export interface InventarioAvesDto {
  id: number;
  companyId: number;
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateInventarioAvesDto {
  loteId: string;
  granjaId: number;
  nucleoId: string;
  galponId?: string;
  cantidadHembras: number;
  cantidadMachos: number;
  fechaUltimoConteo: Date;
}

export interface UpdateInventarioAvesDto extends CreateInventarioAvesDto {
  id: number;
}

export interface InventarioAvesSearchRequest {
  loteId?: string;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  estado?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  soloActivos?: boolean;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Movimiento de Aves
export interface MovimientoAvesDto {
  id: number;
  companyId: number;
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface CreateMovimientoAvesDto {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  tipoMovimiento: string;
  observaciones?: string;
  fechaMovimiento: Date;
}

export interface MovimientoAvesSearchRequest {
  numeroMovimiento?: string;
  tipoMovimiento?: string;
  estado?: string;
  loteOrigenId?: string;
  loteDestinoId?: string;
  granjaOrigenId?: number;
  granjaDestinoId?: number;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioMovimientoId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Historial de Inventario
export interface HistorialInventarioDto {
  id: number;
  companyId: number;
  loteId: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  tipoEvento: string;
  referenciaMovimientoId?: string;
  fechaRegistro: Date;
  createdAt: Date;
  updatedAt?: Date;
}

export interface HistorialInventarioSearchRequest {
  inventarioId?: number;
  loteId?: string;
  tipoCambio?: string;
  movimientoId?: number;
  granjaId?: number;
  nucleoId?: string;
  galponId?: string;
  fechaDesde?: Date;
  fechaHasta?: Date;
  usuarioCambioId?: number;
  sortBy?: string;
  sortDesc?: boolean;
  page: number;
  pageSize: number;
}

// Interfaces Auxiliares
export interface ResumenInventarioDto {
  granjaId: number;
  granjaNombre: string;
  nucleoId: string;
  nucleoNombre?: string;
  galponId?: string;
  galponNombre?: string;
  cantidadLotes: number;
  totalHembras: number;
  totalMachos: number;
  totalAves: number;
  fechaUltimaActualizacion: Date;
}

export interface TrasladoRapidoRequest {
  loteOrigenId: string;
  loteDestinoId: string;
  cantidadHembras: number;
  cantidadMachos: number;
  observaciones?: string;
}

export interface TrasladoRapidoResponse {
  success: boolean;
  message: string;
  movimientoId?: number;
  inventarioOrigenActualizado?: {
    loteId: string;
    cantidadHembras: number;
    cantidadMachos: number;
  };
  inventarioDestinoActualizado?: {
    loteId: string;
    cantidadHembras: number;
    cantidadMachos: number;
  };
}

export interface EventoTrazabilidadDto {
  fecha: Date;
  tipoEvento: string;
  descripcion: string;
  cantidadHembrasAntes: number;
  cantidadMachosAntes: number;
  cantidadHembrasDespues: number;
  cantidadMachosDespues: number;
  usuario?: string;
  referenciaMovimiento?: string;
}

export interface TrazabilidadLoteDto {
  loteId: string;
  eventos: EventoTrazabilidadDto[];
}

// Resultado paginado
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
```

---

## 🛠️ **Servicio Angular**

```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class TrasladosAvesService {
  private inventarioUrl = 'http://localhost:5002/api/InventarioAves';
  private movimientoUrl = 'http://localhost:5002/api/MovimientoAves';
  private historialUrl = 'http://localhost:5002/api/HistorialInventario';

  constructor(private http: HttpClient) {}

  // =====================================================
  // INVENTARIO DE AVES
  // =====================================================

  // Obtener inventario por ID
  getInventarioById(id: number): Observable<InventarioAvesDto> {
    return this.http.get<InventarioAvesDto>(`${this.inventarioUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Obtener inventario por lote
  getInventarioByLote(loteId: string): Observable<InventarioAvesDto> {
    return this.http.get<InventarioAvesDto>(`${this.inventarioUrl}/lote/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda de inventarios
  searchInventarios(request: InventarioAvesSearchRequest): Observable<PagedResult<InventarioAvesDto>> {
    return this.http.post<PagedResult<InventarioAvesDto>>(`${this.inventarioUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Crear inventario
  createInventario(dto: CreateInventarioAvesDto): Observable<InventarioAvesDto> {
    return this.http.post<InventarioAvesDto>(this.inventarioUrl, dto)
      .pipe(catchError(this.handleError));
  }

  // Actualizar inventario
  updateInventario(id: number, dto: UpdateInventarioAvesDto): Observable<InventarioAvesDto> {
    return this.http.put<InventarioAvesDto>(`${this.inventarioUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Eliminar inventario
  deleteInventario(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.inventarioUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Ajustar cantidades
  ajustarInventario(loteId: string, ajuste: any): Observable<InventarioAvesDto> {
    return this.http.post<InventarioAvesDto>(`${this.inventarioUrl}/ajustar/${loteId}`, ajuste)
      .pipe(catchError(this.handleError));
  }

  // Obtener resumen
  getResumenInventario(): Observable<any> {
    return this.http.get<any>(`${this.inventarioUrl}/resumen`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MOVIMIENTOS DE AVES
  // =====================================================

  // Crear movimiento
  createMovimiento(dto: CreateMovimientoAvesDto): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(this.movimientoUrl, dto)
      .pipe(catchError(this.handleError));
  }

  // Obtener movimiento por ID
  getMovimientoById(id: number): Observable<MovimientoAvesDto> {
    return this.http.get<MovimientoAvesDto>(`${this.movimientoUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda de movimientos
  searchMovimientos(request: MovimientoAvesSearchRequest): Observable<PagedResult<MovimientoAvesDto>> {
    return this.http.post<PagedResult<MovimientoAvesDto>>(`${this.movimientoUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Traslado rápido
  trasladoRapido(request: TrasladoRapidoRequest): Observable<TrasladoRapidoResponse> {
    return this.http.post<TrasladoRapidoResponse>(`${this.movimientoUrl}/traslado-rapido`, request)
      .pipe(catchError(this.handleError));
  }

  // Procesar movimiento
  procesarMovimiento(id: number): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.movimientoUrl}/${id}/procesar`, {})
      .pipe(catchError(this.handleError));
  }

  // Cancelar movimiento
  cancelarMovimiento(id: number, motivo: string): Observable<MovimientoAvesDto> {
    return this.http.post<MovimientoAvesDto>(`${this.movimientoUrl}/${id}/cancelar`, { motivoCancelacion: motivo })
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // HISTORIAL DE INVENTARIO
  // =====================================================

  // Obtener historial por ID
  getHistorialById(id: number): Observable<HistorialInventarioDto> {
    return this.http.get<HistorialInventarioDto>(`${this.historialUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda en historial
  searchHistorial(request: HistorialInventarioSearchRequest): Observable<PagedResult<HistorialInventarioDto>> {
    return this.http.post<PagedResult<HistorialInventarioDto>>(`${this.historialUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Obtener trazabilidad de lote
  getTrazabilidadLote(loteId: string): Observable<TrazabilidadLoteDto> {
    return this.http.get<TrazabilidadLoteDto>(`${this.historialUrl}/trazabilidad/${loteId}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // MÉTODOS AUXILIARES
  // =====================================================

  // Validar traslado
  validarTraslado(loteOrigenId: string, loteDestinoId: string, cantidadHembras: number, cantidadMachos: number): Observable<boolean> {
    const request = {
      loteOrigenId,
      loteDestinoId,
      cantidadHembras,
      cantidadMachos
    };
    return this.http.post<boolean>(`${this.movimientoUrl}/validar`, request)
      .pipe(catchError(this.handleError));
  }

  // Obtener lotes disponibles
  getLotesDisponibles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.inventarioUrl}/lotes-disponibles`)
      .pipe(catchError(this.handleError));
  }

  // Manejo de errores
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

---

## 🎨 **Componentes Sugeridos**

### **1. Dashboard de Inventario**

```typescript
// inventario-dashboard.component.ts
export class InventarioDashboardComponent implements OnInit {
  resumen: any = null;
  inventarios: InventarioAvesDto[] = [];
  loading = false;
  error: string | null = null;

  // Filtros
  filtros: InventarioAvesSearchRequest = {
    soloActivos: true,
    sortBy: 'lote_id',
    sortDesc: false,
    page: 1,
    pageSize: 20
  };

  constructor(private trasladosService: TrasladosAvesService) {}

  ngOnInit() {
    this.cargarResumen();
    this.cargarInventarios();
  }

  async cargarResumen() {
    try {
      this.resumen = await this.trasladosService.getResumenInventario().toPromise();
    } catch (error: any) {
      this.error = error.message;
    }
  }

  async cargarInventarios() {
    this.loading = true;
    try {
      const result = await this.trasladosService.searchInventarios(this.filtros).toPromise();
      this.inventarios = result.items;
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  onFiltroChange() {
    this.filtros.page = 1;
    this.cargarInventarios();
  }
}
```

### **2. Formulario de Traslado**

```typescript
// traslado-form.component.ts
export class TrasladoFormComponent implements OnInit {
  @Output() trasladoRealizado = new EventEmitter<TrasladoRapidoResponse>();

  form: FormGroup;
  lotesDisponibles: string[] = [];
  loading = false;
  error: string | null = null;

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosAvesService
  ) {
    this.form = this.createForm();
  }

  ngOnInit() {
    this.cargarLotesDisponibles();
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

  async cargarLotesDisponibles() {
    try {
      this.lotesDisponibles = await this.trasladosService.getLotesDisponibles().toPromise();
    } catch (error: any) {
      this.error = error.message;
    }
  }

  async onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.value;
    
    // Validar que no sea el mismo lote
    if (formValue.loteOrigenId === formValue.loteDestinoId) {
      this.error = 'El lote origen y destino no pueden ser el mismo';
      return;
    }

    // Validar que haya al menos una ave
    if (formValue.cantidadHembras + formValue.cantidadMachos === 0) {
      this.error = 'Debe trasladar al menos una ave';
      return;
    }

    this.loading = true;
    this.error = null;

    try {
      const result = await this.trasladosService.trasladoRapido(formValue).toPromise();
      
      if (result.success) {
        this.trasladoRealizado.emit(result);
        this.form.reset();
      } else {
        this.error = result.message;
      }
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  // Validadores personalizados
  isFieldInvalid(fieldName: string): boolean {
    const field = this.form.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  getFieldError(fieldName: string): string {
    const field = this.form.get(fieldName);
    if (field?.errors) {
      if (field.errors['required']) return `${fieldName} es requerido`;
      if (field.errors['min']) return `Valor mínimo: ${field.errors['min'].min}`;
    }
    return '';
  }
}
```

### **3. Historial de Movimientos**

```typescript
// movimientos-list.component.ts
export class MovimientosListComponent implements OnInit {
  movimientos: MovimientoAvesDto[] = [];
  totalRecords = 0;
  loading = false;
  error: string | null = null;

  // Filtros
  filtros: MovimientoAvesSearchRequest = {
    sortBy: 'fecha_movimiento',
    sortDesc: true,
    page: 1,
    pageSize: 20
  };

  constructor(private trasladosService: TrasladosAvesService) {}

  ngOnInit() {
    this.cargarMovimientos();
  }

  async cargarMovimientos() {
    this.loading = true;
    this.error = null;

    try {
      const result = await this.trasladosService.searchMovimientos(this.filtros).toPromise();
      this.movimientos = result.items;
      this.totalRecords = result.total;
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  onFiltroChange() {
    this.filtros.page = 1;
    this.cargarMovimientos();
  }

  onPageChange(page: number) {
    this.filtros.page = page;
    this.cargarMovimientos();
  }

  async cancelarMovimiento(id: number) {
    const motivo = prompt('Ingrese el motivo de cancelación:');
    if (!motivo) return;

    try {
      await this.trasladosService.cancelarMovimiento(id, motivo).toPromise();
      this.cargarMovimientos(); // Recargar lista
    } catch (error: any) {
      this.error = error.message;
    }
  }

  async procesarMovimiento(id: number) {
    try {
      await this.trasladosService.procesarMovimiento(id).toPromise();
      this.cargarMovimientos(); // Recargar lista
    } catch (error: any) {
      this.error = error.message;
    }
  }
}
```

---

## 📱 **Templates HTML**

### **Dashboard de Inventario:**

```html
<!-- inventario-dashboard.component.html -->
<div class="container-fluid">
  <!-- Resumen General -->
  <div class="row mb-4" *ngIf="resumen">
    <div class="col-md-3">
      <div class="card bg-primary text-white">
        <div class="card-body">
          <h5 class="card-title">Total Lotes</h5>
          <h2>{{ resumen.totalLotes | number }}</h2>
        </div>
      </div>
    </div>
    <div class="col-md-3">
      <div class="card bg-success text-white">
        <div class="card-body">
          <h5 class="card-title">Total Hembras</h5>
          <h2>{{ resumen.totalHembras | number }}</h2>
        </div>
      </div>
    </div>
    <div class="col-md-3">
      <div class="card bg-info text-white">
        <div class="card-body">
          <h5 class="card-title">Total Machos</h5>
          <h2>{{ resumen.totalMachos | number }}</h2>
        </div>
      </div>
    </div>
    <div class="col-md-3">
      <div class="card bg-warning text-white">
        <div class="card-body">
          <h5 class="card-title">Total Aves</h5>
          <h2>{{ resumen.totalAves | number }}</h2>
        </div>
      </div>
    </div>
  </div>

  <!-- Filtros -->
  <div class="card mb-4">
    <div class="card-header">
      <h5>Filtros de Búsqueda</h5>
    </div>
    <div class="card-body">
      <div class="row">
        <div class="col-md-3">
          <label class="form-label">Lote ID</label>
          <input type="text" class="form-control" [(ngModel)]="filtros.loteId" 
                 (change)="onFiltroChange()" placeholder="Ej: L001">
        </div>
        <div class="col-md-3">
          <label class="form-label">Granja ID</label>
          <input type="number" class="form-control" [(ngModel)]="filtros.granjaId" 
                 (change)="onFiltroChange()" placeholder="ID de granja">
        </div>
        <div class="col-md-3">
          <label class="form-label">Núcleo ID</label>
          <input type="text" class="form-control" [(ngModel)]="filtros.nucleoId" 
                 (change)="onFiltroChange()" placeholder="Ej: N001">
        </div>
        <div class="col-md-3">
          <label class="form-label">Solo Activos</label>
          <div class="form-check">
            <input class="form-check-input" type="checkbox" [(ngModel)]="filtros.soloActivos" 
                   (change)="onFiltroChange()" id="soloActivos">
            <label class="form-check-label" for="soloActivos">
              Mostrar solo activos
            </label>
          </div>
        </div>
      </div>
    </div>
  </div>

  <!-- Tabla de Inventarios -->
  <div class="card">
    <div class="card-header d-flex justify-content-between align-items-center">
      <h5>Inventario de Aves</h5>
      <button class="btn btn-primary" (click)="openCreateModal()">
        <i class="fas fa-plus"></i> Nuevo Inventario
      </button>
    </div>
    <div class="card-body">
      <div class="table-responsive">
        <table class="table table-striped table-hover">
          <thead class="table-dark">
            <tr>
              <th>Lote ID</th>
              <th>Granja</th>
              <th>Núcleo</th>
              <th>Galpón</th>
              <th>Hembras</th>
              <th>Machos</th>
              <th>Total</th>
              <th>Último Conteo</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let inventario of inventarios">
              <td><strong>{{ inventario.loteId }}</strong></td>
              <td>{{ inventario.granjaId }}</td>
              <td>{{ inventario.nucleoId }}</td>
              <td>{{ inventario.galponId || '-' }}</td>
              <td class="text-success">{{ inventario.cantidadHembras | number }}</td>
              <td class="text-info">{{ inventario.cantidadMachos | number }}</td>
              <td class="text-primary">
                <strong>{{ (inventario.cantidadHembras + inventario.cantidadMachos) | number }}</strong>
              </td>
              <td>{{ inventario.fechaUltimoConteo | date:'short' }}</td>
              <td>
                <div class="btn-group btn-group-sm">
                  <button class="btn btn-outline-primary" (click)="editInventario(inventario.id)">
                    <i class="fas fa-edit"></i>
                  </button>
                  <button class="btn btn-outline-success" (click)="ajustarInventario(inventario.loteId)">
                    <i class="fas fa-balance-scale"></i>
                  </button>
                  <button class="btn btn-outline-info" (click)="verTrazabilidad(inventario.loteId)">
                    <i class="fas fa-history"></i>
                  </button>
                  <button class="btn btn-outline-danger" (click)="deleteInventario(inventario.id)">
                    <i class="fas fa-trash"></i>
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Loading -->
      <div class="text-center" *ngIf="loading">
        <div class="spinner-border" role="status">
          <span class="visually-hidden">Cargando...</span>
        </div>
      </div>

      <!-- Error -->
      <div class="alert alert-danger" *ngIf="error">
        <i class="fas fa-exclamation-triangle"></i> {{ error }}
      </div>
    </div>
  </div>
</div>
```

### **Formulario de Traslado:**

```html
<!-- traslado-form.component.html -->
<div class="card">
  <div class="card-header">
    <h5><i class="fas fa-exchange-alt"></i> Traslado Rápido de Aves</h5>
  </div>
  <div class="card-body">
    <form [formGroup]="form" (ngSubmit)="onSubmit()">
      <div class="row">
        <div class="col-md-6">
          <label for="loteOrigenId" class="form-label">Lote Origen *</label>
          <select class="form-select" id="loteOrigenId" formControlName="loteOrigenId"
                  [class.is-invalid]="isFieldInvalid('loteOrigenId')">
            <option value="">Seleccione lote origen</option>
            <option *ngFor="let lote of lotesDisponibles" [value]="lote">{{ lote }}</option>
          </select>
          <div class="invalid-feedback" *ngIf="isFieldInvalid('loteOrigenId')">
            {{ getFieldError('loteOrigenId') }}
          </div>
        </div>
        
        <div class="col-md-6">
          <label for="loteDestinoId" class="form-label">Lote Destino *</label>
          <select class="form-select" id="loteDestinoId" formControlName="loteDestinoId"
                  [class.is-invalid]="isFieldInvalid('loteDestinoId')">
            <option value="">Seleccione lote destino</option>
            <option *ngFor="let lote of lotesDisponibles" [value]="lote">{{ lote }}</option>
          </select>
          <div class="invalid-feedback" *ngIf="isFieldInvalid('loteDestinoId')">
            {{ getFieldError('loteDestinoId') }}
          </div>
        </div>
      </div>

      <div class="row mt-3">
        <div class="col-md-6">
          <label for="cantidadHembras" class="form-label">Cantidad Hembras *</label>
          <input type="number" class="form-control" id="cantidadHembras" 
                 formControlName="cantidadHembras" min="0"
                 [class.is-invalid]="isFieldInvalid('cantidadHembras')">
          <div class="invalid-feedback" *ngIf="isFieldInvalid('cantidadHembras')">
            {{ getFieldError('cantidadHembras') }}
          </div>
        </div>
        
        <div class="col-md-6">
          <label for="cantidadMachos" class="form-label">Cantidad Machos *</label>
          <input type="number" class="form-control" id="cantidadMachos" 
                 formControlName="cantidadMachos" min="0"
                 [class.is-invalid]="isFieldInvalid('cantidadMachos')">
          <div class="invalid-feedback" *ngIf="isFieldInvalid('cantidadMachos')">
            {{ getFieldError('cantidadMachos') }}
          </div>
        </div>
      </div>

      <div class="row mt-3">
        <div class="col-12">
          <label for="observaciones" class="form-label">Observaciones</label>
          <textarea class="form-control" id="observaciones" formControlName="observaciones" 
                    rows="3" placeholder="Motivo del traslado, observaciones adicionales..."></textarea>
        </div>
      </div>

      <!-- Resumen del Traslado -->
      <div class="alert alert-info mt-3" *ngIf="form.get('cantidadHembras')?.value > 0 || form.get('cantidadMachos')?.value > 0">
        <h6><i class="fas fa-info-circle"></i> Resumen del Traslado</h6>
        <p class="mb-1">
          <strong>Total a trasladar:</strong> 
          {{ (form.get('cantidadHembras')?.value || 0) + (form.get('cantidadMachos')?.value || 0) }} aves
        </p>
        <p class="mb-0">
          <strong>Detalle:</strong> 
          {{ form.get('cantidadHembras')?.value || 0 }} hembras + 
          {{ form.get('cantidadMachos')?.value || 0 }} machos
        </p>
      </div>

      <!-- Error -->
      <div class="alert alert-danger mt-3" *ngIf="error">
        <i class="fas fa-exclamation-triangle"></i> {{ error }}
      </div>

      <!-- Botones -->
      <div class="d-flex justify-content-end mt-4">
        <button type="button" class="btn btn-secondary me-2" (click)="form.reset()">
          <i class="fas fa-undo"></i> Limpiar
        </button>
        <button type="submit" class="btn btn-success" [disabled]="form.invalid || loading">
          <span class="spinner-border spinner-border-sm me-1" *ngIf="loading"></span>
          <i class="fas fa-exchange-alt" *ngIf="!loading"></i>
          {{ loading ? 'Procesando...' : 'Realizar Traslado' }}
        </button>
      </div>
    </form>
  </div>
</div>
```

---

## 🔧 **Configuración de Rutas**

```typescript
// app-routing.module.ts
const routes: Routes = [
  {
    path: 'traslados-aves',
    children: [
      { path: '', component: InventarioDashboardComponent },
      { path: 'inventario', component: InventarioListComponent },
      { path: 'traslados', component: TrasladoFormComponent },
      { path: 'movimientos', component: MovimientosListComponent },
      { path: 'historial', component: HistorialInventarioComponent },
      { path: 'trazabilidad/:loteId', component: TrazabilidadLoteComponent }
    ]
  }
];
```

---

## 🎯 **Funcionalidades Implementadas**

### **✅ Gestión de Inventario:**
- [x] Crear, leer, actualizar, eliminar inventarios
- [x] Búsqueda avanzada con filtros múltiples
- [x] Ajuste de cantidades con registro de eventos
- [x] Resumen por granja, núcleo y galpón
- [x] Validaciones de negocio

### **✅ Sistema de Traslados:**
- [x] Traslado rápido entre lotes
- [x] Validación de cantidades disponibles
- [x] Actualización automática de inventarios
- [x] Registro completo de movimientos
- [x] Estados de movimiento (pendiente, procesado, cancelado)

### **✅ Historial y Trazabilidad:**
- [x] Registro automático de todos los cambios
- [x] Trazabilidad completa por lote
- [x] Búsqueda en historial con filtros
- [x] Información de usuario y fecha
- [x] Referencias a movimientos relacionados

### **✅ Funcionalidades Avanzadas:**
- [x] Dashboard con métricas en tiempo real
- [x] Validaciones de negocio complejas
- [x] Manejo de errores detallado
- [x] Paginación y ordenamiento
- [x] Filtros dinámicos

---

## 📊 **Datos de Prueba**

### **Inventarios de Ejemplo:**
```json
{
  "inventarios": [
    {
      "loteId": "L001",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G001",
      "cantidadHembras": 4500,
      "cantidadMachos": 500
    },
    {
      "loteId": "L002",
      "granjaId": 1,
      "nucleoId": "N001",
      "galponId": "G002",
      "cantidadHembras": 3800,
      "cantidadMachos": 200
    }
  ]
}
```

### **Tipos de Movimiento:**
- **Traslado**: Movimiento entre lotes de la misma empresa
- **Venta**: Salida de aves por venta
- **Mortalidad**: Reducción por mortalidad natural
- **Selección**: Retiro por selección genética
- **Ajuste**: Corrección por conteo físico

---

## ✅ **Checklist de Implementación Frontend**

### **Funcionalidades Básicas (Prioridad Alta):**
- [ ] Dashboard de inventario con resumen
- [ ] Lista de inventarios con filtros
- [ ] Formulario de traslado rápido
- [ ] Lista de movimientos con estados
- [ ] Historial de cambios por lote

### **Funcionalidades Avanzadas (Prioridad Media):**
- [ ] Trazabilidad completa de lotes
- [ ] Validaciones en tiempo real
- [ ] Reportes de movimientos
- [ ] Alertas por inventario bajo
- [ ] Exportación de datos

### **Funcionalidades Premium (Prioridad Baja):**
- [ ] Gráficos de tendencias
- [ ] Predicción de inventarios
- [ ] Integración con otros módulos
- [ ] Notificaciones automáticas
- [ ] API para dispositivos móviles

---

## 🎉 **¡Todo Listo para Implementar!**

Con esta documentación el frontend tiene **todo lo necesario** para implementar un sistema completo y robusto de traslados de aves.

**El backend está 100% operativo y esperando las peticiones!** 🚀

**APIs funcionando en:** `http://localhost:5002/swagger`

**Documentación ubicada en:** `documentacion/frontend-traslados-aves.md`
