// src/app/features/tickets/components/ticket-timeline/ticket-timeline.component.ts
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { TicketTimelineEvento, TipoEventoTimeline } from '../../models/ticket-tarea.models';
import { fechaHoraCorta } from '../../../../shared/utils/format';

/** Estilo visual de cada tipo de evento: color del punto y trazo del icono. */
interface EstiloEvento { punto: string; anillo: string; icono: string; }

const ESTILOS: Record<TipoEventoTimeline, EstiloEvento> = {
  CREADO:       { punto: 'bg-ital-orange',  anillo: 'ring-ital-orange/20',  icono: 'M12 4.5v15m7.5-7.5h-15' },
  APERTURA:     { punto: 'bg-amber-500',    anillo: 'ring-amber-500/20',    icono: 'M13 10V3L4 14h7v7l9-11h-7Z' },
  ASIGNADO:     { punto: 'bg-sky-500',      anillo: 'ring-sky-500/20',      icono: 'M15 19.128a9.4 9.4 0 0 0 2.625.372 9.3 9.3 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.3 12.3 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z' },
  ESTADO:       { punto: 'bg-indigo-500',   anillo: 'ring-indigo-500/20',   icono: 'M3 7.5 7.5 3m0 0L12 7.5M7.5 3v13.5m13.5 0L16.5 21m0 0L12 16.5m4.5 4.5V7.5' },
  COMENTARIO:   { punto: 'bg-slate-400',    anillo: 'ring-slate-400/20',    icono: 'M8 10.5h8m-8 4h5m-6.6 5.4L3 21V6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H7.4a2 2 0 0 0-1.4.6Z' },
  SISTEMA:      { punto: 'bg-violet-500',   anillo: 'ring-violet-500/20',   icono: 'M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.28c.063.375.313.686.646.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7 7 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.828c.424.35.534.954.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6 6 0 0 1-.22.128c-.332.183-.582.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.02-.397-1.11-.94l-.213-1.28c-.062-.375-.312-.687-.644-.87a6 6 0 0 1-.22-.128c-.326-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a7 7 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.248a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124q.108-.066.22-.128c.333-.183.583-.495.645-.869zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0' },
  ADJUNTO:      { punto: 'bg-cyan-500',     anillo: 'ring-cyan-500/20',     icono: 'm18.375 12.739-7.693 7.693a4.5 4.5 0 0 1-6.364-6.364l10.94-10.94A3 3 0 1 1 19.5 7.372L8.552 18.32m.009-.01-.01.01m5.699-9.941-7.81 7.81a1.5 1.5 0 0 0 2.112 2.13' },
  TAREA:        { punto: 'bg-emerald-500',  anillo: 'ring-emerald-500/20',  icono: 'M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0' },
  TIEMPO:       { punto: 'bg-amber-600',    anillo: 'ring-amber-600/20',    icono: 'M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0' },
  SOLUCION:     { punto: 'bg-emerald-600',  anillo: 'ring-emerald-600/20',  icono: 'm4.5 12.75 6 6 9-13.5' },
  CIERRE:       { punto: 'bg-ital-green',   anillo: 'ring-ital-green/20',   icono: 'M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25' },
  NOTIFICACION: { punto: 'bg-sky-400',      anillo: 'ring-sky-400/20',      icono: 'M21.75 6.75v10.5a2.25 2.25 0 0 1-2.25 2.25h-15a2.25 2.25 0 0 1-2.25-2.25V6.75m19.5 0A2.25 2.25 0 0 0 19.5 4.5h-15a2.25 2.25 0 0 0-2.25 2.25m19.5 0v.243a2.25 2.25 0 0 1-1.07 1.916l-7.5 4.615a2.25 2.25 0 0 1-2.36 0L3.32 8.91a2.25 2.25 0 0 1-1.07-1.916V6.75' },
};

const ESTILO_DEFECTO: EstiloEvento = ESTILOS.COMENTARIO;

/**
 * Línea de tiempo vertical de un caso: un punto por evento, con icono y color según el tipo.
 * Presentacional puro (solo `@Input`) ⇒ OnPush es la estrategia correcta.
 */
@Component({
  selector: 'app-ticket-timeline',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!eventos?.length) {
      <p class="py-6 text-center text-sm text-ital-muted">Todavía no hay actividad registrada.</p>
    } @else {
      <ol class="relative space-y-0">
        @for (e of eventos; track $index; let last = $last) {
          <li class="relative flex gap-3 pb-5" [class.pb-0]="last">
            <!-- Riel vertical -->
            @if (!last) {
              <span class="absolute left-[13px] top-7 bottom-0 w-px bg-slate-200" aria-hidden="true"></span>
            }

            <span class="relative z-10 mt-0.5 grid h-[27px] w-[27px] shrink-0 place-items-center rounded-full text-white ring-4 ring-white"
                  [class]="estilo(e.tipo).punto">
              <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
                <path stroke-linecap="round" stroke-linejoin="round" [attr.d]="estilo(e.tipo).icono" />
              </svg>
            </span>

            <div class="min-w-0 flex-1 rounded-xl px-3 py-2 transition-colors"
                 [class.bg-slate-50]="e.esInterna" [class.ring-1]="e.esInterna" [class.ring-slate-200]="e.esInterna">
              <div class="flex flex-wrap items-baseline gap-x-2 gap-y-0.5">
                <p class="text-sm font-semibold text-slate-800">{{ e.titulo }}</p>
                @if (e.esInterna) {
                  <span class="rounded bg-slate-200 px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide text-slate-600">Interna</span>
                }
                <span class="ml-auto shrink-0 text-[11px] text-ital-muted">{{ fechaHora(e.momento) }}</span>
              </div>

              @if (e.detalle) {
                <p class="mt-0.5 whitespace-pre-line break-words text-sm text-slate-600">{{ e.detalle }}</p>
              }
              @if (e.autor) {
                <p class="mt-1 text-[11px] font-medium text-ital-muted">{{ e.autor }}</p>
              }
            </div>
          </li>
        }
      </ol>
    }
  `,
})
export class TicketTimelineComponent {
  @Input() eventos: TicketTimelineEvento[] | null = null;

  estilo(tipo: TipoEventoTimeline): EstiloEvento { return ESTILOS[tipo] ?? ESTILO_DEFECTO; }

  fechaHora(iso: string): string { return fechaHoraCorta(iso); }
}
