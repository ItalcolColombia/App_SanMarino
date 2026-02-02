// frontend/src/app/features/movimientos-aves/components/modal-movimiento-aves/modal-movimiento-aves.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { MovimientosAvesService, CrearMovimientoAvesDto, ActualizarMovimientoAvesDto, MovimientoAvesDto, InformacionLoteDto } from '../../services/movimientos-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';
import { ConfirmationModalComponent, ConfirmationModalData } from '../../../../shared/components/confirmation-modal/confirmation-modal.component';
import { CountryFilterService } from '../../../../core/services/country/country-filter.service';

@Component({
  selector: 'app-modal-movimiento-aves',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FiltroSelectComponent, ConfirmationModalComponent],
  templateUrl: './modal-movimiento-aves.component.html',
  styleUrls: ['./modal-movimiento-aves.component.scss']
})
export class ModalMovimientoAvesComponent implements OnInit, OnChanges {
  @Input() isOpen: boolean = false;
  @Input() loteId: number | null = null;
  @Input() editingMovimiento: MovimientoAvesDto | null = null;

  @Output() close = new EventEmitter<void>();
  @Output() save = new EventEmitter<MovimientoAvesDto>();

  // Formulario
  formMovimiento!: FormGroup;

  // Estado
  informacionLote = signal<InformacionLoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingInfo = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
  showSuccessModal = signal<boolean>(false);
  showErrorModal = signal<boolean>(false);
  successMessage = signal<string>('');
  errorMessage = signal<string>('');
  isEditMode = signal<boolean>(false);
  isReadOnly = computed(() => {
    return this.editingMovimiento?.estado === 'Completado' || this.editingMovimiento?.estado === 'Cancelado';
  });

  // Computed properties para títulos y botones dinámicos
  modalTitle = computed(() => {
    if (this.editingMovimiento && this.isReadOnly()) {
      return `Detalle de Movimiento de Aves`;
    }
    if (this.editingMovimiento && !this.isReadOnly() && this.isEditMode()) {
      return `Editar Movimiento de Aves`;
    }
    return `Nuevo Movimiento de Aves`;
  });

  saveButtonText = computed(() => {
    if (this.editingMovimiento && this.isEditMode()) {
      return 'Actualizar Registro';
    }
    return 'Guardar Movimiento';
  });

  // Granjas para destino
  granjas = signal<FarmDto[]>([]);

  // Tipos de movimiento desde lista maestra
  tiposMovimiento = signal<string[]>([]);
  
  // Motivos de movimiento desde lista maestra
  motivosMovimiento = signal<string[]>([]);
  
  // Plantas destino desde lista maestra (misma que en traslados de huevos)
  plantasDestino = signal<string[]>([]);
  
  // Signal para rastrear cambios en tipoMovimiento del formulario
  tipoMovimientoForm = signal<string>('');

  // Filtros para seleccionar lote destino
  selectedGranjaDestinoId: number | null = null;
  selectedNucleoDestinoId: string | null = null;
  selectedGalponDestinoId: string | null = null;
  selectedLoteDestinoId: number | null = null;

  // Tipo de destino: 'Granja', 'Lote' o 'Planta'
  tipoDestino: 'Granja' | 'Lote' | 'Planta' = 'Granja';

  // Modal de confirmación
  showConfirmationModal = signal<boolean>(false);
  confirmationModalData = signal<ConfirmationModalData>({
    title: 'Confirmar',
    message: '¿Estás seguro?',
    type: 'info',
    confirmText: 'Confirmar',
    cancelText: 'Cancelar',
    showCancel: true
  });
  isConfirmingSave = signal<boolean>(false);

  // Tab activo
  activeTab: 'general' | 'cantidades' | 'despacho' = 'general';

  constructor(
    private fb: FormBuilder,
    private movimientosService: MovimientosAvesService,
    private farmService: FarmService,
    private masterListService: MasterListService,
    private countryFilterService: CountryFilterService
  ) {
    this.initForm();
  }

  ngOnInit(): void {
    this.cargarGranjas();
    this.cargarTiposMovimiento();
    this.cargarMotivosMovimiento();
    this.cargarPlantasDestino();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      if (this.loteId) {
        this.cargarInformacionLote(this.loteId);
      }
      if (this.editingMovimiento) {
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        // Cargar información del lote desde el movimiento
        if (this.editingMovimiento.origen?.loteId) {
          this.cargarInformacionLote(this.editingMovimiento.origen.loteId);
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
      }
    }
    
    if (changes['editingMovimiento']) {
      if (this.editingMovimiento) {
        if (this.isReadOnly()) {
          this.isEditMode.set(false);
        } else {
          this.isEditMode.set(true);
        }
        this.loadEditingData();
        if (this.editingMovimiento.origen?.loteId) {
          this.cargarInformacionLote(this.editingMovimiento.origen.loteId);
        }
      } else {
        this.isEditMode.set(false);
        this.resetForm();
      }
    }
  }

  private initForm(): void {
    const hoy = new Date().toISOString().split('T')[0];
    
    this.formMovimiento = this.fb.group({
      fechaMovimiento: [hoy, [Validators.required]],
      tipoMovimiento: [null, [Validators.required]],
      tipoDestino: ['Granja'],
      granjaDestinoId: [null],
      loteDestinoId: [null],
      plantaDestino: [null],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      cantidadMixtas: [0, [Validators.required, Validators.min(0)]],
      motivoMovimiento: [null],
      descripcion: [null],
      observaciones: [null],
      // Campos específicos para despacho (Ecuador)
      numeroDespacho: [{ value: null, disabled: true }], // Solo lectura, generado automáticamente
      edadAves: [null],
      raza: [{ value: null, disabled: true }], // Solo lectura, cargado desde lote
      anoTablaGenetica: [{ value: null, disabled: true }], // Solo lectura, cargado desde lote
      placa: [null],
      horaSalida: [null],
      guiaAgrocalidad: [null],
      sellos: [null],
      ayuno: [null],
      conductor: [null],
      totalPollosGalpon: [null],
      pesoBruto: [null],
      pesoTara: [null]
    }, { validators: this.validarMovimiento.bind(this) });

    // Cuando cambia el tipo de movimiento, actualizar validadores
    this.formMovimiento.get('tipoMovimiento')?.valueChanges.subscribe(tipo => {
      this.tipoMovimientoForm.set(tipo || '');
      this.actualizarValidadoresPorTipoMovimiento(tipo);
    });

    // Cuando cambia el tipo de destino, actualizar validadores
    this.formMovimiento.get('tipoDestino')?.valueChanges.subscribe(tipo => {
      this.tipoDestino = tipo;
      this.actualizarValidadoresDestino();
    });

    // Cuando cambia la granja destino, actualizar el filtro
    this.formMovimiento.get('granjaDestinoId')?.valueChanges.subscribe(granjaId => {
      this.onGranjaDestinoFormChange();
    });
  }

  private actualizarValidadoresPorTipoMovimiento(tipoMovimiento: string | null): void {
    const motivoMovimiento = this.formMovimiento.get('motivoMovimiento');
    const descripcion = this.formMovimiento.get('descripcion');
    const tipoDestino = this.formMovimiento.get('tipoDestino');
    const granjaDestinoId = this.formMovimiento.get('granjaDestinoId');
    const loteDestinoId = this.formMovimiento.get('loteDestinoId');
    const plantaDestino = this.formMovimiento.get('plantaDestino');

    if (this.esTipoVenta(tipoMovimiento)) {
      // Para ventas: motivo y descripción son requeridos, no hay destino
      motivoMovimiento?.setValidators([Validators.required]);
      descripcion?.setValidators([Validators.required]);
      tipoDestino?.clearValidators();
      granjaDestinoId?.clearValidators();
      loteDestinoId?.clearValidators();
      plantaDestino?.clearValidators();
    } else if (this.esTipoTraslado(tipoMovimiento)) {
      // Para traslados: tipo de destino es requerido, motivo no es requerido
      motivoMovimiento?.clearValidators();
      descripcion?.clearValidators();
      tipoDestino?.setValidators([Validators.required]);
      this.actualizarValidadoresDestino();
    } else {
      // Si no hay tipo seleccionado, limpiar todos
      motivoMovimiento?.clearValidators();
      descripcion?.clearValidators();
      tipoDestino?.clearValidators();
      granjaDestinoId?.clearValidators();
      loteDestinoId?.clearValidators();
      plantaDestino?.clearValidators();
    }

    motivoMovimiento?.updateValueAndValidity();
    descripcion?.updateValueAndValidity();
    tipoDestino?.updateValueAndValidity();
    granjaDestinoId?.updateValueAndValidity();
    loteDestinoId?.updateValueAndValidity();
    plantaDestino?.updateValueAndValidity();
  }

  private actualizarValidadoresDestino(): void {
    const tipoMovimiento = this.formMovimiento.get('tipoMovimiento')?.value;
    if (!this.esTipoTraslado(tipoMovimiento)) return;

    const granjaDestino = this.formMovimiento.get('granjaDestinoId');
    const loteDestino = this.formMovimiento.get('loteDestinoId');
    const plantaDestino = this.formMovimiento.get('plantaDestino');
    const tipoDestino = this.formMovimiento.get('tipoDestino')?.value;

    // Limpiar todos primero
    granjaDestino?.clearValidators();
    loteDestino?.clearValidators();
    plantaDestino?.clearValidators();

    if (tipoDestino === 'Granja') {
      granjaDestino?.setValidators([Validators.required]);
    } else if (tipoDestino === 'Lote') {
      loteDestino?.setValidators([Validators.required]);
    } else if (tipoDestino === 'Planta') {
      plantaDestino?.setValidators([Validators.required]);
    }

    granjaDestino?.updateValueAndValidity();
    loteDestino?.updateValueAndValidity();
    plantaDestino?.updateValueAndValidity();
  }

  private validarMovimiento(control: AbstractControl): ValidationErrors | null {
    const cantidadHembras = control.get('cantidadHembras')?.value || 0;
    const cantidadMachos = control.get('cantidadMachos')?.value || 0;
    const cantidadMixtas = control.get('cantidadMixtas')?.value || 0;
    const totalAves = cantidadHembras + cantidadMachos + cantidadMixtas;

    if (totalAves === 0) {
      return { sinCantidad: true };
    }

    // Validar que no se exceda la disponibilidad
    const infoLote = this.informacionLote();
    if (infoLote) {
      if (cantidadHembras > infoLote.cantidadHembras) {
        return { excedeHembras: true };
      }
      if (cantidadMachos > infoLote.cantidadMachos) {
        return { excedeMachos: true };
      }
      if (cantidadMixtas > infoLote.cantidadMixtas) {
        return { excedeMixtas: true };
      }

      // Validación especial para Producción: solo hembras O machos, no ambos
      if (infoLote.tipoLote === 'Produccion') {
        if (cantidadHembras > 0 && cantidadMachos > 0) {
          return { produccionNoMixto: true };
        }
        if (cantidadMixtas > 0) {
          return { produccionNoMixtas: true };
        }
      }
    }

    return null;
  }

  private cargarInformacionLote(loteId: number): void {
    this.loadingInfo.set(true);
    this.error.set(null);

    // Cargar información completa del lote desde el endpoint actualizado
    // Este endpoint calcula las aves actuales desde los registros diarios
    this.movimientosService.getInformacionLote(loteId).subscribe({
      next: (info) => {
        console.log('Información del lote cargada:', info);
        this.informacionLote.set(info);
        this.loadingInfo.set(false);
        
        // Cargar raza y año genético desde el lote
        if (info.raza) {
          const razaControl = this.formMovimiento.get('raza');
          if (razaControl) {
            razaControl.enable({ emitEvent: false });
            razaControl.setValue(info.raza, { emitEvent: false });
            razaControl.disable({ emitEvent: false });
          }
        }
        
        if (info.anoTablaGenetica) {
          const anoTablaGeneticaControl = this.formMovimiento.get('anoTablaGenetica');
          if (anoTablaGeneticaControl) {
            anoTablaGeneticaControl.enable({ emitEvent: false });
            anoTablaGeneticaControl.setValue(info.anoTablaGenetica, { emitEvent: false });
            anoTablaGeneticaControl.disable({ emitEvent: false });
          }
        }
        
        // Auto-completar edad si está disponible
        const edadCalculada = this.calcularEdadDesdeLote();
        if (edadCalculada !== null) {
          this.formMovimiento.patchValue({ edadAves: edadCalculada }, { emitEvent: false });
        }
        
        // Cargar último número de despacho si es Ecuador
        if (this.isEcuador()) {
          this.cargarUltimoNumeroDespacho();
        }
      },
      error: (error) => {
        console.error('Error cargando información del lote:', error);
        this.error.set('Error al cargar información del lote');
        this.informacionLote.set(null);
        this.loadingInfo.set(false);
      }
    });
  }

  private cargarUltimoNumeroDespacho(): void {
    this.movimientosService.getUltimoNumeroDespacho().subscribe({
      next: (response) => {
        console.log('Respuesta último número de despacho:', response);
        const siguienteNumero = response.siguienteNumero || response.ultimoId || 1;
        const numeroDespachoControl = this.formMovimiento.get('numeroDespacho');
        if (numeroDespachoControl) {
          numeroDespachoControl.enable({ emitEvent: false });
          numeroDespachoControl.setValue(siguienteNumero, { emitEvent: false });
          numeroDespachoControl.disable({ emitEvent: false });
        }
      },
      error: (error) => {
        console.warn('No se pudo cargar el último número de despacho:', error);
        // Si falla, usar 1 como valor por defecto
        const numeroDespachoControl = this.formMovimiento.get('numeroDespacho');
        if (numeroDespachoControl) {
          numeroDespachoControl.enable({ emitEvent: false });
          numeroDespachoControl.setValue(1, { emitEvent: false });
          numeroDespachoControl.disable({ emitEvent: false });
        }
      }
    });
  }

  private loadEditingData(): void {
    if (!this.editingMovimiento) return;

    const m = this.editingMovimiento;
    const fecha = m.fechaMovimiento instanceof Date 
      ? m.fechaMovimiento.toISOString().split('T')[0]
      : new Date(m.fechaMovimiento).toISOString().split('T')[0];

    // Determinar tipo de destino
    let tipoDestino: 'Granja' | 'Lote' | 'Planta' = 'Granja';
    if (m.destino?.loteId) {
      tipoDestino = 'Lote';
    } else if ((m as any).plantaDestino) {
      tipoDestino = 'Planta';
    }

    // Si estamos en modo solo lectura, deshabilitar el formulario
    if (this.isReadOnly()) {
      this.formMovimiento.disable();
    } else {
      this.formMovimiento.enable();
    }

    // Habilitar temporalmente los campos disabled para poder establecer los valores
    const razaControl = this.formMovimiento.get('raza');
    const anoTablaGeneticaControl = this.formMovimiento.get('anoTablaGenetica');
    const numeroDespachoControl = this.formMovimiento.get('numeroDespacho');
    
    if (razaControl) razaControl.enable({ emitEvent: false });
    if (anoTablaGeneticaControl) anoTablaGeneticaControl.enable({ emitEvent: false });
    if (numeroDespachoControl) numeroDespachoControl.enable({ emitEvent: false });
    
    this.formMovimiento.patchValue({
      fechaMovimiento: fecha,
      tipoMovimiento: m.tipoMovimiento,
      tipoDestino: tipoDestino,
      granjaDestinoId: m.destino?.granjaId || null,
      loteDestinoId: m.destino?.loteId || null,
      plantaDestino: (m as any).plantaDestino || null,
      cantidadHembras: m.cantidadHembras,
      cantidadMachos: m.cantidadMachos,
      cantidadMixtas: m.cantidadMixtas,
      motivoMovimiento: m.motivoMovimiento,
      descripcion: (m as any).descripcion || null,
      observaciones: m.observaciones,
      // Campos específicos para despacho (Ecuador)
      edadAves: m.edadAves || null,
      raza: m.raza || null,
      anoTablaGenetica: (m as any).anoTablaGenetica || null,
      placa: m.placa || null,
      horaSalida: m.horaSalida || null,
      guiaAgrocalidad: m.guiaAgrocalidad || null,
      sellos: m.sellos || null,
      ayuno: m.ayuno || null,
      conductor: m.conductor || null,
      totalPollosGalpon: m.totalPollosGalpon || null,
      pesoBruto: m.pesoBruto || null,
      pesoTara: m.pesoTara || null
    }, { emitEvent: false });
    
    // Volver a deshabilitar los campos readonly
    if (razaControl) razaControl.disable({ emitEvent: false });
    if (anoTablaGeneticaControl) anoTablaGeneticaControl.disable({ emitEvent: false });
    if (numeroDespachoControl) numeroDespachoControl.disable({ emitEvent: false });

    this.tipoDestino = tipoDestino;

    // Si hay granja destino, inicializar los filtros
    if (m.destino?.granjaId && tipoDestino === 'Granja') {
      this.selectedGranjaDestinoId = m.destino.granjaId;
    }
    if (m.destino?.loteId && tipoDestino === 'Lote') {
      this.selectedLoteDestinoId = m.destino.loteId;
    }
  }

  private resetForm(): void {
    const hoy = new Date().toISOString().split('T')[0];
    
    // Habilitar temporalmente los campos disabled para poder resetearlos
    const razaControl = this.formMovimiento.get('raza');
    const anoTablaGeneticaControl = this.formMovimiento.get('anoTablaGenetica');
    const numeroDespachoControl = this.formMovimiento.get('numeroDespacho');
    
    if (razaControl) razaControl.enable({ emitEvent: false });
    if (anoTablaGeneticaControl) anoTablaGeneticaControl.enable({ emitEvent: false });
    if (numeroDespachoControl) numeroDespachoControl.enable({ emitEvent: false });
    
    this.formMovimiento.reset({
      fechaMovimiento: hoy,
      tipoMovimiento: null,
      tipoDestino: 'Granja',
      granjaDestinoId: null,
      loteDestinoId: null,
      plantaDestino: null,
      cantidadHembras: 0,
      cantidadMachos: 0,
      cantidadMixtas: 0,
      motivoMovimiento: null,
      descripcion: null,
      observaciones: null,
      // Campos específicos para despacho (Ecuador)
      numeroDespacho: null,
      edadAves: null,
      raza: null,
      anoTablaGenetica: null,
      placa: null,
      horaSalida: null,
      guiaAgrocalidad: null,
      sellos: null,
      ayuno: null,
      conductor: null,
      totalPollosGalpon: null,
      pesoBruto: null,
      pesoTara: null
    });
    
    // Volver a deshabilitar los campos readonly
    if (razaControl) razaControl.disable({ emitEvent: false });
    if (anoTablaGeneticaControl) anoTablaGeneticaControl.disable({ emitEvent: false });
    if (numeroDespachoControl) numeroDespachoControl.disable({ emitEvent: false });
    
    this.error.set(null);
    this.success.set(null);
    this.informacionLote.set(null);
    this.selectedGranjaDestinoId = null;
    this.selectedNucleoDestinoId = null;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
  }

  // Métodos para manejar cambios en los filtros del lote destino
  onGranjaDestinoChange(granjaId: number | null): void {
    this.selectedGranjaDestinoId = granjaId;
    this.selectedNucleoDestinoId = null;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.formMovimiento.patchValue({ loteDestinoId: null });
  }

  onGranjaDestinoFormChange(): void {
    const granjaId = this.formMovimiento.get('granjaDestinoId')?.value;
    if (granjaId) {
      this.selectedGranjaDestinoId = granjaId;
      this.selectedNucleoDestinoId = null;
      this.selectedGalponDestinoId = null;
      this.selectedLoteDestinoId = null;
      this.formMovimiento.patchValue({ loteDestinoId: null });
    }
  }

  onNucleoDestinoChange(nucleoId: string | null): void {
    this.selectedNucleoDestinoId = nucleoId;
    this.selectedGalponDestinoId = null;
    this.selectedLoteDestinoId = null;
    this.formMovimiento.patchValue({ loteDestinoId: null });
  }

  onGalponDestinoChange(galponId: string | null): void {
    this.selectedGalponDestinoId = galponId;
    this.selectedLoteDestinoId = null;
    this.formMovimiento.patchValue({ loteDestinoId: null });
  }

  onLoteDestinoChange(loteId: number | null): void {
    this.selectedLoteDestinoId = loteId;
    this.formMovimiento.patchValue({ loteDestinoId: loteId });
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

  private cargarTiposMovimiento(): void {
    this.masterListService.getByKey('movimiento_de_aves_tipo_movimiento').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.tiposMovimiento.set(masterList.options);
          // Si no hay valor por defecto y hay opciones, establecer la primera como valor por defecto
          if (this.tiposMovimiento().length > 0 && !this.formMovimiento.get('tipoMovimiento')?.value) {
            this.formMovimiento.patchValue({ tipoMovimiento: this.tiposMovimiento()[0] });
          }
        } else {
          // Fallback a valores por defecto si no se encuentra la lista maestra
          this.tiposMovimiento.set(['Traslado']);
        }
      },
      error: (error) => {
        console.error('Error cargando tipos de movimiento desde lista maestra:', error);
        // Fallback a valores por defecto en caso de error
        this.tiposMovimiento.set(['Traslado']);
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

  private cargarMotivosMovimiento(): void {
    // Usar directamente la clave que sabemos que existe (traslado_de_huevos_venta_motivo)
    // Si en el futuro se crea una específica para movimientos de aves, se puede cambiar
    this.masterListService.getByKey('traslado_de_huevos_venta_motivo').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.motivosMovimiento.set(masterList.options);
        } else {
          this.motivosMovimiento.set([]);
        }
      },
      error: (error) => {
        console.warn('No se pudo cargar motivos de movimiento desde lista maestra. Continuando sin motivos.', error);
        this.motivosMovimiento.set([]);
      }
    });
  }

  onSubmitMovimiento(): void {
    console.log('=== onSubmitMovimiento llamado ===');
    console.log('Form válido:', this.formMovimiento.valid);
    console.log('Form errors:', this.formMovimiento.errors);
    console.log('Form value:', this.formMovimiento.value);
    
    if (this.formMovimiento.invalid) {
      console.error('❌ Formulario inválido');
      this.formMovimiento.markAllAsTouched();
      this.showErrorMessage('Por favor, complete todos los campos requeridos correctamente.');
      return;
    }

    // Mostrar modal de confirmación antes de guardar
    const isEdit = this.editingMovimiento && this.isEditMode();
    console.log('Mostrando modal de confirmación. isEdit:', isEdit);
    this.confirmationModalData.set({
      title: isEdit ? 'Confirmar Actualización' : 'Confirmar Creación',
      message: isEdit 
        ? `¿Estás seguro de que deseas actualizar el movimiento ${this.editingMovimiento?.numeroMovimiento}?`
        : '¿Estás seguro de que deseas crear este movimiento de aves?',
      type: 'info',
      confirmText: isEdit ? 'Actualizar' : 'Crear',
      cancelText: 'Cancelar',
      showCancel: true
    });
    this.isConfirmingSave.set(true);
    this.showConfirmationModal.set(true);
    console.log('Modal de confirmación abierto. showConfirmationModal:', this.showConfirmationModal());
  }

  onConfirmSave(): void {
    console.log('=== onConfirmSave llamado ===');
    console.log('isConfirmingSave:', this.isConfirmingSave());
    console.log('showConfirmationModal:', this.showConfirmationModal());
    console.trace('Stack trace de onConfirmSave');
    
    const wasConfirming = this.isConfirmingSave();
    
    // Cerrar el modal de confirmación primero
    this.showConfirmationModal.set(false);
    this.isConfirmingSave.set(false);
    
    console.log('Modal cerrado. wasConfirming:', wasConfirming);
    
    if (!wasConfirming) {
      console.log('ℹ️ No es confirmación de guardado, cerrando modal');
      this.onConfirmationModalClose();
      return;
    }
    
    // Ejecutar el guardado inmediatamente
    console.log('✅ Ejecutando guardado...');
    console.log('Llamando a executeSave()...');
    try {
      this.executeSave();
      console.log('executeSave() llamado exitosamente');
    } catch (error) {
      console.error('❌ Error al ejecutar executeSave():', error);
      this.loading.set(false);
      this.showErrorMessage('Error al procesar el guardado. Por favor, intenta nuevamente.');
    }
  }

  onCancelSave(): void {
    console.log('=== onCancelSave llamado ===');
    this.showConfirmationModal.set(false);
    this.isConfirmingSave.set(false);
  }

  onConfirmationModalClose(): void {
    this.showConfirmationModal.set(false);
    this.isConfirmingSave.set(false);
  }

  private executeSave(): void {
    console.log('=== executeSave llamado ===');
    console.log('loteId:', this.loteId);
    console.log('informacionLote:', this.informacionLote());
    
    // Validaciones
    if (!this.editingMovimiento && !this.loteId) {
      console.error('❌ Error: No se puede crear un movimiento sin loteId');
      this.showErrorMessage('Error: No se ha seleccionado un lote. Por favor, selecciona un lote antes de crear el movimiento.');
      return;
    }
    
    if (this.formMovimiento.invalid) {
      console.error('❌ Error: El formulario es inválido');
      this.formMovimiento.markAllAsTouched();
      this.showErrorMessage('Por favor, complete todos los campos requeridos correctamente.');
      return;
    }
    
    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    const formValue = this.formMovimiento.getRawValue(); // Usar getRawValue() para obtener valores de campos disabled
    console.log('Form value (getRawValue):', formValue);
    const fechaMovimiento = typeof formValue.fechaMovimiento === 'string'
      ? new Date(formValue.fechaMovimiento)
      : (formValue.fechaMovimiento instanceof Date ? formValue.fechaMovimiento : new Date());

    if (this.editingMovimiento && this.isEditMode()) {
      // Actualizar movimiento
      const dto: ActualizarMovimientoAvesDto = {
        fechaMovimiento: fechaMovimiento,
        tipoMovimiento: formValue.tipoMovimiento,
        granjaDestinoId: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Granja' ? formValue.granjaDestinoId : null,
        loteDestinoId: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Lote' ? formValue.loteDestinoId : null,
        plantaDestino: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Planta' ? formValue.plantaDestino : null,
        cantidadHembras: formValue.cantidadHembras,
        cantidadMachos: formValue.cantidadMachos,
        cantidadMixtas: formValue.cantidadMixtas,
        motivoMovimiento: formValue.motivoMovimiento,
        descripcion: this.esTipoVenta(formValue.tipoMovimiento) ? formValue.descripcion : null,
        observaciones: formValue.observaciones,
        // Campos específicos para despacho (Ecuador)
        edadAves: formValue.edadAves || undefined,
        raza: formValue.raza || undefined,
        placa: formValue.placa || undefined,
        horaSalida: formValue.horaSalida || undefined,
        guiaAgrocalidad: formValue.guiaAgrocalidad || undefined,
        sellos: formValue.sellos || undefined,
        ayuno: formValue.ayuno || undefined,
        conductor: formValue.conductor || undefined,
        totalPollosGalpon: formValue.totalPollosGalpon || undefined,
        pesoBruto: formValue.pesoBruto || undefined,
        pesoTara: formValue.pesoTara || undefined
      };

      this.movimientosService.actualizarMovimientoAves(this.editingMovimiento.id, dto).subscribe({
        next: (movimiento) => {
          console.log('✅ Movimiento actualizado exitosamente:', movimiento);
          this.loading.set(false);
          this.showSuccessMessage(`Movimiento ${movimiento.numeroMovimiento} actualizado exitosamente`);
          this.save.emit(movimiento);
        },
        error: (error) => {
          console.error('❌ Error actualizando movimiento:', error);
          console.error('Error status:', error.status);
          console.error('Error error:', error.error);
          this.loading.set(false);
          const errorMessage = error.error?.message || error.error?.error || error.message || 'Error desconocido';
          this.showErrorMessage(`No se pudo actualizar el movimiento. ${errorMessage}`);
        }
      });
    } else {
      // Crear movimiento
      const dto: CrearMovimientoAvesDto = {
        fechaMovimiento: fechaMovimiento,
        tipoMovimiento: formValue.tipoMovimiento,
        loteOrigenId: this.loteId || undefined,
        granjaOrigenId: this.informacionLote()?.granjaId || undefined,
        granjaDestinoId: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Granja' ? formValue.granjaDestinoId : undefined,
        loteDestinoId: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Lote' ? formValue.loteDestinoId : undefined,
        cantidadHembras: formValue.cantidadHembras,
        cantidadMachos: formValue.cantidadMachos,
        cantidadMixtas: formValue.cantidadMixtas,
        motivoMovimiento: formValue.motivoMovimiento,
        descripcion: this.esTipoVenta(formValue.tipoMovimiento) ? formValue.descripcion : undefined,
        observaciones: formValue.observaciones,
        // Campos específicos para despacho (Ecuador)
        edadAves: formValue.edadAves || undefined,
        raza: formValue.raza || undefined,
        placa: formValue.placa || undefined,
        horaSalida: formValue.horaSalida || undefined,
        guiaAgrocalidad: formValue.guiaAgrocalidad || undefined,
        sellos: formValue.sellos || undefined,
        ayuno: formValue.ayuno || undefined,
        conductor: formValue.conductor || undefined,
        totalPollosGalpon: formValue.totalPollosGalpon || undefined,
        pesoBruto: formValue.pesoBruto || undefined,
        pesoTara: formValue.pesoTara || undefined,
        plantaDestino: this.esTipoTraslado(formValue.tipoMovimiento) && formValue.tipoDestino === 'Planta' ? formValue.plantaDestino : undefined
      };

      // Validar DTO antes de enviar
      if (!dto.tipoMovimiento) {
        console.error('❌ Error: tipoMovimiento es requerido');
        this.loading.set(false);
        this.showErrorMessage('Error: El tipo de movimiento es requerido.');
        return;
      }
      
      if (!dto.loteOrigenId) {
        console.error('❌ Error: loteOrigenId es requerido');
        this.loading.set(false);
        this.showErrorMessage('Error: El lote de origen es requerido.');
        return;
      }
      
      if (dto.cantidadHembras === 0 && dto.cantidadMachos === 0 && dto.cantidadMixtas === 0) {
        console.error('❌ Error: Debe especificar al menos una cantidad de aves');
        this.loading.set(false);
        this.showErrorMessage('Error: Debe especificar al menos una cantidad de aves (hembras, machos o mixtas).');
        return;
      }
      
      console.log('✅ Validaciones del DTO pasadas, enviando al servicio...');
      console.log('DTO completo a enviar:', JSON.stringify(dto, null, 2));
      console.log('URL del servicio:', 'POST /api/MovimientoAves');
      
      this.movimientosService.crearMovimientoAves(dto).subscribe({
        next: (movimiento) => {
          console.log('✅ Movimiento creado exitosamente:', movimiento);
          this.loading.set(false);
          const tipoMovimiento = formValue.tipoMovimiento?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.showSuccessMessage(`${tipoMovimiento} creado exitosamente. Número: ${movimiento.numeroMovimiento}`);
          this.save.emit(movimiento);
        },
        error: (error) => {
          console.error('❌ Error creando movimiento:', error);
          console.error('Error status:', error.status);
          console.error('Error error:', error.error);
          this.loading.set(false);
          const tipoMovimiento = formValue.tipoMovimiento?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          const errorMessage = error.error?.message || error.error?.error || error.message || 'Error desconocido';
          this.showErrorMessage(`No se pudo crear el ${tipoMovimiento}. ${errorMessage}`);
        }
      });
    }
  }

  closeModal(): void {
    this.close.emit();
  }

  onOverlayClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeModal();
    }
  }

  closeSuccessModal(): void {
    this.showSuccessModal.set(false);
    this.closeModal();
  }

  closeErrorModal(): void {
    this.showErrorModal.set(false);
  }

  calcularEdadDiasDesdeEncasetamiento(): number {
    const info = this.informacionLote();
    if (!info || !info.diasDesdeEncasetamiento) {
      // Fallback: calcular desde fechaEncasetamiento si está disponible
      if (info?.fechaEncasetamiento) {
        const fechaEncaset = new Date(info.fechaEncasetamiento);
        if (!isNaN(fechaEncaset.getTime())) {
          const hoy = new Date();
          const diffTime = hoy.getTime() - fechaEncaset.getTime();
          return Math.floor(diffTime / (1000 * 60 * 60 * 24));
        }
      }
      return 0;
    }
    return info.diasDesdeEncasetamiento;
  }

  calcularEtapa(): number {
    const info = this.informacionLote();
    if (info?.etapa) {
      return info.etapa;
    }
    // Fallback: calcular desde días
    const dias = this.calcularEdadDiasDesdeEncasetamiento();
    return Math.floor(dias / 7) + 1; // Semanas (1-indexed)
  }

  getTipoLote(): string {
    const info = this.informacionLote();
    if (info?.tipoLote) {
      return info.tipoLote;
    }
    // Fallback: calcular desde etapa
    const etapa = this.calcularEtapa();
    return etapa >= 26 ? 'Produccion' : 'Levante';
  }

  // Métodos helper para detectar tipo de movimiento
  esTipoVenta(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('venta') || tipo.toLowerCase().includes('sale');
  }

  esTipoTraslado(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase().includes('traslado') || tipo.toLowerCase().includes('transfer');
  }

  esTipoGranja(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase() === 'granja' || tipo.toLowerCase() === 'farm';
  }

  esTipoPlanta(tipo: string | null | undefined): boolean {
    if (!tipo) return false;
    return tipo.toLowerCase() === 'planta' || tipo.toLowerCase() === 'plant';
  }

  // Método para manejar cambios en cantidades en Producción (solo hembras O machos)
  onCantidadProduccionChange(tipo: 'hembras' | 'machos'): void {
    if (this.informacionLote()?.tipoLote !== 'Produccion') return;

    if (tipo === 'hembras') {
      const cantidadHembras = this.formMovimiento.get('cantidadHembras')?.value || 0;
      if (cantidadHembras > 0) {
        this.formMovimiento.patchValue({ cantidadMachos: 0, cantidadMixtas: 0 }, { emitEvent: false });
      }
    } else if (tipo === 'machos') {
      const cantidadMachos = this.formMovimiento.get('cantidadMachos')?.value || 0;
      if (cantidadMachos > 0) {
        this.formMovimiento.patchValue({ cantidadHembras: 0, cantidadMixtas: 0 }, { emitEvent: false });
      }
    }
  }

  // Métodos para el modal de confirmación (éxito/error)
  showSuccessMessage(message: string): void {
    this.confirmationModalData.set({
      title: 'Éxito',
      message: message,
      type: 'success',
      confirmText: 'Aceptar',
      showCancel: false
    });
    this.isConfirmingSave.set(false);
    this.showConfirmationModal.set(true);
  }

  showErrorMessage(message: string): void {
    this.confirmationModalData.set({
      title: 'Error',
      message: message,
      type: 'error',
      confirmText: 'Aceptar',
      showCancel: false
    });
    this.isConfirmingSave.set(false);
    this.showConfirmationModal.set(true);
  }

  // Métodos helper
  isEcuador(): boolean {
    return this.countryFilterService.isEcuador();
  }

  onTabChange(tab: 'general' | 'cantidades' | 'despacho'): void {
    this.activeTab = tab;
  }

  // Métodos para cálculos y formateo
  autoCompletarEdad(): void {
    const edadCalculada = this.calcularEdadDesdeLote();
    if (edadCalculada !== null) {
      this.formMovimiento.patchValue({ edadAves: edadCalculada });
    }
  }

  calcularEdadDesdeLote(): number | null {
    const info = this.informacionLote();
    if (!info) return null;

    let fechaReferencia: Date | null = null;

    if (info.tipoLote === 'Produccion' && info.fechaInicioProduccion) {
      fechaReferencia = new Date(info.fechaInicioProduccion);
    } else if (info.fechaEncasetamiento) {
      fechaReferencia = new Date(info.fechaEncasetamiento);
    }

    if (!fechaReferencia) return null;

    const hoy = new Date();
    const diffTime = hoy.getTime() - fechaReferencia.getTime();
    const diffDays = Math.floor(diffTime / (1000 * 60 * 60 * 24));
    return diffDays >= 0 ? diffDays : null;
  }

  calcularPesoNeto(): number | null {
    const pesoBruto = this.formMovimiento.get('pesoBruto')?.value;
    const pesoTara = this.formMovimiento.get('pesoTara')?.value;
    
    if (pesoBruto != null && pesoTara != null) {
      return pesoBruto - pesoTara;
    }
    return null;
  }

  calcularPromedioPesoAve(): number | null {
    const pesoNeto = this.calcularPesoNeto();
    const cantidadHembras = this.formMovimiento.get('cantidadHembras')?.value || 0;
    const cantidadMachos = this.formMovimiento.get('cantidadMachos')?.value || 0;
    const cantidadMixtas = this.formMovimiento.get('cantidadMixtas')?.value || 0;
    const totalAves = cantidadHembras + cantidadMachos + cantidadMixtas;

    if (pesoNeto != null && totalAves > 0) {
      return pesoNeto / totalAves;
    }
    return null;
  }

  formatearNumero(num: number | null | undefined): string {
    if (num == null || isNaN(num)) return '0.00';
    return num.toFixed(2);
  }
}
