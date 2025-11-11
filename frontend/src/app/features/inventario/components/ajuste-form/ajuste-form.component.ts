// src/app/features/inventario/components/ajuste-form/ajuste-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faMinus, faTrash, faCheckCircle, faScrewdriverWrench } from '@fortawesome/free-solid-svg-icons';
import { InventarioService, CatalogItemDto, FarmDto } from '../../services/inventario.service';

@Component({
  selector: 'app-ajuste-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FontAwesomeModule],
  templateUrl: './ajuste-form.component.html',
  styleUrls: ['./ajuste-form.component.scss']
})
export class AjusteFormComponent implements OnInit {
  faPlus = faPlus;
  faMinus = faMinus;
  faTrash = faTrash;
  faCheckCircle = faCheckCircle;
  faWrench = faScrewdriverWrench;

  form!: FormGroup;
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  loading = false;
  showSuccessModal = false;
  lastAdjustmentSign: '1' | '-1' = '-1';

  constructor(private fb: FormBuilder, private invSvc: InventarioService) {}

  ngOnInit(): void {
    this.initForm();
    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  initForm() {
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
  }

  submit() {
    if (this.form.invalid) return;
    const { farmId, catalogItemId, signo, quantity, unit, reason, reference } = this.form.value;
    const payload = { catalogItemId, quantity: Number(signo) * Number(quantity), unit, reason, reference };

    // Guardar el signo antes de limpiar
    this.lastAdjustmentSign = signo;

    this.loading = true;
    this.invSvc.postAdjust(farmId, payload)
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: () => {
          this.clearForm();
          this.showSuccessModal = true;
        },
        error: (err) => {
          console.error('Error al registrar ajuste:', err);
          alert('Error al registrar el ajuste. Por favor, intente nuevamente.');
        }
      });
  }

  clearForm() {
    const signo = this.form.value.signo;
    this.form.reset({
      farmId: null,
      catalogItemId: null,
      signo,
      quantity: null,
      unit: 'kg',
      reason: 'Ajuste de inventario',
      reference: ''
    });
    // Marcar todos los campos como untouched
    Object.keys(this.form.controls).forEach(key => {
      this.form.controls[key].markAsUntouched();
      this.form.controls[key].markAsPristine();
    });
  }

  closeSuccessModal() {
    this.showSuccessModal = false;
  }

  get operationType(): string {
    return this.form.value.signo === '1' ? 'suma' : 'resta';
  }
}
