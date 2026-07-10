// src/app/features/config/item-inventario/pages/item-inventario-form/item-inventario-form.component.ts
import { Component, OnInit, ChangeDetectionStrategy } from '@angular/core';

import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ItemInventarioService, ItemInventarioCreateRequest, ItemInventarioUpdateRequest } from '../../services/item-inventario.service';

@Component({
  selector: 'app-item-inventario-form',
  standalone: true,
  imports: [FormsModule, RouterModule],
  templateUrl: './item-inventario-form.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./item-inventario-form.component.scss']
})
export class ItemInventarioFormComponent implements OnInit {
  loading = false;
  editingId: number | null = null;
  errorMessage: string | null = null;

  codigo = '';
  nombre = '';
  tipoItem = 'alimento';
  unidad = 'kg';
  descripcion = '';
  activo = true;
  grupo = '';
  tipoInventarioCodigo = '';
  descripcionTipoInventario = '';
  referencia = '';
  descripcionItem = '';
  concepto = '';

  readonly tipos: string[] = ['alimento', 'medicamento', 'insumo', 'otro'];
  readonly unidades: string[] = ['kg', 'und', 'l', 'ml', 'g', 'lb', 'saco'];

  constructor(
    private svc: ItemInventarioService,
    private router: Router,
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.editingId = +idParam;
      this.loadItem(this.editingId);
    }
  }

  loadItem(id: number): void {
    this.loading = true;
    this.svc.getById(id).subscribe({
      next: (item) => {
        this.codigo = item.codigo;
        this.nombre = item.nombre;
        this.tipoItem = item.tipoItem;
        this.unidad = item.unidad;
        this.descripcion = item.descripcion || '';
        this.activo = item.activo;
        this.grupo = item.grupo ?? '';
        this.tipoInventarioCodigo = item.tipoInventarioCodigo ?? '';
        this.descripcionTipoInventario = item.descripcionTipoInventario ?? '';
        this.referencia = item.referencia ?? '';
        this.descripcionItem = item.descripcionItem ?? '';
        this.concepto = item.concepto ?? '';
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'No se pudo cargar el ítem.';
      }
    });
  }

  save(): void {
    this.errorMessage = null;
    if (!this.nombre?.trim()) {
      this.errorMessage = 'El nombre es obligatorio.';
      return;
    }
    if (!this.editingId && !this.codigo?.trim()) {
      this.errorMessage = 'El código es obligatorio.';
      return;
    }

    this.loading = true;
    if (this.editingId) {
      const req: ItemInventarioUpdateRequest = {
        nombre: this.nombre.trim(),
        tipoItem: this.tipoItem,
        unidad: this.unidad,
        descripcion: this.descripcion?.trim() || null,
        activo: this.activo,
        grupo: this.grupo?.trim() || null,
        tipoInventarioCodigo: this.tipoInventarioCodigo?.trim() || null,
        descripcionTipoInventario: this.descripcionTipoInventario?.trim() || null,
        referencia: this.referencia?.trim() || null,
        descripcionItem: this.descripcionItem?.trim() || null,
        concepto: this.concepto?.trim() || null
      };
      this.svc.update(this.editingId, req).subscribe({
        next: () => this.goBack(),
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.message || 'Error al actualizar.';
        }
      });
    } else {
      const req: ItemInventarioCreateRequest = {
        codigo: this.codigo.trim(),
        nombre: this.nombre.trim(),
        tipoItem: this.tipoItem,
        unidad: this.unidad,
        descripcion: this.descripcion?.trim() || null,
        activo: this.activo,
        grupo: this.grupo?.trim() || null,
        tipoInventarioCodigo: this.tipoInventarioCodigo?.trim() || null,
        descripcionTipoInventario: this.descripcionTipoInventario?.trim() || null,
        referencia: this.referencia?.trim() || null,
        descripcionItem: this.descripcionItem?.trim() || null,
        concepto: this.concepto?.trim() || null
      };
      this.svc.create(req).subscribe({
        next: () => this.goBack(),
        error: (err) => {
          this.loading = false;
          this.errorMessage = err.error?.message || 'Error al crear.';
        }
      });
    }
  }

  goBack(): void {
    this.router.navigate(['../'], { relativeTo: this.route });
  }
}
