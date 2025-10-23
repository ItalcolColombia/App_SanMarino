import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'inventarioFilter',
  standalone: true
})
export class InventarioFilterPipe implements PipeTransform {

  transform(value: unknown, ...args: unknown[]): unknown {
    return null;
  }

}
