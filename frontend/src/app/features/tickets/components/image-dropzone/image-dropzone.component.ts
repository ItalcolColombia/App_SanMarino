// src/app/features/tickets/components/image-dropzone/image-dropzone.component.ts
import { Component, EventEmitter, Input, OnDestroy, Output, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../../../shared/services/toast.service';
import { compressImage, blobToBase64 } from '../../services/image-compression.util';
import { TicketImagenInput } from '../../models/ticket.models';

interface PreviewImage {
  previewUrl: string;   // object URL (NO data URL) para la miniatura
  base64: string;       // data URL para enviar al backend
  fileName: string;
  contentType: string;
  sizeBytes: number;    // tamaño tras compresión
  originalSize: number;
}

/**
 * Carga de múltiples imágenes con drag & drop + compresión en cliente.
 * Comprime cada imagen antes de codificar a Base64 y emite la lista lista
 * para enviar. Previsualiza con object URLs (que revoca al quitar/destruir).
 */
@Component({
  selector: 'app-image-dropzone',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="space-y-3">
      <!-- Zona drag & drop -->
      <label
        class="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-4 py-8 text-center transition"
        [class.border-ital-orange]="dragOver()"
        [class.bg-ital-orange-50]="dragOver()"
        [class.border-slate-300]="!dragOver()"
        [class.bg-slate-50]="!dragOver()"
        (dragover)="onDragOver($event)"
        (dragleave)="onDragLeave($event)"
        (drop)="onDrop($event)">
        <svg class="h-8 w-8 text-ital-orange" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round"
            d="M3 16.5v2.25A2.25 2.25 0 0 0 5.25 21h13.5A2.25 2.25 0 0 0 21 18.75V16.5m-13.5-9L12 3m0 0 4.5 4.5M12 3v13.5" />
        </svg>
        <div class="text-sm text-slate-600">
          <span class="font-semibold text-ital-orange">Hacé clic</span> o arrastrá imágenes aquí
        </div>
        <div class="text-xs text-slate-400">
          PNG, JPG, WebP · máx {{ maxSizeMb }} MB · hasta {{ maxImages }} imágenes
        </div>
        <input type="file" accept="image/*" multiple class="hidden" (change)="onSelect($event)" />
      </label>

      <!-- Estado -->
      <div class="flex items-center justify-between text-xs text-slate-500">
        <span>{{ images().length }}/{{ maxImages }} imágenes</span>
        @if (processing() > 0) {
          <span class="inline-flex items-center gap-1.5 text-ital-orange">
            <span class="h-3 w-3 animate-spin rounded-full border-2 border-ital-orange border-t-transparent"></span>
            comprimiendo {{ processing() }}…
          </span>
        }
      </div>

      <!-- Miniaturas -->
      @if (images().length > 0) {
        <div class="grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-5">
          @for (img of images(); track img.previewUrl; let i = $index) {
            <div class="group relative aspect-square overflow-hidden rounded-lg ring-1 ring-slate-200">
              <img [src]="img.previewUrl" [alt]="img.fileName" class="h-full w-full object-cover" />
              <button type="button"
                class="absolute right-1 top-1 grid h-6 w-6 place-items-center rounded-full bg-black/55 text-white opacity-0 transition group-hover:opacity-100"
                (click)="remove(i)" title="Quitar">
                <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
                </svg>
              </button>
              <span class="absolute inset-x-0 bottom-0 truncate bg-black/45 px-1.5 py-0.5 text-[10px] text-white">
                {{ formatKb(img.sizeBytes) }}
              </span>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class ImageDropzoneComponent implements OnDestroy {
  @Input() maxImages = 10;
  @Input() maxSizeMb = 8;
  @Output() imagesChange = new EventEmitter<TicketImagenInput[]>();

  private readonly toast = inject(ToastService);

  readonly images = signal<PreviewImage[]>([]);
  readonly dragOver = signal(false);
  readonly processing = signal(0);

  onDragOver(e: DragEvent): void { e.preventDefault(); this.dragOver.set(true); }
  onDragLeave(e: DragEvent): void { e.preventDefault(); this.dragOver.set(false); }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    this.dragOver.set(false);
    if (e.dataTransfer?.files?.length) this.handleFiles(e.dataTransfer.files);
  }

  onSelect(e: Event): void {
    const input = e.target as HTMLInputElement;
    if (input.files?.length) this.handleFiles(input.files);
    input.value = ''; // permite volver a elegir el mismo archivo
  }

  private async handleFiles(list: FileList): Promise<void> {
    for (const file of Array.from(list)) {
      if (this.images().length + this.processing() >= this.maxImages) {
        this.toast.warning(`Máximo ${this.maxImages} imágenes por ticket.`);
        break;
      }
      if (!file.type.startsWith('image/')) {
        this.toast.error(`"${file.name}" no es una imagen.`);
        continue;
      }
      if (file.size > this.maxSizeMb * 1024 * 1024) {
        this.toast.error(`"${file.name}" supera ${this.maxSizeMb} MB.`);
        continue;
      }

      this.processing.update(n => n + 1);
      try {
        const { blob, contentType } = await compressImage(file);
        const base64 = await blobToBase64(blob);
        const preview: PreviewImage = {
          previewUrl: URL.createObjectURL(blob),
          base64,
          fileName: file.name,
          contentType,
          sizeBytes: blob.size,
          originalSize: file.size,
        };
        this.images.update(arr => [...arr, preview]);
        this.emit();
      } catch {
        this.toast.error(`No se pudo procesar "${file.name}".`);
      } finally {
        this.processing.update(n => n - 1);
      }
    }
  }

  remove(idx: number): void {
    this.images.update(arr => {
      const copy = [...arr];
      const [removed] = copy.splice(idx, 1);
      if (removed) URL.revokeObjectURL(removed.previewUrl);
      return copy;
    });
    this.emit();
  }

  private emit(): void {
    this.imagesChange.emit(
      this.images().map(i => ({
        base64: i.base64,
        fileName: i.fileName,
        contentType: i.contentType,
        sizeBytes: i.sizeBytes,
      })),
    );
  }

  formatKb(bytes: number): string {
    return bytes >= 1024 * 1024
      ? `${(bytes / (1024 * 1024)).toFixed(1)} MB`
      : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  ngOnDestroy(): void {
    this.images().forEach(i => URL.revokeObjectURL(i.previewUrl));
  }
}
