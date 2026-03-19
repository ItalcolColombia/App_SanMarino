// src/app/features/config/item-inventario-ecuador/pages/item-inventario-ecuador-form/item-inventario-ecuador-form.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ItemInventarioEcuadorService, ItemInventarioEcuadorCreateRequest, ItemInventarioEcuadorUpdateRequest } from '../../services/item-inventario-ecuador.service';

@Component({
  selector: 'app-item-inventario-ecuador-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './item-inventario-ecuador-form.component.html',
  styleUrls: ['./item-inventario-ecuador-form.component.scss']
})
export class ItemInventarioEcuadorFormComponent implements OnInit {
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
    private svc: ItemInventarioEcuadorService,
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
      const req: ItemInventarioEcuadorUpdateRequest = {
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
      const req: ItemInventarioEcuadorCreateRequest = {
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
