// src/app/features/profile/components/mis-dispositivos/mis-dispositivos.component.ts
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faBan, faCircleCheck, faDesktop, faMobileScreen, faRotate, faTriangleExclamation
} from '@fortawesome/free-solid-svg-icons';

import { SessionAdminService, SesionActiva } from '../../../../core/services/session/session-admin.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { ToastService } from '../../../../shared/services/toast.service';
import {
  describirDispositivo, esDispositivoMovil, estadoDeSesion, haceCuanto, EstadoSesionUi
} from '../../../../shared/utils/sesiones/describir-sesion.funcion';

/**
 * «Mis dispositivos»: los equipos donde mi usuario tiene la sesión abierta, con el botón de
 * cerrarlos.
 *
 * Es lo que permite que quien pierde una tablet **actúe sin esperar a un administrador**, que en la
 * práctica es la diferencia entre cerrar la puerta en dos minutos o el lunes a la mañana.
 *
 * `Eager` explícito: estado mutable + `subscribe` (regla del repo).
 */
@Component({
  selector: 'app-mis-dispositivos',
  standalone: true,
  imports: [DatePipe, FontAwesomeModule],
  templateUrl: './mis-dispositivos.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrls: ['./mis-dispositivos.component.scss']
})
export class MisDispositivosComponent implements OnInit {
  faDesktop = faDesktop;
  faMobileScreen = faMobileScreen;
  faRotate = faRotate;
  faBan = faBan;
  faCircleCheck = faCircleCheck;
  faTriangleExclamation = faTriangleExclamation;

  sesiones: SesionActiva[] = [];
  cargando = false;
  error = '';
  cerrandoId: number | null = null;

  private readonly api = inject(SessionAdminService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {
    this.cargar();
  }

  cargar(): void {
    this.cargando = true;
    this.error = '';

    this.api.mias().subscribe({
      next: (filas) => {
        this.sesiones = filas ?? [];
        this.cargando = false;
      },
      error: () => {
        this.error = 'No se pudieron cargar tus dispositivos.';
        this.cargando = false;
      }
    });
  }

  async cerrar(sesion: SesionActiva): Promise<void> {
    // Cerrar la sesión desde la que se está mirando te deja afuera acá mismo: hay que decirlo
    // antes, no después.
    const mensaje = sesion.esLaActual
      ? 'Estás cerrando LA SESIÓN QUE ESTÁS USANDO. Vas a salir de la aplicación y tendrás que ' +
        'entrar de nuevo, y para eso necesitás red.'
      : `Se cerrará la sesión de ${describirDispositivo(sesion)}.\n\n` +
        'Surte efecto en menos de un minuto desde que ese equipo toque la red.';

    const confirmado = await this.confirmDialog.ask({
      title: sesion.esLaActual ? 'Cerrar esta sesión' : 'Cerrar sesión en ese equipo',
      message: mensaje,
      type: sesion.esLaActual ? 'error' : 'warning',
      confirmText: 'Cerrar sesión',
      preformatted: true
    });

    if (!confirmado) return;

    this.cerrandoId = sesion.id;
    this.api.cerrarMia(sesion.id, 'Cerrada por el propio usuario').subscribe({
      next: () => {
        this.cerrandoId = null;
        this.toast.success('Sesión cerrada.');
        this.cargar();
      },
      error: () => {
        this.cerrandoId = null;
        this.toast.error('No se pudo cerrar la sesión.');
      }
    });
  }

  dispositivo(sesion: SesionActiva): string {
    return describirDispositivo(sesion);
  }

  esMovil(sesion: SesionActiva): boolean {
    return esDispositivoMovil(sesion);
  }

  estado(sesion: SesionActiva): EstadoSesionUi {
    return estadoDeSesion(sesion, Date.now());
  }

  ultimoContacto(sesion: SesionActiva): string {
    return haceCuanto(sesion.lastSeenAt, Date.now());
  }
}
