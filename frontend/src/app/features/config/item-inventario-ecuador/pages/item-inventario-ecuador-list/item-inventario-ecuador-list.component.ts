// src/app/features/config/item-inventario-ecuador/pages/item-inventario-ecuador-list/item-inventario-ecuador-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faPlus, faPen, faTrash, faSearch, faUpload } from '@fortawesome/free-solid-svg-icons';

import {
  ItemInventarioEcuadorService,
  ItemInventarioEcuadorDto,
  ItemInventarioEcuadorCargaMasivaResult
} from '../../services/item-inventario-ecuador.service';

@Component({
  selector: 'app-item-inventario-ecuador-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, FontAwesomeModule],
  templateUrl: './item-inventario-ecuador-list.component.html',
  styleUrls: ['./item-inventario-ecuador-list.component.scss']
})
export class ItemInventarioEcuadorListComponent implements OnInit {
  faPlus = faPlus;
  faPen = faPen;
  faTrash = faTrash;
  faSearch = faSearch;
  faUpload = faUpload;

  loading = false;
  items: ItemInventarioEcuadorDto[] = [];
  q = '';
  tipoFilter = '';
  activoFilter: boolean | null = null;
  readonly tipos: string[] = ['alimento', 'medicamento', 'insumo', 'otro'];

  showCargaMasiva = false;
  cargaMasivaFile: File | null = null;
  cargaMasivaLoading = false;
  cargaMasivaResult: ItemInventarioEcuadorCargaMasivaResult | null = null;
  cargaMasivaError: string | null = null;

  // Modal: Nuevo ítem
  showNuevoModal = false;
  nuevoLoading = false;
  nuevoError: string | null = null;
  nuevo = {
    codigo: '',
    nombre: '',
    tipoItem: 'alimento',
    unidad: 'kg',
    descripcion: '',
    activo: true,
    grupo: '',
    tipoInventarioCodigo: '',
    descripcionTipoInventario: '',
    referencia: '',
    descripcionItem: '',
    concepto: ''
  };
  readonly unidades: string[] = ['kg', 'und', 'l', 'ml', 'g', 'lb', 'saco'];

  // Modal: Editar ítem
  showEditarModal = false;
  editarLoading = false;
  editarError: string | null = null;
  editarId: number | null = null;
  editar = {
    codigo: '',
    nombre: '',
    tipoItem: 'alimento',
    unidad: 'kg',
    descripcion: '',
    activo: true,
    grupo: '',
    tipoInventarioCodigo: '',
    descripcionTipoInventario: '',
    referencia: '',
    descripcionItem: '',
    concepto: ''
  };

  // Paginación (frontend)
  page = 1;
  pageSize = 5; // default
  /** Opciones del selector: 10, 20 o todos (0 = todos) */
  readonly pageSizes: number[] = [10, 20, 0];

  constructor(private svc: ItemInventarioEcuadorService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    const activo = this.activoFilter === null ? undefined : this.activoFilter;
    this.svc.getAll(this.q || undefined, this.tipoFilter || undefined, activo).subscribe({
      next: (list) => {
        this.items = list;
        this.page = 1;
        this.loading = false;
      },
      error: () => (this.loading = false)
    });
  }

  delete(item: ItemInventarioEcuadorDto): void {
    if (!confirm(`¿Eliminar el ítem "${item.nombre}"?`)) return;
    this.loading = true;
    this.svc.delete(item.id).subscribe({
      next: () => this.load(),
      error: () => (this.loading = false)
    });
  }

  openNuevoModal(): void {
    this.nuevoError = null;
    this.nuevoLoading = false;
    this.nuevo = {
      codigo: '',
      nombre: '',
      tipoItem: 'alimento',
      unidad: 'kg',
      descripcion: '',
      activo: true,
      grupo: '',
      tipoInventarioCodigo: '',
      descripcionTipoInventario: '',
      referencia: '',
      descripcionItem: '',
      concepto: ''
    };
    this.showNuevoModal = true;
  }

  closeNuevoModal(): void {
    this.showNuevoModal = false;
  }

  saveNuevo(): void {
    this.nuevoError = null;
    if (!this.nuevo.codigo?.trim()) {
      this.nuevoError = 'El código es obligatorio.';
      return;
    }
    if (!this.nuevo.nombre?.trim()) {
      this.nuevoError = 'El nombre es obligatorio.';
      return;
    }

    this.nuevoLoading = true;
    this.svc.create({
      codigo: this.nuevo.codigo.trim(),
      nombre: this.nuevo.nombre.trim(),
      tipoItem: this.nuevo.tipoItem,
      unidad: this.nuevo.unidad,
      descripcion: this.nuevo.descripcion?.trim() || null,
      activo: this.nuevo.activo,
      grupo: this.nuevo.grupo?.trim() || null,
      tipoInventarioCodigo: this.nuevo.tipoInventarioCodigo?.trim() || null,
      descripcionTipoInventario: this.nuevo.descripcionTipoInventario?.trim() || null,
      referencia: this.nuevo.referencia?.trim() || null,
      descripcionItem: this.nuevo.descripcionItem?.trim() || null,
      concepto: this.nuevo.concepto?.trim() || null
    }).subscribe({
      next: () => {
        this.nuevoLoading = false;
        this.showNuevoModal = false;
        this.load();
      },
      error: (err) => {
        this.nuevoLoading = false;
        this.nuevoError = err.error?.message || 'Error al crear el ítem.';
      }
    });
  }

  openEditarModal(item: ItemInventarioEcuadorDto): void {
    this.editarError = null;
    this.editarLoading = false;
    this.editarId = item.id;
    this.editar = {
      codigo: item.codigo,
      nombre: item.nombre,
      tipoItem: item.tipoItem,
      unidad: item.unidad,
      descripcion: item.descripcion || '',
      activo: item.activo,
      grupo: item.grupo || '',
      tipoInventarioCodigo: item.tipoInventarioCodigo || '',
      descripcionTipoInventario: item.descripcionTipoInventario || '',
      referencia: item.referencia || '',
      descripcionItem: item.descripcionItem || '',
      concepto: item.concepto || ''
    };
    this.showEditarModal = true;
  }

  closeEditarModal(): void {
    this.showEditarModal = false;
    this.editarId = null;
  }

  saveEditar(): void {
    if (!this.editarId) return;
    this.editarError = null;
    if (!this.editar.nombre?.trim()) {
      this.editarError = 'El nombre es obligatorio.';
      return;
    }

    this.editarLoading = true;
    this.svc.update(this.editarId, {
      nombre: this.editar.nombre.trim(),
      tipoItem: this.editar.tipoItem,
      unidad: this.editar.unidad,
      descripcion: this.editar.descripcion?.trim() || null,
      activo: this.editar.activo,
      grupo: this.editar.grupo?.trim() || null,
      tipoInventarioCodigo: this.editar.tipoInventarioCodigo?.trim() || null,
      descripcionTipoInventario: this.editar.descripcionTipoInventario?.trim() || null,
      referencia: this.editar.referencia?.trim() || null,
      descripcionItem: this.editar.descripcionItem?.trim() || null,
      concepto: this.editar.concepto?.trim() || null
    }).subscribe({
      next: () => {
        this.editarLoading = false;
        this.showEditarModal = false;
        this.editarId = null;
        this.load();
      },
      error: (err) => {
        this.editarLoading = false;
        this.editarError = err.error?.message || 'Error al actualizar el ítem.';
      }
    });
  }

  onCargaMasivaFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.cargaMasivaFile = input.files?.[0] ?? null;
    this.cargaMasivaResult = null;
    this.cargaMasivaError = null;
  }

  procesarCargaMasiva(): void {
    if (!this.cargaMasivaFile) {
      this.cargaMasivaError = 'Seleccione un archivo Excel (.xlsx).';
      return;
    }
    this.cargaMasivaLoading = true;
    this.cargaMasivaError = null;
    this.cargaMasivaResult = null;
    this.svc.cargaMasivaExcel(this.cargaMasivaFile).subscribe({
      next: (res) => {
        this.cargaMasivaResult = res;
        this.cargaMasivaLoading = false;
        this.load();
      },
      error: (err) => {
        this.cargaMasivaLoading = false;
        this.cargaMasivaError = err.error?.message || 'Error en la carga masiva.';
      }
    });
  }

  get totalItems(): number {
    return this.items.length;
  }

  get totalPages(): number {
    if (this.pageSize <= 0) return 1;
    return Math.max(1, Math.ceil(this.totalItems / this.pageSize));
  }

  get pagedItems(): ItemInventarioEcuadorDto[] {
    if (this.pageSize <= 0) return this.items;
    const p = Math.min(Math.max(1, this.page), this.totalPages);
    const start = (p - 1) * this.pageSize;
    return this.items.slice(start, start + this.pageSize);
  }

  prevPage(): void {
    this.page = Math.max(1, this.page - 1);
  }

  nextPage(): void {
    this.page = Math.min(this.totalPages, this.page + 1);
  }

  goToPage(value: number): void {
    if (!Number.isFinite(value)) return;
    this.page = Math.min(this.totalPages, Math.max(1, Math.trunc(value)));
  }

  onPageSizeChange(): void {
    this.page = 1;
  }
}
