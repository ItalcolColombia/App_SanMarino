// src/app/features/inventario/components/inventario-list/inventario-list.component.ts
import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faRotateRight } from '@fortawesome/free-solid-svg-icons';

import {
  InventarioService,
  FarmDto,
  FarmInventoryDto
} from '../../services/inventario.service';

@Component({
  selector: 'app-inventario-list',
  standalone: true,
  imports: [CommonModule, FormsModule, FontAwesomeModule],
  templateUrl: './inventario-list.component.html',
  styleUrls: ['./inventario-list.component.scss']
})
export class InventarioListComponent implements OnInit {
  faRefresh = faRotateRight;

  filtro: string = '';
  farms: FarmDto[] = [];
  selectedFarmId: number | null = null;

  loading = false;
  items: FarmInventoryDto[] = [];

  farmMap: Record<number, string> = {};

  constructor(private invSvc: InventarioService) {}

  trackByItem = (_: number, it: FarmInventoryDto) => it.id;

  get filtered(): FarmInventoryDto[] {
    const q = this.filtro?.toLowerCase().trim();
    if (!q) return this.items;
    return this.items.filter(x =>
      (x.codigo ?? '').toLowerCase().includes(q) ||
      (x.nombre ?? '').toLowerCase().includes(q) ||
      (x.location ?? '').toLowerCase().includes(q) ||
      (x.lotNumber ?? '').toLowerCase().includes(q)
    );
  }

  ngOnInit(): void {
    this.invSvc.getFarms().subscribe(fs => {
      this.farms = fs;
      fs.forEach(f => (this.farmMap[f.id] = f.name));
      if (fs.length) {
        this.selectedFarmId = fs[0].id;
        this.load();
      }
    });
  }

  load() {
    if (!this.selectedFarmId) return;
    this.loading = true;
    this.invSvc.getInventory(this.selectedFarmId)
      .pipe(finalize(() => (this.loading = false)))
      .subscribe(list => (this.items = list));
  }

  getFarmName(id: number): string {
    return this.farmMap[id] ?? '–';
  }
}
