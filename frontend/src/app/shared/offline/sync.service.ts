import { HttpClient, HttpContext, HttpErrorResponse } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { ConexionService } from '../../core/pwa/conexion.service';
import { TokenStorageService } from '../../core/auth/token-storage.service';
import { ES_PUSH_DE_SYNC } from './sync-context';
import { environment } from '../../../environments/environment';
import { clasificarResultadoPush, debeFrenar } from './funciones/clasificar-resultado-push.funcion';
import { filtrarOperacionesParticion } from './funciones/filtrar-operaciones-particion.funcion';
import { proximoIntento } from './funciones/backoff.funcion';
import { OutboxService } from './outbox.service';
import type { OperacionPendiente, ResultadoPush } from './models/outbox.model';

/** Mismo tope que el servidor (`SyncPushCalculos.MaxOperacionesPorLote`). */
export const MAX_POR_LOTE = 25;

/**
 * Empuja al servidor lo capturado sin red (F3).
 *
 * ## Lo que hace que esto sea seguro
 *
 * Cada operación viaja con el `clientOpId` que se generó **al encolarla**, y ese id no cambia entre
 * reintentos. El servidor tiene un índice único sobre él: si una respuesta se pierde en la red y el
 * dispositivo reenvía, el efecto no se aplica dos veces — devuelve la respuesta original con
 * `replay: true`. Sin eso, un 504 seguido de un reintento contaría la mortalidad del día dos veces.
 *
 * Y solo sale **lo de la sesión activa**: el token lo pone el interceptor y el servidor estampa el
 * autor desde ese token, así que empujar la cola entera firmaría con un operario lo que capturó
 * otro. Ver `filtrarOperacionesParticion`.
 */
@Injectable({ providedIn: 'root' })
export class SyncService {
  private readonly http = inject(HttpClient);
  private readonly outbox = inject(OutboxService);
  private readonly conexion = inject(ConexionService);
  private readonly storage = inject(TokenStorageService);

  /** Hay un envío en curso. Evita que dos disparos (reconexión + timer) se pisen. */
  readonly sincronizando = signal(false);

  /** Momento del último envío con al menos una operación confirmada. */
  readonly ultimoEnvioOk = signal<number | null>(null);

  private readonly url = `${environment.apiUrl}/Sync/push`;

  constructor() {
    // Al recuperar la red se intenta enviar. No hay timer: reintentar contra una red que no está
    // solo gasta batería, y el backoff ya cubre el caso de "hay red pero el servidor no contesta".
    effect(() => {
      if (this.conexion.enLinea()) {
        void this.sincronizar();
      }
    });
  }

  /**
   * Envía lo que esté listo. Silencioso: nunca lanza.
   * Devuelve cuántas operaciones quedaron confirmadas.
   */
  async sincronizar(): Promise<number> {
    if (this.sincronizando()) {
      return 0;
    }

    this.sincronizando.set(true);
    try {
      return await this.enviarPendientes();
    } catch {
      return 0;
    } finally {
      this.sincronizando.set(false);
    }
  }

  private async enviarPendientes(): Promise<number> {
    const ahora = Date.now();
    const todas = await this.outbox.listarTodas();

    // Solo lo de la sesión ACTIVA que ya cumplió su backoff. Lo ajeno se queda en la cola —intacto—
    // hasta que su dueño vuelva a entrar (R9), y lo rechazado espera a que una persona lo edite o
    // lo descarte desde la bandeja. Sin sesión no sale nada.
    const listas = filtrarOperacionesParticion(todas, this.storage.identidadActual(), ahora);

    if (listas.length === 0) {
      return 0;
    }

    let confirmadas = 0;

    for (let i = 0; i < listas.length; i += MAX_POR_LOTE) {
      const lote = listas.slice(i, i + MAX_POR_LOTE);
      const resultado = await this.empujarLote(lote);

      confirmadas += resultado.confirmadas;

      if (resultado.frenar) {
        // El servidor pidió parar (429/503). Seguir con el resto es la forma más rápida de que el
        // rate limiter deje al dispositivo afuera todavía más tiempo.
        break;
      }
    }

    if (confirmadas > 0) {
      this.ultimoEnvioOk.set(Date.now());
    }

    await this.outbox.refrescarContadores();
    return confirmadas;
  }

  private async empujarLote(lote: OperacionPendiente[]): Promise<{ confirmadas: number; frenar: boolean }> {
    const cuerpo = {
      operaciones: lote.map(op => ({
        clientOpId: op.clientOpId,
        tipo: op.tipo,
        companyId: op.companyId,
        paisId: op.paisId,
        deviceId: op.deviceId,
        capturadoAtDispositivo: op.capturadoAtDispositivo,
        payload: op.payload
      }))
    };

    try {
      const respuesta = await firstValueFrom(
        this.http.post<{ resultados: ResultadoPush[] }>(this.url, cuerpo, {
          context: new HttpContext().set(ES_PUSH_DE_SYNC, true)
        })
      );

      return { confirmadas: await this.aplicarResultados(lote, respuesta?.resultados ?? []), frenar: false };
    } catch (error) {
      const status = error instanceof HttpErrorResponse ? error.status : null;
      const retryAfter = error instanceof HttpErrorResponse ? error.headers?.get('Retry-After') : null;

      // El lote entero falló: ninguna operación se confirma. Todas vuelven a la cola con backoff.
      await this.reprogramarLote(lote, retryAfter);
      return { confirmadas: 0, frenar: debeFrenar(status) };
    }
  }

  private async aplicarResultados(lote: OperacionPendiente[], resultados: ResultadoPush[]): Promise<number> {
    const porId = new Map(resultados.map(r => [String(r.clientOpId).toLowerCase(), r]));
    let confirmadas = 0;

    for (const op of lote) {
      const resultado = porId.get(op.clientOpId.toLowerCase());

      switch (clasificarResultadoPush(resultado)) {
        case 'borrar':
          await this.outbox.descartar(op.clientOpId);
          confirmadas++;
          break;

        case 'bandeja':
          await this.outbox.actualizar({
            ...op,
            estado: 'rechazada',
            intentos: op.intentos + 1,
            proximoIntentoEn: null,
            errorCodigo: resultado?.errorCodigo ?? null,
            detalle: resultado?.detalle ?? null
          });
          break;

        case 'reintentar':
          await this.outbox.actualizar({
            ...op,
            intentos: op.intentos + 1,
            proximoIntentoEn: proximoIntento(op.intentos + 1, Date.now()),
            errorCodigo: resultado?.errorCodigo ?? null,
            detalle: resultado?.detalle ?? null
          });
          break;
      }
    }

    return confirmadas;
  }

  private async reprogramarLote(lote: OperacionPendiente[], retryAfter: string | null): Promise<void> {
    for (const op of lote) {
      await this.outbox.actualizar({
        ...op,
        intentos: op.intentos + 1,
        proximoIntentoEn: proximoIntento(op.intentos + 1, Date.now(), retryAfter)
      });
    }
  }
}
