// src/app/features/inventario/components/kardex-list/kardex-list.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { InventarioService, FarmDto, CatalogItemDto, KardexItemDto } from '../../services/inventario.service';

@Component({
  selector: 'app-kardex-list',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './kardex-list.component.html',
  styleUrls: ['./kardex-list.component.scss']
})
export class KardexListComponent {
  farms: FarmDto[] = [];
  items: CatalogItemDto[] = [];
  kardex: KardexItemDto[] = [];
  loading = false;

  farmId: number | null = null;
  itemId: number | null = null;
  desde: string | null = null;
  hasta: string | null = null;

  constructor(private invSvc: InventarioService) {
    this.invSvc.getFarms().subscribe(f => this.farms = f);
    this.invSvc.getCatalogo().subscribe(c => this.items = c.filter(x => x.activo));
  }

  load() {
    if (!this.farmId || !this.itemId) return;
    this.loading = true;
    this.invSvc.getKardex(this.farmId, this.itemId, this.desde || undefined, this.hasta || undefined)
      .subscribe({
        next: (rows) => { this.kardex = rows; this.loading = false; },
        error: () => { this.loading = false; }
      });
  }

  get signClass() {
    return (qty: number) => qty > 0 ? 'text-green' : qty < 0 ? 'text-red' : '';
  }

  trackByDate(index: number, item: KardexItemDto): string {
    return item.fecha.toString();
  }

  getTypeClass(tipo: string): string {
    const tipoLower = tipo.toLowerCase();
    if (tipoLower.includes('entry') || tipoLower.includes('entrada') || tipoLower.includes('transferin')) {
      return 'badge-entry';
    }
    if (tipoLower.includes('exit') || tipoLower.includes('salida') || tipoLower.includes('transferout')) {
      return 'badge-exit';
    }
    if (tipoLower.includes('adjust') || tipoLower.includes('ajuste')) {
      return 'badge-adjust';
    }
    return 'badge-default';
  }

  /**
   * Traduce el tipo de movimiento del inglés al español
   */
  translateMovementType(tipo: string): string {
    const tipoLower = tipo.toLowerCase().trim();

    // Mapeo de tipos en inglés a español
    const translationMap: Record<string, string> = {
      'entry': 'Entrada',
      'entrada': 'Entrada',
      'exit': 'Salida',
      'salida': 'Salida',
      'transfer': 'Traslado',
      'traslado': 'Traslado',
      'transferin': 'Traslado (Entrada)',
      'transfer-out': 'Traslado (Salida)',
      'transferout': 'Traslado (Salida)',
      'adjust': 'Ajuste',
      'ajuste': 'Ajuste',
      'adjustment': 'Ajuste'
    };

    // Primero intenta búsqueda exacta
    if (translationMap[tipoLower]) {
      return translationMap[tipoLower];
    }

    // Si no encuentra exacto, busca por coincidencia parcial
    if (tipoLower.includes('entry') || tipoLower.includes('transferin')) {
      return 'Entrada';
    }
    if (tipoLower.includes('exit') || tipoLower.includes('transferout') || tipoLower.includes('transfer-out')) {
      return 'Salida';
    }
    if (tipoLower.includes('transfer')) {
      return 'Traslado';
    }
    if (tipoLower.includes('adjust')) {
      return 'Ajuste';
    }

    // Si no se encuentra ninguna traducción, devuelve el original en formato título
    return tipo.charAt(0).toUpperCase() + tipo.slice(1).toLowerCase();
  }
}
