import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { filasSelector, type FilaSelector } from './funciones/filas-selector.funcion';

/** Dígitos del PIN. Decisión del usuario (18ago26): 6, como propuso el plan. */
export const DIGITOS_PIN = 6;

/**
 * Selector de perfil: quién retoma la tablet.
 *
 * ## Por qué esta pantalla NO lleva `authGuard`
 *
 * Por definición la abre alguien que **todavía no tiene sesión activa**. Un guard la haría inalcanzable
 * exactamente en el escenario para el que existe, igual que en `/diagnostico`.
 *
 * ## Lo que muestra, y lo que no
 *
 * Todo sale del **padrón**, que va sin cifrar a propósito para poder pintarse sin red y sin PIN: nombre,
 * empresa, hace cuánto se usó y cuántas capturas esperan. **No** muestra permisos ni menú: eso vive
 * dentro del blob cifrado y abrirlo exige el PIN (R-M7).
 *
 * ## Después de activar se RECARGA la página
 *
 * No es pereza. Hay `BehaviorSubject` con datos de la empresa en los ~33 módulos de features y caché de
 * flags en `ActiveCompanyConfigService`; cambiar de slot sin recargar significa auditar cada servicio
 * con estado y confiar en que ninguno se olvidó. Con `location.reload()` la garantía es estructural:
 * **ningún objeto de la empresa de A puede sobrevivir a la sesión de B.** Cuesta 1-2 s que sirve el SW,
 * sin red.
 *
 * `changeDetection: Eager` — hay estado mutable que se llena desde promesas (cripto e IndexedDB); con
 * OnPush la pantalla quedaría en «Cargando…» (ver CLAUDE.md).
 */
@Component({
  selector: 'app-selector-usuario',
  standalone: true,
  imports: [FormsModule, NgClass],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './selector-usuario.component.html'
})
export class SelectorUsuarioComponent implements OnInit {
  private readonly llavero = inject(LlaveroSesionesService);
  private readonly router = inject(Router);

  readonly digitosPin = DIGITOS_PIN;

  cargando = true;

  /** Sesiones guardadas en este equipo, de la más reciente a la más vieja. */
  filas: FilaSelector[] = [];

  /** La fila cuyo PIN se está pidiendo. `null` = todavía se está eligiendo. */
  elegida: FilaSelector | null = null;

  pin = '';
  activando = false;
  error = '';

  /** El llavero no existe en este dispositivo (sin `crypto.subtle`, o sea contexto no seguro). */
  sinLlavero = false;

  async ngOnInit(): Promise<void> {
    await this.recargar();
  }

  async recargar(): Promise<void> {
    this.cargando = true;
    this.sinLlavero = !this.llavero.disponible();

    if (this.sinLlavero) {
      this.filas = [];
      this.cargando = false;
      return;
    }

    const slots = this.llavero.slotsAparcados();
    const pendientes = await this.llavero.pendientesPorSlot(slots);
    this.filas = filasSelector(slots, pendientes, Date.now());
    this.cargando = false;
  }

  /**
   * Elegir una sesión. Si no se puede abrir sin red, esta pantalla no tiene nada que ofrecer: se va al
   * login, que es donde el problema **sí** se resuelve.
   */
  elegir(fila: FilaSelector): void {
    this.error = '';
    this.pin = '';

    if (fila.estado !== 'activable') {
      void this.irAlLogin();
      return;
    }

    this.elegida = fila;
  }

  volverALaLista(): void {
    this.elegida = null;
    this.pin = '';
    this.error = '';
  }

  /** El botón solo se habilita con el PIN completo: sin esto se gasta un intento por un dedo lento. */
  get pinCompleto(): boolean {
    return this.pin.length === DIGITOS_PIN;
  }

  /** Descarta todo lo que no sea dígito y corta en el largo del PIN. */
  alEscribirPin(valor: string): void {
    this.pin = (valor ?? '').replace(/\D/g, '').slice(0, DIGITOS_PIN);
  }

  async activar(): Promise<void> {
    if (!this.elegida || !this.pinCompleto || this.activando) {
      return;
    }

    this.activando = true;
    this.error = '';

    const resultado = await this.llavero.activar(this.elegida.slot.slotId, this.pin);
    this.pin = '';

    switch (resultado.estado) {
      case 'activado':
        // No se apaga `activando`: la página se está por ir. Dejarlo en false haría parpadear el
        // formulario habilitado durante la recarga.
        this.recargarPagina();
        return;

      case 'pin_incorrecto':
        // Se le dice cuántos quedan (decisión del usuario, 18ago26): un contador a ciegas hace que el
        // operario descubra la destrucción del slot recién cuando ya pasó.
        this.error =
          resultado.intentosRestantes === 1
            ? 'PIN incorrecto. Te queda 1 intento antes de tener que entrar con red.'
            : `PIN incorrecto. Te quedan ${resultado.intentosRestantes} intentos.`;
        break;

      case 'slot_destruido':
        this.error =
          'Se agotaron los intentos. Esta sesión guardada se borró del equipo: entrá con red para volver a usarla. ' +
          'Lo que hayas capturado sin enviar sigue en la tablet.';
        this.elegida = null;
        await this.recargar();
        break;

      case 'no_disponible':
        this.error = 'No se puede abrir esta sesión en este equipo. Entrá con red.';
        this.elegida = null;
        await this.recargar();
        break;
    }

    this.activando = false;
  }

  irAlLogin(): Promise<boolean> {
    return this.router.navigate(['/login']);
  }

  /**
   * Aislado en un método para poder verificarlo en los tests: `location.reload()` en un spec recarga
   * el runner y se lleva puesta la suite entera.
   */
  protected recargarPagina(): void {
    window.location.reload();
  }
}
