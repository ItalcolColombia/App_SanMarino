// src/app/features/tickets/components/ticket-perfil-editor/ticket-perfil-editor.component.ts
import {
  Component, EventEmitter, Input, OnInit, Output, inject, signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
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
  imports: [CommonModule, FormsModule],
  templateUrl: './ticket-perfil-editor.component.html',
})
export class TicketPerfilEditorComponent implements OnInit {
  @Input({ required: true }) modo!: Modo;
  /** Para modo='usuario': Guid del usuario. Para modo='rol': roleId como string. */
  @Input({ required: true }) entityId!: string;
  @Output() saved = new EventEmitter<void>();

  private readonly svc = inject(TicketPerfilService);
  private readonly toast = inject(ToastService);
  private readonly storage = inject(TokenStorageService);

  readonly tipos = TIPOS_TICKET;
  readonly loading = signal(false);
  readonly saving = signal(false);

  nivel = 'NORMAL';
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
      if ('nivel' in dto) this.nivel = dto.nivel;
      for (const t of this.tipos) {
        this.resolutorActivo[t.value] = false;
        // Pre-selecciona el país del usuario como default de cada toggle
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
}
