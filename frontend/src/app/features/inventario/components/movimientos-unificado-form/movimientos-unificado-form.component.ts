// src/app/features/inventario/components/movimientos-unificado-form/movimientos-unificado-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faArrowDown,
  faArrowUp,
  faArrowRight,
  faTrash,
  faCheckCircle,
  faBuilding
} from '@fortawesome/free-solid-svg-icons';

import {
  InventarioService,
  CatalogItemDto,
  FarmDto
} from '../../services/inventario.service';

type OperationType = 'entrada' | 'salida' | 'traslado';
type CatalogItemType = 'alimento' | 'medicamento' | 'accesorio' | 'biologico' | 'consumible' | 'otro';

@Component({
  selector: 'app-movimientos-unificado-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, FontAwesomeModule],
  templateUrl: './movimientos-unificado-form.component.html',
  styleUrls: ['./movimientos-unificado-form.component.scss']
})
export class MovimientosUnificadoFormComponent implements OnInit {
  // Iconos
  faIn = faArrowDown;
  faOut = faArrowUp;
  faTransfer = faArrowRight;
  faTrash = faTrash;
  faCheckCircle = faCheckCircle;
  faBuilding = faBuilding;

  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  filteredItems: CatalogItemDto[] = [];
  loading = false;
  showSuccessModal = false;
  
  // Tipos de item disponibles
  readonly tiposItem: CatalogItemType[] = ['alimento', 'medicamento', 'accesorio', 'biologico', 'consumible', 'otro'];
  
  // Búsqueda de productos
  searchTerm: string = '';
  searching = false;

  // Datos para el modal
  lastOperationType: OperationType = 'entrada';
  transferData: { fromFarm: string; toFarm: string; product: string; quantity: number } | null = null;

  constructor(
    private fb: FormBuilder,
    private invSvc: InventarioService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.invSvc.getFarms().subscribe(f => this.farms = f);
    
    // Cargar productos iniciales (todos los activos)
    this.loadProducts();
    
    // Filtrar productos cuando cambia el tipo de item
    this.form.get('typeItem')?.valueChanges.subscribe(typeItem => {
      this.loadProducts(typeItem || null, this.searchTerm || null);
    });
  }

  loadProducts(typeItem: string | null = null, search: string | null = null): void {
    this.searching = true;
    this.invSvc.getCatalogoByType(typeItem, search).subscribe({
      next: (items) => {
        this.items = items;
        this.filteredItems = items;
        this.searching = false;
        
        // Limpiar selección de producto si ya no está en la lista filtrada
        const currentProductId = this.form.get('catalogItemId')?.value;
        if (currentProductId && !this.filteredItems.find(i => i.id === currentProductId)) {
          this.form.patchValue({ catalogItemId: null });
        }
      },
      error: (err) => {
        console.error('Error al cargar productos:', err);
        this.items = [];
        this.filteredItems = [];
        this.searching = false;
      }
    });
  }

  onSearchChange(searchTerm: string): void {
    this.searchTerm = searchTerm;
    const typeItem = this.form.get('typeItem')?.value || null;
    this.loadProducts(typeItem, searchTerm || null);
  }

  // Listas predefinidas de motivos
  readonly entryReasons = ['Planta Sanmarino', 'Planta Itacol'];
  readonly exitDestinations = ['Venta', 'Movimiento', 'Devolución'];

  initForm() {
    this.form = this.fb.group({
      operationType: ['entrada', Validators.required], // 'entrada' | 'salida' | 'traslado'

      // Para entrada/salida
      farmId: [null],

      // Para traslado
      fromFarmId: [null],
      toFarmId: [null],

      // Tipo de item (filtro para productos)
      typeItem: [''], // Opcional: si está vacío, muestra todos los productos

      // Campos comunes
      catalogItemId: [null, Validators.required],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      reference: [''],
      reason: [''],

      // Campos nuevos
      origin: [''],      // Origen para entrada
      destination: ['']  // Destino para salida
    });

    // Validaciones dinámicas según el tipo de operación
    this.form.get('operationType')?.valueChanges.subscribe(type => {
      this.updateValidators(type);
      this.updateFormDefaults(type);
    });
  }

  updateValidators(type: OperationType) {
    const farmId = this.form.get('farmId');
    const fromFarmId = this.form.get('fromFarmId');
    const toFarmId = this.form.get('toFarmId');
    const origin = this.form.get('origin');
    const destination = this.form.get('destination');

    if (type === 'traslado') {
      // Traslado: necesita fromFarmId y toFarmId
      farmId?.clearValidators();
      origin?.clearValidators();
      destination?.clearValidators();
      fromFarmId?.setValidators([Validators.required]);
      toFarmId?.setValidators([Validators.required]);
    } else if (type === 'entrada') {
      // Entrada: necesita farmId y origin
      fromFarmId?.clearValidators();
      toFarmId?.clearValidators();
      destination?.clearValidators();
      farmId?.setValidators([Validators.required]);
      origin?.setValidators([Validators.required]);
    } else {
      // Salida: necesita farmId y destination
      fromFarmId?.clearValidators();
      toFarmId?.clearValidators();
      origin?.clearValidators();
      farmId?.setValidators([Validators.required]);
      destination?.setValidators([Validators.required]);
    }

    farmId?.updateValueAndValidity({ emitEvent: false });
    fromFarmId?.updateValueAndValidity({ emitEvent: false });
    toFarmId?.updateValueAndValidity({ emitEvent: false });
    origin?.updateValueAndValidity({ emitEvent: false });
    destination?.updateValueAndValidity({ emitEvent: false });
  }

  updateFormDefaults(type: OperationType) {
    // Limpiar campos no necesarios al cambiar tipo
    if (type === 'traslado') {
      this.form.patchValue({ farmId: null, origin: '', destination: '', reason: '' });
    } else {
      this.form.patchValue({ fromFarmId: null, toFarmId: null });
      if (type === 'entrada') {
        this.form.patchValue({ destination: '', reason: '' });
      } else if (type === 'salida') {
        this.form.patchValue({ origin: '', reason: '' });
      }
    }
  }

  get operationType(): OperationType {
    return this.form.value.operationType || 'entrada';
  }

  get isTransfer(): boolean {
    return this.operationType === 'traslado';
  }

  get isEntry(): boolean {
    return this.operationType === 'entrada';
  }

  get isExit(): boolean {
    return this.operationType === 'salida';
  }

  submit() {
    if (this.form.invalid) return;

    const operationType = this.operationType;
    const { catalogItemId, quantity, unit, reference, reason } = this.form.value;

    this.lastOperationType = operationType;

    this.loading = true;
    let request$;

    if (operationType === 'traslado') {
      const { fromFarmId, toFarmId } = this.form.value;

      // Validar que las granjas sean diferentes
      if (fromFarmId === toFarmId) {
        alert('La granja origen y destino deben ser diferentes.');
        this.loading = false;
        return;
      }

      // Guardar datos para el modal
      const fromFarm = this.farms.find(f => f.id === fromFarmId)?.name || '';
      const toFarm = this.farms.find(f => f.id === toFarmId)?.name || '';
      const product = this.items.find(i => i.id === catalogItemId);
      const productName = product ? `${product.codigo} — ${product.nombre}` : '';

      this.transferData = {
        fromFarm,
        toFarm,
        product: productName,
        quantity
      };

      const payload = { toFarmId, catalogItemId, quantity, unit, reference, reason };
      request$ = this.invSvc.postTransfer(fromFarmId, payload);

    } else {
      const { farmId, origin, destination } = this.form.value;

      if (operationType === 'entrada') {
        const payload = { catalogItemId, quantity, unit, reference, reason, origin };
        request$ = this.invSvc.postEntry(farmId, payload);
      } else {
        const payload = { catalogItemId, quantity, unit, reference, reason, destination };
        request$ = this.invSvc.postExit(farmId, payload);
      }
    }

    request$.pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.clearForm();
        this.showSuccessModal = true;
      },
      error: (err) => {
        console.error('Error al registrar movimiento:', err);
        const tipoTexto = operationType === 'traslado' ? 'traslado' :
                         operationType === 'entrada' ? 'entrada' : 'salida';
        alert(`Error al registrar el ${tipoTexto}. Por favor, intente nuevamente.`);
      }
    });
  }

  clearForm() {
    const operationType = this.form.value.operationType;

    this.form.reset({
      operationType,
      farmId: null,
      fromFarmId: null,
      toFarmId: null,
      typeItem: '',
      catalogItemId: null,
      quantity: null,
      unit: 'kg',
      reference: '',
      reason: '',
      origin: '',
      destination: ''
    });
    
    // Restablecer búsqueda y recargar productos
    this.searchTerm = '';
    this.loadProducts();

    // Actualizar validadores después del reset
    this.updateValidators(operationType);

    // Marcar todos los campos como untouched
    Object.keys(this.form.controls).forEach(key => {
      this.form.controls[key].markAsUntouched();
      this.form.controls[key].markAsPristine();
    });
  }

  closeSuccessModal() {
    this.showSuccessModal = false;
    this.transferData = null;
  }

  // Helpers para nombres de granjas (para visualización de traslado)
  getFromFarmName(): string {
    const farmId = this.form.value.fromFarmId;
    return this.farms.find(f => f.id === farmId)?.name || 'Seleccione...';
  }

  getToFarmName(): string {
    const farmId = this.form.value.toFarmId;
    return this.farms.find(f => f.id === farmId)?.name || 'Seleccione...';
  }

  getFarmName(farmId: number | null): string {
    if (!farmId) return 'Seleccione...';
    return this.farms.find(f => f.id === farmId)?.name || 'Seleccione...';
  }

  // Título dinámico del botón
  get submitButtonText(): string {
    switch (this.operationType) {
      case 'entrada': return 'Registrar entrada';
      case 'salida': return 'Registrar salida';
      case 'traslado': return 'Registrar traslado';
      default: return 'Registrar';
    }
  }

  // Icono dinámico del botón
  get submitButtonIcon() {
    switch (this.operationType) {
      case 'entrada': return this.faIn;
      case 'salida': return this.faOut;
      case 'traslado': return this.faTransfer;
      default: return this.faIn;
    }
  }

}

