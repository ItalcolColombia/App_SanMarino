// src/app/features/tickets/components/ticket-perfil-editor/ticket-perfil-editor.component.ts
import {
  Component, EventEmitter, Input, OnInit, Output, inject, signal, ChangeDetectorRef,
  ChangeDetectionStrategy
} from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { FormsModule } from '@angular/forms';
import {
  TicketPerfilService, TicketPerfilDto, TicketResolutorRolDto,
  ResolutorItemRequest, AlcanceResolutor,
} from '../../services/ticket-perfil.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { esAdminDeAplicacion } from '../../../config/role-management/funciones/catalogos-globales.funcion';
import { estadoDesdeFilas, resumenAtiende } from '../../funciones/estado-resolutores.funcion';
import { TIPOS_TICKET } from '../../models/ticket.models';
import { environment } from '../../../../../environments/environment';

type Modo = 'usuario' | 'rol';

interface EmpresaOpcion { id: number; name: string; }

/**
 * Editor de la configuración de tickets.
 *
 * Dos preguntas distintas, nunca mezcladas (ver
 * `fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md`):
 * - **modo='rol'** (Roles → Tickets): ① qué puede ABRIR quien tenga el rol y ② qué ATIENDE el rol.
 *   Antes esta pantalla solo tenía ②, así que quien quería habilitar la apertura convertía a todo el
 *   rol en resolutor sin darse cuenta.
 * - **modo='usuario'** (Usuarios → Tickets): la excepción personal de apertura, con lo que ya le da su
 *   rol a la vista.
 *
 * El alcance del resolutor es explícito: **esta empresa** o **todas las empresas (Global)**, y Global
 * solo lo puede elegir el admin global. Antes la etiqueta «Global» era el país nulo de la fila.
 */
@Component({
  selector: 'app-ticket-perfil-editor',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './ticket-perfil-editor.component.html',
})
export class TicketPerfilEditorComponent implements OnInit {
  @Input({ required: true }) modo!: Modo;
  /** Para modo='usuario': Guid del usuario. Para modo='rol': roleId como string. */
  @Input({ required: true }) entityId!: string;
  /** Cuando es true, oculta el footer con los botones de guardado (el padre controla el save). */
  @Input() hideSaveButton = false;
  @Output() saved = new EventEmitter<void>();
  /** Emite cada vez que el usuario modifica nivel o toggles (para que el padre detecte cambios pendientes). */
  @Output() changed = new EventEmitter<void>();

  private readonly svc = inject(TicketPerfilService);
  private readonly toast = inject(ToastService);
  private readonly storage = inject(TokenStorageService);
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly tipos = TIPOS_TICKET;
  /** Niveles de apertura (referencia estable para el template). */
  readonly niveles = [
    { v: 'NORMAL',        label: 'Normal',        desc: 'Abre Soporte y Dudas' },
    { v: 'IMPLEMENTADOR', label: 'Implementador', desc: 'Abre además Desarrollo y Requerimiento' },
  ];
  readonly loading = signal(false);
  readonly saving = signal(false);

  /** modo='usuario': nivel del perfil PERSONAL. Vacío = sin perfil guardado. */
  nivel = '';
  /** modo='usuario': lo que ya le dan sus roles (informativo). */
  nivelPorRol: string | null = null;
  /** modo='rol': nivel que el rol le da a su gente. Vacío = el rol no lo define ⇒ Normal. */
  nivelCreacionRol = '';

  /** Estado del toggle por tipo: true = el rol atiende ese tipo. */
  resolutorActivo: Record<string, boolean> = {};
  /** Alcance por tipo: EMPRESA (esta empresa) | GLOBAL (todas). */
  resolutorAlcance: Record<string, AlcanceResolutor> = {};
  /** País de la fila existente. Ya no se edita: la empresa manda. Se conserva para no pisarlo. */
  resolutorPais: Record<string, number | null> = {};

  /** Empresa donde vive la configuración (la del usuario/rol, no la del que edita). */
  companyId: number | null = null;
  companyName: string | null = null;
  /** ¿La sesión puede marcar GLOBAL y mirar otras empresas? (admin global). */
  puedeElegirGlobal = false;
  /** Solo para el admin global: empresas donde puede pararse a configurar. */
  empresas: EmpresaOpcion[] = [];
  /** Mensaje del backend cuando la configuración no se puede leer (empresa ambigua, sin permiso). */
  errorCarga: string | null = null;

  /** Resolutores personales activos (modo usuario): se muestran, no se editan acá. */
  atiendePersonal: { tipo: string; label: string; etiqueta: string }[] = [];

  ngOnInit(): void {
    const session = this.storage.get();
    this.puedeElegirGlobal = !!session?.user?.isSuperAdmin || esAdminDeAplicacion(session?.user?.roles ?? []);
    if (this.puedeElegirGlobal) this.cargarEmpresas();
    this.load();
  }

  private cargarEmpresas(): void {
    // Ruta "global" (no "admin"): el WAF bloquea cualquier path con /admin.
    this.http.get<EmpresaOpcion[]>(`${environment.apiUrl}/Company/global`).subscribe({
      next: e => { this.empresas = e ?? []; this.cdr.detectChanges(); },
      error: () => {},
    });
  }

  /** Cambiar de empresa recarga: cada empresa tiene su propia configuración. */
  cambiarEmpresa(valor: string | number | null): void {
    const id = valor === null || valor === '' ? null : Number(valor);
    this.companyId = id;
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.errorCarga = null;

    const onData = (dto: TicketPerfilDto | TicketResolutorRolDto) => {
      this.companyId = dto.companyId ?? this.companyId ?? null;
      this.companyName = dto.companyName ?? null;
      if (dto.puedeElegirGlobal !== undefined) this.puedeElegirGlobal = dto.puedeElegirGlobal;

      if ('nivel' in dto) {
        const perfil = dto as TicketPerfilDto;
        // Solo pre-seleccionar nivel si hay un perfil guardado previamente.
        this.nivel = perfil.hasProfile ? perfil.nivel : '';
        this.nivelPorRol = perfil.nivelPorRol ?? null;
      } else {
        // '' y 'NORMAL' son lo mismo en efecto (Soporte y Dudas): la pantalla muestra dos opciones.
        this.nivelCreacionRol =
          (dto as TicketResolutorRolDto).nivelCreacion === 'IMPLEMENTADOR' ? 'IMPLEMENTADOR' : '';
      }

      const estado = estadoDesdeFilas(this.tipos.map(t => t.value), dto.resolutores);
      this.resolutorActivo = estado.activo;
      this.resolutorAlcance = estado.alcance;
      this.resolutorPais = estado.pais;
      this.atiendePersonal = resumenAtiende(dto.resolutores, this.companyName,
        tipo => this.tipos.find(t => t.value === tipo)?.label ?? tipo);

      this.loading.set(false);
    };

    const onError = (err: unknown) => {
      this.loading.set(false);
      this.errorCarga = this.mensajeDeError(err) ?? 'No se pudo cargar la configuración de tickets.';
      this.toast.error(this.errorCarga);
    };

    if (this.modo === 'usuario') {
      this.svc.getPerfilUsuario(this.entityId, this.companyId).subscribe({ next: onData, error: onError });
    } else {
      this.svc.getPerfilRol(Number(this.entityId), this.companyId).subscribe({ next: onData, error: onError });
    }
  }

  /** Ítems de resolutor que la pantalla tiene prendidos, con su alcance explícito. */
  construirResolutores(): ResolutorItemRequest[] {
    return this.tipos
      .filter(t => this.resolutorActivo[t.value])
      .map(t => ({
        tipo: t.value,
        paisId: this.resolutorPais[t.value] ?? null,
        alcance: this.resolutorAlcance[t.value] ?? 'EMPRESA',
      }));
  }

  /** Lo que el padre manda al guardar un rol (Roles → Tickets). */
  construirRequestRol() {
    return {
      resolutores: this.construirResolutores(),
      nivelCreacion: this.nivelCreacionRol ?? '',
      companyId: this.companyId,
    };
  }

  alternarResolutor(tipo: string): void {
    this.resolutorActivo[tipo] = !this.resolutorActivo[tipo];
    if (this.resolutorActivo[tipo] && !this.resolutorAlcance[tipo]) this.resolutorAlcance[tipo] = 'EMPRESA';
    this.changed.emit();
  }

  cambiarAlcance(tipo: string, alcance: AlcanceResolutor): void {
    this.resolutorAlcance[tipo] = alcance;
    this.changed.emit();
  }

  save(): void {
    this.saving.set(true);
    const onDone = () => {
      this.saving.set(false);
      this.toast.success('Configuración de tickets guardada.');
      this.saved.emit();
    };
    const onError = (err: unknown) => {
      this.saving.set(false);
      this.toast.error(this.mensajeDeError(err) ?? 'No se pudo guardar la configuración.');
    };

    if (this.modo === 'usuario') {
      this.svc.upsertPerfilUsuario(this.entityId, {
        nivel: this.nivel,
        resolutores: this.construirResolutores(),
        companyId: this.companyId,
      }).subscribe({ next: onDone, error: onError });
    } else {
      this.svc.upsertPerfilRol(Number(this.entityId), this.construirRequestRol())
        .subscribe({ next: onDone, error: onError });
    }
  }

  /** Mensaje del backend (403/400 traen el motivo concreto: es lo que hay que mostrar). */
  private mensajeDeError(err: unknown): string | null {
    const cuerpo = (err as { error?: unknown })?.error;
    if (typeof cuerpo === 'string' && cuerpo.trim()) return cuerpo;
    const mensaje = (cuerpo as { message?: string })?.message;
    return mensaje?.trim() ? mensaje : null;
  }
}
