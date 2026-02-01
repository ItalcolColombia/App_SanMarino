// frontend/src/app/features/traslados-huevos/components/modal-traslado-huevos/modal-traslado-huevos.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { TrasladosHuevosService, DisponibilidadLoteDto, CrearTrasladoHuevosDto, ActualizarTrasladoHuevosDto, HuevosDisponiblesDto, TrasladoHuevosDto } from '../../services/traslados-huevos.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService } from '../../../farm/services/farm.service';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { NucleoService, NucleoDto } from '../../../lote-produccion/services/nucleo.service';
import { GalponService } from '../../../galpon/services/galpon.service';

@Component({
  selector: 'app-modal-traslado-huevos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FiltroSelectComponent],
  templateUrl: './modal-traslado-huevos.component.html',
  styleUrls: ['./modal-traslado-huevos.component.scss']
})
export class ModalTrasladoHuevosComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() editingTraslado: TrasladoHuevosDto | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<TrasladoHuevosDto>();

  // Formulario
  formHuevos!: FormGroup;

  // Estado
  disponibilidad = signal<DisponibilidadLoteDto | null>(null);
  loteInfo = signal<LoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingDisponibilidad = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
  showSuccessModal = signal<boolean>(false);
  showErrorModal = signal<boolean>(false);
  successMessage = signal<string>('');
  errorMessage = signal<string>('');
  isEditMode = signal<boolean>(false);
  isReadOnly = computed(() => {
    return this.editingTraslado?.estado === 'Completado' || this.editingTraslado?.estado === 'Cancelado';
  });

  // Signal para rastrear cambios en tipoOperacion del formulario
  tipoOperacionForm = signal<string>('');

  // Computed properties para títulos y botones dinámicos
  modalTitle = computed(() => {
    if (this.editingTraslado && this.isReadOnly()) {
      const tipo = this.editingTraslado.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
      return `Detalle de ${tipo} de Huevos`;
    }
    if (this.editingTraslado && !this.isReadOnly() && this.isEditMode()) {
      const tipo = this.editingTraslado.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
      return `Editar ${tipo} de Huevos`;
    }
    // Si no hay editingTraslado, usar el valor del formulario
    const tipoOperacion = this.tipoOperacionForm() || this.formHuevos?.get('tipoOperacion')?.value || '';
    const tipo = tipoOperacion.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
    return `Nuevo ${tipo} de Huevos`;
  });

  saveButtonText = computed(() => {
    if (this.editingTraslado && this.isEditMode()) {
      return 'Actualizar Registro';
    }
    const tipoOperacion = this.tipoOperacionForm() || this.formHuevos?.get('tipoOperacion')?.value || '';
    if (tipoOperacion.toLowerCase().includes('venta')) {
      return 'Guardar Venta';
    }
    return 'Guardar Traslado';
  });

  // Granjas para destino
  granjas = signal<any[]>([]);

  // Tipos de operación desde lista maestra
  tiposOperacion = signal<string[]>([]);

  // Tipos de destino desde lista maestra
  tiposDestino = signal<string[]>([]);

  // Plantas destino desde lista maestra
  plantasDestino = signal<string[]>([]);

  // Motivos de venta desde lista maestra
  motivosVenta = signal<string[]>([]);

  // Filtros para seleccionar lote destino (solo cuando tipo destino es Granja)
  selectedGranjaDestinoId: number | null = null;
  selectedNucleoDestinoId: string | null = null;
  selectedGalponDestinoId: string | null = null;
  selectedLoteDestinoId: number | null = null;

  // Tipos de huevo para el formulario
  tiposHuevo: Array<{ key: string; label: string; disponible: () => number }> = [
    { key: 'limpio', label: 'Limpio', disponible: () => this.disponibilidad()?.huevos?.limpio ?? 0 },
    { key: 'tratado', label: 'Tratado', disponible: () => this.disponibilidad()?.huevos?.tratado ?? 0 },
    { key: 'sucio', label: 'Sucio', disponible: () => this.disponibilidad()?.huevos?.sucio ?? 0 },
    { key: 'deforme', label: 'Deforme', disponible: () => this.disponibilidad()?.huevos?.deforme ?? 0 },
    { key: 'blanco', label: 'Blanco', disponible: () => this.disponibilidad()?.huevos?.blanco ?? 0 },
    { key: 'dobleYema', label: 'Doble Yema', disponible: () => this.disponibilidad()?.huevos?.dobleYema ?? 0 },
    { key: 'piso', label: 'Piso', disponible: () => this.disponibilidad()?.huevos?.piso ?? 0 },
    { key: 'pequeno', label: 'Pequeño', disponible: () => this.disponibilidad()?.huevos?.pequeno ?? 0 },
    { key: 'roto', label: 'Roto', disponible: () => this.disponibilidad()?.huevos?.roto ?? 0 },
    { key: 'desecho', label: 'Desecho', disponible: () => this.disponibilidad()?.huevos?.desecho ?? 0 },
    { key: 'otro', label: 'Otro', disponible: () => this.disponibilidad()?.huevos?.otro ?? 0 }
  ];

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosHuevosService,
    private loteService: LoteService,
    private farmService: FarmService,
    private masterListService: MasterListService,
    private nucleoService: NucleoService,
    private galponService: GalponService
  ) {
    this.initForm();
  }

  ngOnInit(): void {
    this.cargarGranjas();
    this.cargarTiposOperacion();
    this.cargarTiposDestino();
    this.cargarPlantasDestino();
    this.cargarMotivosVenta();
  }

  private cargarTiposOperacion(): void {
    this.masterListService.getByKey('traslado_de_huevos_tipo_de_operacion').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.tiposOperacion.set(masterList.options);
          // Si no hay valor por defecto y hay opciones, establecer la primera como valor por defecto
          if (this.tiposOperacion().length > 0 && !this.formHuevos.get('tipoOperacion')?.value) {
            this.formHuevos.patchValue({ tipoOperacion: this.tiposOperacion()[0] });
          }
        } else {
          // Fallback a valores por defecto si no se encuentra la lista maestra
          this.tiposOperacion.set(['Traslado', 'Venta']);
        }
      },
      error: (error) => {
        console.error('Error cargando tipos de operación desde lista maestra:', error);
        // Fallback a valores por defecto en caso de error
        this.tiposOperacion.set(['Traslado', 'Venta']);
      }
    });
  }

  private cargarTiposDestino(): void {
    this.masterListService.getByKey('traslado_de_huevos_tipo_destino').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.tiposDestino.set(masterList.options);
        } else {
          // Fallback a valores por defecto si no se encuentra la lista maestra
          this.tiposDestino.set(['Granja', 'Planta']);
        }
      },
      error: (error) => {
        console.error('Error cargando tipos de destino desde lista maestra:', error);
        // Fallback a valores por defecto en caso de error
        this.tiposDestino.set(['Granja', 'Planta']);
      }
    });
  }

  private cargarPlantasDestino(): void {
    this.masterListService.getByKey('traslado_de_huevos_planta_destino').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.plantasDestino.set(masterList.options);
        } else {
          this.plantasDestino.set([]);
        }
      },
      error: (error) => {
        console.error('Error cargando plantas destino desde lista maestra:', error);
        this.plantasDestino.set([]);
      }
    });
  }

  private cargarMotivosVenta(): void {
    this.masterListService.getByKey('traslado_de_huevos_venta_motivo').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.motivosVenta.set(masterList.options);
        } else {
          this.motivosVenta.set([]);
        }
      },
      error: (error) => {
        console.error('Error cargando motivos de venta desde lista maestra:', error);
        this.motivosVenta.set([]);
      }
    });
  }

  // Métodos para manejar cambios en los filtros del lote destino
  onGranjaDestinoChange(granjaId: number | null): void {
    this.selectedGranjaDestinoId = granjaId;
    this.selectedNucleoDestinoId = null;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.formHuevos.patchValue({ loteDestinoId: null });
  }

  // Cuando cambia la granja destino desde el formulario, actualizar el filtro
  onGranjaDestinoFormChange(): void {
    const granjaId = this.formHuevos.get('granjaDestinoId')?.value;
    if (granjaId) {
      this.selectedGranjaDestinoId = granjaId;
      this.selectedNucleoDestinoId = null;
      this.selectedGalponDestinoId = null;
      this.selectedLoteDestinoId = null;
      this.formHuevos.patchValue({ loteDestinoId: null });
    }
  }

  onNucleoDestinoChange(nucleoId: string | null): void {
    this.selectedNucleoDestinoId = nucleoId;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.formHuevos.patchValue({ loteDestinoId: null });
  }

  onGalponDestinoChange(galponId: string | null): void {
    this.selectedGalponDestinoId = galponId;
    this.selectedLoteDestinoId = null;
    this.formHuevos.patchValue({ loteDestinoId: null });
  }

  onLoteDestinoChange(loteId: number | null): void {
    this.selectedLoteDestinoId = loteId;
    this.formHuevos.patchValue({ loteDestinoId: loteId?.toString() || null });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      if (this.loteId) {
        this.cargarLoteInfo(this.loteId);
        this.cargarDisponibilidad(this.loteId.toString());
      }
      if (this.editingTraslado) {
        // Si el estado es Completado o Cancelado, es solo lectura
        // Si es Pendiente, puede ser edición o solo lectura dependiendo de isEditMode
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          // Por defecto, si es Pendiente, activar modo edición
          // Esto se puede cambiar con el botón "Editar" en el modal
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        // Si estamos editando, cargar el lote desde el traslado
        if (this.editingTraslado.loteId) {
          const loteIdNum = Number(this.editingTraslado.loteId);
          if (!isNaN(loteIdNum)) {
            this.cargarDisponibilidad(this.editingTraslado.loteId);
            this.cargarLoteInfo(loteIdNum);
          }
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
      }
    }
    
    if (changes['editingTraslado']) {
      if (this.editingTraslado) {
        // Si el estado es Completado o Cancelado, es solo lectura
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          // Por defecto, si es Pendiente, activar modo edición
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        // Cargar el lote desde el traslado
        if (this.editingTraslado.loteId) {
          const loteIdNum = Number(this.editingTraslado.loteId);
          if (!isNaN(loteIdNum)) {
            this.cargarDisponibilidad(this.editingTraslado.loteId);
            this.cargarLoteInfo(loteIdNum);
          }
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
      }
    }
  }

  private cargarLoteInfo(loteId?: number): void {
    const idToLoad = loteId || this.loteId;
    if (!idToLoad) return;

    this.loteService.getById(idToLoad).subscribe({
      next: (lote) => {
        this.loteInfo.set(lote);
      },
      error: (error) => {
        console.error('Error cargando información del lote:', error);
        this.loteInfo.set(null);
      }
    });
  }

  private initForm(): void {
    const hoyHuevos = new Date().toISOString().split('T')[0];
    // Valor por defecto será el primero de la lista maestra cuando se cargue
    const tipoOperacionDefault = this.tiposOperacion().length > 0 ? this.tiposOperacion()[0] : '';
    // Inicializar signal con el valor por defecto
    this.tipoOperacionForm.set(tipoOperacionDefault);
    
    const huevosControls: any = {
      loteId: ['', [Validators.required]],
      fechaTraslado: [hoyHuevos, [Validators.required]],
      tipoOperacion: [tipoOperacionDefault, [Validators.required]],
      tipoDestino: [null],
      granjaDestinoId: [null],
      plantaDestino: [null],
      loteDestinoId: [null],
      motivo: [null],
      descripcion: [null],
      observaciones: [null]
    };

    this.tiposHuevo.forEach(tipo => {
      huevosControls[`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`] = [0, [Validators.min(0)]];
    });

    this.formHuevos = this.fb.group(huevosControls, { validators: this.validarTrasladoHuevos.bind(this) });

    this.formHuevos.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresDestino(tipo);
      // Actualizar signal para que los computed properties reaccionen
      this.tipoOperacionForm.set(tipo || '');
    });

    // Cuando cambia el tipo de destino, limpiar y actualizar validadores
    this.formHuevos.get('tipoDestino')?.valueChanges.subscribe(tipoDestino => {
      // Limpiar los campos de destino cuando cambia el tipo
      this.formHuevos.patchValue({
        granjaDestinoId: null,
        plantaDestino: null,
        loteDestinoId: null
      }, { emitEvent: false });
      // Limpiar filtros de lote destino
      this.selectedGranjaDestinoId = null;
      this.selectedNucleoDestinoId = null;
      this.selectedGalponDestinoId = null;
      this.selectedLoteDestinoId = null;
      this.actualizarValidadoresDestino(this.formHuevos.get('tipoOperacion')?.value);
    });

    // Cuando cambia la granja destino, actualizar el filtro de lote
    this.formHuevos.get('granjaDestinoId')?.valueChanges.subscribe(granjaId => {
      this.onGranjaDestinoFormChange();
    });
  }

  private loadEditingData(): void {
    if (!this.editingTraslado) return;

    const t = this.editingTraslado;
    const fecha = t.fechaTraslado instanceof Date 
      ? t.fechaTraslado.toISOString().split('T')[0]
      : new Date(t.fechaTraslado).toISOString().split('T')[0];

    // Determinar si el loteDestinoId es una planta (está en la lista de plantas)
    const loteDestinoEsPlanta = t.loteDestinoId && this.plantasDestino().includes(t.loteDestinoId);
    const plantaDestinoValue = loteDestinoEsPlanta ? t.loteDestinoId : null;

    // Si estamos en modo solo lectura, deshabilitar el formulario
    if (this.isReadOnly()) {
      this.formHuevos.disable();
    } else {
      this.formHuevos.enable();
      // Mantener loteId deshabilitado si estamos editando
      this.formHuevos.get('loteId')?.disable();
    }

    this.formHuevos.patchValue({
      loteId: t.loteId,
      fechaTraslado: fecha,
      tipoOperacion: t.tipoOperacion,
      cantidadLimpio: t.cantidadLimpio,
      cantidadTratado: t.cantidadTratado,
      cantidadSucio: t.cantidadSucio,
      cantidadDeforme: t.cantidadDeforme,
      cantidadBlanco: t.cantidadBlanco,
      cantidadDobleYema: t.cantidadDobleYema,
      cantidadPiso: t.cantidadPiso,
      cantidadPequeno: t.cantidadPequeno,
      cantidadRoto: t.cantidadRoto,
      cantidadDesecho: t.cantidadDesecho,
      cantidadOtro: t.cantidadOtro,
      tipoDestino: t.tipoDestino,
      granjaDestinoId: t.granjaDestinoId,
      plantaDestino: plantaDestinoValue,
      loteDestinoId: loteDestinoEsPlanta ? null : t.loteDestinoId,
      motivo: t.motivo,
      descripcion: t.descripcion,
      observaciones: t.observaciones
    }, { emitEvent: false });

    // Actualizar signal para que los computed properties reaccionen
    this.tipoOperacionForm.set(t.tipoOperacion || '');

    // Si hay granja destino, inicializar los filtros
    if (t.granjaDestinoId && t.tipoDestino === 'Granja') {
      this.selectedGranjaDestinoId = t.granjaDestinoId;
      // Intentar obtener el lote destino si existe
      if (t.loteDestinoId && !loteDestinoEsPlanta) {
        this.selectedLoteDestinoId = Number(t.loteDestinoId);
      }
    }
  }

  private resetForm(): void {
    const hoyHuevos = new Date().toISOString().split('T')[0];
    // Usar el primer valor de la lista maestra como valor por defecto
    const tipoOperacionDefault = this.tiposOperacion().length > 0 ? this.tiposOperacion()[0] : '';
    this.formHuevos.reset({
      loteId: this.loteId?.toString() || '',
      fechaTraslado: hoyHuevos,
      tipoOperacion: tipoOperacionDefault,
      tipoDestino: null,
      granjaDestinoId: null,
      plantaDestino: null,
      loteDestinoId: null,
      motivo: null,
      descripcion: null,
      observaciones: null
    });
    this.tiposHuevo.forEach(tipo => {
      this.formHuevos.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.setValue(0);
    });
    this.error.set(null);
    this.success.set(null);
    this.disponibilidad.set(null);
    this.loteInfo.set(null);
  }

  private actualizarValidadoresDestino(tipo: string): void {
    const granjaDestino = this.formHuevos.get('granjaDestinoId');
    const plantaDestino = this.formHuevos.get('plantaDestino');
    const loteDestino = this.formHuevos.get('loteDestinoId');
    const tipoDestino = this.formHuevos.get('tipoDestino');
    const motivo = this.formHuevos.get('motivo');
    const descripcion = this.formHuevos.get('descripcion');

    // Determinar si es tipo "Venta" (case-insensitive para flexibilidad)
    const esVenta = tipo && tipo.toLowerCase().includes('venta');
    
    if (esVenta) {
      // Para ventas, no se requiere destino
      granjaDestino?.clearValidators();
      plantaDestino?.clearValidators();
      loteDestino?.clearValidators();
      tipoDestino?.clearValidators();
      motivo?.setValidators([Validators.required]);
      descripcion?.setValidators([Validators.required]);
    } else {
      // Para traslados, se requiere tipo de destino
      tipoDestino?.setValidators([Validators.required]);
      motivo?.clearValidators();
      descripcion?.clearValidators();

      // Validar granja o planta según el tipo de destino seleccionado
      const tipoDestinoValue = tipoDestino?.value;
      const esGranja = tipoDestinoValue && tipoDestinoValue.toLowerCase().includes('granja');
      const esPlanta = tipoDestinoValue && tipoDestinoValue.toLowerCase().includes('planta');

      if (esGranja) {
        granjaDestino?.setValidators([Validators.required]);
        plantaDestino?.clearValidators();
      } else if (esPlanta) {
        granjaDestino?.clearValidators();
        plantaDestino?.setValidators([Validators.required]);
      } else {
        // Si no hay tipo de destino seleccionado, limpiar validadores
        granjaDestino?.clearValidators();
        plantaDestino?.clearValidators();
      }
    }

    granjaDestino?.updateValueAndValidity();
    plantaDestino?.updateValueAndValidity();
    loteDestino?.updateValueAndValidity();
    tipoDestino?.updateValueAndValidity();
    motivo?.updateValueAndValidity();
    descripcion?.updateValueAndValidity();
  }

  // Métodos helper para determinar el tipo de destino
  esTipoGranja(tipoDestino: string | null | undefined): boolean {
    if (!tipoDestino) return false;
    return tipoDestino.toLowerCase().includes('granja');
  }

  esTipoPlanta(tipoDestino: string | null | undefined): boolean {
    if (!tipoDestino) return false;
    return tipoDestino.toLowerCase().includes('planta');
  }

  // Métodos helper para determinar el tipo de operación
  esTipoVenta(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('venta');
  }

  esTipoTraslado(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('traslado');
  }

  private validarTrasladoHuevos(control: AbstractControl): ValidationErrors | null {
    let totalHuevos = 0;
    this.tiposHuevo.forEach(tipo => {
      const cantidad = control.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.value || 0;
      totalHuevos += cantidad;
    });

    if (totalHuevos === 0) {
      return { sinCantidad: true };
    }

    return null;
  }

  private cargarGranjas(): void {
    this.farmService.getAll().subscribe({
      next: (granjas) => {
        this.granjas.set(granjas);
      },
      error: (error) => {
        console.error('Error cargando granjas:', error);
      }
    });
  }

  private cargarDisponibilidad(loteId: string): void {
    this.loadingDisponibilidad.set(true);
    this.error.set(null);

    this.trasladosService.getDisponibilidadLote(loteId).subscribe({
      next: (disponibilidad) => {
        this.disponibilidad.set(disponibilidad);
        this.loadingDisponibilidad.set(false);

        if (disponibilidad.tipoLote !== 'Produccion') {
          this.error.set('Este lote es de levante, selecciona traslado de aves');
        }

        // Si estamos editando, no actualizamos el loteId del formulario
        if (!this.editingTraslado && this.loteId) {
          this.formHuevos.patchValue({ loteId: loteId });
        }
      },
      error: (error) => {
        console.error('Error cargando disponibilidad:', error);
        this.error.set('Error al cargar disponibilidad del lote');
        this.disponibilidad.set(null);
        this.loadingDisponibilidad.set(false);
      }
    });
  }

  onSubmitHuevos(): void {
    if (this.formHuevos.invalid) {
      this.formHuevos.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    const formValue = this.formHuevos.value;
    const fechaTraslado = typeof formValue.fechaTraslado === 'string'
      ? new Date(formValue.fechaTraslado)
      : (formValue.fechaTraslado instanceof Date ? formValue.fechaTraslado : new Date());

    const dto: CrearTrasladoHuevosDto = {
      loteId: String(formValue.loteId || this.loteId),
      fechaTraslado: fechaTraslado,
      tipoOperacion: formValue.tipoOperacion,
      cantidadLimpio: formValue.cantidadLimpio || 0,
      cantidadTratado: formValue.cantidadTratado || 0,
      cantidadSucio: formValue.cantidadSucio || 0,
      cantidadDeforme: formValue.cantidadDeforme || 0,
      cantidadBlanco: formValue.cantidadBlanco || 0,
      cantidadDobleYema: formValue.cantidadDobleYema || 0,
      cantidadPiso: formValue.cantidadPiso || 0,
      cantidadPequeno: formValue.cantidadPequeno || 0,
      cantidadRoto: formValue.cantidadRoto || 0,
      cantidadDesecho: formValue.cantidadDesecho || 0,
      cantidadOtro: formValue.cantidadOtro || 0,
      granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
      loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
      tipoDestino: formValue.tipoDestino,
      // Si es planta, guardar el nombre de la planta en loteDestinoId o crear un campo nuevo si es necesario
      motivo: formValue.motivo,
      descripcion: formValue.descripcion,
      observaciones: formValue.observaciones
    };

    // Si se seleccionó planta destino, guardarla en loteDestinoId
    if (formValue.plantaDestino && !formValue.loteDestinoId) {
      dto.loteDestinoId = formValue.plantaDestino;
    }

    if (this.editingTraslado && this.isEditMode()) {
      // Actualizar traslado existente
      const updateDto: ActualizarTrasladoHuevosDto = {
        fechaTraslado: fechaTraslado,
        tipoOperacion: formValue.tipoOperacion,
        cantidadLimpio: formValue.cantidadLimpio || 0,
        cantidadTratado: formValue.cantidadTratado || 0,
        cantidadSucio: formValue.cantidadSucio || 0,
        cantidadDeforme: formValue.cantidadDeforme || 0,
        cantidadBlanco: formValue.cantidadBlanco || 0,
        cantidadDobleYema: formValue.cantidadDobleYema || 0,
        cantidadPiso: formValue.cantidadPiso || 0,
        cantidadPequeno: formValue.cantidadPequeno || 0,
        cantidadRoto: formValue.cantidadRoto || 0,
        cantidadDesecho: formValue.cantidadDesecho || 0,
        cantidadOtro: formValue.cantidadOtro || 0,
        granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
        loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
        tipoDestino: formValue.tipoDestino,
        motivo: formValue.motivo,
        descripcion: formValue.descripcion,
        observaciones: formValue.observaciones
      };

      // Si se seleccionó planta destino, guardarla en loteDestinoId
      if (formValue.plantaDestino && !formValue.loteDestinoId) {
        updateDto.loteDestinoId = formValue.plantaDestino;
      }

      this.trasladosService.actualizarTrasladoHuevos(this.editingTraslado.id, updateDto).subscribe({
        next: (result) => {
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.successMessage.set(`${tipoOperacion} actualizado exitosamente. Número: ${result.numeroTraslado}`);
          this.showSuccessModal.set(true);
          this.error.set(null);
          this.success.set(null);
          setTimeout(() => {
            this.showSuccessModal.set(false);
            this.save.emit(result);
          }, 2000);
        },
        error: (error) => {
          console.error('Error actualizando traslado de huevos:', error);
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          this.errorMessage.set(`No se pudo actualizar el ${tipoOperacion}. ${error.message || 'Error desconocido'}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
          // NO cerrar el modal en caso de error
        }
      });
    } else {
      // Crear nuevo traslado
      const dto: CrearTrasladoHuevosDto = {
        loteId: String(formValue.loteId || this.loteId),
        fechaTraslado: fechaTraslado,
        tipoOperacion: formValue.tipoOperacion,
        cantidadLimpio: formValue.cantidadLimpio || 0,
        cantidadTratado: formValue.cantidadTratado || 0,
        cantidadSucio: formValue.cantidadSucio || 0,
        cantidadDeforme: formValue.cantidadDeforme || 0,
        cantidadBlanco: formValue.cantidadBlanco || 0,
        cantidadDobleYema: formValue.cantidadDobleYema || 0,
        cantidadPiso: formValue.cantidadPiso || 0,
        cantidadPequeno: formValue.cantidadPequeno || 0,
        cantidadRoto: formValue.cantidadRoto || 0,
        cantidadDesecho: formValue.cantidadDesecho || 0,
        cantidadOtro: formValue.cantidadOtro || 0,
        granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
        loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
        tipoDestino: formValue.tipoDestino,
        motivo: formValue.motivo,
        descripcion: formValue.descripcion,
        observaciones: formValue.observaciones
      };

      // Si se seleccionó planta destino, guardarla en loteDestinoId
      if (formValue.plantaDestino && !formValue.loteDestinoId) {
        dto.loteDestinoId = formValue.plantaDestino;
      }

      this.trasladosService.crearTrasladoHuevos(dto).subscribe({
        next: (result) => {
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.successMessage.set(`${tipoOperacion} creado exitosamente. Número: ${result.numeroTraslado}`);
          this.showSuccessModal.set(true);
          this.error.set(null);
          this.success.set(null);
          setTimeout(() => {
            this.showSuccessModal.set(false);
            this.save.emit(result);
          }, 2000);
        },
        error: (error) => {
          console.error('Error creando traslado de huevos:', error);
          this.loading.set(false);
          const tipoOperacion = formValue.tipoOperacion?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          this.errorMessage.set(`No se pudo guardar el ${tipoOperacion}. ${error.message || 'Error desconocido'}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
          // NO cerrar el modal en caso de error
        }
      });
    }
  }

  closeModal(): void {
    this.resetForm();
    this.showSuccessModal.set(false);
    this.showErrorModal.set(false);
    this.close.emit();
  }

  closeSuccessModal(): void {
    this.showSuccessModal.set(false);
    this.save.emit();
  }

  closeErrorModal(): void {
    this.showErrorModal.set(false);
  }

  onOverlayClick(event: MouseEvent): void {
    // Cerrar solo si se hace clic en el overlay, no en el contenido del modal
    if (event.target === event.currentTarget) {
      this.closeModal();
    }
  }

  getMaxCantidad(tipoKey: string): number {
    const disponibilidad = this.disponibilidad();
    if (!disponibilidad?.huevos) return 0;

    const keyMap: Record<string, keyof HuevosDisponiblesDto> = {
      'limpio': 'limpio',
      'tratado': 'tratado',
      'sucio': 'sucio',
      'deforme': 'deforme',
      'blanco': 'blanco',
      'dobleYema': 'dobleYema',
      'piso': 'piso',
      'pequeno': 'pequeno',
      'roto': 'roto',
      'desecho': 'desecho',
      'otro': 'otro'
    };

    const valor = disponibilidad.huevos[keyMap[tipoKey]];
    return typeof valor === 'number' ? valor : 0;
  }
}
