// src/app/features/inventario/components/movimiento-alimento-form/movimiento-alimento-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faBoxesStacked,
  faCalendar,
  faFileUpload,
  faCheckCircle,
  faTimes,
  faBuilding,
  faWarehouse
} from '@fortawesome/free-solid-svg-icons';

import {
  InventarioService,
  CatalogItemDto,
  FarmDto
} from '../../services/inventario.service';
import { GalponService } from '../../../galpon/services/galpon.service';
import { GalponDetailDto } from '../../../galpon/models/galpon.models';

// Tipos de documento origen
type DocumentoOrigen = 'Autoconsumo' | 'RVN' | 'EAN';
// Tipos de entrada
type TipoEntrada = 'Entrada Nueva' | 'Traslado entre galpon' | 'Traslados entre granjas';

@Component({
  selector: 'app-movimiento-alimento-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, FontAwesomeModule],
  templateUrl: './movimiento-alimento-form.component.html',
  styleUrls: ['./movimiento-alimento-form.component.scss']
})
export class MovimientoAlimentoFormComponent implements OnInit {
  // Iconos
  faBoxes = faBoxesStacked;
  faCalendar = faCalendar;
  faUpload = faFileUpload;
  faCheck = faCheckCircle;
  faTimes = faTimes;
  faBuilding = faBuilding;
  faWarehouse = faWarehouse;

  form!: FormGroup;
  farms: FarmDto[] = [];
  galpones: GalponDetailDto[] = [];
  items: CatalogItemDto[] = [];
  filteredItems: CatalogItemDto[] = [];
  loading = false;
  showSuccessModal = false;
  
  // Opciones predefinidas
  readonly documentosOrigen: DocumentoOrigen[] = ['Autoconsumo', 'RVN', 'EAN'];
  readonly tiposEntrada: TipoEntrada[] = ['Entrada Nueva', 'Traslado entre galpon', 'Traslados entre granjas'];
  readonly unidades: string[] = ['kg', 'und', 'l', 'bultos'];
  
  // Búsqueda de productos
  searchTerm: string = '';
  searching = false;

  // Datos para el modal de éxito
  lastMovementData: {
    farm: string;
    producto: string;
    cantidad: number;
    unidad: string;
    documentoOrigen?: string;
    tipoEntrada?: string;
  } | null = null;

  constructor(
    private fb: FormBuilder,
    private invSvc: InventarioService,
    private galponSvc: GalponService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.loadFarms();
    
    // Cargar productos iniciales (solo tipo alimento)
    this.loadProducts('alimento', null);
    
    // Filtrar productos cuando cambia la búsqueda
    this.form.get('searchProduct')?.valueChanges.subscribe(search => {
      this.searchTerm = search || '';
      this.loadProducts('alimento', this.searchTerm || null);
    });
  }

  loadFarms(): void {
    this.invSvc.getFarms().subscribe({
      next: (farms) => {
        this.farms = farms;
        if (farms.length > 0) {
          this.form.patchValue({ farmId: farms[0].id });
          this.loadGalpones(farms[0].id);
        }
      },
      error: (err) => console.error('Error al cargar granjas:', err)
    });
  }

  loadGalpones(farmId: number): void {
    this.galponSvc.getByGranja(farmId).subscribe({
      next: (galpones) => {
        this.galpones = galpones;
      },
      error: (err) => {
        console.error('Error al cargar galpones:', err);
        this.galpones = [];
      }
    });
  }

  onFarmChange(event: any): void {
    const farmId = parseInt(event.target.value, 10);
    if (!isNaN(farmId)) {
      this.loadGalpones(farmId);
      // Limpiar selección de galpón al cambiar granja
      this.form.patchValue({ galponDestinoId: null });
    }
  }

  loadProducts(typeItem: string | null = null, search: string | null = null): void {
    this.searching = true;
    this.invSvc.getCatalogoByType(typeItem, search).subscribe({
      next: (items) => {
        // Filtrar solo alimentos si typeItem es 'alimento'
        if (typeItem === 'alimento') {
          this.items = items.filter(item => 
            item.metadata?.type_item === 'alimento' || 
            item.metadata?.type_item === 'Alimento'
          );
        } else {
          this.items = items;
        }
        this.filteredItems = this.items;
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

  initForm() {
    this.form = this.fb.group({
      // Granja destino (donde llega el alimento)
      farmId: [null, Validators.required],
      
      // Granja origen (solo para traslados entre granjas)
      farmIdOrigen: [null], // Opcional, requerido solo si tipoEntrada es "Traslados entre granjas"
      
      // Producto
      catalogItemId: [null, Validators.required],
      searchProduct: [''],
      
      // Cantidad y unidad
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      
      // Fecha del movimiento
      fechaMovimiento: [new Date().toISOString().split('T')[0], Validators.required],
      
      // Documento origen
      documentoOrigen: ['', Validators.required],
      
      // Tipo de entrada
      tipoEntrada: ['', Validators.required],
      
      // Galpón destino
      galponDestinoId: [null], // Opcional
      
      // Referencia y motivo
      reference: [''],
      reason: ['']
    });

    // Validar que farmIdOrigen sea requerido cuando tipoEntrada es "Traslados entre granjas"
    this.form.get('tipoEntrada')?.valueChanges.subscribe(tipoEntrada => {
      const farmIdOrigenControl = this.form.get('farmIdOrigen');
      if (tipoEntrada === 'Traslados entre granjas') {
        farmIdOrigenControl?.setValidators([Validators.required]);
        farmIdOrigenControl?.updateValueAndValidity();
      } else {
        farmIdOrigenControl?.clearValidators();
        farmIdOrigenControl?.setValue(null);
        farmIdOrigenControl?.updateValueAndValidity();
      }
    });
  }

  get selectedProduct(): CatalogItemDto | null {
    const productId = this.form.get('catalogItemId')?.value;
    return this.items.find(p => p.id === productId) || null;
  }

  get selectedFarm(): FarmDto | null {
    const farmId = this.form.get('farmId')?.value;
    return this.farms.find(f => f.id === farmId) || null;
  }

  get selectedGalpon(): GalponDetailDto | null {
    const galponId = this.form.get('galponDestinoId')?.value;
    return this.galpones.find(g => g.galponId === galponId) || null;
  }

  submit() {
    if (this.form.invalid) {
      // Marcar todos los campos como touched para mostrar errores
      Object.keys(this.form.controls).forEach(key => {
        this.form.controls[key].markAsTouched();
      });
      return;
    }

    const formValue = this.form.value;
    const tipoEntrada = formValue.tipoEntrada;
    
    // Si es traslado entre granjas, usar el endpoint de transfer
    if (tipoEntrada === 'Traslados entre granjas') {
      this.submitTransfer(formValue);
    } else {
      this.submitEntry(formValue);
    }
  }

  submitEntry(formValue: any) {
    const farmId = formValue.farmId;
    
    // Preparar payload para entrada
    const payload: any = {
      catalogItemId: formValue.catalogItemId,
      quantity: formValue.quantity,
      unit: formValue.unit,
      reference: formValue.reference || undefined,
      reason: formValue.reason || undefined,
      documentoOrigen: formValue.documentoOrigen,
      tipoEntrada: formValue.tipoEntrada,
      galponDestinoId: formValue.galponDestinoId || undefined,
      fechaMovimiento: formValue.fechaMovimiento ? new Date(formValue.fechaMovimiento).toISOString() : undefined
    };

    this.loading = true;

    // Guardar datos para el modal
    const farm = this.selectedFarm;
    const product = this.selectedProduct;
    this.lastMovementData = {
      farm: farm?.name || '',
      producto: product ? `${product.codigo} — ${product.nombre}` : '',
      cantidad: formValue.quantity,
      unidad: formValue.unit,
      documentoOrigen: formValue.documentoOrigen,
      tipoEntrada: formValue.tipoEntrada
    };

    this.invSvc.postEntry(farmId, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => {
          this.clearForm();
          this.showSuccessModal = true;
        },
        error: (err) => {
          console.error('Error al registrar movimiento de alimento:', err);
          alert('Error al registrar el movimiento. Por favor, intente nuevamente.');
        }
      });
  }

  submitTransfer(formValue: any) {
    const farmIdOrigen = formValue.farmIdOrigen;
    const farmIdDestino = formValue.farmId;
    
    if (!farmIdOrigen || farmIdOrigen === farmIdDestino) {
      alert('La granja origen debe ser diferente a la granja destino.');
      return;
    }
    
    // Preparar payload para traslado
    const payload: any = {
      toFarmId: farmIdDestino,
      catalogItemId: formValue.catalogItemId,
      quantity: formValue.quantity,
      unit: formValue.unit,
      reference: formValue.reference || `Traslado: ${formValue.documentoOrigen}`,
      reason: formValue.reason || `Traslado de alimento - ${formValue.tipoEntrada}`,
      documentoOrigen: formValue.documentoOrigen,
      tipoEntrada: formValue.tipoEntrada,
      galponDestinoId: formValue.galponDestinoId || undefined,
      fechaMovimiento: formValue.fechaMovimiento ? new Date(formValue.fechaMovimiento).toISOString() : undefined
    };

    this.loading = true;

    // Guardar datos para el modal
    const farmOrigen = this.farms.find(f => f.id === farmIdOrigen);
    const farmDestino = this.selectedFarm;
    const product = this.selectedProduct;
    this.lastMovementData = {
      farm: `${farmOrigen?.name || ''} → ${farmDestino?.name || ''}`,
      producto: product ? `${product.codigo} — ${product.nombre}` : '',
      cantidad: formValue.quantity,
      unidad: formValue.unit,
      documentoOrigen: formValue.documentoOrigen,
      tipoEntrada: formValue.tipoEntrada
    };

    this.invSvc.postTransfer(farmIdOrigen, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => {
          this.clearForm();
          this.showSuccessModal = true;
        },
        error: (err) => {
          console.error('Error al registrar traslado de alimento:', err);
          alert('Error al registrar el traslado. Por favor, intente nuevamente.');
        }
      });
  }

  clearForm() {
    const farmId = this.form.get('farmId')?.value;
    this.form.reset({
      farmId: farmId,
      farmIdOrigen: null,
      catalogItemId: null,
      searchProduct: '',
      quantity: null,
      unit: 'kg',
      fechaMovimiento: new Date().toISOString().split('T')[0],
      documentoOrigen: '',
      tipoEntrada: '',
      galponDestinoId: null,
      reference: '',
      reason: ''
    });
    
    this.searchTerm = '';
    this.loadProducts('alimento', null);

    // Marcar todos los campos como untouched
    Object.keys(this.form.controls).forEach(key => {
      this.form.controls[key].markAsUntouched();
      this.form.controls[key].markAsPristine();
    });
  }

  closeSuccessModal() {
    this.showSuccessModal = false;
    this.lastMovementData = null;
  }

  // Helpers para validación visual
  isFieldInvalid(fieldName: string): boolean {
    const field = this.form.get(fieldName);
    return !!(field && field.invalid && field.touched);
  }

  getFieldError(fieldName: string): string {
    const field = this.form.get(fieldName);
    if (!field || !field.errors || !field.touched) return '';
    
    if (field.errors['required']) return 'Este campo es requerido';
    if (field.errors['min']) return 'El valor debe ser mayor a 0';
    
    return '';
  }
}
