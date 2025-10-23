import { Pipe, PipeTransform } from '@angular/core';
import { FarmInventoryDto } from '../services/inventario.service';

@Pipe({ name: 'inventarioFilter', standalone: true })
export class InventarioFilterPipe implements PipeTransform {
  transform(list: FarmInventoryDto[], term: string): FarmInventoryDto[] {
    if (!term?.trim()) return list;
    const q = term.toLowerCase().trim();
    return list.filter(x =>
      (x.codigo ?? '').toLowerCase().includes(q) ||
      (x.nombre ?? '').toLowerCase().includes(q) ||
      (x.location ?? '').toLowerCase().includes(q) ||
      (x.lotNumber ?? '').toLowerCase().includes(q)
    );
    // si quisieras filtrar por granja/compañía, necesitarías maps de nombres
  }
}
