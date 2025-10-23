// src/app/features/inventario/components/conteo-fisico/conteo-fisico.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventarioService, FarmDto, CatalogItemDto } from '../../services/inventario.service';

interface ConteoRow {
  catalogItemId: number;
  codigo: string;
  nombre: string;
  unit: string;
  sistema: number; // stock del sistema
  conteo: number | null; // editable
}

@Component({
  selector: 'app-conteo-fisico',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './conteo-fisico.component.html',
  styleUrls: ['./conteo-fisico.component.scss']
})
export class ConteoFisicoComponent {
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  rows: ConteoRow[] = [];
  loading = false;
  farmId: number | null = null;
  filtro = '';

  constructor(private invSvc: InventarioService) {
    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  load() {
    if (!this.farmId) return;
    this.loading = true;
    this.invSvc.getStock(this.farmId).subscribe({
      next: (stock) => {
        // Espera formato compatible a tu componente de lista
        this.rows = stock.map((s: any) => ({
          catalogItemId: s.catalogItemId,
          codigo: s.codigo,
          nombre: s.nombre,
          unit: s.unit,
          sistema: s.quantity,
          conteo: null
        }));
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  get filtered(): ConteoRow[] {
    const fx = this.filtro.trim().toLowerCase();
    if (!fx) return this.rows;
    return this.rows.filter(r =>
      r.codigo.toLowerCase().includes(fx) ||
      r.nombre.toLowerCase().includes(fx)
    );
  }

  guardarConteo() {
    if (!this.farmId) return;
    const payload = this.rows
      .filter(r => r.conteo !== null && !Number.isNaN(Number(r.conteo)))
      .map(r => ({
        catalogItemId: r.catalogItemId,
        conteo: Number(r.conteo)
      }));

    if (payload.length === 0) { alert('No hay conteos para enviar'); return; }

    this.loading = true;
    this.invSvc.postConteoFisico(this.farmId, { items: payload })
      .subscribe({
        next: () => { this.loading = false; alert('Conteo guardado'); },
        error: () => this.loading = false
      });
  }

  trackByCatalogItemId(index: number, item: ConteoRow): number {
    return item.catalogItemId;
  }

}
