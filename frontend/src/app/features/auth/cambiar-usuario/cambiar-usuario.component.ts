import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';
import { LlaveroSesionesService } from '../../../core/auth/llavero-sesiones.service';
import { TokenStorageService } from '../../../core/auth/token-storage.service';
import { ConexionService } from '../../../core/pwa/conexion.service';
import { OutboxService } from '../../../shared/offline/outbox.service';
import { SyncService } from '../../../shared/offline/sync.service';
import { DIGITOS_PIN } from '../selector-usuario/selector-usuario.component';

/**
 * Aparcar la sesión propia para que entre otro operario en la misma tablet.
 *
 * ## Por qué se pide el PIN dos veces
 *
 * El PIN **no se guarda en ninguna parte**: es la entrada del KDF que cifra la sesión. Un dedo torcido
 * acá no se nota hoy —el blob se escribe igual— y se cobra mañana, en el galpón, como cinco intentos
 * fallidos y la sesión destruida. Confirmarlo es la única red de contención posible.
 *
 * ## Por qué NO se purga nada
 *
 * Aparcar no es cerrar sesión: quien aparca vuelve, a veces en media hora, y su caché es justamente lo
 * que le permite trabajar sin red al volver. Purgarla le costaría otra vez el alistamiento (R-M6).
 *
 * `changeDetection: Eager` — estado mutable llenado desde promesas (cripto, IndexedDB).
 */
@Component({
  selector: 'app-cambiar-usuario',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './cambiar-usuario.component.html'
})
export class CambiarUsuarioComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly llavero = inject(LlaveroSesionesService);
  private readonly storage = inject(TokenStorageService);
  private readonly outbox = inject(OutboxService);
  private readonly sync = inject(SyncService);
  private readonly conexion = inject(ConexionService);
  private readonly router = inject(Router);

  readonly digitosPin = DIGITOS_PIN;

  nombre = '';
  pin = '';
  confirmacion = '';
  error = '';
  aparcando = false;

  /** El llavero no existe en este equipo: no hay forma de aparcar sin cifrar, y no se cifra a medias. */
  sinLlavero = false;

  /** Capturas propias sin enviar. Se muestran porque quedan en la tablet hasta que el dueño vuelva. */
  pendientes = 0;
  enviando = false;
  mensajeEnvio = '';

  async ngOnInit(): Promise<void> {
    this.sinLlavero = !this.llavero.disponible();
    this.nombre = this.storage.get()?.user?.fullName?.trim() || this.storage.get()?.user?.username || 'tu sesión';
    await this.contarPendientes();
  }

  get hayRed(): boolean {
    return this.conexion.hayConexionReal();
  }

  get pinCompleto(): boolean {
    return this.pin.length === DIGITOS_PIN && this.confirmacion.length === DIGITOS_PIN;
  }

  get coinciden(): boolean {
    return this.pin === this.confirmacion;
  }

  alEscribirPin(valor: string): void {
    this.pin = soloDigitos(valor);
    this.error = '';
  }

  alEscribirConfirmacion(valor: string): void {
    this.confirmacion = soloDigitos(valor);
    this.error = '';
  }

  /** Enviar antes de irse. Solo tiene sentido con red; sin red el aviso es información, no un bloqueo. */
  async enviarPendientes(): Promise<void> {
    this.enviando = true;
    this.mensajeEnvio = '';

    const confirmadas = await this.sync.sincronizar();
    await this.contarPendientes();

    this.enviando = false;
    this.mensajeEnvio =
      confirmadas > 0 ? `Se enviaron ${confirmadas} captura(s).` : 'No se pudo enviar nada todavía.';
  }

  async aparcar(): Promise<void> {
    if (this.aparcando || this.sinLlavero) {
      return;
    }

    if (!this.pinCompleto) {
      this.error = `El PIN tiene que ser de ${DIGITOS_PIN} dígitos.`;
      return;
    }

    if (!this.coinciden) {
      // Se limpia solo la confirmación: reescribir los 12 dígitos por un error de tipeo en los
      // últimos 6 es exactamente cómo se termina eligiendo un PIN más corto de memoria.
      this.error = 'Los dos PIN no coinciden. Escribilos de nuevo.';
      this.confirmacion = '';
      return;
    }

    this.aparcando = true;
    this.error = '';

    if (await this.auth.cambiarDeUsuario(this.pin)) {
      // La sesión ya salió del storage; el selector es la única pantalla que corresponde ahora.
      await this.router.navigate(['/selector-usuario'], { replaceUrl: true });
      return;
    }

    // No se pudo cifrar: la sesión sigue activa y no se perdió nada.
    this.aparcando = false;
    this.error = 'No se pudo guardar la sesión en este equipo. Seguís conectado; probá de nuevo o cerrá sesión.';
  }

  cancelar(): Promise<boolean> {
    return this.router.navigate(['/home']);
  }

  private async contarPendientes(): Promise<void> {
    const estado = await this.outbox.estado();
    this.pendientes = estado.pendientes + estado.rechazadas;
  }
}

function soloDigitos(valor: string): string {
  return (valor ?? '').replace(/\D/g, '').slice(0, DIGITOS_PIN);
}
