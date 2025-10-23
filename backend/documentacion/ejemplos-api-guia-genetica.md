# 🧪 Ejemplos Prácticos - API Guía Genética

## 🎯 **Casos de Uso Reales para el Frontend**

---

## 📋 **1. Flujo Completo de Gestión**

### **Paso 1: Obtener Lista Completa**
```http
GET /api/ProduccionAvicolaRaw
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response Exitosa:**
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
    "uniformidad": "92"
  },
  {
    "id": 2,
    "companyId": 1,
    "anioGuia": "2024",
    "raza": "Cobb 500",
    "edad": "25",
    "mortSemH": "3.20",
    "retiroAcH": "5.50",
    "mortSemM": "4.15",
    "retiroAcM": "6.80",
    "consAcH": "2800",
    "consAcM": "2950",
    "grAveDiaH": "112.0",
    "grAveDiaM": "118.0",
    "pesoH": "2400",
    "pesoM": "2650",
    "uniformidad": "88"
  }
]
```

### **Paso 2: Búsqueda Avanzada con Filtros**
```http
POST /api/ProduccionAvicolaRaw/search
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "anioGuia": "2024",
  "raza": "Cobb 500",
  "page": 1,
  "pageSize": 10,
  "sortBy": "edad",
  "sortDesc": false
}
```

**Response Paginada:**
```json
{
  "items": [
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
      "pesoH": "180",
      "pesoM": "185",
      "uniformidad": "92"
    }
  ],
  "total": 25,
  "page": 1,
  "pageSize": 10
}
```

---

## 📊 **2. CRUD Completo**

### **Crear Nuevo Registro**
```http
POST /api/ProduccionAvicolaRaw
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "companyId": 1,
  "anioGuia": "2024",
  "raza": "Ross 308",
  "edad": "15",
  "mortSemH": "2.1",
  "retiroAcH": "3.5",
  "mortSemM": "2.8",
  "retiroAcM": "4.2",
  "consAcH": "1850",
  "consAcM": "1920",
  "grAveDiaH": "74.0",
  "grAveDiaM": "76.8",
  "pesoH": "1480",
  "pesoM": "1620",
  "uniformidad": "89.5"
}
```

**Response 201:**
```json
{
  "id": 156,
  "companyId": 1,
  "anioGuia": "2024",
  "raza": "Ross 308",
  "edad": "15",
  "mortSemH": "2.1",
  "retiroAcH": "3.5",
  "mortSemM": "2.8",
  "retiroAcM": "4.2",
  "consAcH": "1850",
  "consAcM": "1920",
  "grAveDiaH": "74.0",
  "grAveDiaM": "76.8",
  "pesoH": "1480",
  "pesoM": "1620",
  "uniformidad": "89.5"
}
```

### **Obtener por ID**
```http
GET /api/ProduccionAvicolaRaw/156
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### **Actualizar Registro**
```http
PUT /api/ProduccionAvicolaRaw/156
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
  "id": 156,
  "companyId": 1,
  "anioGuia": "2024",
  "raza": "Ross 308",
  "edad": "15",
  "mortSemH": "2.0",
  "retiroAcH": "3.4",
  "mortSemM": "2.7",
  "retiroAcM": "4.1",
  "consAcH": "1840",
  "consAcM": "1910",
  "uniformidad": "90.0"
}
```

### **Eliminar Registro**
```http
DELETE /api/ProduccionAvicolaRaw/156
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response 200:**
```json
true
```

---

## 📤 **3. Importación desde Excel**

### **Obtener Información del Template**
```http
GET /api/ExcelImport/produccion-avicola/template-info
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

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

### **Descargar Template Excel**
```http
GET /api/ExcelImport/produccion-avicola/template
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Response:** Archivo Excel para descargar

### **Validar Archivo Excel**
```http
POST /api/ExcelImport/produccion-avicola/validate
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: multipart/form-data

file: [archivo-guia-genetica.xlsx]
```

**Response Exitosa:**
```json
{
  "success": true,
  "message": "Archivo validado correctamente",
  "totalRowsProcessed": 150,
  "totalRowsImported": 0,
  "totalRowsFailed": 0,
  "errors": []
}
```

**Response con Errores:**
```json
{
  "success": false,
  "message": "Se encontraron errores en el archivo",
  "totalRowsProcessed": 150,
  "totalRowsImported": 0,
  "totalRowsFailed": 5,
  "errors": [
    "Fila 15: Raza 'Cobb 600' no reconocida",
    "Fila 23: Edad '0' fuera del rango permitido",
    "Fila 45: Valor de mortalidad '150%' inválido",
    "Fila 67: Falta columna requerida 'AÑOGUÍA'",
    "Fila 89: Peso negativo no permitido"
  ]
}
```

### **Importar Archivo Excel**
```http
POST /api/ExcelImport/produccion-avicola
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: multipart/form-data

file: [archivo-guia-genetica.xlsx]
```

**Response Exitosa:**
```json
{
  "success": true,
  "message": "Importación completada exitosamente",
  "totalRowsProcessed": 150,
  "totalRowsImported": 148,
  "totalRowsFailed": 2,
  "errors": [
    "Fila 15: Valor duplicado para Cobb 500, 2024, semana 10",
    "Fila 89: Datos incompletos, se omitió el registro"
  ]
}
```

---

## 🔍 **4. Búsquedas Específicas**

### **Buscar por Raza y Año**
```http
POST /api/ProduccionAvicolaRaw/search
Content-Type: application/json

{
  "anioGuia": "2024",
  "raza": "Cobb 500",
  "page": 1,
  "pageSize": 50,
  "sortBy": "edad",
  "sortDesc": false
}
```

### **Buscar Rango de Edades**
```http
POST /api/ProduccionAvicolaRaw/search
Content-Type: application/json

{
  "anioGuia": "2024",
  "page": 1,
  "pageSize": 100
}
```

**Luego filtrar en frontend por edad:**
```typescript
// En el componente Angular
const guiasFiltradas = this.guias.filter(guia => {
  const edad = parseInt(guia.edad || '0');
  return edad >= 1 && edad <= 25; // Solo levante
});
```

### **Buscar Solo Reproductoras**
```typescript
// Filtro en frontend para reproductoras (semana 26+)
const reproductoras = this.guias.filter(guia => {
  const edad = parseInt(guia.edad || '0');
  return edad >= 26;
});
```

---

## 🎨 **5. Ejemplos de Uso en Angular**

### **Servicio Completo con Manejo de Errores:**
```typescript
import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, BehaviorSubject } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';

@Injectable({
  providedIn: 'root'
})
export class GuiaGeneticaService {
  private baseUrl = 'http://localhost:5002/api/ProduccionAvicolaRaw';
  private excelUrl = 'http://localhost:5002/api/ExcelImport/produccion-avicola';
  
  // Cache para mejorar rendimiento
  private guiasCache$ = new BehaviorSubject<ProduccionAvicolaRawDto[]>([]);
  private lastCacheUpdate = 0;
  private cacheTimeout = 5 * 60 * 1000; // 5 minutos

  constructor(private http: HttpClient) {}

  // Obtener todas las guías con cache
  getAll(forceRefresh = false): Observable<ProduccionAvicolaRawDto[]> {
    const now = Date.now();
    const cacheExpired = now - this.lastCacheUpdate > this.cacheTimeout;
    
    if (!forceRefresh && !cacheExpired && this.guiasCache$.value.length > 0) {
      return this.guiasCache$.asObservable();
    }

    return this.http.get<ProduccionAvicolaRawDto[]>(this.baseUrl)
      .pipe(
        tap(guias => {
          this.guiasCache$.next(guias);
          this.lastCacheUpdate = now;
        }),
        catchError(this.handleError)
      );
  }

  // Búsqueda con filtros avanzados
  searchWithFilters(filtros: GuiaGeneticaFiltros): Observable<ProduccionAvicolaRawDto[]> {
    const request: ProduccionAvicolaRawSearchRequest = {
      anioGuia: filtros.anioGuia,
      raza: filtros.raza,
      page: 1,
      pageSize: 1000, // Obtener todos para filtrar localmente
      sortBy: 'edad',
      sortDesc: false
    };

    return this.search(request).pipe(
      map(result => {
        let guias = result.items;

        // Filtros adicionales que no están en la API
        if (filtros.edadDesde || filtros.edadHasta) {
          guias = guias.filter(guia => {
            const edad = GuiaGeneticaUtils.parseNumericValue(guia.edad);
            if (!edad) return false;
            
            if (filtros.edadDesde && edad < filtros.edadDesde) return false;
            if (filtros.edadHasta && edad > filtros.edadHasta) return false;
            
            return true;
          });
        }

        if (filtros.busquedaTexto) {
          const texto = filtros.busquedaTexto.toLowerCase();
          guias = guias.filter(guia => 
            (guia.raza && guia.raza.toLowerCase().includes(texto)) ||
            (guia.anioGuia && guia.anioGuia.toLowerCase().includes(texto))
          );
        }

        return guias;
      })
    );
  }

  // Obtener razas disponibles
  getRazasDisponibles(): Observable<string[]> {
    return this.getAll().pipe(
      map(guias => {
        const razas = new Set(guias.map(g => g.raza).filter(r => r));
        return Array.from(razas).sort();
      })
    );
  }

  // Obtener años disponibles
  getAniosDisponibles(): Observable<string[]> {
    return this.getAll().pipe(
      map(guias => {
        const anios = new Set(guias.map(g => g.anioGuia).filter(a => a));
        return Array.from(anios).sort().reverse(); // Más recientes primero
      })
    );
  }

  // Obtener guía específica por raza, año y edad
  getGuiaEspecifica(raza: string, anio: string, edad: string): Observable<ProduccionAvicolaRawDto | null> {
    return this.getAll().pipe(
      map(guias => 
        guias.find(g => g.raza === raza && g.anioGuia === anio && g.edad === edad) || null
      )
    );
  }

  // Comparar dos razas
  compararRazas(raza1: string, raza2: string, anio: string): Observable<ComparacionGuias[]> {
    return this.getAll().pipe(
      map(guias => {
        const guias1 = guias.filter(g => g.raza === raza1 && g.anioGuia === anio);
        const guias2 = guias.filter(g => g.raza === raza2 && g.anioGuia === anio);
        
        const comparaciones: ComparacionGuias[] = [];
        
        guias1.forEach(g1 => {
          const g2 = guias2.find(g => g.edad === g1.edad);
          if (g2) {
            const diferencias = this.calcularDiferencias(g1, g2);
            comparaciones.push({
              raza1,
              raza2,
              edad: g1.edad || '',
              diferencias
            });
          }
        });
        
        return comparaciones.sort((a, b) => 
          parseInt(a.edad) - parseInt(b.edad)
        );
      })
    );
  }

  // Importar con progreso
  importFromExcelWithProgress(file: File): Observable<ExcelImportResultDto> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<ExcelImportResultDto>(this.excelUrl, formData)
      .pipe(
        tap(result => {
          if (result.success) {
            // Limpiar cache para forzar recarga
            this.lastCacheUpdate = 0;
          }
        }),
        catchError(this.handleError)
      );
  }

  // Validar archivo con detalles
  validateExcelDetailed(file: File): Observable<{
    isValid: boolean;
    errors: string[];
    warnings: string[];
    preview: any[];
  }> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<ExcelImportResultDto>(`${this.excelUrl}/validate`, formData)
      .pipe(
        map(result => ({
          isValid: result.success,
          errors: result.errors,
          warnings: [], // Se pueden agregar warnings específicos
          preview: [] // Se puede agregar preview de los primeros registros
        })),
        catchError(this.handleError)
      );
  }

  // Métodos privados auxiliares
  private calcularDiferencias(g1: ProduccionAvicolaRawDto, g2: ProduccionAvicolaRawDto): DiferenciaIndicador[] {
    const diferencias: DiferenciaIndicador[] = [];
    
    const campos = [
      { campo: 'mortSemH', nombre: 'Mortalidad Hembras', unidad: '%' },
      { campo: 'mortSemM', nombre: 'Mortalidad Machos', unidad: '%' },
      { campo: 'consAcH', nombre: 'Consumo Hembras', unidad: 'g' },
      { campo: 'consAcM', nombre: 'Consumo Machos', unidad: 'g' },
      { campo: 'pesoH', nombre: 'Peso Hembras', unidad: 'g' },
      { campo: 'pesoM', nombre: 'Peso Machos', unidad: 'g' },
      { campo: 'uniformidad', nombre: 'Uniformidad', unidad: '%' }
    ];

    campos.forEach(({ campo, nombre, unidad }) => {
      const valor1 = GuiaGeneticaUtils.parseNumericValue(g1[campo as keyof ProduccionAvicolaRawDto] as string);
      const valor2 = GuiaGeneticaUtils.parseNumericValue(g2[campo as keyof ProduccionAvicolaRawDto] as string);
      
      if (valor1 !== null && valor2 !== null) {
        const diferencia = valor2 - valor1;
        const porcentajeDiferencia = valor1 !== 0 ? (diferencia / valor1) * 100 : 0;
        
        diferencias.push({
          indicador: nombre,
          valor1,
          valor2,
          diferencia,
          porcentajeDiferencia,
          unidad
        });
      }
    });

    return diferencias;
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
          errorMessage = 'Registro no encontrado';
          break;
        case 413:
          errorMessage = 'Archivo demasiado grande';
          break;
        case 415:
          errorMessage = 'Tipo de archivo no soportado';
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

### **Componente con Funcionalidades Avanzadas:**
```typescript
import { Component, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

@Component({
  selector: 'app-guia-genetica-advanced',
  templateUrl: './guia-genetica-advanced.component.html'
})
export class GuiaGeneticaAdvancedComponent implements OnInit {
  // Datos
  guias: ProduccionAvicolaRawDto[] = [];
  guiasFiltradas: ProduccionAvicolaRawDto[] = [];
  
  // Estados
  loading = false;
  error: string | null = null;
  
  // Filtros
  filtrosForm: FormGroup;
  razasDisponibles: string[] = [];
  aniosDisponibles: string[] = [];
  
  // Paginación
  currentPage = 1;
  pageSize = 20;
  totalRecords = 0;
  
  // Visualización
  vistaActual: 'tabla' | 'tarjetas' | 'graficos' = 'tabla';
  columnasVisibles = new Set(['raza', 'edad', 'mortSemH', 'consAcH', 'pesoH', 'uniformidad']);
  
  // Comparación
  modoComparacion = false;
  guiasSeleccionadas: ProduccionAvicolaRawDto[] = [];

  constructor(
    private guiaService: GuiaGeneticaService,
    private fb: FormBuilder
  ) {
    this.filtrosForm = this.createFiltrosForm();
  }

  ngOnInit() {
    this.setupFiltros();
    this.cargarDatos();
    this.cargarFiltrosDisponibles();
  }

  createFiltrosForm(): FormGroup {
    return this.fb.group({
      anioGuia: [''],
      raza: [''],
      edadDesde: [''],
      edadHasta: [''],
      busquedaTexto: [''],
      soloLevante: [false],
      soloReproductoras: [false]
    });
  }

  setupFiltros() {
    // Búsqueda con debounce
    this.filtrosForm.get('busquedaTexto')?.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged()
      )
      .subscribe(() => this.aplicarFiltros());

    // Otros filtros
    this.filtrosForm.valueChanges
      .pipe(debounceTime(100))
      .subscribe(() => this.aplicarFiltros());
  }

  async cargarDatos() {
    this.loading = true;
    this.error = null;

    try {
      this.guias = await this.guiaService.getAll().toPromise();
      this.aplicarFiltros();
    } catch (error: any) {
      this.error = error.message;
    } finally {
      this.loading = false;
    }
  }

  async cargarFiltrosDisponibles() {
    try {
      [this.razasDisponibles, this.aniosDisponibles] = await Promise.all([
        this.guiaService.getRazasDisponibles().toPromise(),
        this.guiaService.getAniosDisponibles().toPromise()
      ]);
    } catch (error) {
      console.error('Error al cargar filtros:', error);
    }
  }

  aplicarFiltros() {
    const filtros = this.filtrosForm.value;
    let guiasFiltradas = [...this.guias];

    // Filtro por año
    if (filtros.anioGuia) {
      guiasFiltradas = guiasFiltradas.filter(g => g.anioGuia === filtros.anioGuia);
    }

    // Filtro por raza
    if (filtros.raza) {
      guiasFiltradas = guiasFiltradas.filter(g => g.raza === filtros.raza);
    }

    // Filtro por rango de edad
    if (filtros.edadDesde) {
      guiasFiltradas = guiasFiltradas.filter(g => {
        const edad = GuiaGeneticaUtils.parseNumericValue(g.edad);
        return edad !== null && edad >= filtros.edadDesde;
      });
    }

    if (filtros.edadHasta) {
      guiasFiltradas = guiasFiltradas.filter(g => {
        const edad = GuiaGeneticaUtils.parseNumericValue(g.edad);
        return edad !== null && edad <= filtros.edadHasta;
      });
    }

    // Filtro por texto
    if (filtros.busquedaTexto) {
      const texto = filtros.busquedaTexto.toLowerCase();
      guiasFiltradas = guiasFiltradas.filter(g =>
        (g.raza && g.raza.toLowerCase().includes(texto)) ||
        (g.anioGuia && g.anioGuia.toLowerCase().includes(texto))
      );
    }

    // Filtros especiales
    if (filtros.soloLevante) {
      guiasFiltradas = guiasFiltradas.filter(g => {
        const edad = GuiaGeneticaUtils.parseNumericValue(g.edad);
        return edad !== null && edad <= 25;
      });
    }

    if (filtros.soloReproductoras) {
      guiasFiltradas = guiasFiltradas.filter(g => {
        const edad = GuiaGeneticaUtils.parseNumericValue(g.edad);
        return edad !== null && edad > 25;
      });
    }

    this.guiasFiltradas = guiasFiltradas;
    this.totalRecords = guiasFiltradas.length;
    this.currentPage = 1; // Reset página
  }

  // Métodos de paginación
  get guiasPaginadas(): ProduccionAvicolaRawDto[] {
    const inicio = (this.currentPage - 1) * this.pageSize;
    const fin = inicio + this.pageSize;
    return this.guiasFiltradas.slice(inicio, fin);
  }

  onPageChange(page: number) {
    this.currentPage = page;
  }

  onPageSizeChange(size: number) {
    this.pageSize = size;
    this.currentPage = 1;
  }

  // Métodos de visualización
  cambiarVista(vista: 'tabla' | 'tarjetas' | 'graficos') {
    this.vistaActual = vista;
  }

  toggleColumna(columna: string) {
    if (this.columnasVisibles.has(columna)) {
      this.columnasVisibles.delete(columna);
    } else {
      this.columnasVisibles.add(columna);
    }
  }

  // Métodos de comparación
  toggleComparacion() {
    this.modoComparacion = !this.modoComparacion;
    if (!this.modoComparacion) {
      this.guiasSeleccionadas = [];
    }
  }

  toggleSeleccion(guia: ProduccionAvicolaRawDto) {
    const index = this.guiasSeleccionadas.findIndex(g => g.id === guia.id);
    if (index >= 0) {
      this.guiasSeleccionadas.splice(index, 1);
    } else if (this.guiasSeleccionadas.length < 3) { // Máximo 3 para comparar
      this.guiasSeleccionadas.push(guia);
    }
  }

  isSeleccionada(guia: ProduccionAvicolaRawDto): boolean {
    return this.guiasSeleccionadas.some(g => g.id === guia.id);
  }

  // Métodos de exportación
  exportarDatos(formato: 'excel' | 'csv' | 'pdf') {
    // Implementar exportación según formato
    console.log(`Exportando ${this.guiasFiltradas.length} registros en formato ${formato}`);
  }

  // Métodos de utilidad
  formatearValor(valor: string | undefined, tipo: 'numero' | 'porcentaje' = 'numero'): string {
    if (tipo === 'porcentaje') {
      return GuiaGeneticaUtils.formatPercentage(valor);
    }
    return GuiaGeneticaUtils.formatNumericValue(valor);
  }

  getColorRaza(raza: string): string {
    return GuiaGeneticaUtils.getColorRaza(raza);
  }

  getTipoAve(edad: string): string {
    return GuiaGeneticaUtils.getTipoAve(edad);
  }
}
```

---

## 📊 **6. Datos de Prueba Completos**

### **Registros de Ejemplo por Raza:**

```json
{
  "cobb500_levante": [
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
      "raza": "Cobb 500", 
      "edad": "10",
      "mortSemH": "1.8",
      "consAcH": "850",
      "pesoH": "920",
      "uniformidad": "90"
    },
    {
      "anioGuia": "2024",
      "raza": "Cobb 500",
      "edad": "25",
      "mortSemH": "3.2",
      "consAcH": "2800",
      "pesoH": "2400",
      "uniformidad": "88"
    }
  ],
  "ross308_levante": [
    {
      "anioGuia": "2024",
      "raza": "Ross 308",
      "edad": "1", 
      "mortSemH": "0.30",
      "consAcH": "120",
      "pesoH": "175",
      "uniformidad": "91"
    },
    {
      "anioGuia": "2024",
      "raza": "Ross 308",
      "edad": "25",
      "mortSemH": "3.0",
      "consAcH": "2750",
      "pesoH": "2350",
      "uniformidad": "89"
    }
  ]
}
```

### **Estructura de Archivo Excel de Prueba:**

| AÑOGUÍA | RAZA     | Edad | %MortSemH | RetiroAcH | ConsAcH | PesoH | %Uniform |
|---------|----------|------|-----------|-----------|---------|-------|----------|
| 2024    | Cobb 500 | 1    | 0.25      | 0.25      | 125     | 180   | 92       |
| 2024    | Cobb 500 | 2    | 0.35      | 0.60      | 250     | 320   | 91.5     |
| 2024    | Cobb 500 | 3    | 0.45      | 1.05      | 380     | 480   | 91       |

---

## ✅ **Checklist de Implementación Frontend**

### **Funcionalidades Básicas (Prioridad Alta):**
- [ ] Lista paginada de guías genéticas
- [ ] Filtros por año, raza y edad
- [ ] Crear/editar/eliminar registros
- [ ] Importación desde Excel
- [ ] Validación de archivos Excel
- [ ] Descarga de template

### **Funcionalidades Avanzadas (Prioridad Media):**
- [ ] Búsqueda con texto libre
- [ ] Filtros por tipo de ave (levante/reproductora)
- [ ] Comparación entre razas
- [ ] Visualización en gráficos
- [ ] Exportación de datos
- [ ] Cache de datos

### **Funcionalidades Premium (Prioridad Baja):**
- [ ] Análisis de tendencias
- [ ] Alertas por valores fuera de rango
- [ ] Integración con liquidación técnica
- [ ] Reportes automáticos
- [ ] Sincronización en tiempo real

---

## 🎉 **¡Todo Listo para Implementar!**

Con estos ejemplos y documentación, el equipo de frontend tiene **todo lo necesario** para implementar un sistema completo y robusto de gestión de guía genética.

**El backend está 100% operativo y esperando las peticiones!** 🚀
