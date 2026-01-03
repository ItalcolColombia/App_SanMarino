import { Component, Input, Output, EventEmitter, OnInit, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { CrearSeguimientoRequest, SeguimientoItemDto } from '../../services/produccion.service';
import { CatalogoAlimentosService, CatalogItemDto, CatalogItemType } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { InventarioService, FarmInventoryDto } from '../../../inventario/services/inventario.service';
import { EMPTY, forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

// Interfaz extendida localmente para incluir tipoItem y unidad
interface CatalogItemExtended extends CatalogItemDto {
  tipoItem?: string;
  unidad?: string;
}

@Component({
  selector: 'app-modal-seguimiento-diario',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './modal-seguimiento-diario.component.html',
  styleUrls: ['./modal-seguimiento-diario.component.scss']
})
export class ModalSeguimientoDiarioComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() produccionLoteId: number | null = null;
  @Input() editingSeguimiento: SeguimientoItemDto | null = null;
  @Input() loading: boolean = false;
  @Input() fechaEncaset: string | Date | null = null; // Fecha de encaset para calcular etapa
  @Input() granjaId: number | null = null; // ID de la granja para cargar inventario

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<CrearSeguimientoRequest>();

  // Formulario
  form!: FormGroup;

  // Catálogo de alimentos (desde inventario de la granja)
  alimentosCatalog: CatalogItemExtended[] = [];
  alimentosFiltradosHembras: CatalogItemExtended[] = [];
  alimentosFiltradosMachos: CatalogItemExtended[] = [];
  private alimentosByCode = new Map<string, CatalogItemExtended>();
  private alimentosById = new Map<number, CatalogItemExtended>();
  private alimentosByName = new Map<string, CatalogItemExtended>();

  // Tipos de ítem
  tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];

  // Inventario
  inventarioDisponibleHembras: number | null = null;
  inventarioDisponibleMachos: number | null = null;
  inventarioUnidadHembras: string = 'kg';
  inventarioUnidadMachos: string = 'kg';
  inventarioCantidadOriginalHembras: number | null = null;
  inventarioCantidadOriginalMachos: number | null = null;
  cargandoInventarioHembras = false;
  cargandoInventarioMachos = false;
  mensajeInventarioHembras: string = '';
  mensajeInventarioMachos: string = '';

  constructor(
    private fb: FormBuilder,
    private catalogSvc: CatalogoAlimentosService,
    private inventarioSvc: InventarioService
  ) { }

  ngOnInit(): void {
    this.initializeForm();
  }

  ngOnChanges(): void {
    if (this.isOpen) {
      // Actualizar produccionLoteId en el formulario si está disponible
      if (this.produccionLoteId && this.form) {
        this.form.patchValue({ produccionLoteId: this.produccionLoteId });
      }

      // Cargar inventario de la granja si está disponible
      if (this.granjaId) {
        this.cargarInventarioGranja(this.granjaId);
      }

      if (this.editingSeguimiento) {
        this.populateForm();
      } else {
        this.resetForm();
      }
    }
  }

  // ================== FORMULARIO ==================
  private initializeForm(): void {
    this.form = this.fb.group({
      fechaRegistro: [this.todayYMD(), Validators.required],
      produccionLoteId: [null, Validators.required],
      mortalidadH: [0, [Validators.required, Validators.min(0)]],
      mortalidadM: [0, [Validators.required, Validators.min(0)]],
      selH: [0, [Validators.required, Validators.min(0)]],
      // Nuevos campos para tipo de ítem
      tipoItemHembras: [null],
      tipoItemMachos: [null],
      // Alimentos (filtrados por tipo)
      tipoAlimentoHembras: [null],
      tipoAlimentoMachos: [null],
      // Consumo con unidad de medida - el backend hace la conversión
      consumoHembras: [0, [Validators.required, Validators.min(0)]],
      unidadConsumoHembras: ['kg', Validators.required], // 'kg' o 'g'
      consumoMachos: [0, [Validators.required, Validators.min(0)]],
      unidadConsumoMachos: ['kg'], // 'kg' o 'g'
      huevosTotales: [0, [Validators.required, Validators.min(0)]],
      huevosIncubables: [0, [Validators.required, Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
      huevoLimpio: [0, [Validators.min(0)]],
      huevoTratado: [0, [Validators.min(0)]],
      // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
      huevoSucio: [0, [Validators.min(0)]],
      huevoDeforme: [0, [Validators.min(0)]],
      huevoBlanco: [0, [Validators.min(0)]],
      huevoDobleYema: [0, [Validators.min(0)]],
      huevoPiso: [0, [Validators.min(0)]],
      huevoPequeno: [0, [Validators.min(0)]],
      huevoRoto: [0, [Validators.min(0)]],
      huevoDesecho: [0, [Validators.min(0)]],
      huevoOtro: [0, [Validators.min(0)]],
      tipoAlimento: ['Standard', Validators.required],
      pesoHuevo: [0, [Validators.required, Validators.min(0)]],
      etapa: [1, [Validators.required, Validators.min(1), Validators.max(3)]],
      observaciones: [''],
      // Campos de Pesaje Semanal (registro una vez por semana)
      pesoH: [null, [Validators.min(0)]],
      pesoM: [null, [Validators.min(0)]],
      uniformidad: [null, [Validators.min(0), Validators.max(100)]],
      coeficienteVariacion: [null, [Validators.min(0), Validators.max(100)]],
      observacionesPesaje: ['']
    });

    // Calcular etapa automáticamente cuando cambia la fecha
    this.form.get('fechaRegistro')?.valueChanges.subscribe(() => {
      this.calcularYActualizarEtapa();
    });

    // Suscribirse a cambios en tipoItem para filtrar productos
    this.form.get('tipoItemHembras')?.valueChanges.subscribe(tipo => {
      this.filtrarAlimentosPorTipo('hembras', tipo);
      this.form.patchValue({ tipoAlimentoHembras: null }, { emitEvent: false });
      this.inventarioDisponibleHembras = null;
      this.mensajeInventarioHembras = '';
    });

    this.form.get('tipoItemMachos')?.valueChanges.subscribe(tipo => {
      this.filtrarAlimentosPorTipo('machos', tipo);
      this.form.patchValue({ tipoAlimentoMachos: null }, { emitEvent: false });
      this.inventarioDisponibleMachos = null;
      this.mensajeInventarioMachos = '';
    });

    // Consultar inventario cuando se selecciona un alimento
    this.form.get('tipoAlimentoHembras')?.valueChanges.subscribe(id => {
      if (id && this.granjaId) {
        this.consultarInventario('hembras', id);
      } else {
        this.inventarioDisponibleHembras = null;
        this.mensajeInventarioHembras = '';
      }
    });

    this.form.get('tipoAlimentoMachos')?.valueChanges.subscribe(id => {
      if (id && this.granjaId) {
        this.consultarInventario('machos', id);
      } else {
        this.inventarioDisponibleMachos = null;
        this.mensajeInventarioMachos = '';
      }
    });
  }

  private resetForm(): void {
    const fechaHoy = this.todayYMD();
    this.form.reset({
      fechaRegistro: fechaHoy,
      produccionLoteId: this.produccionLoteId,
      mortalidadH: 0,
      mortalidadM: 0,
      selH: 0,
      consumoHembras: 0,
      unidadConsumoHembras: 'kg',
      consumoMachos: 0,
      unidadConsumoMachos: 'kg',
      tipoItemHembras: null,
      tipoItemMachos: null,
      tipoAlimentoHembras: null,
      tipoAlimentoMachos: null,
      huevosTotales: 0,
      huevosIncubables: 0,
      huevoLimpio: 0,
      huevoTratado: 0,
      huevoSucio: 0,
      huevoDeforme: 0,
      huevoBlanco: 0,
      huevoDobleYema: 0,
      huevoPiso: 0,
      huevoPequeno: 0,
      huevoRoto: 0,
      huevoDesecho: 0,
      huevoOtro: 0,
      tipoAlimento: 'Standard',
      pesoHuevo: 0,
      etapa: this.calcularEtapa(fechaHoy),
      observaciones: '',
      // Campos de Pesaje Semanal
      pesoH: null,
      pesoM: null,
      uniformidad: null,
      coeficienteVariacion: null,
      observacionesPesaje: ''
    });
  }

  private populateForm(): void {
    if (!this.editingSeguimiento) return;

    const fechaRegistro = this.toYMD(this.editingSeguimiento.fechaRegistro);
    
    // Leer valores desde metadata si están disponibles
    const metadata: any = this.editingSeguimiento.metadata || {};
    const consumoOriginalHembras = metadata?.consumoOriginalHembras ?? this.editingSeguimiento.consKgH;
    const unidadConsumoOriginalHembras = metadata?.unidadConsumoOriginalHembras ?? 'kg';
    const consumoOriginalMachos = metadata?.consumoOriginalMachos ?? this.editingSeguimiento.consKgM;
    const unidadConsumoOriginalMachos = metadata?.unidadConsumoOriginalMachos ?? 'kg';
    const tipoItemHembras = metadata?.tipoItemHembras ?? null;
    const tipoItemMachos = metadata?.tipoItemMachos ?? null;
    const tipoAlimentoHembras = metadata?.tipoAlimentoHembras ?? null;
    const tipoAlimentoMachos = metadata?.tipoAlimentoMachos ?? null;

    // Convertir a gramos si la unidad original es kg y el valor es pequeño (para mejor UX)
    let consumoHembrasDisplay = consumoOriginalHembras;
    if (unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1) {
      consumoHembrasDisplay = consumoOriginalHembras * 1000;
    }
    
    let consumoMachosDisplay = consumoOriginalMachos;
    if (unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1) {
      consumoMachosDisplay = consumoOriginalMachos * 1000;
    }

    this.form.patchValue({
      fechaRegistro: fechaRegistro,
      produccionLoteId: this.editingSeguimiento.produccionLoteId,
      mortalidadH: this.editingSeguimiento.mortalidadH,
      mortalidadM: this.editingSeguimiento.mortalidadM,
      selH: this.editingSeguimiento.selH || 0,
      consumoHembras: consumoHembrasDisplay,
      unidadConsumoHembras: unidadConsumoOriginalHembras === 'kg' && consumoOriginalHembras < 1 ? 'g' : unidadConsumoOriginalHembras,
      consumoMachos: consumoMachosDisplay,
      unidadConsumoMachos: unidadConsumoOriginalMachos === 'kg' && consumoOriginalMachos < 1 ? 'g' : unidadConsumoOriginalMachos,
      tipoItemHembras: tipoItemHembras,
      tipoItemMachos: tipoItemMachos,
      tipoAlimentoHembras: tipoAlimentoHembras,
      tipoAlimentoMachos: tipoAlimentoMachos,
      huevosTotales: this.editingSeguimiento.huevosTotales,
      huevosIncubables: this.editingSeguimiento.huevosIncubables,
      huevoLimpio: (this.editingSeguimiento as any).huevoLimpio || 0,
      huevoTratado: (this.editingSeguimiento as any).huevoTratado || 0,
      huevoSucio: (this.editingSeguimiento as any).huevoSucio || 0,
      huevoDeforme: (this.editingSeguimiento as any).huevoDeforme || 0,
      huevoBlanco: (this.editingSeguimiento as any).huevoBlanco || 0,
      huevoDobleYema: (this.editingSeguimiento as any).huevoDobleYema || 0,
      huevoPiso: (this.editingSeguimiento as any).huevoPiso || 0,
      huevoPequeno: (this.editingSeguimiento as any).huevoPequeno || 0,
      huevoRoto: (this.editingSeguimiento as any).huevoRoto || 0,
      huevoDesecho: (this.editingSeguimiento as any).huevoDesecho || 0,
      huevoOtro: (this.editingSeguimiento as any).huevoOtro || 0,
      tipoAlimento: this.editingSeguimiento.tipoAlimento || 'Standard',
      pesoHuevo: this.editingSeguimiento.pesoHuevo,
      etapa: this.editingSeguimiento.etapa || this.calcularEtapa(fechaRegistro || this.todayYMD()),
      observaciones: this.editingSeguimiento.observaciones || '',
      // Campos de Pesaje Semanal
      pesoH: (this.editingSeguimiento as any).pesoH || null,
      pesoM: (this.editingSeguimiento as any).pesoM || null,
      uniformidad: (this.editingSeguimiento as any).uniformidad || null,
      coeficienteVariacion: (this.editingSeguimiento as any).coeficienteVariacion || null,
      observacionesPesaje: (this.editingSeguimiento as any).observacionesPesaje || ''
    });

    // Cargar inventario y alimentos si hay tipo de ítem seleccionado
    if (tipoItemHembras && this.granjaId) {
      this.cargarInventarioGranja(this.granjaId);
      setTimeout(() => {
        if (tipoAlimentoHembras) {
          this.consultarInventario('hembras', tipoAlimentoHembras);
        }
      }, 100);
    }
    
    if (tipoItemMachos && this.granjaId) {
      this.cargarInventarioGranja(this.granjaId);
      setTimeout(() => {
        if (tipoAlimentoMachos) {
          this.consultarInventario('machos', tipoAlimentoMachos);
        }
      }, 100);
    }
  }

  // ================== EVENTOS ==================
  onClose(): void {
    this.close.emit();
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // Validación adicional: produccionLoteId es requerido
    if (!this.produccionLoteId) {
      console.error('ProduccionLoteId no está definido');
      return;
    }

    const raw = this.form.value;
    const ymd = this.toYMD(raw.fechaRegistro);

    if (!ymd) {
      console.error('Fecha de registro inválida');
      return;
    }

    const request: CrearSeguimientoRequest = {
      produccionLoteId: this.produccionLoteId, // Usar el Input directamente
      fechaRegistro: this.ymdToIsoAtNoon(ymd),
      mortalidadH: Number(raw.mortalidadH) || 0,
      mortalidadM: Number(raw.mortalidadM) || 0,
      selH: Number(raw.selH) || 0,
      consumoH: Number(raw.consumoHembras) || 0,
      unidadConsumoH: raw.unidadConsumoHembras || 'kg',
      consumoM: Number(raw.consumoMachos) || 0,
      unidadConsumoM: raw.unidadConsumoMachos || 'kg',
      tipoItemHembras: raw.tipoItemHembras || undefined,
      tipoItemMachos: raw.tipoItemMachos || undefined,
      tipoAlimentoHembras: raw.tipoAlimentoHembras ? Number(raw.tipoAlimentoHembras) : undefined,
      tipoAlimentoMachos: raw.tipoAlimentoMachos ? Number(raw.tipoAlimentoMachos) : undefined,
      huevosTotales: Number(raw.huevosTotales) || 0,
      huevosIncubables: Number(raw.huevosIncubables) || 0,
      huevoLimpio: Number(raw.huevoLimpio) || 0,
      huevoTratado: Number(raw.huevoTratado) || 0,
      huevoSucio: Number(raw.huevoSucio) || 0,
      huevoDeforme: Number(raw.huevoDeforme) || 0,
      huevoBlanco: Number(raw.huevoBlanco) || 0,
      huevoDobleYema: Number(raw.huevoDobleYema) || 0,
      huevoPiso: Number(raw.huevoPiso) || 0,
      huevoPequeno: Number(raw.huevoPequeno) || 0,
      huevoRoto: Number(raw.huevoRoto) || 0,
      huevoDesecho: Number(raw.huevoDesecho) || 0,
      huevoOtro: Number(raw.huevoOtro) || 0,
      tipoAlimento: raw.tipoAlimento || 'Standard',
      pesoHuevo: Number(raw.pesoHuevo) || 0,
      etapa: Number(raw.etapa) || this.calcularEtapa(ymd),
      observaciones: raw.observaciones?.trim() || undefined,
      // Campos de Pesaje Semanal
      pesoH: raw.pesoH ? Number(raw.pesoH) : undefined,
      pesoM: raw.pesoM ? Number(raw.pesoM) : undefined,
      uniformidad: raw.uniformidad ? Number(raw.uniformidad) : undefined,
      coeficienteVariacion: raw.coeficienteVariacion ? Number(raw.coeficienteVariacion) : undefined,
      observacionesPesaje: raw.observacionesPesaje?.trim() || undefined
    };

    this.save.emit(request);
  }

  // ================== HELPERS ==================
  getTotalMortalidad(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    return hembras + machos;
  }

  getTotalRetiradas(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const machos = Number(this.form.get('mortalidadM')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    return hembras + machos + selH;
  }

  getTotalRetiradasHembras(): number {
    const hembras = Number(this.form.get('mortalidadH')?.value) || 0;
    const selH = Number(this.form.get('selH')?.value) || 0;
    return hembras + selH;
  }

  getTotalConsumo(): number {
    const consumoH = Number(this.form.get('consumoHembras')?.value) || 0;
    const unidadH = this.form.get('unidadConsumoHembras')?.value || 'kg';
    const consumoM = Number(this.form.get('consumoMachos')?.value) || 0;
    const unidadM = this.form.get('unidadConsumoMachos')?.value || 'kg';
    
    // Convertir todo a kg para sumar
    const consumoHkg = unidadH === 'g' ? consumoH / 1000 : consumoH;
    const consumoMkg = unidadM === 'g' ? consumoM / 1000 : consumoM;
    return consumoHkg + consumoMkg;
  }

  getEficienciaProduccion(): number {
    const total = Number(this.form.get('huevosTotales')?.value) || 0;
    const incubables = Number(this.form.get('huevosIncubables')?.value) || 0;

    if (total === 0) return 0;
    return Math.round((incubables / total) * 100);
  }

  calcularYActualizarEtapa(): void {
    const fechaRegistro = this.form.get('fechaRegistro')?.value;
    if (fechaRegistro) {
      const etapa = this.calcularEtapa(fechaRegistro);
      this.form.patchValue({ etapa }, { emitEvent: false });
    }
  }

  calcularEtapa(fechaRegistro: string | Date | null): number {
    if (!fechaRegistro || !this.fechaEncaset) return 1;

    const fechaEncaset = new Date(this.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    const semana = Math.max(25, Math.ceil(diffDays / 7));

    // Etapa 1: semana 25-33
    if (semana >= 25 && semana <= 33) return 1;
    // Etapa 2: semana 34-50
    if (semana >= 34 && semana <= 50) return 2;
    // Etapa 3: semana >50
    return 3;
  }

  getEtapaLabel(etapa: number): string {
    const labels: { [key: number]: string } = {
      1: 'Etapa 1 (Semana 25-33)',
      2: 'Etapa 2 (Semana 34-50)',
      3: 'Etapa 3 (Semana >50)'
    };
    return labels[etapa] || `Etapa ${etapa}`;
  }

  /** Hoy en formato YYYY-MM-DD (local, sin zona) para <input type="date"> */
  private todayYMD(): string {
    const d = new Date();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    return `${d.getFullYear()}-${mm}-${dd}`;
  }

  /** Normaliza cadenas mm/dd/aaaa, dd/mm/aaaa, ISO o Date a YYYY-MM-DD (local) */
  private toYMD(input: string | Date | null | undefined): string | null {
    if (!input) return null;

    if (input instanceof Date && !isNaN(input.getTime())) {
      const y = input.getFullYear();
      const m = String(input.getMonth() + 1).padStart(2, '0');
      const d = String(input.getDate()).padStart(2, '0');
      return `${y}-${m}-${d}`;
    }

    const s = String(input).trim();

    // YYYY-MM-DD
    const ymd = /^(\d{4})-(\d{2})-(\d{2})$/;
    const m1 = s.match(ymd);
    if (m1) return `${m1[1]}-${m1[2]}-${m1[3]}`;

    // mm/dd/aaaa o dd/mm/aaaa
    const sl = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/;
    const m2 = s.match(sl);
    if (m2) {
      let a = parseInt(m2[1], 10);
      let b = parseInt(m2[2], 10);
      const yyyy = parseInt(m2[3], 10);
      let mm = a, dd = b;
      if (a > 12 && b <= 12) { mm = b; dd = a; }
      const mmS = String(mm).padStart(2, '0');
      const ddS = String(dd).padStart(2, '0');
      return `${yyyy}-${mmS}-${ddS}`;
    }

    // ISO (con T). Extrae la fecha en LOCAL sin cambiar el día
    const d = new Date(s);
    if (!isNaN(d.getTime())) {
      const y = d.getFullYear();
      const m = String(d.getMonth() + 1).padStart(2, '0');
      const day = String(d.getDate()).padStart(2, '0');
      return `${y}-${m}-${day}`;
    }

    return null;
  }

  /** Convierte YYYY-MM-DD a ISO asegurando MEDIODÍA local → evita cruzar de día por zona horaria */
  private ymdToIsoAtNoon(ymd: string): string {
    const iso = new Date(`${ymd}T12:00:00`);
    return iso.toISOString();
  }

  // ================== INVENTARIO Y ALIMENTOS ==================
  
  /**
   * Carga el inventario de la granja y lo mapea a CatalogItemDto
   */
  cargarInventarioGranja(granjaId: number): void {
    if (!granjaId) return;

    this.inventarioSvc.getInventory(granjaId).pipe(
      catchError(err => {
        console.error('Error al cargar inventario:', err);
        return of([]);
      })
    ).subscribe((items: FarmInventoryDto[]) => {
      // Filtrar solo items activos con cantidad > 0
      const itemsActivos = items.filter((item: FarmInventoryDto) => item.active && item.quantity > 0);
      
      // Mapear a CatalogItemExtended
      this.alimentosCatalog = itemsActivos.map((item: FarmInventoryDto) => {
        const catalogItem: CatalogItemExtended = {
          id: item.catalogItemId,
          codigo: item.codigo,
          nombre: item.nombre,
          tipoItem: item.catalogItemMetadata?.type_item || 'alimento',
          unidad: item.unit,
          activo: item.active,
          metadata: item.catalogItemMetadata
        };
        
        this.alimentosByCode.set(item.codigo, catalogItem);
        this.alimentosById.set(item.catalogItemId, catalogItem);
        this.alimentosByName.set(item.nombre.toLowerCase(), catalogItem);
        
        return catalogItem;
      });

      // Aplicar filtros actuales si hay tipo de ítem seleccionado
      const tipoH = this.form.get('tipoItemHembras')?.value;
      const tipoM = this.form.get('tipoItemMachos')?.value;
      if (tipoH) this.filtrarAlimentosPorTipo('hembras', tipoH);
      if (tipoM) this.filtrarAlimentosPorTipo('machos', tipoM);
    });
  }

  /**
   * Filtra alimentos por tipo de ítem
   */
  filtrarAlimentosPorTipo(sexo: 'hembras' | 'machos', tipo: string | null): void {
    if (!tipo) {
      if (sexo === 'hembras') {
        this.alimentosFiltradosHembras = [];
      } else {
        this.alimentosFiltradosMachos = [];
      }
      return;
    }

    const filtrados = this.alimentosCatalog.filter((item: CatalogItemExtended) => item.tipoItem === tipo);
    
    if (sexo === 'hembras') {
      this.alimentosFiltradosHembras = filtrados;
    } else {
      this.alimentosFiltradosMachos = filtrados;
    }
  }

  /**
   * Consulta el inventario disponible para un alimento específico
   */
  consultarInventario(sexo: 'hembras' | 'machos', catalogItemId: number): void {
    if (!this.granjaId || !catalogItemId) return;

    if (sexo === 'hembras') {
      this.cargandoInventarioHembras = true;
      this.mensajeInventarioHembras = '';
    } else {
      this.cargandoInventarioMachos = true;
      this.mensajeInventarioMachos = '';
    }

    this.inventarioSvc.getInventoryByItem(this.granjaId, catalogItemId).pipe(
      catchError(err => {
        console.error('Error al consultar inventario:', err);
        if (sexo === 'hembras') {
          this.cargandoInventarioHembras = false;
          this.mensajeInventarioHembras = 'Error al consultar inventario';
        } else {
          this.cargandoInventarioMachos = false;
          this.mensajeInventarioMachos = 'Error al consultar inventario';
        }
        return of(null);
      })
    ).subscribe(item => {
      if (sexo === 'hembras') {
        this.cargandoInventarioHembras = false;
        if (item) {
          this.inventarioDisponibleHembras = item.quantity;
          this.inventarioUnidadHembras = item.unit;
          this.inventarioCantidadOriginalHembras = item.quantity;
          this.mensajeInventarioHembras = `Disponible: ${item.quantity} ${item.unit}`;
        } else {
          this.inventarioDisponibleHembras = null;
          this.mensajeInventarioHembras = 'No hay inventario disponible';
        }
      } else {
        this.cargandoInventarioMachos = false;
        if (item) {
          this.inventarioDisponibleMachos = item.quantity;
          this.inventarioUnidadMachos = item.unit;
          this.inventarioCantidadOriginalMachos = item.quantity;
          this.mensajeInventarioMachos = `Disponible: ${item.quantity} ${item.unit}`;
        } else {
          this.inventarioDisponibleMachos = null;
          this.mensajeInventarioMachos = 'No hay inventario disponible';
        }
      }
    });
  }
}
