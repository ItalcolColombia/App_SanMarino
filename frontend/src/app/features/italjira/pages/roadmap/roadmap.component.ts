// src/app/features/italjira/pages/roadmap/roadmap.component.ts
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { TicketService } from '../../../tickets/services/ticket.service';
import { ESTADO_DOT, ESTADO_LABEL, TIPO_LABEL } from '../../../tickets/models/ticket.models';
import {
  PRIORIDADES, PRIORIDAD_ACENTO, PRIORIDAD_LABEL,
  TAREA_ESTADO_DOT, TAREA_ESTADO_LABEL, TicketRoadmap, TicketRoadmapItem, TicketTableroFiltro,
} from '../../../tickets/models/ticket-tarea.models';
import { TicketSlaChipComponent } from '../../../tickets/components/ticket-sla-chip/ticket-sla-chip.component';
import { TicketFiltrosComponent } from '../../../tickets/components/ticket-filtros/ticket-filtros.component';
import { ToastService } from '../../../../shared/services/toast.service';

/** Una división del eje temporal (un mes o una semana según el rango visible). */
interface Periodo { etiqueta: string; sub: string; inicio: Date; ancho: number; }

/** Barra dibujable de un caso o de una tarea. */
interface Barra { visible: boolean; izquierda: number; ancho: number; }

const MS_DIA = 86_400_000;

/**
 * Roadmap tipo Jira: cada fila es un caso, con su barra sobre un eje temporal y sus tareas
 * anidadas. Estado mutable + subscribe ⇒ `Eager` obligatorio.
 */
@Component({
  selector: 'app-tickets-roadmap',
  standalone: true,
  imports: [RouterLink, TicketSlaChipComponent, TicketFiltrosComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './roadmap.component.html',
})
export class RoadmapComponent implements OnInit {
  private readonly svc = inject(TicketService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly data = signal<TicketRoadmap | null>(null);
  readonly cargando = signal(false);
  /** Ids de los casos con sus tareas desplegadas. */
  readonly expandidos = signal<Set<number>>(new Set());

  readonly estadoLabel = ESTADO_LABEL;
  readonly estadoDot = ESTADO_DOT;
  readonly tipoLabel = TIPO_LABEL;
  readonly prioridades = PRIORIDADES;
  readonly prioridadLabel = PRIORIDAD_LABEL;
  readonly prioridadAcento = PRIORIDAD_ACENTO;
  readonly tareaEstadoLabel = TAREA_ESTADO_LABEL;
  readonly tareaEstadoDot = TAREA_ESTADO_DOT;

  /** Último filtro emitido por la barra compartida. */
  private filtro: TicketTableroFiltro = {};

  /**
   * «Ahora» CONGELADO por carga. Si cada barra llamara a `new Date()`, dos pasadas de detección
   * de cambios darían anchos distintos por microsegundos y Angular tiraría NG0100 en cada ciclo.
   */
  readonly ahora = signal(this.hoyCero());

  readonly items = computed<TicketRoadmapItem[]>(() => this.data()?.items ?? []);

  /**
   * Ventana visible del eje. Se toma del rango que devolvió el backend, con un margen de una
   * semana a cada lado, y siempre incluye HOY para que el marcador se vea.
   */
  readonly ventana = computed<{ inicio: Date; fin: Date; dias: number }>(() => {
    const d = this.data();
    const hoy = this.ahora();

    const desde = d?.desde ? this.parseYmd(d.desde) : null;
    const hasta = d?.hasta ? this.parseYmd(d.hasta) : null;

    // Sin fechas planificadas: se muestra el mes de hoy ± 15 días.
    if (!desde || !hasta) {
      const inicio = new Date(hoy.getTime() - 15 * MS_DIA);
      const fin = new Date(hoy.getTime() + 45 * MS_DIA);
      return { inicio, fin, dias: Math.round((fin.getTime() - inicio.getTime()) / MS_DIA) };
    }

    const inicio = new Date(Math.min(desde.getTime(), hoy.getTime()) - 7 * MS_DIA);
    const fin = new Date(Math.max(hasta.getTime(), hoy.getTime()) + 7 * MS_DIA);
    return { inicio, fin, dias: Math.max(1, Math.round((fin.getTime() - inicio.getTime()) / MS_DIA)) };
  });

  /** Divisiones del eje: semanas si el rango es corto, meses si es largo. */
  readonly periodos = computed<Periodo[]>(() => {
    const { inicio, fin, dias } = this.ventana();
    return dias <= 120 ? this.periodosSemanales(inicio, fin, dias) : this.periodosMensuales(inicio, fin, dias);
  });

  /** Posición de la línea de HOY, en % del ancho del eje. */
  readonly posicionHoy = computed<number>(() => {
    const { inicio, dias } = this.ventana();
    const offset = (this.ahora().getTime() - inicio.getTime()) / MS_DIA;
    return Math.max(0, Math.min(100, (offset / dias) * 100));
  });

  // La carga inicial la dispara la barra de filtros con su primera emisión.
  ngOnInit(): void { /* sin trabajo propio */ }

  /** La barra compartida publica el filtro ya armado; acá solo se recarga. */
  onFiltro(filtro: TicketTableroFiltro): void {
    this.filtro = filtro;
    this.cargar();
  }

  cargar(): void {
    this.cargando.set(true);
    this.ahora.set(this.hoyCero());   // se refresca una vez por carga, no por evaluación
    this.svc.roadmap(this.filtro)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.cargando.set(false)))
      .subscribe({
        next: r => this.data.set(r),
        error: () => this.toast.error('No se pudo cargar el roadmap.'),
      });
  }

  abrirCaso(id: number): void { this.router.navigate(['/tickets', id]); }

  alternar(id: number): void {
    // Set nuevo: el signal necesita otra referencia para notificar a la vista.
    const actual = new Set(this.expandidos());
    actual.has(id) ? actual.delete(id) : actual.add(id);
    this.expandidos.set(actual);
  }

  estaExpandido(id: number): boolean { return this.expandidos().has(id); }

  // ── Cálculo de barras ────────────────────────────────────────

  /**
   * Barra del caso. Sin fechas planificadas cae a la ventana real (creación → solución/hoy),
   * para que el caso igual aparezca en el roadmap en vez de desaparecer.
   */
  barraCaso(item: TicketRoadmapItem): Barra {
    if (item.fechaInicioPlan || item.fechaFinPlan) {
      return this.barra(item.fechaInicioPlan, item.fechaFinPlan);
    }
    const inicio = new Date(item.createdAt);
    // Sin solución, la barra llega hasta HOY — el «hoy» congelado, no el reloj.
    const fin = item.fechaSolucion ? new Date(item.fechaSolucion) : this.ahora();
    return this.barraEntre(inicio, fin);
  }

  barraTarea(inicio: string | null, fin: string | null): Barra { return this.barra(inicio, fin); }

  /** True si la barra del caso es estimada (no hay fechas planificadas cargadas). */
  esBarraEstimada(item: TicketRoadmapItem): boolean {
    return !item.fechaInicioPlan && !item.fechaFinPlan;
  }

  private barra(desdeYmd: string | null, hastaYmd: string | null): Barra {
    if (!desdeYmd && !hastaYmd) return { visible: false, izquierda: 0, ancho: 0 };
    // Con una sola fecha se dibuja un bloque de un día: comunica el hito igual.
    const desde = desdeYmd ? this.parseYmd(desdeYmd) : this.parseYmd(hastaYmd!);
    const hasta = hastaYmd ? this.parseYmd(hastaYmd) : this.parseYmd(desdeYmd!);
    return this.barraEntre(desde, hasta);
  }

  private barraEntre(desde: Date, hasta: Date): Barra {
    const { inicio, dias } = this.ventana();
    const offsetIni = (desde.getTime() - inicio.getTime()) / MS_DIA;
    const offsetFin = (hasta.getTime() - inicio.getTime()) / MS_DIA + 1;   // el día de fin cuenta entero

    const izquierda = Math.max(0, (offsetIni / dias) * 100);
    const derecha = Math.min(100, (offsetFin / dias) * 100);
    const ancho = Math.max(1.2, derecha - izquierda);   // mínimo visible aunque sea un día

    return { visible: derecha > 0 && izquierda < 100, izquierda, ancho };
  }

  // ── Eje temporal ─────────────────────────────────────────────

  private periodosSemanales(inicio: Date, fin: Date, diasTotales: number): Periodo[] {
    const periodos: Periodo[] = [];
    // Arranca el lunes de la semana del inicio.
    const cursor = new Date(inicio);
    cursor.setDate(cursor.getDate() - ((cursor.getDay() + 6) % 7));

    while (cursor < fin) {
      const siguiente = new Date(cursor);
      siguiente.setDate(siguiente.getDate() + 7);
      const desde = cursor < inicio ? inicio : cursor;
      const hasta = siguiente > fin ? fin : siguiente;
      const dias = Math.max(0, (hasta.getTime() - desde.getTime()) / MS_DIA);

      if (dias > 0) {
        periodos.push({
          etiqueta: `${cursor.getDate()} ${this.MESES_CORTOS[cursor.getMonth()]}`,
          sub: `S${this.numeroDeSemana(cursor)}`,
          inicio: new Date(cursor),
          ancho: (dias / diasTotales) * 100,
        });
      }
      cursor.setDate(cursor.getDate() + 7);
    }
    return periodos;
  }

  private periodosMensuales(inicio: Date, fin: Date, diasTotales: number): Periodo[] {
    const periodos: Periodo[] = [];
    const cursor = new Date(inicio.getFullYear(), inicio.getMonth(), 1);

    while (cursor < fin) {
      const siguiente = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1);
      const desde = cursor < inicio ? inicio : cursor;
      const hasta = siguiente > fin ? fin : siguiente;
      const dias = Math.max(0, (hasta.getTime() - desde.getTime()) / MS_DIA);

      if (dias > 0) {
        periodos.push({
          etiqueta: this.MESES_CORTOS[cursor.getMonth()],
          sub: `${cursor.getFullYear()}`,
          inicio: new Date(cursor),
          ancho: (dias / diasTotales) * 100,
        });
      }
      cursor.setMonth(cursor.getMonth() + 1);
    }
    return periodos;
  }

  private readonly MESES_CORTOS = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];

  /** Semana ISO (lunes como primer día) — solo para la etiqueta del eje. */
  private numeroDeSemana(fecha: Date): number {
    const d = new Date(Date.UTC(fecha.getFullYear(), fecha.getMonth(), fecha.getDate()));
    d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
    const inicioAnio = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
    return Math.ceil(((d.getTime() - inicioAnio.getTime()) / MS_DIA + 1) / 7);
  }

  /** `yyyy-MM-dd` → Date local, sin desplazamiento de zona horaria. */
  private parseYmd(ymd: string): Date {
    const soloFecha = ymd.length > 10 ? ymd.slice(0, 10) : ymd;
    const [a, m, d] = soloFecha.split('-').map(Number);
    return new Date(a, (m ?? 1) - 1, d ?? 1);
  }

  /** Hoy a medianoche: el eje del roadmap trabaja en días, no en instantes. */
  private hoyCero(): Date {
    const d = new Date();
    d.setHours(0, 0, 0, 0);
    return d;
  }

  iniciales(nombre: string | null): string {
    if (!nombre) return '?';
    return nombre.trim().split(/\s+/).slice(0, 2).map(p => p[0]).join('').toUpperCase();
  }
}
