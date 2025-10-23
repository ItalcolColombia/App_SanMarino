//app/features/traslados-aves/pages/traslado-form/traslado-form.component.ts
import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { SidebarComponent } from '../../../../shared/components/sidebar/sidebar.component';
import { HierarchicalFilterComponent, HierarchicalFilterCriteria, HierarchicalFilterState } from '../../../../shared/components/hierarchical-filter/hierarchical-filter.component';
import { LoteDto } from '../../../lote/services/lote.service';
import { 
  TrasladosAvesService, 
  TrasladoRapidoRequest, 
  TrasladoRapidoResponse,
  InventarioAvesDto
} from '../../services/traslados-aves.service';

@Component({
  selector: 'app-traslado-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, SidebarComponent, HierarchicalFilterComponent],
  templateUrl: './traslado-form.component.html',
  styleUrls: ['./traslado-form.component.scss']
})
export class TrasladoFormComponent implements OnInit {
  // Signals para manejo de estado reactivo
  form!: FormGroup;
  lotesDisponibles = signal<string[]>([]);
  inventarioOrigen = signal<InventarioAvesDto | null>(null);
  inventarioDestino = signal<InventarioAvesDto | null>(null);
  loading = signal<boolean>(false);
  error = signal<string | null>(null);
  success = signal<TrasladoRapidoResponse | null>(null);
  validandoLotes = signal<boolean>(false);
  
  // Filtros de lotes
  loteOrigenSeleccionado = signal<LoteDto | null>(null);
  loteDestinoSeleccionado = signal<LoteDto | null>(null);

  // Computed properties
  isFormValid = computed(() => this.form?.valid || false);
  hasError = computed(() => !!this.error());
  hasSuccess = computed(() => !!this.success());
  isProcessing = computed(() => this.loading());
  
  totalATrasladas = computed(() => {
    if (!this.form) return 0;
    const hembras = this.form.get('cantidadHembras')?.value || 0;
    const machos = this.form.get('cantidadMachos')?.value || 0;
    return hembras + machos;
  });

  constructor(
    private fb: FormBuilder,
    private trasladosService: TrasladosAvesService,
    private router: Router,
    private route: ActivatedRoute
  ) {
    this.initForm();
  }

  ngOnInit(): void {
    this.cargarLotesDisponibles();
  }

  private initForm(): void {
    this.form = this.fb.group({
      loteOrigenId: ['', [Validators.required]],
      loteDestinoId: ['', [Validators.required]],
      cantidadHembras: [0, [Validators.required, Validators.min(0)]],
      cantidadMachos: [0, [Validators.required, Validators.min(0)]],
      observaciones: ['']
    });

    // Suscribirse a cambios en los lotes para cargar inventarios
    this.form.get('loteOrigenId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        this.cargarInventarioOrigen(Number(loteId));  // Convert string to number
      } else {
        this.inventarioOrigen.set(null);
      }
      this.limpiarMensajes();
    });

    this.form.get('loteDestinoId')?.valueChanges.subscribe(loteId => {
      if (loteId) {
        this.cargarInventarioDestino(Number(loteId));  // Convert string to number
      } else {
        this.inventarioDestino.set(null);
      }
      this.limpiarMensajes();
    });

    // Validar cantidades cuando cambien
    this.form.get('cantidadHembras')?.valueChanges.subscribe(() => {
      this.validarCantidades();
    });

    this.form.get('cantidadMachos')?.valueChanges.subscribe(() => {
      this.validarCantidades();
    });
  }

  async cargarLotesDisponibles(): Promise<void> {
    try {
      this.error.set(null);
      const lotes = await this.trasladosService.getLotesDisponibles().toPromise();
      this.lotesDisponibles.set(lotes || []);
    } catch (error: any) {
      console.error('Error al cargar lotes:', error);
      this.error.set(error.message || 'Error al cargar los lotes disponibles');
    }
  }

  async cargarInventarioOrigen(loteId: number): Promise<void> {
    try {
      this.validandoLotes.set(true);
      const inventario = await this.trasladosService.getInventarioByLote(String(loteId)).toPromise();
      this.inventarioOrigen.set(inventario || null);
      this.validarCantidades();
    } catch (error: any) {
      console.error('Error cargando inventario ORIGEN:', error);
      this.inventarioOrigen.set(null);
      this.error.set('Error al cargar el inventario del lote origen');
    } finally {
      this.validandoLotes.set(false);
    }
  }

  async cargarInventarioDestino(loteId: number): Promise<void> {  // Changed from string to number
    try {
      this.validandoLotes.set(true);
      const inventario = await this.trasladosService.getInventarioByLote(loteId.toString()).toPromise();  // Convert to string for API call
      this.inventarioDestino.set(inventario || null);
    } catch (error: any) {
      console.error('Error al cargar inventario destino:', error);
      this.inventarioDestino.set(null);
      this.error.set('Error al cargar el inventario del lote destino');
    } finally {
      this.validandoLotes.set(false);
    }
  }

  /** Valida que si se traslada un género (>0), sea exactamente igual al disponible. 0 también es válido. */
  private validarIgualDisponible(valor: number, disponible: number): string | null {
    if (valor === 0) return null;              // mover 0 es válido
    if (disponible === 0 && valor > 0) return `No hay disponibles (${disponible})`;
    if (valor !== disponible) return `Debe trasladar exactamente ${disponible}`;
    return null;
  }

  private validarCantidades(): void {
    const inv = this.inventarioOrigen();
    if (!inv) return;

    const hCtrl = this.form.get('cantidadHembras');
    const mCtrl = this.form.get('cantidadMachos');

    const h = hCtrl?.value ?? 0;
    const m = mCtrl?.value ?? 0;

    // limpia errores previos
    hCtrl?.setErrors(null);
    mCtrl?.setErrors(null);
    this.error.set(null);

    // === REGLA ESTRICTA ===
    const errH = this.validarIgualDisponible(Number(h), inv.cantidadHembras);
    const errM = this.validarIgualDisponible(Number(m), inv.cantidadMachos);

    if (errH) {
      hCtrl?.setErrors({ equalsAvailable: { required: inv.cantidadHembras, actual: h } });
    }
    if (errM) {
      mCtrl?.setErrors({ equalsAvailable: { required: inv.cantidadMachos, actual: m } });
    }

    // Al menos 1 ave total
    if ((Number(h) + Number(m)) === 0) {
      this.error.set('Debe trasladar al menos una ave');
    } else if (!errH && !errM) {
      // si todo OK, limpia mensaje de “al menos 1”
      if (this.error() === 'Debe trasladar al menos una ave') this.error.set(null);
    }
  }

  async onSubmit(): Promise<void> {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }

    const { loteOrigenId, loteDestinoId, cantidadHembras, cantidadMachos, observaciones } = this.form.value;

    if (loteOrigenId === loteDestinoId) {
      this.error.set('El lote origen y destino no pueden ser el mismo');
      return;
    }
    if ((cantidadHembras ?? 0) + (cantidadMachos ?? 0) === 0) {
      this.error.set('Debe trasladar al menos una ave');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    try {
      const req: TrasladoRapidoRequest = {
        loteOrigenId: String(loteOrigenId),
        loteDestinoId: String(loteDestinoId),
        cantidadHembras: Number(cantidadHembras) || 0,
        cantidadMachos: Number(cantidadMachos) || 0,
        observaciones: observaciones || undefined
      };

      const result = await this.trasladosService.trasladoRapido(req).toPromise();

      if (result?.success) {
        this.success.set(result);
        // refresca inventarios mostrados
        if (this.loteOrigenSeleccionado()) await this.cargarInventarioOrigen(this.loteOrigenSeleccionado()!.loteId);
        if (this.loteDestinoSeleccionado()) await this.cargarInventarioDestino(this.loteDestinoSeleccionado()!.loteId);
        // limpia cantidades (mantén lotes para ver nuevos saldos)
        this.form.patchValue({ cantidadHembras: 0, cantidadMachos: 0, observaciones: '' });
      } else {
        this.error.set(result?.message || 'Error al realizar el traslado');
      }
    } catch (e: any) {
      this.error.set(e?.message || 'Error al realizar el traslado');
    } finally {
      this.loading.set(false);
    }
  }


  limpiarFormulario(): void {
    this.form.reset();
    this.inventarioOrigen.set(null);
    this.inventarioDestino.set(null);
    this.limpiarMensajes();
  }

  limpiarMensajes(): void {
    this.error.set(null);
    this.success.set(null);
  }

  // Manejo de filtros de lotes
  onLoteOrigenSelected(lote: LoteDto | null): void {
    this.loteOrigenSeleccionado.set(lote);
    // limpiar cantidades al cambiar lote
    this.form.patchValue({ cantidadHembras: 0, cantidadMachos: 0 });

    if (lote) {
      // ✅ la llave correcta del form es loteOrigenId
      this.form.patchValue({ loteOrigenId: String(lote.loteId) });
      this.cargarInventarioOrigen(lote.loteId);
    } else {
      this.form.patchValue({ loteOrigenId: '' });
      this.inventarioOrigen.set(null);
    }
    this.limpiarMensajes();
  }

  onLoteDestinoSelected(lote: LoteDto | null): void {
    this.loteDestinoSeleccionado.set(lote);
    // limpiar cantidades al cambiar lote
    this.form.patchValue({ cantidadHembras: 0, cantidadMachos: 0 });

    if (lote) {
      // ✅ la llave correcta del form es loteDestinoId
      this.form.patchValue({ loteDestinoId: String(lote.loteId) });
      this.cargarInventarioDestino(lote.loteId);
    } else {
      this.form.patchValue({ loteDestinoId: '' });
      this.inventarioDestino.set(null);
    }
    this.limpiarMensajes();
  }


 


  // Navegación
  navegarADashboard(): void {
    this.router.navigate(['../dashboard'], { relativeTo: this.route });
  }

  navegarAMovimientos(): void {
    this.router.navigate(['../movimientos'], { relativeTo: this.route });
  }

  // Utilidades de validación
  isFieldInvalid(fieldName: string): boolean {
    const field = this.form.get(fieldName);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  getFieldError(fieldName: string): string {
    const field = this.form.get(fieldName);
    if (field?.errors) {
      if (field.errors['required']) return `${this.getFieldLabel(fieldName)} es requerido`;
      if (field.errors['min']) return `Valor mínimo: ${field.errors['min'].min}`;
      if (field.errors['exceedsAvailable']) {
        const e = field.errors['exceedsAvailable'];
        return `Cantidad máxima disponible: ${e.max}`;
      }
      if (field.errors['equalsAvailable']) {
        const e = field.errors['equalsAvailable'];
        return `Debe trasladar exactamente ${this.formatearNumero(e.required)}`;
      }
    }
    return '';
  }


  private getFieldLabel(fieldName: string): string {
    const labels: Record<string, string> = {
      'loteOrigenId': 'Lote origen',
      'loteDestinoId': 'Lote destino',
      'cantidadHembras': 'Cantidad de hembras',
      'cantidadMachos': 'Cantidad de machos'
    };
    return labels[fieldName] || fieldName;
  }

  // Utilidades de formato
  formatearNumero(numero: number): string {
    return numero.toLocaleString('es-CO');
  }

  formatearFecha(fecha: Date | string): string {
    if (!fecha) return '—';
    const date = typeof fecha === 'string' ? new Date(fecha) : fecha;
    return date.toLocaleDateString('es-CO', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  // Métodos para autocompletar cantidades
  trasladarTodasLasHembras(): void {
    const inventario = this.inventarioOrigen();
    if (inventario) {
      this.form.get('cantidadHembras')?.setValue(inventario.cantidadHembras);
    }
  }

  trasladarTodosLosMachos(): void {
    const inventario = this.inventarioOrigen();
    if (inventario) {
      this.form.get('cantidadMachos')?.setValue(inventario.cantidadMachos);
    }
  }

  trasladarTodo(): void {
    this.trasladarTodasLasHembras();
    this.trasladarTodosLosMachos();
  }

  async procesarTraslado(): Promise<void> {
    if (!this.form.valid) {
      this.error.set('Por favor complete todos los campos requeridos');
      return;
    }

    const { loteOrigenId, loteDestinoId, cantidadHembras, cantidadMachos, observaciones } = this.form.value;

    if ((cantidadHembras ?? 0) + (cantidadMachos ?? 0) === 0) {
      this.error.set('Debe trasladar al menos una ave');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);

    try {
      const req: TrasladoRapidoRequest = {
        loteOrigenId: String(loteOrigenId),
        loteDestinoId: String(loteDestinoId),
        cantidadHembras: Number(cantidadHembras) || 0,
        cantidadMachos: Number(cantidadMachos) || 0,
        observaciones: observaciones || undefined
      };

      const result = await this.trasladosService.trasladoRapido(req).toPromise();

      if (result?.success) {
        this.success.set(result);
        // refresca inventarios mostrados
        if (this.loteOrigenSeleccionado()) await this.cargarInventarioOrigen(this.loteOrigenSeleccionado()!.loteId);
        if (this.loteDestinoSeleccionado()) await this.cargarInventarioDestino(this.loteDestinoSeleccionado()!.loteId);
        // limpia cantidades (mantén lotes para ver nuevos saldos)
        this.form.patchValue({ cantidadHembras: 0, cantidadMachos: 0, observaciones: '' });
      } else {
        this.error.set(result?.message || 'Error al realizar el traslado');
      }
    } catch (e: any) {
      this.error.set(e?.message || 'Error al realizar el traslado');
    } finally {
      this.loading.set(false);
    }
  }
}
