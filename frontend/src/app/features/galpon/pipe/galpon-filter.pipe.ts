// src/app/features/galpon/pipe/galpon-filter.pipe.ts
import { Pipe, PipeTransform } from '@angular/core';
import { GalponDetailDto } from '../models/galpon.models';

@Pipe({ name: 'galponFilter', standalone: true })
export class GalponFilterPipe implements PipeTransform {
  transform(items: GalponDetailDto[] | null | undefined, term: string): GalponDetailDto[] {
    if (!items) return [];
    if (!term)  return items;
    const t = term.trim().toLowerCase();

    return items.filter(g => {
      return (
        g.galponId?.toLowerCase().includes(t) ||
        g.galponNombre?.toLowerCase().includes(t) ||
        g.nucleo?.nucleoNombre?.toLowerCase().includes(t) ||
        g.nucleo?.nucleoId?.toLowerCase().includes(t) ||
        g.farm?.name?.toLowerCase().includes(t) ||
        String(g.farm?.id ?? '').includes(t) ||
        g.company?.name?.toLowerCase().includes(t) ||
        g.company?.identifier?.toLowerCase().includes(t) ||
        g.tipoGalpon?.toLowerCase().includes(t)
      );
    });
  }
}
