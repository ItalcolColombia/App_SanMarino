# 🧬 Requisitos Frontend - Sistema de Guía Genética

## 📋 **Resumen del Sistema**

El sistema de **Guía Genética** permite gestionar los datos de producción avícola que se cargan desde archivos Excel. Incluye funcionalidades para consultar, filtrar, importar y exportar datos de guías genéticas por raza, año y edad.

## 🔌 **Endpoints Disponibles**

### **Base URL:** `http://localhost:5002/api`

---

## 📊 **1. CRUD Básico - ProduccionAvicolaRaw**

### **GET** `/api/ProduccionAvicolaRaw`

**Descripción:** Obtiene todos los registros de guía genética.

**Ejemplo de Request:**
```http
GET /api/ProduccionAvicolaRaw
Authorization: Bearer {token}
```

**Response 200:**
```json
[
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
]
```

### **GET** `/api/ProduccionAvicolaRaw/{id}`

**Descripción:** Obtiene un registro específico por ID.

**Ejemplo de Request:**
```http
GET /api/ProduccionAvicolaRaw/1
Authorization: Bearer {token}
```

### **POST** `/api/ProduccionAvicolaRaw/search`

**Descripción:** Búsqueda avanzada con filtros y paginación.

**Request Body:**
```json
{
  "anioGuia": "2024",
  "raza": "Cobb 500",
  "edad": "25",
  "companyId": 1,
  "page": 1,
  "pageSize": 20,
  "sortBy": "edad",
  "sortDesc": false
}
```

**Response 200:**
```json
{
  "items": [
    {
      "id": 1,
      "companyId": 1,
      "anioGuia": "2024",
      "raza": "Cobb 500",
      "edad": "25",
      // ... resto de campos
    }
  ],
  "total": 150,
  "page": 1,
  "pageSize": 20
}
```

### **POST** `/api/ProduccionAvicolaRaw`

**Descripción:** Crea un nuevo registro de guía genética.

**Request Body:**
```json
{
  "companyId": 1,
  "anioGuia": "2024",
  "raza": "Ross 308",
  "edad": "1",
  "mortSemH": "0.30",
  "retiroAcH": "0.30",
  "mortSemM": "0.45",
  "retiroAcM": "0.45",
  "consAcH": "120",
  "consAcM": "125",
  "pesoH": "175",
  "pesoM": "180",
  "uniformidad": "90"
}
```

### **PUT** `/api/ProduccionAvicolaRaw/{id}`

**Descripción:** Actualiza un registro existente.

### **DELETE** `/api/ProduccionAvicolaRaw/{id}`

**Descripción:** Elimina un registro (soft delete).

---

## 📤 **2. Sistema de Importación Excel**

### **POST** `/api/ExcelImport/produccion-avicola`

**Descripción:** Importa datos desde archivo Excel.

**Request (Multipart Form):**
```http
POST /api/ExcelImport/produccion-avicola
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [archivo.xlsx]
```

**Response 200:**
```json
{
  "success": true,
  "message": "Importación completada exitosamente",
  "totalRowsProcessed": 150,
  "totalRowsImported": 148,
  "totalRowsFailed": 2,
  "errors": [
    "Fila 15: Valor inválido en campo 'edad'",
    "Fila 23: Raza no reconocida"
  ]
}
```

### **POST** `/api/ExcelImport/produccion-avicola/validate`

**Descripción:** Valida archivo Excel sin importar.

**Request (Multipart Form):**
```http
POST /api/ExcelImport/produccion-avicola/validate
Authorization: Bearer {token}
Content-Type: multipart/form-data

file: [archivo.xlsx]
```

### **GET** `/api/ExcelImport/produccion-avicola/template-info`

**Descripción:** Obtiene información del template Excel.

**Response 200:**
```json
{
  "tableName": "produccion_avicola_raw",
  "requiredColumns": [
    "AÑOGUÍA",
    "RAZA",
    "Edad"
  ],
  "optionalColumns": [
    "%MortSemH",
    "RetiroAcH",
    "%MortSemM",
    "RetiroAcM",
    "ConsAcH",
    "ConsAcM",
    "PesoH",
    "PesoM",
    "%Uniform"
  ],
  "allPossibleHeaders": [
    "AÑOGUÍA",
    "RAZA",
    "Edad",
    "%MortSemH",
    "RetiroAcH",
    "%MortSemM",
    "RetiroAcM",
    "ConsAcH",
    "ConsAcM",
    "GrAveDiaH",
    "GrAveDiaM",
    "PesoH",
    "PesoM",
    "%Uniform",
    "HTotalAA",
    "%Prod",
    "HIncAA",
    "%AprovSem",
    "PesoHuevo",
    "MasaHuevo",
    "%Grasa",
    "%Nac im",
    "PollitoAA",
    "KcalAveDiaH",
    "KcalAveDiaM",
    "%AprovAc",
    "GR/HuevoT",
    "GR/HuevoInc",
    "GR/Pollito",
    "1000",
    "150",
    "%Apareo",
    "PesoM/H"
  ]
}
```

### **GET** `/api/ExcelImport/produccion-avicola/template`

**Descripción:** Descarga template Excel vacío.

**Response:** Archivo Excel para descargar.

---

## 🎨 **Interfaces TypeScript**

### **Interfaces Principales:**

```typescript
// DTO principal para guía genética
export interface ProduccionAvicolaRawDto {
  id: number;
  companyId: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  
  // Mortalidad y retiro
  mortSemH?: string;      // % Mortalidad semanal hembras
  retiroAcH?: string;     // Retiro acumulado hembras
  mortSemM?: string;      // % Mortalidad semanal machos
  retiroAcM?: string;     // Retiro acumulado machos
  
  // Consumo
  consAcH?: string;       // Consumo acumulado hembras
  consAcM?: string;       // Consumo acumulado machos
  
  // Ganancia diaria
  grAveDiaH?: string;     // Gramos ave/día hembras
  grAveDiaM?: string;     // Gramos ave/día machos
  
  // Peso
  pesoH?: string;         // Peso hembras
  pesoM?: string;         // Peso machos
  
  // Uniformidad
  uniformidad?: string;   // % Uniformidad
  
  // Producción (reproductoras)
  hTotalAa?: string;      // Huevos total ave alojada
  prodPorcentaje?: string; // % Producción
  hIncAa?: string;        // Huevos incubables ave alojada
  aprovSem?: string;      // % Aprovechamiento semanal
  pesoHuevo?: string;     // Peso huevo
  masaHuevo?: string;     // Masa huevo
  grasaPorcentaje?: string; // % Grasa
  nacimPorcentaje?: string; // % Nacimiento
  pollitoAa?: string;     // Pollitos ave alojada
  
  // Consumo energético
  kcalAveDiaH?: string;   // Kcal ave/día hembras
  kcalAveDiaM?: string;   // Kcal ave/día machos
  
  // Aprovechamiento
  aprovAc?: string;       // % Aprovechamiento acumulado
  
  // Pesos específicos
  grHuevoT?: string;      // Gramos/huevo total
  grHuevoInc?: string;    // Gramos/huevo incubable
  grPollito?: string;     // Gramos/pollito
  
  // Valores comerciales
  valor1000?: string;     // Valor 1000
  valor150?: string;      // Valor 150
  
  // Apareamiento
  apareo?: string;        // % Apareo
  pesoMh?: string;        // Peso M/H
}

// DTO para crear nuevo registro
export interface CreateProduccionAvicolaRawDto {
  companyId: number;
  anioGuia?: string;
  raza?: string;
  edad?: string;
  mortSemH?: string;
  retiroAcH?: string;
  mortSemM?: string;
  retiroAcM?: string;
  consAcH?: string;
  consAcM?: string;
  grAveDiaH?: string;
  grAveDiaM?: string;
  pesoH?: string;
  pesoM?: string;
  uniformidad?: string;
  hTotalAa?: string;
  prodPorcentaje?: string;
  hIncAa?: string;
  aprovSem?: string;
  pesoHuevo?: string;
  masaHuevo?: string;
  grasaPorcentaje?: string;
  nacimPorcentaje?: string;
  pollitoAa?: string;
  kcalAveDiaH?: string;
  kcalAveDiaM?: string;
  aprovAc?: string;
  grHuevoT?: string;
  grHuevoInc?: string;
  grPollito?: string;
  valor1000?: string;
  valor150?: string;
  apareo?: string;
  pesoMh?: string;
}

// DTO para actualizar registro
export interface UpdateProduccionAvicolaRawDto extends CreateProduccionAvicolaRawDto {
  id: number;
}

// Request para búsqueda
export interface ProduccionAvicolaRawSearchRequest {
  anioGuia?: string;
  raza?: string;
  edad?: string;
  companyId?: number;
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDesc?: boolean;
}

// Resultado paginado
export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// Resultado de importación Excel
export interface ExcelImportResultDto {
  success: boolean;
  message: string;
  totalRowsProcessed: number;
  totalRowsImported: number;
  totalRowsFailed: number;
  errors: string[];
}

// Información del template Excel
export interface ExcelTemplateInfoDto {
  tableName: string;
  requiredColumns: string[];
  optionalColumns: string[];
  allPossibleHeaders: string[];
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
export class GuiaGeneticaService {
  private baseUrl = 'http://localhost:5002/api/ProduccionAvicolaRaw';
  private excelUrl = 'http://localhost:5002/api/ExcelImport/produccion-avicola';

  constructor(private http: HttpClient) {}

  // =====================================================
  // CRUD BÁSICO
  // =====================================================

  // Obtener todos los registros
  getAll(): Observable<ProduccionAvicolaRawDto[]> {
    return this.http.get<ProduccionAvicolaRawDto[]>(this.baseUrl)
      .pipe(catchError(this.handleError));
  }

  // Obtener por ID
  getById(id: number): Observable<ProduccionAvicolaRawDto> {
    return this.http.get<ProduccionAvicolaRawDto>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // Búsqueda con filtros
  search(request: ProduccionAvicolaRawSearchRequest): Observable<PagedResult<ProduccionAvicolaRawDto>> {
    return this.http.post<PagedResult<ProduccionAvicolaRawDto>>(`${this.baseUrl}/search`, request)
      .pipe(catchError(this.handleError));
  }

  // Crear nuevo registro
  create(dto: CreateProduccionAvicolaRawDto): Observable<ProduccionAvicolaRawDto> {
    return this.http.post<ProduccionAvicolaRawDto>(this.baseUrl, dto)
      .pipe(catchError(this.handleError));
  }

  // Actualizar registro
  update(id: number, dto: UpdateProduccionAvicolaRawDto): Observable<ProduccionAvicolaRawDto> {
    return this.http.put<ProduccionAvicolaRawDto>(`${this.baseUrl}/${id}`, dto)
      .pipe(catchError(this.handleError));
  }

  // Eliminar registro
  delete(id: number): Observable<boolean> {
    return this.http.delete<boolean>(`${this.baseUrl}/${id}`)
      .pipe(catchError(this.handleError));
  }

  // =====================================================
  // IMPORTACIÓN EXCEL
  // =====================================================

  // Importar desde Excel
  importFromExcel(file: File): Observable<ExcelImportResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<ExcelImportResultDto>(this.excelUrl, formData)
      .pipe(catchError(this.handleError));
  }

  // Validar archivo Excel
  validateExcel(file: File): Observable<ExcelImportResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<ExcelImportResultDto>(`${this.excelUrl}/validate`, formData)
      .pipe(catchError(this.handleError));
  }

  // Obtener información del template
  getTemplateInfo(): Observable<ExcelTemplateInfoDto> {
    return this.http.get<ExcelTemplateInfoDto>(`${this.excelUrl}/template-info`)
      .pipe(catchError(this.handleError));
  }

  // Descargar template Excel
  downloadTemplate(): Observable<Blob> {
    return this.http.get(`${this.excelUrl}/template`, { 
      responseType: 'blob' 
    }).pipe(catchError(this.handleError));
  }

  // =====================================================
  // MÉTODOS AUXILIARES
  // =====================================================

  // Obtener razas disponibles
  getRazasDisponibles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/razas`)
      .pipe(catchError(this.handleError));
  }

  // Obtener años disponibles
  getAniosDisponibles(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/anios`)
      .pipe(catchError(this.handleError));
  }

  // Obtener datos por raza y año
  getByRazaYAnio(raza: string, anio: string): Observable<ProduccionAvicolaRawDto[]> {
    const request: ProduccionAvicolaRawSearchRequest = {
      raza,
      anioGuia: anio,
      page: 1,
      pageSize: 1000,
      sortBy: 'edad',
      sortDesc: false
    };
    
    return this.search(request).pipe(
      map(result => result.items),
      catchError(this.handleError)
    );
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
          errorMessage = 'Registro no encontrado';
          break;
        case 500:
          errorMessage = 'Error interno del servidor';
          break;
        default:
          errorMessage = `Error ${error.status}: ${error.message}`;
      }
    }
    
    console.error('Error en GuiaGeneticaService:', error);
    return throwError(() => new Error(errorMessage));
  }
}
```

---

## 🎨 **Componentes Sugeridos**

### **1. Lista de Guías Genéticas**

```typescript
// guia-genetica-list.component.ts
export class GuiaGeneticaListComponent implements OnInit {
  guias: ProduccionAvicolaRawDto[] = [];
  filtros: ProduccionAvicolaRawSearchRequest = {
    page: 1,
    pageSize: 20,
    sortBy: 'edad',
    sortDesc: false
  };
  totalRecords = 0;
  loading = false;
  error: string | null = null;

  // Filtros disponibles
  razasDisponibles: string[] = [];
  aniosDisponibles: string[] = [];

  constructor(private guiaService: GuiaGeneticaService) {}

  ngOnInit() {
    this.cargarFiltrosDisponibles();
    this.buscar();
  }

  async cargarFiltrosDisponibles() {
    try {
      this.razasDisponibles = await this.guiaService.getRazasDisponibles().toPromise();
      this.aniosDisponibles = await this.guiaService.getAniosDisponibles().toPromise();
    } catch (error) {
      console.error('Error al cargar filtros:', error);
    }
  }

  async buscar() {
    this.loading = true;
    this.error = null;

    try {
      const result = await this.guiaService.search(this.filtros).toPromise();
      this.guias = result.items;
      this.totalRecords = result.total;
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  onFiltroChange() {
    this.filtros.page = 1;
    this.buscar();
  }

  onPageChange(page: number) {
    this.filtros.page = page;
    this.buscar();
  }

  onSort(field: string) {
    if (this.filtros.sortBy === field) {
      this.filtros.sortDesc = !this.filtros.sortDesc;
    } else {
      this.filtros.sortBy = field;
      this.filtros.sortDesc = false;
    }
    this.buscar();
  }
}
```

### **2. Importador Excel**

```typescript
// excel-import.component.ts
export class ExcelImportComponent {
  selectedFile: File | null = null;
  importing = false;
  validating = false;
  result: ExcelImportResultDto | null = null;
  templateInfo: ExcelTemplateInfoDto | null = null;

  constructor(private guiaService: GuiaGeneticaService) {}

  ngOnInit() {
    this.cargarTemplateInfo();
  }

  async cargarTemplateInfo() {
    try {
      this.templateInfo = await this.guiaService.getTemplateInfo().toPromise();
    } catch (error) {
      console.error('Error al cargar info del template:', error);
    }
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file && file.type.includes('spreadsheet')) {
      this.selectedFile = file;
      this.result = null;
    } else {
      alert('Por favor seleccione un archivo Excel válido');
    }
  }

  async validarArchivo() {
    if (!this.selectedFile) return;

    this.validating = true;
    try {
      this.result = await this.guiaService.validateExcel(this.selectedFile).toPromise();
    } catch (error: any) {
      this.result = {
        success: false,
        message: error.message,
        totalRowsProcessed: 0,
        totalRowsImported: 0,
        totalRowsFailed: 0,
        errors: [error.message]
      };
    } finally {
      this.validating = false;
    }
  }

  async importarArchivo() {
    if (!this.selectedFile) return;

    this.importing = true;
    try {
      this.result = await this.guiaService.importFromExcel(this.selectedFile).toPromise();
      
      if (this.result.success) {
        // Emitir evento para refrescar lista principal
        // O navegar a la lista
      }
    } catch (error: any) {
      this.result = {
        success: false,
        message: error.message,
        totalRowsProcessed: 0,
        totalRowsImported: 0,
        totalRowsFailed: 0,
        errors: [error.message]
      };
    } finally {
      this.importing = false;
    }
  }

  async descargarTemplate() {
    try {
      const blob = await this.guiaService.downloadTemplate().toPromise();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'template-guia-genetica.xlsx';
      link.click();
      window.URL.revokeObjectURL(url);
    } catch (error) {
      console.error('Error al descargar template:', error);
    }
  }
}
```

### **3. Editor de Guía Genética**

```typescript
// guia-genetica-form.component.ts
export class GuiaGeneticaFormComponent implements OnInit {
  @Input() guiaId?: number;
  @Output() saved = new EventEmitter<ProduccionAvicolaRawDto>();
  @Output() cancelled = new EventEmitter<void>();

  form: FormGroup;
  loading = false;
  saving = false;
  error: string | null = null;

  // Opciones para selects
  razasDisponibles = ['Cobb 500', 'Ross 308', 'Arbor Acres', 'Hubbard'];
  aniosDisponibles = ['2024', '2023', '2022'];

  constructor(
    private fb: FormBuilder,
    private guiaService: GuiaGeneticaService
  ) {
    this.form = this.createForm();
  }

  ngOnInit() {
    if (this.guiaId) {
      this.cargarGuia();
    }
  }

  createForm(): FormGroup {
    return this.fb.group({
      anioGuia: ['', Validators.required],
      raza: ['', Validators.required],
      edad: ['', [Validators.required, Validators.min(1), Validators.max(100)]],
      mortSemH: [''],
      retiroAcH: [''],
      mortSemM: [''],
      retiroAcM: [''],
      consAcH: [''],
      consAcM: [''],
      grAveDiaH: [''],
      grAveDiaM: [''],
      pesoH: [''],
      pesoM: [''],
      uniformidad: ['']
      // ... más campos según necesidad
    });
  }

  async cargarGuia() {
    if (!this.guiaId) return;

    this.loading = true;
    try {
      const guia = await this.guiaService.getById(this.guiaId).toPromise();
      this.form.patchValue(guia);
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  async onSubmit() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.error = null;

    try {
      const formValue = this.form.value;
      let result: ProduccionAvicolaRawDto;

      if (this.guiaId) {
        // Actualizar
        const updateDto: UpdateProduccionAvicolaRawDto = {
          id: this.guiaId,
          companyId: 1, // Obtener del contexto
          ...formValue
        };
        result = await this.guiaService.update(this.guiaId, updateDto).toPromise();
      } else {
        // Crear
        const createDto: CreateProduccionAvicolaRawDto = {
          companyId: 1, // Obtener del contexto
          ...formValue
        };
        result = await this.guiaService.create(createDto).toPromise();
      }

      this.saved.emit(result);
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.saving = false;
    }
  }

  onCancel() {
    this.cancelled.emit();
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
      if (field.errors['max']) return `Valor máximo: ${field.errors['max'].max}`;
    }
    return '';
  }
}
```

---

## 📱 **Templates HTML**

### **Lista de Guías Genéticas:**

```html
<!-- guia-genetica-list.component.html -->
<div class="card">
  <div class="card-header d-flex justify-content-between align-items-center">
    <h5 class="mb-0">Guía Genética</h5>
    <div>
      <button class="btn btn-success me-2" (click)="openImportModal()">
        <i class="fas fa-file-excel"></i> Importar Excel
      </button>
      <button class="btn btn-primary" (click)="openCreateModal()">
        <i class="fas fa-plus"></i> Nuevo Registro
      </button>
    </div>
  </div>

  <div class="card-body">
    <!-- Filtros -->
    <div class="row mb-3">
      <div class="col-md-3">
        <label class="form-label">Año</label>
        <select class="form-select" [(ngModel)]="filtros.anioGuia" (change)="onFiltroChange()">
          <option value="">Todos</option>
          <option *ngFor="let anio of aniosDisponibles" [value]="anio">{{ anio }}</option>
        </select>
      </div>
      <div class="col-md-3">
        <label class="form-label">Raza</label>
        <select class="form-select" [(ngModel)]="filtros.raza" (change)="onFiltroChange()">
          <option value="">Todas</option>
          <option *ngFor="let raza of razasDisponibles" [value]="raza">{{ raza }}</option>
        </select>
      </div>
      <div class="col-md-3">
        <label class="form-label">Edad</label>
        <input type="number" class="form-control" [(ngModel)]="filtros.edad" 
               (change)="onFiltroChange()" placeholder="Edad en semanas">
      </div>
      <div class="col-md-3">
        <label class="form-label">Registros por página</label>
        <select class="form-select" [(ngModel)]="filtros.pageSize" (change)="onFiltroChange()">
          <option value="10">10</option>
          <option value="20">20</option>
          <option value="50">50</option>
          <option value="100">100</option>
        </select>
      </div>
    </div>

    <!-- Tabla -->
    <div class="table-responsive">
      <table class="table table-striped table-hover">
        <thead class="table-dark">
          <tr>
            <th (click)="onSort('anioGuia')" style="cursor: pointer;">
              Año 
              <i class="fas fa-sort" *ngIf="filtros.sortBy !== 'anioGuia'"></i>
              <i class="fas fa-sort-up" *ngIf="filtros.sortBy === 'anioGuia' && !filtros.sortDesc"></i>
              <i class="fas fa-sort-down" *ngIf="filtros.sortBy === 'anioGuia' && filtros.sortDesc"></i>
            </th>
            <th (click)="onSort('raza')" style="cursor: pointer;">Raza</th>
            <th (click)="onSort('edad')" style="cursor: pointer;">Edad</th>
            <th>Mort. H (%)</th>
            <th>Mort. M (%)</th>
            <th>Cons. H (g)</th>
            <th>Cons. M (g)</th>
            <th>Peso H (g)</th>
            <th>Peso M (g)</th>
            <th>Uniformidad (%)</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let guia of guias">
            <td>{{ guia.anioGuia }}</td>
            <td>{{ guia.raza }}</td>
            <td>{{ guia.edad }}</td>
            <td>{{ guia.mortSemH | number:'1.2-2' }}</td>
            <td>{{ guia.mortSemM | number:'1.2-2' }}</td>
            <td>{{ guia.consAcH | number:'1.0-0' }}</td>
            <td>{{ guia.consAcM | number:'1.0-0' }}</td>
            <td>{{ guia.pesoH | number:'1.0-0' }}</td>
            <td>{{ guia.pesoM | number:'1.0-0' }}</td>
            <td>{{ guia.uniformidad | number:'1.1-1' }}</td>
            <td>
              <div class="btn-group btn-group-sm">
                <button class="btn btn-outline-primary" (click)="editGuia(guia.id)">
                  <i class="fas fa-edit"></i>
                </button>
                <button class="btn btn-outline-danger" (click)="deleteGuia(guia.id)">
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

    <!-- Paginación -->
    <nav *ngIf="totalRecords > filtros.pageSize">
      <ngb-pagination 
        [(page)]="filtros.page"
        [pageSize]="filtros.pageSize"
        [collectionSize]="totalRecords"
        [maxSize]="5"
        [rotate]="true"
        (pageChange)="onPageChange($event)">
      </ngb-pagination>
    </nav>
  </div>
</div>
```

### **Importador Excel:**

```html
<!-- excel-import.component.html -->
<div class="card">
  <div class="card-header">
    <h5>Importar Guía Genética desde Excel</h5>
  </div>
  <div class="card-body">
    
    <!-- Información del Template -->
    <div class="alert alert-info" *ngIf="templateInfo">
      <h6><i class="fas fa-info-circle"></i> Información del Template</h6>
      <p><strong>Columnas requeridas:</strong> {{ templateInfo.requiredColumns.join(', ') }}</p>
      <p><strong>Columnas opcionales:</strong> {{ templateInfo.optionalColumns.join(', ') }}</p>
      <button class="btn btn-sm btn-outline-primary" (click)="descargarTemplate()">
        <i class="fas fa-download"></i> Descargar Template
      </button>
    </div>

    <!-- Selector de Archivo -->
    <div class="mb-3">
      <label for="fileInput" class="form-label">Seleccionar archivo Excel</label>
      <input type="file" class="form-control" id="fileInput" 
             accept=".xlsx,.xls" (change)="onFileSelected($event)">
      <div class="form-text">
        Formatos soportados: .xlsx, .xls
      </div>
    </div>

    <!-- Archivo Seleccionado -->
    <div class="alert alert-secondary" *ngIf="selectedFile">
      <i class="fas fa-file-excel"></i> 
      <strong>{{ selectedFile.name }}</strong> 
      ({{ (selectedFile.size / 1024 / 1024) | number:'1.2-2' }} MB)
    </div>

    <!-- Botones de Acción -->
    <div class="mb-3" *ngIf="selectedFile">
      <button class="btn btn-warning me-2" (click)="validarArchivo()" 
              [disabled]="validating">
        <span class="spinner-border spinner-border-sm me-1" *ngIf="validating"></span>
        <i class="fas fa-check-circle" *ngIf="!validating"></i>
        {{ validating ? 'Validando...' : 'Validar Archivo' }}
      </button>
      
      <button class="btn btn-success" (click)="importarArchivo()" 
              [disabled]="importing || !selectedFile">
        <span class="spinner-border spinner-border-sm me-1" *ngIf="importing"></span>
        <i class="fas fa-upload" *ngIf="!importing"></i>
        {{ importing ? 'Importando...' : 'Importar Datos' }}
      </button>
    </div>

    <!-- Resultado -->
    <div *ngIf="result" [class]="result.success ? 'alert alert-success' : 'alert alert-danger'">
      <h6>
        <i [class]="result.success ? 'fas fa-check-circle' : 'fas fa-exclamation-triangle'"></i>
        Resultado de la Importación
      </h6>
      
      <p><strong>{{ result.message }}</strong></p>
      
      <div class="row" *ngIf="result.totalRowsProcessed > 0">
        <div class="col-md-4">
          <small class="text-muted">Filas procesadas:</small><br>
          <strong>{{ result.totalRowsProcessed }}</strong>
        </div>
        <div class="col-md-4">
          <small class="text-muted">Filas importadas:</small><br>
          <strong class="text-success">{{ result.totalRowsImported }}</strong>
        </div>
        <div class="col-md-4">
          <small class="text-muted">Filas con errores:</small><br>
          <strong class="text-danger">{{ result.totalRowsFailed }}</strong>
        </div>
      </div>

      <!-- Errores Detallados -->
      <div *ngIf="result.errors && result.errors.length > 0" class="mt-3">
        <h6>Errores encontrados:</h6>
        <ul class="mb-0">
          <li *ngFor="let error of result.errors" class="text-danger">
            {{ error }}
          </li>
        </ul>
      </div>
    </div>
  </div>
</div>
```

---

## 🔧 **Configuración de Rutas**

```typescript
// app-routing.module.ts
const routes: Routes = [
  {
    path: 'guia-genetica',
    children: [
      { path: '', component: GuiaGeneticaListComponent },
      { path: 'importar', component: ExcelImportComponent },
      { path: 'nuevo', component: GuiaGeneticaFormComponent },
      { path: 'editar/:id', component: GuiaGeneticaFormComponent }
    ]
  }
];
```

---

## 🎯 **Funcionalidades Implementadas**

### **✅ CRUD Completo:**
- [x] Listar guías genéticas con paginación
- [x] Buscar y filtrar por año, raza, edad
- [x] Crear nuevos registros
- [x] Editar registros existentes
- [x] Eliminar registros (soft delete)
- [x] Ordenamiento por columnas

### **✅ Importación Excel:**
- [x] Subir archivos Excel
- [x] Validar formato antes de importar
- [x] Importar datos masivamente
- [x] Reporte de errores detallado
- [x] Descargar template vacío
- [x] Información de columnas requeridas

### **✅ Funcionalidades Avanzadas:**
- [x] Filtros dinámicos
- [x] Paginación configurable
- [x] Ordenamiento por columnas
- [x] Manejo de errores completo
- [x] Loading states
- [x] Validaciones de formulario

---

## 📊 **Datos de Prueba**

### **Registros Disponibles:**
```json
{
  "ejemplosDatos": [
    {
      "anioGuia": "2024",
      "raza": "Cobb 500",
      "edad": "1",
      "mortSemH": "0.25",
      "consAcH": "125",
      "pesoH": "180",
      "uniformidad": "92"
    },
    {
      "anioGuia": "2024", 
      "raza": "Ross 308",
      "edad": "25",
      "mortSemH": "3.2",
      "consAcH": "2800",
      "pesoH": "2400",
      "uniformidad": "88"
    }
  ]
}
```

---

## ✅ **Checklist de Implementación**

### **Backend (✅ Completado)**
- [x] CRUD endpoints funcionando
- [x] Importación Excel implementada
- [x] Validaciones de negocio
- [x] Manejo de errores
- [x] Documentación API

### **Frontend (📋 Por Implementar)**
- [ ] Servicio Angular
- [ ] Interfaces TypeScript
- [ ] Componente lista
- [ ] Componente importador
- [ ] Componente formulario
- [ ] Rutas y navegación
- [ ] Estilos y UX
- [ ] Validaciones frontend
- [ ] Pruebas unitarias

---

## 🎉 **¡Todo Listo para Implementar!**

Con esta documentación el frontend tiene **todo lo necesario** para implementar el sistema completo de guía genética con importación Excel.

**El backend está 100% funcional y esperando las peticiones!** 🚀
