// src/app/shared/components/firma-canvas/firma-canvas.component.ts
// Recuadro para firmar a mano: con el dedo en celular/tablet o con el mouse en escritorio.
// Emite el trazo como data URL PNG (o null si está en blanco) para que el llamador lo mande al backend.
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';

/**
 * Un solo juego de handlers `pointer*` cubre mouse, dedo y lápiz — por eso no hay ramas
 * touch/mouse separadas. El canvas se dibuja a resolución del dispositivo (devicePixelRatio) para
 * que la firma no salga pixelada en celular, pero se exporta a un tamaño fijo y liviano.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.Eager,
  selector: 'app-firma-canvas',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div>
      <div class="flex items-center justify-between gap-2">
        <label class="form-label mb-0">{{ label }}</label>
        <button
          type="button"
          class="text-xs font-semibold underline"
          style="color: var(--ital-muted)"
          [disabled]="vacio"
          (click)="limpiar()"
        >
          Limpiar
        </button>
      </div>

      <div
        class="relative mt-1 overflow-hidden rounded-xl border"
        [style.border-color]="vacio ? 'var(--ital-green-100)' : 'var(--ital-orange)'"
        style="background: #fff; touch-action: none"
      >
        <canvas
          #lienzo
          class="block w-full"
          style="height: 170px; touch-action: none; cursor: crosshair"
          (pointerdown)="inicio($event)"
          (pointermove)="mover($event)"
          (pointerup)="fin($event)"
          (pointerleave)="fin($event)"
          (pointercancel)="fin($event)"
        ></canvas>

        <!-- Guía: línea y leyenda, ocultas apenas empieza el trazo para no ensuciar la firma -->
        <div *ngIf="vacio" class="pointer-events-none absolute inset-0 flex flex-col items-center justify-end pb-5">
          <div class="mb-2 h-px w-4/5" style="background: var(--ital-green-100)"></div>
          <span class="text-xs" style="color: var(--ital-muted)">{{ ayuda }}</span>
        </div>
      </div>
    </div>
  `,
})
export class FirmaCanvasComponent implements AfterViewInit, OnDestroy {
  @Input() label = 'Firmá acá';
  @Input() ayuda = 'Firmá con el dedo o el mouse';

  /** Trazo actual: data URL PNG, o null mientras el recuadro esté en blanco. */
  @Output() firmaCambio = new EventEmitter<string | null>();

  @ViewChild('lienzo') lienzoRef!: ElementRef<HTMLCanvasElement>;

  vacio = true;

  private ctx: CanvasRenderingContext2D | null = null;
  private dibujando = false;
  private redimensionar = () => this.ajustarLienzo();

  ngAfterViewInit(): void {
    this.ajustarLienzo();
    window.addEventListener('resize', this.redimensionar);
  }

  ngOnDestroy(): void {
    window.removeEventListener('resize', this.redimensionar);
  }

  /**
   * Ajusta el buffer del canvas al tamaño real en pantalla × devicePixelRatio. Preserva lo dibujado
   * (si alguien gira el teléfono a mitad de la firma, el trazo no se borra).
   */
  private ajustarLienzo(): void {
    const canvas = this.lienzoRef?.nativeElement;
    if (!canvas) return;

    const previo = this.vacio ? null : canvas.toDataURL('image/png');
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;

    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));

    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);
    ctx.lineWidth = 2.2;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#1f2937';
    this.ctx = ctx;

    if (previo) {
      const img = new Image();
      img.onload = () => ctx.drawImage(img, 0, 0, rect.width, rect.height);
      img.src = previo;
    }
  }

  private punto(ev: PointerEvent): { x: number; y: number } {
    const rect = this.lienzoRef.nativeElement.getBoundingClientRect();
    return { x: ev.clientX - rect.left, y: ev.clientY - rect.top };
  }

  inicio(ev: PointerEvent): void {
    if (!this.ctx) return;
    ev.preventDefault();
    this.lienzoRef.nativeElement.setPointerCapture?.(ev.pointerId);
    this.dibujando = true;

    const { x, y } = this.punto(ev);
    this.ctx.beginPath();
    this.ctx.moveTo(x, y);
    // Un toque sin arrastre también deja marca (firmas cortas, puntos de una rúbrica).
    this.ctx.lineTo(x + 0.1, y);
    this.ctx.stroke();

    if (this.vacio) {
      this.vacio = false;
      this.emitir();
    }
  }

  mover(ev: PointerEvent): void {
    if (!this.dibujando || !this.ctx) return;
    ev.preventDefault();
    const { x, y } = this.punto(ev);
    this.ctx.lineTo(x, y);
    this.ctx.stroke();
  }

  fin(ev: PointerEvent): void {
    if (!this.dibujando) return;
    this.dibujando = false;
    this.lienzoRef.nativeElement.releasePointerCapture?.(ev.pointerId);
    this.emitir();
  }

  limpiar(): void {
    const canvas = this.lienzoRef?.nativeElement;
    if (!canvas || !this.ctx) return;
    this.ctx.clearRect(0, 0, canvas.width, canvas.height);
    this.vacio = true;
    this.emitir();
  }

  private emitir(): void {
    this.firmaCambio.emit(this.vacio ? null : this.exportarPng());
  }

  /**
   * Exporta el trazo a un PNG de 600×200 con fondo blanco. Tamaño fijo para que el peso no dependa
   * del dispositivo (un celular con dpr 3 mandaría una imagen enorme) y fondo opaco porque el PNG
   * termina embebido en actas y correos donde la transparencia se ve como un cuadro negro.
   */
  private exportarPng(): string {
    const origen = this.lienzoRef.nativeElement;
    const salida = document.createElement('canvas');
    salida.width = 600;
    salida.height = 200;

    const ctx = salida.getContext('2d');
    if (!ctx) return origen.toDataURL('image/png');

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, salida.width, salida.height);
    ctx.drawImage(origen, 0, 0, salida.width, salida.height);
    return salida.toDataURL('image/png');
  }
}
