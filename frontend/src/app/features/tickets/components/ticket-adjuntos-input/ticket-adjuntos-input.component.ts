// src/app/features/tickets/components/ticket-adjuntos-input/ticket-adjuntos-input.component.ts
import {
  Component, EventEmitter, Input, OnDestroy, Output, inject, signal,
  ChangeDetectionStrategy
} from '@angular/core';

import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../../shared/services/toast.service';

export interface AdjuntoArchivoStaged {
  base64: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface AdjuntoLinkStaged {
  url: string;
  titulo: string;
}

export interface AdjuntosInputState {
  archivos: AdjuntoArchivoStaged[];
  links: AdjuntoLinkStaged[];
}

const ACCEPTED_TYPES: Record<string, string> = {
  'application/pdf': 'PDF',
  'application/msword': 'Word',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'Word',
  'application/vnd.ms-excel': 'Excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'Excel',
};

const ACCEPT_ATTR =
  '.pdf,.doc,.docx,.xls,.xlsx,application/pdf,application/msword,' +
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document,' +
  'application/vnd.ms-excel,' +
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

/**
 * Editor de adjuntos para el formulario de ticket.
 * Permite agregar archivos (PDF, Word, Excel) y links externos.
 * Emite AdjuntosInputState cada vez que cambia el estado.
 */
@Component({
  selector: 'app-ticket-adjuntos-input',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
<div class="space-y-4">

  <!-- ── Tabs ──────────────────────────────────────────────── -->
  <div class="flex gap-1 rounded-xl bg-slate-100 p-1">
    <button type="button"
            class="flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition"
            [class.bg-white]="tab() === 'archivo'"
            [class.shadow-sm]="tab() === 'archivo'"
            [class.text-slate-700]="tab() === 'archivo'"
            [class.text-slate-400]="tab() !== 'archivo'"
            (click)="tab.set('archivo')">
      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round"
              d="M18.375 12.739l-7.693 7.693a4.5 4.5 0 0 1-6.364-6.364l10.94-10.94A3 3 0 1 1 19.5 7.372L8.552 18.32m.009-.01-.01.01m5.699-9.941-7.81 7.81a1.5 1.5 0 0 0 2.112 2.13" />
      </svg>
      Archivos
      @if (archivos().length > 0) {
        <span class="inline-flex h-4 min-w-4 items-center justify-center rounded-full bg-ital-orange px-1 text-[10px] font-bold text-white">{{ archivos().length }}</span>
      }
    </button>
    <button type="button"
            class="flex flex-1 items-center justify-center gap-1.5 rounded-lg px-3 py-1.5 text-xs font-semibold transition"
            [class.bg-white]="tab() === 'link'"
            [class.shadow-sm]="tab() === 'link'"
            [class.text-slate-700]="tab() === 'link'"
            [class.text-slate-400]="tab() !== 'link'"
            (click)="tab.set('link')">
      <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
        <path stroke-linecap="round" stroke-linejoin="round"
              d="M13.19 8.688a4.5 4.5 0 0 1 1.242 7.244l-4.5 4.5a4.5 4.5 0 0 1-6.364-6.364l1.757-1.757m13.35-.622 1.757-1.757a4.5 4.5 0 0 0-6.364-6.364l-4.5 4.5a4.5 4.5 0 0 0 1.242 7.244" />
      </svg>
      Links
      @if (links().length > 0) {
        <span class="inline-flex h-4 min-w-4 items-center justify-center rounded-full bg-indigo-500 px-1 text-[10px] font-bold text-white">{{ links().length }}</span>
      }
    </button>
  </div>

  <!-- ── Panel: Archivos ───────────────────────────────────── -->
  @if (tab() === 'archivo') {
    <div class="space-y-3">
      <!-- Drop zone -->
      <label class="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4 py-6 text-center transition"
             [class.border-ital-orange]="dragOver()"
             [class.bg-ital-orange-50]="dragOver()"
             [class.border-slate-300]="!dragOver()"
             [class.bg-slate-50]="!dragOver()"
             (dragover)="onDragOver($event)"
             (dragleave)="onDragLeave($event)"
             (drop)="onDrop($event)">
        <svg class="h-7 w-7 text-ital-orange" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round"
                d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" />
        </svg>
        <span class="text-sm text-slate-600">
          <span class="font-semibold text-ital-orange">Hacé clic</span> o arrastrá documentos aquí
        </span>
        <span class="text-xs text-slate-400">PDF, Word, Excel · máx {{ maxSizeMb }} MB · hasta {{ maxArchivos }} archivos</span>
        <input type="file" [accept]="acceptAttr" multiple class="hidden" (change)="onSelectFile($event)" />
      </label>

      <!-- Procesando -->
      @if (processing() > 0) {
        <div class="flex items-center gap-1.5 text-xs text-ital-orange">
          <span class="h-3 w-3 animate-spin rounded-full border-2 border-ital-orange border-t-transparent"></span>
          Procesando {{ processing() }} archivo(s)…
        </div>
      }

      <!-- Lista de archivos -->
      @if (archivos().length > 0) {
        <ul class="space-y-1.5">
          @for (a of archivos(); track a.fileName; let i = $index) {
            <li class="flex items-center gap-2.5 rounded-lg border border-slate-200 bg-white px-3 py-2">
              <span class="grid h-7 w-7 shrink-0 place-items-center rounded-lg text-xs font-bold"
                    [class]="iconoTipo(a.contentType)">{{ etiquetaTipo(a.contentType) }}</span>
              <div class="min-w-0 flex-1">
                <p class="truncate text-xs font-medium text-slate-700">{{ a.fileName }}</p>
                <p class="text-[11px] text-slate-400">{{ formatKb(a.sizeBytes) }}</p>
              </div>
              <button type="button" (click)="removeArchivo(i)"
                      class="grid h-6 w-6 shrink-0 place-items-center rounded-full text-slate-400 transition hover:bg-rose-50 hover:text-rose-500">
                <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </li>
          }
        </ul>
      }
    </div>
  }

  <!-- ── Panel: Links ──────────────────────────────────────── -->
  @if (tab() === 'link') {
    <div class="space-y-3">
      <!-- Agregar link -->
      <div class="space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
        <div>
          <label class="mb-1 block text-xs font-semibold text-slate-600">URL del documento <span class="text-ital-orange">*</span></label>
          <input type="url" [(ngModel)]="nuevaUrl"
                 placeholder="https://drive.google.com/..."
                 class="w-full rounded-lg border-slate-200 px-3 py-2 text-xs shadow-sm focus:border-ital-green focus:ring-ital-green" />
        </div>
        <div>
          <label class="mb-1 block text-xs font-semibold text-slate-600">Título (opcional)</label>
          <input type="text" [(ngModel)]="nuevoTitulo"
                 placeholder="Ej: Manual de usuario v2"
                 class="w-full rounded-lg border-slate-200 px-3 py-2 text-xs shadow-sm focus:border-ital-green focus:ring-ital-green" />
        </div>
        <button type="button" (click)="addLink()"
                [disabled]="!nuevaUrl.trim()"
                class="inline-flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-1.5 text-xs font-semibold text-white transition hover:bg-indigo-700 disabled:opacity-50">
          <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          Agregar link
        </button>
      </div>

      <!-- Lista de links -->
      @if (links().length > 0) {
        <ul class="space-y-1.5">
          @for (l of links(); track l.url; let i = $index) {
            <li class="flex items-center gap-2.5 rounded-lg border border-indigo-100 bg-indigo-50 px-3 py-2">
              <svg class="h-4 w-4 shrink-0 text-indigo-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round"
                      d="M13.19 8.688a4.5 4.5 0 0 1 1.242 7.244l-4.5 4.5a4.5 4.5 0 0 1-6.364-6.364l1.757-1.757m13.35-.622 1.757-1.757a4.5 4.5 0 0 0-6.364-6.364l-4.5 4.5a4.5 4.5 0 0 0 1.242 7.244" />
              </svg>
              <div class="min-w-0 flex-1">
                <p class="truncate text-xs font-medium text-indigo-700">{{ l.titulo || l.url }}</p>
                @if (l.titulo) {
                  <p class="truncate text-[11px] text-indigo-400">{{ l.url }}</p>
                }
              </div>
              <button type="button" (click)="removeLink(i)"
                      class="grid h-6 w-6 shrink-0 place-items-center rounded-full text-indigo-300 transition hover:bg-rose-50 hover:text-rose-500">
                <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
            </li>
          }
        </ul>
      } @else {
        <p class="text-center text-xs text-slate-400">Aún no agregaste links.</p>
      }
    </div>
  }

</div>
  `,
})
export class TicketAdjuntosInputComponent implements OnDestroy {
  @Input() maxArchivos = 5;
  @Input() maxSizeMb = 10;
  @Output() adjuntosChange = new EventEmitter<AdjuntosInputState>();

  private readonly toast = inject(ToastService);

  readonly tab = signal<'archivo' | 'link'>('archivo');
  readonly archivos = signal<AdjuntoArchivoStaged[]>([]);
  readonly links = signal<AdjuntoLinkStaged[]>([]);
  readonly dragOver = signal(false);
  readonly processing = signal(0);

  readonly acceptAttr = ACCEPT_ATTR;

  nuevaUrl = '';
  nuevoTitulo = '';

  onDragOver(e: DragEvent): void { e.preventDefault(); this.dragOver.set(true); }
  onDragLeave(e: DragEvent): void { e.preventDefault(); this.dragOver.set(false); }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(false);
    if (e.dataTransfer?.files?.length) this.handleFiles(e.dataTransfer.files);
  }

  onSelectFile(e: Event): void {
    const input = e.target as HTMLInputElement;
    if (input.files?.length) this.handleFiles(input.files);
    input.value = '';
  }

  private async handleFiles(list: FileList): Promise<void> {
    for (const file of Array.from(list)) {
      if (this.archivos().length + this.processing() >= this.maxArchivos) {
        this.toast.warning(`Máximo ${this.maxArchivos} archivos por ticket.`);
        break;
      }
      if (!ACCEPTED_TYPES[file.type] && !this.isByExtension(file.name)) {
        this.toast.error(`"${file.name}" no es un tipo de archivo soportado (PDF, Word, Excel).`);
        continue;
      }
      if (file.size > this.maxSizeMb * 1024 * 1024) {
        this.toast.error(`"${file.name}" supera ${this.maxSizeMb} MB.`);
        continue;
      }
      this.processing.update(n => n + 1);
      try {
        const base64 = await this.fileToBase64(file);
        this.archivos.update(arr => [...arr, {
          base64,
          fileName: file.name,
          contentType: file.type || this.contentTypeFromName(file.name),
          sizeBytes: file.size,
        }]);
        this.emit();
      } catch {
        this.toast.error(`No se pudo procesar "${file.name}".`);
      } finally {
        this.processing.update(n => n - 1);
      }
    }
  }

  removeArchivo(idx: number): void {
    this.archivos.update(arr => arr.filter((_, i) => i !== idx));
    this.emit();
  }

  addLink(): void {
    const url = this.nuevaUrl.trim();
    if (!url) return;
    if (!url.startsWith('http://') && !url.startsWith('https://')) {
      this.toast.warning('La URL debe comenzar con http:// o https://');
      return;
    }
    this.links.update(arr => [...arr, { url, titulo: this.nuevoTitulo.trim() }]);
    this.nuevaUrl = '';
    this.nuevoTitulo = '';
    this.emit();
  }

  removeLink(idx: number): void {
    this.links.update(arr => arr.filter((_, i) => i !== idx));
    this.emit();
  }

  private emit(): void {
    this.adjuntosChange.emit({ archivos: this.archivos(), links: this.links() });
  }

  etiquetaTipo(ct: string): string {
    if (ct?.includes('pdf')) return 'PDF';
    if (ct?.includes('excel') || ct?.includes('spreadsheet')) return 'XLS';
    if (ct?.includes('word') || ct?.includes('wordprocessing')) return 'DOC';
    return 'DOC';
  }

  iconoTipo(ct: string): string {
    if (ct?.includes('pdf')) return 'bg-rose-100 text-rose-600';
    if (ct?.includes('excel') || ct?.includes('spreadsheet')) return 'bg-emerald-100 text-emerald-700';
    return 'bg-indigo-100 text-indigo-700';
  }

  formatKb(bytes: number): string {
    return bytes >= 1024 * 1024
      ? `${(bytes / (1024 * 1024)).toFixed(1)} MB`
      : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  private isByExtension(name: string): boolean {
    return /\.(pdf|doc|docx|xls|xlsx)$/i.test(name);
  }

  private contentTypeFromName(name: string): string {
    if (/\.pdf$/i.test(name)) return 'application/pdf';
    if (/\.docx$/i.test(name)) return 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    if (/\.doc$/i.test(name)) return 'application/msword';
    if (/\.xlsx$/i.test(name)) return 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
    if (/\.xls$/i.test(name)) return 'application/vnd.ms-excel';
    return 'application/octet-stream';
  }

  private fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  }

  ngOnDestroy(): void { /* noop */ }
}
