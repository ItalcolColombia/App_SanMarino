import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { fechaHoraCorta } from '../../../../shared/utils/format';
import { etiquetarTipoCuadre } from '../../funciones/etiquetar-tipo-cuadre.funcion';
import { CuadresOfflineService } from '../../services/cuadres-offline.service';
import type { CuadrePendiente } from '../../models/cuadre-pendiente.model';

/**
 * Bandeja de cuadre: capturas hechas sin red que **se guardaron** pero entraron **sin descontar
 * inventario**, porque al llegar al servidor ya no había stock de ese ítem.
 *
 * ## Por qué esta pantalla existe
 *
 * El backend viene emitiendo `requiere_cuadre` desde el 22-ago y expone la bandeja
 * (`GET /api/Sync/cuadres`), pero **nadie la llamaba**. El dispositivo del galponero borra esa
 * operación de su cola —y hace bien: el día sí se guardó—, así que sin esta pantalla la divergencia
 * de stock no la veía nadie. Un emisor sin lector es peor que no tener la señal: promete un control
 * que no ocurre.
 *
 * ## Lo que NO hace, dicho también en pantalla
 *
 * «Marcar como revisada» **no repone kilos**. El faltante se corrige cargando el ingreso por el
 * módulo de inventario. Reponer desde acá sería una segunda fórmula para el mismo número.
 *
 * `changeDetection: Eager` — regla del repo: hay estado mutable que se llena desde `subscribe`
 * (`cargando`, `filas`); con OnPush la pantalla se quedaría en «Cargando…» aunque la red respondiera
 * 200.
 */
@Component({
  selector: 'app-cuadres-offline-page',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './cuadres-offline-page.component.html',
  styleUrls: ['./cuadres-offline-page.component.scss']
})
export class CuadresOfflinePageComponent implements OnInit {
  private readonly srv = inject(CuadresOfflineService);
  private readonly toast = inject(ToastService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  cargando = true;
  filas: CuadrePendiente[] = [];

  /** Id en curso de resolución: deshabilita sólo ESA fila, no la tabla entera. */
  resolviendo: number | null = null;

  /**
   * Hubo un error de red al cargar. Se distingue de «no hay cuadres» a propósito: son diagnósticos
   * opuestos y la pantalla vacía sin aviso haría creer que está todo cuadrado.
   */
  error = '';

  async ngOnInit(): Promise<void> {
    await this.recargar();
  }

  async recargar(): Promise<void> {
    this.cargando = true;
    this.error = '';
    try {
      this.filas = await firstValueFrom(this.srv.listar());
    } catch (e) {
      this.filas = [];
      this.error = e instanceof HttpErrorResponse && e.status === 0
        ? 'Sin conexión: esta bandeja se consulta con red.'
        : 'No se pudo cargar la bandeja. Intentá de nuevo.';
    } finally {
      this.cargando = false;
    }
  }

  async resolver(fila: CuadrePendiente): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: 'Marcar como revisada',
      // El texto repite lo que la pantalla ya dice arriba: es el último momento en que alguien puede
      // creer que el botón repone el faltante.
      message: `Se va a marcar como revisada la captura de "${this.etiqueta(fila.tipo)}" `
        + `recibida el ${this.fechaHora(fila.recibidoAt)}. `
        + 'Esto NO repone el inventario: el faltante se corrige cargando el ingreso en inventario.',
      confirmText: 'Marcar como revisada',
      type: 'warning'
    });
    if (!ok) return;

    this.resolviendo = fila.id;
    try {
      await firstValueFrom(this.srv.resolver(fila.id));
      this.quitar(fila.id);
      this.toast.success('Cuadre marcado como revisado.');
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 404) {
        // Ya la resolvió otro, o es de otra empresa. No es un error del usuario: es información.
        this.quitar(fila.id);
        this.toast.info('Esa fila ya no estaba pendiente.');
        return;
      }
      this.toast.error('No se pudo marcar como revisada. Intentá de nuevo.');
    } finally {
      this.resolviendo = null;
    }
  }

  /** Se reasigna el arreglo (no `splice`) para que la vista lo tome como cambio. */
  private quitar(id: number): void {
    this.filas = this.filas.filter(f => f.id !== id);
  }

  /** Delegan en los helpers centrales; el template los llama por `this`. */
  etiqueta(tipo: string | null | undefined): string {
    return etiquetarTipoCuadre(tipo);
  }

  fechaHora(iso: string | null | undefined): string {
    return fechaHoraCorta(iso);
  }
}
