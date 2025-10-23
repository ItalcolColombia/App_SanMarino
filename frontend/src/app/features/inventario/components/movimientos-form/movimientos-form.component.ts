// src/app/features/inventario/components/movimientos-form/movimientos-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faArrowDown, faArrowUp } from '@fortawesome/free-solid-svg-icons';

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

  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  loading = false;

  constructor(
    private fb: FormBuilder,
    private invSvc: InventarioService
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      farmId: [null, Validators.required],
      type:   ['in', Validators.required], // 'in' | 'out'
      catalogItemId: [null, Validators.required],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      reference: [''],
      reason: ['']
    });

    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  submit() {
    if (this.form.invalid) return;
    const { farmId, type, catalogItemId, quantity, unit, reference, reason } = this.form.value;
    const payload = { catalogItemId, quantity, unit, reference, reason };

    this.loading = true;
    const req$ = type === 'in'
      ? this.invSvc.postEntry(farmId, payload)
      : this.invSvc.postExit (farmId, payload);

    req$.pipe(finalize(() => this.loading = false)).subscribe(() => {
      this.form.patchValue({ quantity: null, reference: '', reason: '' });
      alert('Movimiento registrado');
    });
  }
}
