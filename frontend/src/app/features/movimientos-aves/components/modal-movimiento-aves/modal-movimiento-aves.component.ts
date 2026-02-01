// frontend/src/app/features/movimientos-aves/components/modal-movimiento-aves/modal-movimiento-aves.component.ts
import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { MovimientosAvesService, CrearMovimientoAvesDto, ActualizarMovimientoAvesDto, MovimientoAvesDto, InformacionLoteDto } from '../../services/movimientos-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService, FarmDto } from '../../../farm/services/farm.service';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { MasterListService } from '../../../../core/services/master-list/master-list.service';

@Component({
  selector: 'app-modal-movimiento-aves',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FiltroSelectComponent],
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

  constructor(
    private fb: FormBuilder,
    private movimientosService: MovimientosAvesService,
    private farmService: FarmService,
    private masterListService: MasterListService
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
      observaciones: [null]
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
        this.informacionLote.set(info);
        this.loadingInfo.set(false);
      },
      error: (error) => {
        console.error('Error cargando información del lote:', error);
        this.error.set('Error al cargar información del lote');
        this.informacionLote.set(null);
        this.loadingInfo.set(false);
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
      observaciones: m.observaciones
    }, { emitEvent: false });

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
      observaciones: null
    });
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
    // Usar la misma clave que traslados de huevos o crear una nueva específica para movimientos de aves
    this.masterListService.getByKey('movimiento_aves_motivo').subscribe({
      next: (masterList) => {
        if (masterList && masterList.options) {
          this.motivosMovimiento.set(masterList.options);
        } else {
          // Fallback: intentar con la clave de traslados de huevos si no existe la específica
          this.masterListService.getByKey('traslado_de_huevos_venta_motivo').subscribe({
            next: (fallbackList) => {
              if (fallbackList && fallbackList.options) {
                this.motivosMovimiento.set(fallbackList.options);
              } else {
                this.motivosMovimiento.set([]);
              }
            },
            error: () => {
              this.motivosMovimiento.set([]);
            }
          });
        }
      },
      error: (error) => {
        console.error('Error cargando motivos de movimiento desde lista maestra:', error);
        // Fallback: intentar con la clave de traslados de huevos
        this.masterListService.getByKey('traslado_de_huevos_venta_motivo').subscribe({
          next: (fallbackList) => {
            if (fallbackList && fallbackList.options) {
              this.motivosMovimiento.set(fallbackList.options);
            } else {
              this.motivosMovimiento.set([]);
            }
          },
          error: () => {
            this.motivosMovimiento.set([]);
          }
        });
      }
    });
  }

  onSubmitMovimiento(): void {
    if (this.formMovimiento.invalid) {
      this.formMovimiento.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    const formValue = this.formMovimiento.value;
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
        observaciones: formValue.observaciones
      };

      this.movimientosService.actualizarMovimientoAves(this.editingMovimiento.id, dto).subscribe({
        next: (movimiento) => {
          this.loading.set(false);
          this.successMessage.set('Movimiento actualizado exitosamente');
          this.showSuccessModal.set(true);
          this.success.set('Movimiento actualizado exitosamente');
          this.save.emit(movimiento);
        },
        error: (error) => {
          console.error('Error actualizando movimiento:', error);
          this.loading.set(false);
          this.errorMessage.set(`No se pudo actualizar el movimiento. ${error.message || 'Error desconocido'}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
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
        observaciones: formValue.observaciones
      };

      this.movimientosService.crearMovimientoAves(dto).subscribe({
        next: (movimiento) => {
          this.loading.set(false);
          const tipoMovimiento = formValue.tipoMovimiento?.toLowerCase().includes('venta') ? 'Venta' : 'Traslado';
          this.successMessage.set(`${tipoMovimiento} creado exitosamente. Número: ${movimiento.numeroMovimiento}`);
          this.showSuccessModal.set(true);
          this.success.set(`${tipoMovimiento} creado exitosamente`);
          this.save.emit(movimiento);
        },
        error: (error) => {
          console.error('Error creando movimiento:', error);
          this.loading.set(false);
          const tipoMovimiento = formValue.tipoMovimiento?.toLowerCase().includes('venta') ? 'venta' : 'traslado';
          this.errorMessage.set(`No se pudo crear el ${tipoMovimiento}. ${error.message || 'Error desconocido'}`);
          this.showErrorModal.set(true);
          this.error.set(null);
          this.success.set(null);
          // NO cerrar el modal en caso de error
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
}
