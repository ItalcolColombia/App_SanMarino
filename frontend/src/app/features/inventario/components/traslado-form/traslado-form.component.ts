// src/app/features/inventario/components/traslado-form/traslado-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';

import {
  InventarioService,
  CatalogItemDto,
  FarmDto
} from '../../services/inventario.service';

@Component({
  selector: 'app-traslado-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './traslado-form.component.html',
  styleUrls: ['./traslado-form.component.scss']
})
export class TrasladoFormComponent implements OnInit {
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
      fromFarmId: [null, Validators.required],
      toFarmId:   [null, Validators.required],
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
    const { fromFarmId, toFarmId, catalogItemId, quantity, unit, reference, reason } = this.form.value;
    const payload = { toFarmId, catalogItemId, quantity, unit, reference, reason };

    this.loading = true;
    this.invSvc.postTransfer(fromFarmId, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe(() => {
        this.form.patchValue({ quantity: null, reference: '', reason: '' });
        alert('Traslado registrado');
      });
  }
}
