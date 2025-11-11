// src/app/features/inventario/components/movimientos-form/movimientos-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faArrowDown, faArrowUp, faTrash, faCheckCircle } from '@fortawesome/free-solid-svg-icons';

import {
  InventarioService,
  CatalogItemDto,
  FarmDto
} from '../../services/inventario.service';

@Component({
  selector: 'app-movimientos-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './movimientos-form.component.html',
  styleUrls: ['./movimientos-form.component.scss']
})
export class MovimientosFormComponent implements OnInit {
  faIn  = faArrowDown;
  faOut = faArrowUp;
  faTrash = faTrash;
  faCheckCircle = faCheckCircle;

  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  loading = false;
  showSuccessModal = false;
  lastMovementType: 'in' | 'out' = 'in';

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
      farmId: [null, Validators.required],
      type:   ['in', Validators.required], // 'in' | 'out'
      catalogItemId: [null, Validators.required],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      reference: [''],
      reason: ['']
    });
  }

  submit() {
    if (this.form.invalid) return;
    const { farmId, type, catalogItemId, quantity, unit, reference, reason } = this.form.value;
    const payload = { catalogItemId, quantity, unit, reference, reason };

    // Guardar el tipo antes de limpiar
    this.lastMovementType = type;

    this.loading = true;
    const req$ = type === 'in'
      ? this.invSvc.postEntry(farmId, payload)
      : this.invSvc.postExit (farmId, payload);

    req$.pipe(finalize(() => this.loading = false)).subscribe({
      next: () => {
        this.clearForm();
        this.showSuccessModal = true;
      },
      error: (err) => {
        console.error('Error al registrar movimiento:', err);
        alert('Error al registrar el movimiento. Por favor, intente nuevamente.');
      }
    });
  }

  clearForm() {
    this.form.reset({
      farmId: null,
      type: 'in',
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
  }
}
