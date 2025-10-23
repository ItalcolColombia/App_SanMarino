import { Pipe, PipeTransform } from '@angular/core';
import { LoteDto } from '../services/lote.service';

@Pipe({ name: 'loteFilter', standalone: true })
export class LoteFilterPipe implements PipeTransform {
  transform(
    list: LoteDto[] | null | undefined,
    term: string,
    farmMap?: Record<number, string>,
    nucleoMap?: Record<string, string>,
    galponMap?: Record<string, string>
  ): LoteDto[] {
    if (!list) return [];
    if (!term) return list;

    const t = term.trim().toLowerCase();
    return list.filter(l => {
      const farmName   = l.granjaId != null ? (farmMap?.[l.granjaId] ?? '') : '';
      const nucleoName = l.nucleoId ? (nucleoMap?.[String(l.nucleoId)] ?? '') : '';
      const galponName = l.galponId ? (galponMap?.[String(l.galponId)] ?? '') : '';

      return (
        (l.loteId ?? '').toString().toLowerCase().includes(t) ||
        (l.loteNombre ?? '').toLowerCase().includes(t) ||
        (l.nucleoId ?? '').toString().toLowerCase().includes(t) ||
        (l.galponId ?? '').toString().toLowerCase().includes(t) ||
        (l.tecnico ?? '').toLowerCase().includes(t) ||
        farmName.toLowerCase().includes(t) ||
        nucleoName.toLowerCase().includes(t) ||
        galponName.toLowerCase().includes(t)
      );
    });
  }
}
