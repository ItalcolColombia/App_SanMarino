// src/app/features/inventario/components/ajuste-form/ajuste-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { InventarioService, CatalogItemDto, FarmDto } from '../../services/inventario.service';

@Component({
  selector: 'app-ajuste-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './ajuste-form.component.html',
  styleUrls: ['./ajuste-form.component.scss']
})
export class AjusteFormComponent implements OnInit {
  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  loading = false;

  constructor(private fb: FormBuilder, private invSvc: InventarioService) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      farmId: [null, Validators.required],
      catalogItemId: [null, Validators.required],
      // signo: +1 suma, -1 resta
      signo: ['-1', Validators.required],
      quantity: [null, [Validators.required, Validators.min(0.0001)]],
      unit: ['kg', Validators.required],
      reason: ['Ajuste de inventario', [Validators.maxLength(200)]],
      reference: ['']
    });

    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  submit() {
    if (this.form.invalid) return;
    const { farmId, catalogItemId, signo, quantity, unit, reason, reference } = this.form.value;
    const payload = { catalogItemId, quantity: Number(signo) * Number(quantity), unit, reason, reference };

    this.loading = true;
    this.invSvc.postAdjust(farmId, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe(() => {
        this.form.patchValue({ quantity: null, reference: '' });
        alert('Ajuste registrado');
      });
  }
}
