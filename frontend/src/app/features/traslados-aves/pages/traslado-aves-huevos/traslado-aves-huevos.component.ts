// frontend/src/app/features/traslados-aves/pages/traslado-aves-huevos/traslado-aves-huevos.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { TrasladosAvesService, DisponibilidadLoteDto, CrearTrasladoAvesDto } from '../../services/traslados-aves.service';
import { LoteService, LoteDto } from '../../../lote/services/lote.service';
import { FarmService } from '../../../farm/services/farm.service';

@Component({
  selector: 'app-traslado-aves',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent],
  templateUrl: './traslado-aves-huevos.component.html',
  styleUrls: ['./traslado-aves-huevos.component.scss']
})
export class TrasladoAvesComponent implements OnInit {
  // Formulario
  formAves!: FormGroup;

  // Estado
  lotes = signal<LoteDto[]>([]);
  disponibilidad = signal<DisponibilidadLoteDto | null>(null);
  loading = signal<boolean>(false);
  loadingDisponibilidad = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);

  // Granjas para destino
  granjas = signal<any[]>([]);

  // Computed
  loteSeleccionado = computed(() => {
    const loteId = this.formAves?.get('loteId')?.value;
    return this.lotes().find(l => String(l.loteId) === String(loteId));
  });

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosAvesService,
    private loteService: LoteService,
    private farmService: FarmService,
    private router: Router
  ) {
    this.initForm();
  }

  ngOnInit(): void {
    this.cargarLotes();
    this.cargarGranjas();
  }

  private initForm(): void {
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

    // Suscribirse a cambios en loteId para cargar disponibilidad
    this.formAves.get('loteId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        this.cargarDisponibilidad(loteId);
      } else {
        this.disponibilidad.set(null);
      }
    });

    // Suscribirse a cambios en tipoOperacion para mostrar/ocultar campos de destino
    this.formAves.get('tipoOperacion')?.valueChanges.subscribe(tipo => {
      this.actualizarValidadoresDestino(tipo);
    });
  }

  private actualizarValidadoresDestino(tipo: string): void {
    const granjaDestino = this.formAves.get('granjaDestinoId');
    const loteDestino = this.formAves.get('loteDestinoId');
    const tipoDestino = this.formAves.get('tipoDestino');
    const motivo = this.formAves.get('motivo');
    const descripcion = this.formAves.get('descripcion');

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
    const cantidadHembras = control.get('cantidadHembras')?.value || 0;
    const cantidadMachos = control.get('cantidadMachos')?.value || 0;

    if (cantidadHembras === 0 && cantidadMachos === 0) {
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
        if (disponibilidad.tipoLote !== 'Levante') {
          this.error.set('Este lote es de producción, selecciona traslado de huevos');
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


  volverAlDashboard(): void {
    // Usar ruta absoluta para evitar problemas de navegación
    this.router.navigate(['/traslados-aves/dashboard']);
  }
}

