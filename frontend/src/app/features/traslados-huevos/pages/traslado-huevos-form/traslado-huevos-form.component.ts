// frontend/src/app/features/traslados-huevos/pages/traslado-huevos-form/traslado-huevos-form.component.ts
import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { TrasladosHuevosService, DisponibilidadLoteDto, CrearTrasladoHuevosDto, HuevosDisponiblesDto } from '../../services/traslados-huevos.service';
import { FarmService } from '../../../farm/services/farm.service';
import { environment } from '../../../../../environments/environment';

@Component({
  selector: 'app-traslado-huevos-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent, FiltroSelectComponent],
  templateUrl: './traslado-huevos-form.component.html',
  styleUrls: ['./traslado-huevos-form.component.scss']
})
export class TrasladoHuevosFormComponent implements OnInit {
  formHuevos!: FormGroup;

  disponibilidad = signal<DisponibilidadLoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingDisponibilidad = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  granjas = signal<any[]>([]);
  selectedLotePosturaProduccionId: number | null = null;

  /** URL para filter-data: Granja → Núcleo → Galpón → Lote LPP */
  readonly filterDataUrl = `${environment.apiUrl}/traslados/filter-data`;

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
    private farmService: FarmService,
    private router: Router
  ) {
    this.initForm();
  }

  ngOnInit(): void {
    this.cargarGranjas();
  }

  onLoteChange(loteId: number | null): void {
    this.selectedLotePosturaProduccionId = loteId;
    this.formHuevos.get('lotePosturaProduccionId')?.setValue(loteId ?? null);
    if (loteId != null) {
      this.cargarDisponibilidadLPP(loteId);
    } else {
      this.disponibilidad.set(null);
    }
  }

  private initForm(): void {
    // Formulario de traslado de huevos
    const hoyHuevos = new Date().toISOString().split('T')[0]; // Formato YYYY-MM-DD para input date
    const huevosControls: any = {
      lotePosturaProduccionId: [null, [Validators.required]],
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

    this.formHuevos.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresDestino(tipo);
    });
  }

  private actualizarValidadoresDestino(tipo: string): void {
    const granjaDestino = this.formHuevos.get('granjaDestinoId');
    const loteDestino = this.formHuevos.get('loteDestinoId');
    const tipoDestino = this.formHuevos.get('tipoDestino');
    const motivo = this.formHuevos.get('motivo');
    const descripcion = this.formHuevos.get('descripcion');

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

  private cargarDisponibilidadLPP(lotePosturaProduccionId: number): void {
    this.loadingDisponibilidad.set(true);
    this.error.set(null);

    this.trasladosService.getDisponibilidadLoteLPP(lotePosturaProduccionId).subscribe({
      next: (disp) => {
        this.disponibilidad.set(disp);
        this.loadingDisponibilidad.set(false);
      },
      error: (err) => {
        console.error('Error cargando disponibilidad:', err);
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

    const lppId = formValue.lotePosturaProduccionId ? Number(formValue.lotePosturaProduccionId) : undefined;
    const dto: CrearTrasladoHuevosDto = {
      lotePosturaProduccionId: lppId,
      loteId: lppId ? '' : String(formValue.loteId ?? ''),
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
        this.formHuevos.patchValue({
          lotePosturaProduccionId: null,
          fechaTraslado: hoy,
          tipoOperacion: 'Traslado'
        });
        this.tiposHuevo.forEach(tipo => {
          this.formHuevos.get(`cantidad${tipo.key.charAt(0).toUpperCase() + tipo.key.slice(1)}`)?.setValue(0);
        });
        this.selectedLotePosturaProduccionId = null;
        this.disponibilidad.set(null);
        this.loading.set(false);
      },
      error: (error: any) => {
        console.error('Error creando traslado de huevos:', error);
        const msg = error?.error?.message ?? error?.message ?? 'Error al crear traslado de huevos';
        this.error.set(msg);
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
    // Navegar al dashboard de traslados de huevos (o al dashboard principal)
    this.router.navigate(['/traslados-huevos']);
  }
}
