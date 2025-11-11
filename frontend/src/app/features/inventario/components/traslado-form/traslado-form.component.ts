// src/app/features/inventario/components/traslado-form/traslado-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faArrowRight, faTrash, faCheckCircle, faBuilding } from '@fortawesome/free-solid-svg-icons';

import {
  InventarioService,
  CatalogItemDto,
  FarmDto
} from '../../services/inventario.service';

@Component({
  selector: 'app-traslado-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './traslado-form.component.html',
  styleUrls: ['./traslado-form.component.scss']
})
export class TrasladoFormComponent implements OnInit {
  faArrowRight = faArrowRight;
  faTrash = faTrash;
  faCheckCircle = faCheckCircle;
  faBuilding = faBuilding;

  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  loading = false;
  showSuccessModal = false;
  transferData: { fromFarm: string; toFarm: string; product: string; quantity: number } | null = null;

  constructor(
    private fb: FormBuilder,
    private invSvc: InventarioService
  ) {}

  ngOnInit(): void {
    this.initForm();
    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  initForm() {
    this.form = this.fb.group({
      fromFarmId: [null, Validators.required],
      toFarmId:   [null, Validators.required],
      catalogItemId: [null, Validators.required],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      reference: [''],
      reason: ['']
    });
  }

  submit() {
    if (this.form.invalid) return;
    const { fromFarmId, toFarmId, catalogItemId, quantity, unit, reference, reason } = this.form.value;

    // Validar que las granjas sean diferentes
    if (fromFarmId === toFarmId) {
      alert('La granja origen y destino deben ser diferentes.');
      return;
    }

    // Guardar datos para el modal antes de limpiar
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

    this.loading = true;
    this.invSvc.postTransfer(fromFarmId, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => {
          this.clearForm();
          this.showSuccessModal = true;
        },
        error: (err) => {
          console.error('Error al registrar traslado:', err);
          alert('Error al registrar el traslado. Por favor, intente nuevamente.');
        }
      });
  }

  clearForm() {
    this.form.reset({
      fromFarmId: null,
      toFarmId: null,
      catalogItemId: null,
      quantity: null,
      unit: 'kg',
      reference: '',
      reason: ''
    });
    // Marcar todos los campos como untouched para limpiar estados de validación
    Object.keys(this.form.controls).forEach(key => {
      this.form.controls[key].markAsUntouched();
      this.form.controls[key].markAsPristine();
    });
  }

  closeSuccessModal() {
    this.showSuccessModal = false;
    this.transferData = null;
  }

  getFromFarmName(): string {
    const farmId = this.form.value.fromFarmId;
    return this.farms.find(f => f.id === farmId)?.name || 'Seleccione...';
  }

  getToFarmName(): string {
    const farmId = this.form.value.toFarmId;
    return this.farms.find(f => f.id === farmId)?.name || 'Seleccione...';
  }
}
