import { Pipe, PipeTransform } from '@angular/core';
import { FarmDto } from '../services/farm.service';

@Pipe({
  name: 'farmFilter',
  standalone: true
})
export class FarmFilterPipe implements PipeTransform {
  transform(farms: FarmDto[], clienteFiltro: string): FarmDto[] {
    if (!clienteFiltro?.trim()) return farms;
    const filtro = clienteFiltro.toLowerCase();
    return farms.filter(f =>
      f.name?.toLowerCase().includes(filtro) ||
      f.status?.toLowerCase().includes(filtro) ||
      f.regionalId?.toString().includes(filtro) ||
      f.departamentoId?.toString().includes(filtro) ||
       f.ciudadId?.toString().includes(filtro) ||
      f.companyId?.toString().includes(filtro)
    );
  }
}
