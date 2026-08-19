// src/app/features/config/user-management/components/sesiones-usuario/sesiones-usuario.component.ts
import {
  ChangeDetectionStrategy, Component, EventEmitter, inject, Input, OnChanges, Output, SimpleChanges
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faDesktop, faMobileScreen, faTimes, faRotate, faBan, faCircleCheck, faTriangleExclamation
} from '@fortawesome/free-solid-svg-icons';

import { SessionAdminService, SesionActiva } from '../../../../../core/services/session/session-admin.service';
import { ConfirmDialogService } from '../../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../../shared/services/toast.service';
import { describirDispositivo, estadoDeSesion, EstadoSesionUi } from '../../../../../shared/utils/sesiones/describir-sesion.funcion';

/**
 * Sesiones abiertas de un usuario, con el botón de revocar (B1).
 *
 * Es lo que se abre cuando alguien reporta que perdió una tablet: se ve qué dispositivos tienen
 * sesión, cuándo fue el último contacto de cada uno, y se apaga el que corresponda.
 *
 * ⚠️ **La revocación no es instantánea** y la pantalla lo dice: surte efecto en menos de un minuto
 * desde que ese dispositivo toque la red (la verificación se cachea 60 s por tarea del servidor).
 * Prometer «inmediato» sería mentir sobre lo único que importa acá.
 *
 * `Eager` explícito: tiene `subscribe` y estado mutable — con OnPush el spinner queda colgado
 * aunque la red haya devuelto 200 (regla del repo para todo componente/modal nuevo).
 */
@Component({
  selector: 'app-sesiones-usuario',
  standalone: true,
  imports: [DatePipe, FontAwesomeModule],
  templateUrl: './sesiones-usuario.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./sesiones-usuario.component.scss']
})
export class SesionesUsuarioComponent implements OnChanges {
  /** Abierto/cerrado, controlado por la pantalla que lo hospeda. */
  @Input() isOpen = false;

  /** Guid del usuario cuyas sesiones se listan. */
  @Input() userId: string | null = null;

  /** Nombre a mostrar en el encabezado. */
  @Input() userName = '';

  @Output() close = new EventEmitter<void>();

  faDesktop = faDesktop;
  faMobileScreen = faMobileScreen;
  faTimes = faTimes;
  faRotate = faRotate;
  faBan = faBan;
  faCircleCheck = faCircleCheck;
  faTriangleExclamation = faTriangleExclamation;

  sesiones: SesionActiva[] = [];
  cargando = false;
  error = '';
  revocandoId: number | null = null;

  private readonly api = inject(SessionAdminService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['isOpen'] && this.isOpen) {
      this.cargar();
    }
  }

  cargar(): void {
    if (!this.userId) return;

    this.cargando = true;
    this.error = '';

    this.api.deUsuario(this.userId).subscribe({
      next: (filas) => {
        this.sesiones = filas ?? [];
        this.cargando = false;
      },
      error: () => {
        this.error = 'No se pudieron cargar las sesiones.';
        this.cargando = false;
      }
    });
  }

  async revocar(sesion: SesionActiva): Promise<void> {
    const confirmado = await this.confirmDialog.ask({
      title: 'Cerrar esta sesión',
      message:
        `Se cerrará la sesión de ${describirDispositivo(sesion)}.\n\n` +
        'Surte efecto en menos de un minuto desde que ese dispositivo toque la red. ' +
        'Si el equipo tiene capturas sin enviar, no se pierden, pero no van a poder subir ' +
        'hasta que alguien vuelva a iniciar sesión ahí.',
      type: 'warning',
      confirmText: 'Cerrar sesión',
      preformatted: true
    });

    if (!confirmado) return;

    this.revocandoId = sesion.id;
    this.api.revocar(sesion.id, 'Revocada desde administración de usuarios').subscribe({
      next: () => {
        this.revocandoId = null;
        this.toast.success('Sesión cerrada. Surte efecto en menos de un minuto.');
        this.cargar();
      },
      error: () => {
        this.revocandoId = null;
        this.toast.error('No se pudo cerrar la sesión.');
      }
    });
  }

  async revocarTodas(): Promise<void> {
    if (!this.userId || this.sesiones.length === 0) return;

    const confirmado = await this.confirmDialog.ask({
      title: 'Cerrar TODAS las sesiones',
      message:
        `Se cerrarán las ${this.sesiones.length} sesiones abiertas de ${this.userName}.\n\n` +
        'Va a tener que iniciar sesión de nuevo en cada equipo, y para eso necesita red.',
      type: 'error',
      confirmText: 'Cerrar todas',
      preformatted: true
    });

    if (!confirmado) return;

    this.cargando = true;
    this.api.revocarTodas(this.userId, 'Revocación masiva desde administración').subscribe({
      next: (r) => {
        this.toast.success(`${r?.revocadas ?? 0} sesiones cerradas.`);
        this.cargar();
      },
      error: () => {
        this.cargando = false;
        this.toast.error('No se pudieron cerrar las sesiones.');
      }
    });
  }

  cerrar(): void {
    this.close.emit();
  }

  /** Texto legible del equipo (marca/sistema o el id del dispositivo). */
  dispositivo(sesion: SesionActiva): string {
    return describirDispositivo(sesion);
  }

  /** Estado con el que se pinta la fila. */
  estado(sesion: SesionActiva): EstadoSesionUi {
    return estadoDeSesion(sesion, Date.now());
  }
}
