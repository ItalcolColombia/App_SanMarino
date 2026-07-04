// src/app/features/tickets/components/image-lightbox/image-lightbox.component.ts
import {
  Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal,
  ChangeDetectionStrategy
} from '@angular/core';

import { finalize } from 'rxjs';
import { TicketService } from '../../services/ticket.service';
import { TicketImagenMeta } from '../../models/ticket.models';

/**
 * Visor de imágenes a pantalla completa. Carga el Base64 ON-DEMAND (solo al abrir),
 * vía HttpClient (el authInterceptor agrega el JWT). Cachea las imágenes ya cargadas.
 */
@Component({
  selector: 'app-image-lightbox',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (cur() !== null && imagenes[cur()!]) {
      <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 p-4" (click)="close()">
        <button type="button" class="absolute right-4 top-4 grid h-10 w-10 place-items-center rounded-full bg-white/10 text-white transition hover:bg-white/20"
          (click)="close(); $event.stopPropagation()" title="Cerrar">
          <svg class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>

        @if (cur()! > 0) {
          <button type="button" class="absolute left-4 grid h-10 w-10 place-items-center rounded-full bg-white/10 text-white transition hover:bg-white/20"
            (click)="prev(); $event.stopPropagation()">
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M15.75 19.5 8.25 12l7.5-7.5" />
            </svg>
          </button>
        }

        <div class="max-h-full max-w-4xl" (click)="$event.stopPropagation()">
          @if (loading()) {
            <div class="flex h-64 w-64 items-center justify-center">
              <span class="h-8 w-8 animate-spin rounded-full border-2 border-white border-t-transparent"></span>
            </div>
          } @else if (currentSrc()) {
            <img [src]="currentSrc()" [alt]="imagenes[cur()!].fileName ?? ''"
                 class="max-h-[80vh] rounded-lg object-contain shadow-2xl" />
          }
          <div class="mt-2 text-center text-sm text-white/70">
            {{ imagenes[cur()!].fileName }} · {{ cur()! + 1 }} / {{ imagenes.length }}
          </div>
        </div>

        @if (cur()! < imagenes.length - 1) {
          <button type="button" class="absolute right-4 grid h-10 w-10 place-items-center rounded-full bg-white/10 text-white transition hover:bg-white/20"
            (click)="next(); $event.stopPropagation()">
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
            </svg>
          </button>
        }
      </div>
    }
  `,
})
export class ImageLightboxComponent implements OnChanges {
  @Input({ required: true }) ticketId!: number;
  @Input({ required: true }) imagenes: TicketImagenMeta[] = [];
  /** Índice a abrir; null = cerrado. Controlado por el padre. */
  @Input() openIndex: number | null = null;
  @Output() closed = new EventEmitter<void>();

  private readonly svc = inject(TicketService);
  readonly loading = signal(false);
  readonly currentSrc = signal<string | null>(null);
  readonly cur = signal<number | null>(null);
  private readonly cache = new Map<number, string>();

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['openIndex']) {
      this.cur.set(this.openIndex);
      if (this.openIndex !== null) this.loadCurrent();
    }
  }

  private loadCurrent(): void {
    const i = this.cur();
    if (i === null) return;
    const meta = this.imagenes[i];
    if (!meta) return;

    const cached = this.cache.get(meta.id);
    if (cached) { this.currentSrc.set(cached); return; }

    this.loading.set(true);
    this.currentSrc.set(null);
    this.svc.getImagen(this.ticketId, meta.id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: img => { this.cache.set(meta.id, img.imagenBase64); this.currentSrc.set(img.imagenBase64); },
        error: () => this.currentSrc.set(null),
      });
  }

  prev(): void { const i = this.cur(); if (i !== null && i > 0) { this.cur.set(i - 1); this.loadCurrent(); } }
  next(): void { const i = this.cur(); if (i !== null && i < this.imagenes.length - 1) { this.cur.set(i + 1); this.loadCurrent(); } }
  close(): void { this.closed.emit(); }
}
