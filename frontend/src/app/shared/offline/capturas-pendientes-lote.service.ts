import { Injectable, inject } from '@angular/core';

import { TokenStorageService } from '../../core/auth/token-storage.service';
import { OutboxService } from './outbox.service';
import { claveParticion } from './funciones/clave-particion.funcion';
import { resumirCapturasPendientes } from './funciones/resumir-capturas-pendientes.funcion';
import type { CapturaPendienteResumen } from './models/outbox.model';

/**
 * Qué capturas sin enviar tiene el dispositivo **para el lote que la pantalla está mostrando**.
 *
 * Es el puente entre el outbox (F3) y las cuatro pantallas de captura diaria. Existe para que el
 * cableado —leer la cola, resolver la identidad de la sesión, filtrar— se escriba **una vez** y no
 * cuatro: repetirlo por pantalla es cómo una de las cuatro termina filtrando por dos ids en vez de
 * por tres y mostrando capturas de otra empresa.
 *
 * La decisión de qué se muestra vive en `resumirCapturasPendientes` (pura, con tests). Acá sólo se
 * resuelven datos.
 */
@Injectable({ providedIn: 'root' })
export class CapturasPendientesLoteService {
  private readonly outbox = inject(OutboxService);
  private readonly storage = inject(TokenStorageService);

  /**
   * Nunca lanza: una tabla de seguimiento no puede quedarse sin cargar porque IndexedDB no esté
   * disponible. Sin cola, sin sesión o sin lote devuelve un arreglo vacío.
   *
   * @param tipo  Tipo de operación de esa pantalla (`SyncPushCalculos.Tipos`).
   * @param lote  Campo(s) del payload que identifican al lote, con el valor que se está viendo.
   *              Producción pasa los dos (`lotePosturaProduccionId` y `produccionLoteId`): su
   *              payload lleva uno u otro según el flujo, y los valores son distintos.
   */
  async resumir(
    tipo: string,
    lote: Readonly<Record<string, string | number | null | undefined>>
  ): Promise<CapturaPendienteResumen[]> {
    try {
      const identidad = this.storage.identidadActual();
      const particion = claveParticion(identidad);
      if (particion === null) {
        return [];
      }

      // `listar` ya filtra por partición; se le vuelve a pasar a la función pura a propósito, porque
      // es ella la que tiene el test de que lo ajeno no se muestra.
      const operaciones = await this.outbox.listar(identidad);
      return resumirCapturasPendientes(operaciones, { tipo, particion, lote });
    } catch {
      return [];
    }
  }
}
