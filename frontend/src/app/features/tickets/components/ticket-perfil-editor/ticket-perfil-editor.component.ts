// src/app/features/tickets/components/ticket-perfil-editor/ticket-perfil-editor.component.ts
import {
  Component, EventEmitter, Input, OnInit, Output, inject, signal, ChangeDetectorRef,
} from '@angular/core';

import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  TicketPerfilService, TicketPerfilDto, TicketResolutorRolDto,
  ResolutorItemRequest,
} from '../../services/ticket-perfil.service';
import { ToastService } from '../../../../shared/services/toast.service';
import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { TIPOS_TICKET } from '../../models/ticket.models';

type Modo = 'usuario' | 'rol';

export interface PaisOpcion {
  id: number | null;  // null = Global
  label: string;
}

/**
 * Editor de perfiles de atención de tickets.
 * Se embebe en el módulo Usuarios (modo='usuario', entityId=guid)
 * y en Roles (modo='rol', entityId=roleId string).
 */
@Component({
  selector: 'app-ticket-perfil-editor',
  standalone: true,
  imports: [FormsModule],
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
  private readonly cdr = inject(ChangeDetectorRef);

  readonly tipos = TIPOS_TICKET;
  /** Niveles de solicitante (referencia estable para el template). */
  readonly niveles = [
    { v: 'NORMAL',        label: 'Normal',        desc: 'Crea Soporte y Dudas' },
    { v: 'IMPLEMENTADOR', label: 'Implementador', desc: 'Crea además Desarrollo y Requerimiento' },
  ];
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly reaplicando = signal(false);

  /** Vacío si no hay perfil guardado (evita pre-selección sin historial). */
  nivel = '';
  /** Estado del toggle por tipo: true = activo como resolutor */
  resolutorActivo: Record<string, boolean> = {};
  /** País por tipo (null = global) */
  resolutorPais: Record<string, number | null> = {};

  /** Lista de países disponibles para el selector, construida desde el storage. */
  paises: PaisOpcion[] = [];
  /** true si el usuario tiene rol Admin (puede ver/asignar todos los países). */
  esAdmin = false;

  /** Construye la lista de países desde la sesión del storage. */
  private buildPaises(): void {
    const session = this.storage.get();
    if (!session) return;

    // Detectar Admin: tiene permiso tickets.admin O su rol contiene 'Admin'
    const permisos = session.user.permisos ?? [];
    const roles    = session.user.roles ?? [];
    this.esAdmin = permisos.includes('tickets.admin') ||
                   roles.some(r => r.toLowerCase().includes('admin'));

    // Opción Global siempre disponible para admins o cuando se configura un rol
    const opciones: PaisOpcion[] = [
      { id: null, label: '🌍 Todos los países (Global)' },
    ];

    if (this.esAdmin) {
      // Admin: agrega todos los países únicos de companyPaises
      const vistos = new Set<number>();
      for (const cp of session.companyPaises ?? []) {
        if (!vistos.has(cp.paisId)) {
          opciones.push({ id: cp.paisId, label: `${cp.paisNombre} (#${cp.paisId})` });
          vistos.add(cp.paisId);
        }
      }
      // Si no hay companyPaises pero sí activePaisId, lo agrega
      if (vistos.size === 0 && session.activePaisId) {
        opciones.push({ id: session.activePaisId, label: `${session.activePaisNombre ?? 'Mi país'} (#${session.activePaisId})` });
      }
    } else {
      // No-admin: solo muestra su propio país activo
      if (session.activePaisId) {
        opciones.push({
          id: session.activePaisId,
          label: `${session.activePaisNombre ?? 'Mi país'} (#${session.activePaisId})`,
        });
      }
    }

    this.paises = opciones;

    // Pre-seleccionar el país por defecto para nuevos toggles
    // (el país activo del usuario, o null=global si es admin)
    this._defaultPaisId = this.esAdmin ? null : (session.activePaisId ?? null);
  }

  private _defaultPaisId: number | null = null;

  /** Devuelve el nombre del país para el chip informativo. */
  getNombrePais(id: number | null): string {
    if (id === null) return 'todos los países';
    return this.paises.find(p => p.id === id)?.label ?? `País #${id}`;
  }

  ngOnInit(): void {
    this.buildPaises();
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    const onData = (dto: TicketPerfilDto | TicketResolutorRolDto) => {
      if ('nivel' in dto) {
        // Solo pre-seleccionar nivel si hay un perfil guardado previamente.
        const perfilDto = dto as TicketPerfilDto;
        if (perfilDto.hasProfile) this.nivel = perfilDto.nivel;
        // else: nivel queda '' → ningún radio pre-seleccionado.
      }
      for (const t of this.tipos) {
        this.resolutorActivo[t.value] = false;
        this.resolutorPais[t.value] = this._defaultPaisId;
      }
      for (const r of dto.resolutores) {
        if (r.activo) {
          this.resolutorActivo[r.tipo] = true;
          this.resolutorPais[r.tipo] = r.paisId;
        }
      }
      this.loading.set(false);
    };
    const onError = () => { this.loading.set(false); this.toast.error('No se pudo cargar el perfil.'); };

    if (this.modo === 'usuario') {
      this.svc.getPerfilUsuario(this.entityId).subscribe({ next: onData, error: onError });
    } else {
      this.svc.getPerfilRol(Number(this.entityId)).subscribe({ next: onData, error: onError });
    }
  }

  save(): void {
    const resolutores: ResolutorItemRequest[] = this.tipos
      .filter(t => this.resolutorActivo[t.value])
      .map(t => ({ tipo: t.value, paisId: this.resolutorPais[t.value] ?? null }));

    this.saving.set(true);
    const onDone = () => { this.saving.set(false); this.toast.success('Perfil de atención guardado.'); this.saved.emit(); };
    const onError = () => { this.saving.set(false); this.toast.error('No se pudo guardar el perfil.'); };

    if (this.modo === 'usuario') {
      this.svc.upsertPerfilUsuario(this.entityId, { nivel: this.nivel, resolutores }).subscribe({ next: onDone, error: onError });
    } else {
      this.svc.upsertPerfilRol(Number(this.entityId), { resolutores }).subscribe({ next: onDone, error: onError });
    }
  }

  /**
   * Re-aplica la plantilla del rol a todos los usuarios que lo tengan (solo modo='rol').
   * Idempotente: solo agrega lo faltante, no borra ajustes por usuario.
   */
  reaplicarPlantilla(): void {
    if (this.modo !== 'rol') return;
    const ok = confirm(
      'Se aplicará esta plantilla de atención a TODOS los usuarios que tengan este rol.\n' +
      'Solo se agregan los tipos faltantes; no se quitan los ajustes que cada usuario haya hecho.\n\n¿Continuar?'
    );
    if (!ok) return;

    this.reaplicando.set(true);
    this.svc.reaplicarPlantillaRol(Number(this.entityId))
      .pipe(finalize(() => this.reaplicando.set(false)))
      .subscribe({
        next: () => this.toast.success('Plantilla aplicada a los usuarios del rol.'),
        error: () => this.toast.error('No se pudo aplicar la plantilla a los usuarios.'),
      });
  }
}
