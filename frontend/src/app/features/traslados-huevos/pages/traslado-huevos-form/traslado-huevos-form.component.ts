// frontend/src/app/features/traslados-huevos/pages/traslado-huevos-form/traslado-huevos-form.component.ts
import { Component, OnInit, signal, ChangeDetectionStrategy } from '@angular/core';

import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';
import { FiltroSelectComponent } from '../../../lote-produccion/pages/filtro-select/filtro-select.component';
import { TrasladosHuevosService, DisponibilidadLoteDto, CrearTrasladoHuevosDto, HuevosDisponiblesDto } from '../../services/traslados-huevos.service';
import { FarmService } from '../../../farm/services/farm.service';
import { environment } from '../../../../../environments/environment';
import { UserPermissionService } from '../../../../core/auth/user-permission.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { InventarioService } from '../../../inventario/services/inventario.service';
import { CatalogItemDto } from '../../../catalogo-alimentos/services/catalogo-alimentos.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { HuevoCatalogGrupo, HuevoCatalogOption, ITEM_TYPE_HUEVO } from '../../../lote-produccion/models/huevo-clasificacion.model';
import { HuevoItemSeguimiento } from '../../../lote-produccion/services/produccion.service';
import {
  agruparItemsHuevoPorTipo,
  mapearItemsHuevoACatalogo,
  sumarCantidadesHuevo
} from '../../../lote-produccion/funciones/items-huevo-catalogo.funcion';
import {
  extremosVentanaRegistro,
  hintVentanaFechaRegistro,
  PERMISO_FECHA_RETROACTIVA
} from '../../../../shared/utils/fecha/ventana-fecha-registro.funcion';

@Component({
  selector: 'app-traslado-huevos-form',
  standalone: true,
  imports: [ReactiveFormsModule, FiltroSelectComponent],
  templateUrl: './traslado-huevos-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./traslado-huevos-form.component.scss']
})
export class TrasladoHuevosFormComponent implements OnInit {
  formHuevos!: FormGroup;

  // ===== Clasificación de huevos por ÍTEMS (flag de empresa · Santa Reyes, ver F10 §9 del plan) =====
  clasificacionHuevoPorItems = false;
  gruposHuevoItems: HuevoCatalogGrupo[] = [];
  totalHuevosClasificados = 0;
  cargandoItemsHuevo = false;
  private huevoItemsById = new Map<number, HuevoCatalogOption>();

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
    private router: Router,
    private userPermService: UserPermissionService,
    private companyConfig: ActiveCompanyConfigService,
    private inventarioSvc: InventarioService,
    private toast: ToastService
  ) {
    this.initForm();
    this.companyConfig.getFlags().subscribe(flags => {
      if (this.clasificacionHuevoPorItems === flags.clasificacionHuevoPorItems) return;
      this.clasificacionHuevoPorItems = flags.clasificacionHuevoPorItems;
      if (!this.clasificacionHuevoPorItems) return;
      this.cargarItemsHuevo();
      this.asegurarFilaHuevoInicial();
    });
  }

  get huevoItemsArray(): FormArray {
    return this.formHuevos.get('huevoItems') as FormArray;
  }

  private cargarItemsHuevo(): void {
    if (this.cargandoItemsHuevo || this.huevoItemsById.size > 0) return;
    this.cargandoItemsHuevo = true;
    this.inventarioSvc.getCatalogoByType(ITEM_TYPE_HUEVO).pipe(
      catchError(err => {
        console.error('Error al cargar ítems de huevo:', err);
        return of([] as CatalogItemDto[]);
      })
    ).subscribe((items: CatalogItemDto[]) => {
      this.cargandoItemsHuevo = false;
      const opciones = mapearItemsHuevoACatalogo(items ?? []);
      this.huevoItemsById.clear();
      opciones.forEach(o => this.huevoItemsById.set(o.id, o));
      this.gruposHuevoItems = agruparItemsHuevoPorTipo(opciones);
    });
  }

  private get totalItemsHuevoOfrecidos(): number {
    let total = 0;
    for (const g of this.gruposHuevoItems) total += g.items.length;
    return total;
  }

  private crearFilaHuevo(catalogItemId: number | null = null, cantidad: number = 0): FormGroup {
    const grp = this.fb.group({
      catalogItemId: [catalogItemId],
      cantidad: [cantidad, [Validators.min(0)]]
    });
    grp.get('catalogItemId')!.valueChanges.subscribe(valor => this.onCambioItemHuevo(grp, valor));
    return grp;
  }

  private onCambioItemHuevo(fila: FormGroup, valor: unknown): void {
    const id = Number(valor) || 0;
    if (!id) return;
    const duplicado = this.huevoItemsArray.controls
      .some(c => c !== fila && Number(c.get('catalogItemId')?.value) === id);
    if (!duplicado) return;
    fila.get('catalogItemId')!.setValue(null, { emitEvent: false });
    this.toast.warning(
      'Ese ítem de huevo ya está en otra fila. Sumá la cantidad en la fila existente.',
      'Ítem duplicado'
    );
  }

  agregarFilaHuevo(): void {
    const tope = this.totalItemsHuevoOfrecidos;
    if (tope > 0 && this.huevoItemsArray.length >= tope) {
      this.toast.warning('Ya hay una fila por cada ítem de huevo del catálogo.', 'Clasificación de huevos');
      return;
    }
    this.huevoItemsArray.push(this.crearFilaHuevo());
  }

  eliminarFilaHuevo(index: number): void {
    this.huevoItemsArray.removeAt(index);
  }

  itemHuevoUsadoEnOtraFila(catalogItemId: number, index: number): boolean {
    const controls = this.huevoItemsArray.controls;
    for (let i = 0; i < controls.length; i++) {
      if (i === index) continue;
      if (Number(controls[i].get('catalogItemId')?.value) === catalogItemId) return true;
    }
    return false;
  }

  detalleItemHuevo(catalogItemId: unknown): string {
    const id = Number(catalogItemId) || 0;
    if (!id) return '';
    const item = this.huevoItemsById.get(id);
    if (!item) return '';
    if (item.tipoHuevo && item.um) return `${item.tipoHuevo} · ${item.um}`;
    return item.tipoHuevo || item.um || '';
  }

  disponibleItemHuevo(catalogItemId: unknown): number {
    const id = Number(catalogItemId) || 0;
    if (!id) return 0;
    const fila = this.disponibilidad()?.huevoItemsDisponibles?.find(d => d.catalogItemId === id);
    return fila?.cantidad ?? 0;
  }

  private asegurarFilaHuevoInicial(): void {
    if (!this.clasificacionHuevoPorItems) return;
    if (this.huevoItemsArray.length > 0) return;
    this.huevoItemsArray.push(this.crearFilaHuevo());
  }

  private recalcularTotalHuevosClasificados(): void {
    this.totalHuevosClasificados = sumarCantidadesHuevo(
      this.huevoItemsArray.controls.map(c => c.get('cantidad')?.value)
    );
  }

  private construirHuevoItemsPayload(): HuevoItemSeguimiento[] {
    const filas: HuevoItemSeguimiento[] = [];
    for (const control of this.huevoItemsArray.controls) {
      const catalogItemId = Number(control.get('catalogItemId')?.value) || 0;
      const cantidad = Number(control.get('cantidad')?.value) || 0;
      if (catalogItemId <= 0 || cantidad <= 0) continue;
      const item = this.huevoItemsById.get(catalogItemId);
      filas.push({
        catalogItemId,
        codigo: item?.codigo ?? null,
        nombre: item?.nombre ?? null,
        tipoHuevo: item?.tipoHuevo ?? null,
        cantidad,
        um: item?.um ?? null
      });
    }
    return filas;
  }

  /** Ventana de fechas admitida para `fechaTraslado` (mes en curso ∪ últimos 15 días). */
  private get puedeRetroactivar(): boolean {
    return this.userPermService.has(PERMISO_FECHA_RETROACTIVA);
  }

  get fechaTrasladoMin(): string | null {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).min;
  }

  get fechaTrasladoMax(): string {
    return extremosVentanaRegistro(new Date(), this.puedeRetroactivar).max;
  }

  get fechaTrasladoHint(): string {
    return hintVentanaFechaRegistro(new Date(), this.puedeRetroactivar);
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
    huevosControls['huevoItems'] = this.fb.array([]);

    this.formHuevos = this.fb.group(huevosControls, { validators: this.validarTrasladoHuevos.bind(this) });
    this.huevoItemsArray.valueChanges.subscribe(() => this.recalcularTotalHuevosClasificados());

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
    if (this.clasificacionHuevoPorItems) {
      const items = (control.get('huevoItems') as FormArray | null)?.controls ?? [];
      const total = sumarCantidadesHuevo(items.map(c => c.get('cantidad')?.value));
      return total === 0 ? { sinCantidad: true } : null;
    }

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
    const usaHuevoItems = this.clasificacionHuevoPorItems;
    const huevoItems = usaHuevoItems ? this.construirHuevoItemsPayload() : undefined;

    const dto: CrearTrasladoHuevosDto = {
      lotePosturaProduccionId: lppId,
      loteId: lppId ? '' : String(formValue.loteId ?? ''),
      fechaTraslado: fechaTraslado,
      tipoOperacion: formValue.tipoOperacion,
      cantidadLimpio: usaHuevoItems ? 0 : (formValue.cantidadLimpio || 0),
      cantidadTratado: usaHuevoItems ? 0 : (formValue.cantidadTratado || 0),
      cantidadSucio: usaHuevoItems ? 0 : (formValue.cantidadSucio || 0),
      cantidadDeforme: usaHuevoItems ? 0 : (formValue.cantidadDeforme || 0),
      cantidadBlanco: usaHuevoItems ? 0 : (formValue.cantidadBlanco || 0),
      cantidadDobleYema: usaHuevoItems ? 0 : (formValue.cantidadDobleYema || 0),
      cantidadPiso: usaHuevoItems ? 0 : (formValue.cantidadPiso || 0),
      cantidadPequeno: usaHuevoItems ? 0 : (formValue.cantidadPequeno || 0),
      cantidadRoto: usaHuevoItems ? 0 : (formValue.cantidadRoto || 0),
      cantidadDesecho: usaHuevoItems ? 0 : (formValue.cantidadDesecho || 0),
      cantidadOtro: usaHuevoItems ? 0 : (formValue.cantidadOtro || 0),
      huevoItems,
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
        while (this.huevoItemsArray.length) this.huevoItemsArray.removeAt(0);
        this.asegurarFilaHuevoInicial();
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
