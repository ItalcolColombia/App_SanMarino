// frontend/src/app/features/traslados-aves/pages/traslado-aves-huevos/traslado-aves-huevos.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { TrasladosAvesService, DisponibilidadLoteDto, CrearTrasladoAvesDto, CrearTrasladoHuevosDto, HuevosDisponiblesDto } from '../../services/traslados-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService } from '../../../farm/services/farm.service';

@Component({
  selector: 'app-traslado-aves-huevos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './traslado-aves-huevos.component.html',
  styleUrls: ['./traslado-aves-huevos.component.scss']
})
export class TrasladoAvesHuevosComponent implements OnInit {
  // Formularios
  formAves!: FormGroup;
  formHuevos!: FormGroup;

  // Estado
  tipoTraslado = signal<'aves' | 'huevos'>('aves');
  lotes = signal<LoteDto[]>([]);
  disponibilidad = signal<DisponibilidadLoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingDisponibilidad = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  // Granjas para destino
  granjas = signal<any[]>([]);

  // Computed
  isAves = computed(() => this.tipoTraslado() === 'aves');
  isHuevos = computed(() => this.tipoTraslado() === 'huevos');
  loteSeleccionado = computed(() => {
    const loteId = this.isAves()
      ? this.formAves?.get('loteId')?.value
      : this.formHuevos?.get('loteId')?.value;
    return this.lotes().find(l => String(l.loteId) === String(loteId));
  });

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
    private trasladosService: TrasladosAvesService,
    private loteService: LoteService,
    private farmService: FarmService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.initForms();
  }

  ngOnInit(): void {
    this.cargarLotes();
    this.cargarGranjas();
  }

  private initForms(): void {
    // Formulario de traslado de aves
    const hoyAves = new Date().toISOString().split('T')[0]; // Formato YYYY-MM-DD para input date
    this.formAves = this.fb.group({
      loteId: ['', [Validators.required]],
      fechaTraslado: [hoyAves, [Validators.required]],
      tipoOperacion: ['Traslado', [Validators.required]],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      granjaDestinoId: [null],
      loteDestinoId: [null],
      tipoDestino: [null],
      motivo: [null],
      descripcion: [null],
      observaciones: [null]
    }, { validators: this.validarTrasladoAves.bind(this) });

    // Formulario de traslado de huevos
    const hoyHuevos = new Date().toISOString().split('T')[0]; // Formato YYYY-MM-DD para input date
    const huevosControls: any = {
      loteId: ['', [Validators.required]],
      fechaTraslado: [hoyHuevos, [Validators.required]],
      tipoOperacion: ['Traslado', [Validators.required]],
      granjaDestinoId: [null],
      loteDestinoId: [null],
      tipoDestino: [null],
      motivo: [null],
      descripcion: [null],
      observaciones: [null]
    };

    // Agregar controles para cada tipo de huevo
    this.tiposHuevo.forEach(tipo => {
      huevosControls[`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`] = [0, [Validators.min(0)]];
    });

    this.formHuevos = this.fb.group(huevosControls, { validators: this.validarTrasladoHuevos.bind(this) });

    // Suscribirse a cambios en loteId para cargar disponibilidad
    this.formAves.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        this.cargarDisponibilidad(loteId);
      } else {
        this.disponibilidad.set(null);
      }
    });

    this.formHuevos.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        this.cargarDisponibilidad(loteId);
      } else {
        this.disponibilidad.set(null);
      }
    });

    // Suscribirse a cambios en tipoOperacion para mostrar/ocultar campos de destino
    this.formAves.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresDestino(this.formAves, tipo);
    });

    this.formHuevos.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresDestino(this.formHuevos, tipo);
    });
  }

  private actualizarValidadoresDestino(form: FormGroup, tipo: string): void {
    const granjaDestino = form.get('granjaDestinoId');
    const loteDestino = form.get('loteDestinoId');
    const tipoDestino = form.get('tipoDestino');
    const motivo = form.get('motivo');
    const descripcion = form.get('descripcion');

    if (tipo === 'Venta') {
      granjaDestino?.clearValidators();
      loteDestino?.clearValidators();
      tipoDestino?.clearValidators();
      motivo?.setValidators([Validators.required]);
      descripcion?.setValidators([Validators.required]);
    } else {
      granjaDestino?.setValidators([Validators.required]);
      tipoDestino?.setValidators([Validators.required]);
      motivo?.clearValidators();
      descripcion?.clearValidators();
    }

    granjaDestino?.updateValueAndValidity();
    loteDestino?.updateValueAndValidity();
    tipoDestino?.updateValueAndValidity();
    motivo?.updateValueAndValidity();
    descripcion?.updateValueAndValidity();
  }

  private validarTrasladoAves(control: AbstractControl): ValidationErrors | null {
    const tipoOperacion = control.get('tipoOperacion')?.value;
    const cantidadHembras = control.get('cantidadHembras')?.value || 0;
    const cantidadMachos = control.get('cantidadMachos')?.value || 0;

    if (cantidadHembras === 0 && cantidadMachos === 0) {
      return { sinCantidad: true };
    }

    return null;
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

  private cargarLotes(): void {
    this.loading.set(true);
    this.loteService.getAll().subscribe({
      next: (lotes) => {
        this.lotes.set(lotes);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error cargando lotes:', error);
        this.error.set('Error al cargar lotes');
        this.loading.set(false);
      }
    });
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

        // Validar que el tipo de lote coincida con el tipo de traslado
        if (this.isAves() && disponibilidad.tipoLote !== 'Levante') {
          this.error.set('Este lote es de producción, selecciona traslado de huevos');
        } else if (this.isHuevos() && disponibilidad.tipoLote !== 'Produccion') {
          this.error.set('Este lote es de levante, selecciona traslado de aves');
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

  cambiarTipoTraslado(tipo: 'aves' | 'huevos'): void {
    this.tipoTraslado.set(tipo);
    this.disponibilidad.set(null);
    this.error.set(null);
    this.success.set(null);

    // Limpiar selección de lote
    if (tipo === 'aves') {
      this.formHuevos.get('loteId')?.setValue(null);
    } else {
      this.formAves.get('loteId')?.setValue(null);
    }
  }

  onSubmitAves(): void {
    if (this.formAves.invalid) {
      this.formAves.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    const formValue = this.formAves.value;
    const fechaTraslado = typeof formValue.fechaTraslado === 'string'
      ? new Date(formValue.fechaTraslado)
      : (formValue.fechaTraslado instanceof Date ? formValue.fechaTraslado : new Date());

    const dto: CrearTrasladoAvesDto = {
      loteId: String(formValue.loteId),
      fechaTraslado: fechaTraslado,
      tipoOperacion: formValue.tipoOperacion,
      cantidadHembras: formValue.cantidadHembras,
      cantidadMachos: formValue.cantidadMachos,
      granjaDestinoId: formValue.granjaDestinoId ? Number(formValue.granjaDestinoId) : undefined,
      loteDestinoId: formValue.loteDestinoId ? String(formValue.loteDestinoId) : undefined,
      tipoDestino: formValue.tipoDestino,
      motivo: formValue.motivo,
      descripcion: formValue.descripcion,
      observaciones: formValue.observaciones
    };

    this.trasladosService.crearTrasladoAves(dto).subscribe({
      next: (result) => {
        this.success.set(`Traslado de aves creado exitosamente. ID: ${result.id}`);
        const hoy = new Date().toISOString().split('T')[0];
        this.formAves.reset({
          fechaTraslado: hoy,
          tipoOperacion: 'Traslado',
          cantidadHembras: 0,
          cantidadMachos: 0
        });
        this.disponibilidad.set(null);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error creando traslado de aves:', error);
        this.error.set(error.message || 'Error al crear traslado de aves');
        this.loading.set(false);
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
      loteId: String(formValue.loteId),
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

    this.trasladosService.crearTrasladoHuevos(dto).subscribe({
      next: (result) => {
        this.success.set(`Traslado de huevos creado exitosamente. Número: ${result.numeroTraslado}`);
        const hoy = new Date().toISOString().split('T')[0];
        this.formHuevos.reset({
          fechaTraslado: hoy,
          tipoOperacion: 'Traslado'
        });
        this.tiposHuevo.forEach(tipo => {
          this.formHuevos.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.setValue(0);
        });
        this.disponibilidad.set(null);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error creando traslado de huevos:', error);
        this.error.set(error.message || 'Error al crear traslado de huevos');
        this.loading.set(false);
      }
    });
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

  volverAlDashboard(): void {
    // Usar ruta absoluta para evitar problemas de navegación
    this.router.navigate(['/traslados-aves/dashboard']);
  }
}

