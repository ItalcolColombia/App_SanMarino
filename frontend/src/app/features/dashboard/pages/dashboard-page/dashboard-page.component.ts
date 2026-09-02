import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { TokenStorageService } from '../../../../core/auth/token-storage.service';
import { ActiveCompanyConfigService } from '../../../../core/services/company-config/active-company-config.service';
import { formatearNumero } from '../../../../shared/utils/format';
import { PanelEsqueletoComponent } from '../../components/panel-esqueleto/panel-esqueleto.component';
import { TarjetaKpiComponent } from '../../components/tarjeta-kpi/tarjeta-kpi.component';
import { PanelAlimentoComponent } from '../../components/panel-alimento/panel-alimento.component';
import { PanelCumplimientoComponent } from '../../components/panel-cumplimiento/panel-cumplimiento.component';
import { PanelEngordeComponent } from '../../components/panel-engorde/panel-engorde.component';
import { PanelPosturaComponent } from '../../components/panel-postura/panel-postura.component';
import { resolverPanelesVisibles } from '../../funciones/resolver-paneles-visibles.funcion';
import { FiltrosDashboard, Kpi } from '../../models/dashboard-metricas.model';
import { PanelId, PanelVisible } from '../../models/dashboard-panel.model';
import { DashboardPanelesService, DashboardResumen } from '../../services/dashboard-paneles.service';

/**
 * Página del dashboard: orquestador DELGADO.
 *
 * Arma el estado (filtros, resumen, qué paneles corresponden) y delega: la decisión de qué se ve la
 * toma una función pura, y cada panel es su propio componente que se carga solo.
 *
 * ## Carga perezosa de verdad
 *
 * Cada panel va en un `@defer (on viewport)`: su código **y su request** salen recién cuando el
 * panel entra en pantalla. Es el primer uso de `@defer` del repo. Lo que el dashboard anterior
 * llamaba «lazy loading» era una cola con `setTimeout` que igual pedía las 8 llamadas, más un
 * `interval(30000)` que las repetía **todas** cada 30 segundos; eso último se eliminó — acá el
 * refresco es manual y por panel.
 *
 * ## Qué se ve
 *
 * Lo decide `resolverPanelesVisibles` a partir del **menú del usuario** (`role_menus` ∩
 * `company_menus`, ya resuelto por `fn_menu_usuario` y guardado en la sesión), sus **permisos** y los
 * **flags de la empresa activa**. Sin permisos nuevos: si tenés el módulo, tenés su panel.
 *
 * ⚠️ Esto decide lo que se DIBUJA. La protección es el corte del backend, que resuelve empresa y
 * alcance del usuario por su cuenta en cada endpoint.
 */
@Component({
  selector: 'app-dashboard-page',
  standalone: true,
  imports: [
    FormsModule,
    TarjetaKpiComponent,
    PanelEsqueletoComponent,
    PanelPosturaComponent,
    PanelEngordeComponent,
    PanelAlimentoComponent,
    PanelCumplimientoComponent
  ],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './dashboard-page.component.html',
  styleUrls: ['./dashboard-page.component.scss']
})
export class DashboardPageComponent implements OnInit {
  private readonly panelesSvc = inject(DashboardPanelesService);
  private readonly storage = inject(TokenStorageService);
  private readonly companyConfig = inject(ActiveCompanyConfigService);

  /** Paneles que este usuario ve, ya resueltos. Campo, NO getter: el template lo recorre. */
  paneles: PanelVisible[] = [];
  /** `true` cuando ya se resolvió el gating (para no mostrar «no tenés paneles» mientras carga). */
  panelesResueltos = false;

  resumen: DashboardResumen | null = null;
  cargandoResumen = false;
  errorResumen: string | null = null;

  /** Tarjetas del encabezado. Campo, no getter: un getter que aloca rompe change detection (NG0103). */
  kpis: Kpi[] = [];

  filtros: FiltrosDashboard = {
    periodo: periodoPorDefecto(),
    farmId: null
  };

  ngOnInit(): void {
    this.resolverPaneles();
    this.cargarResumen();
  }

  // ───────────────────────────────────────────────────────────── gating

  /**
   * Resuelve qué paneles corresponden. El menú y los permisos ya viajan en la sesión, así que lo
   * único que se pide por red son los flags de la empresa —y su servicio es fail-closed: si falla,
   * responde todo apagado y el panel base igual se dibuja.
   */
  private resolverPaneles(): void {
    const sesion = this.storage.get();
    const menu = sesion?.menu ?? [];
    const permisos = sesion?.user?.permisos ?? [];

    this.companyConfig.getFlags().subscribe({
      next: flags => {
        this.paneles = resolverPanelesVisibles({ menu, permisos, flags });
        this.panelesResueltos = true;
      },
      error: () => {
        this.paneles = resolverPanelesVisibles({ menu, permisos, flags: null });
        this.panelesResueltos = true;
      }
    });
  }

  /** ¿Este panel está entre los visibles? Se usa en el template para elegir el `@defer`. */
  ve(id: PanelId): boolean {
    return this.paneles.some(p => p.id === id);
  }

  /** El panel resuelto (con sus bloques), o `undefined` si el usuario no lo ve. */
  panel(id: PanelId): PanelVisible | undefined {
    return this.paneles.find(p => p.id === id);
  }

  // ───────────────────────────────────────────────────────────── resumen

  cargarResumen(): void {
    this.cargandoResumen = true;
    this.errorResumen = null;

    this.panelesSvc.resumen().subscribe({
      next: r => {
        this.resumen = r;
        this.kpis = construirKpis(r);
        this.cargandoResumen = false;
      },
      error: () => {
        this.resumen = null;
        this.kpis = [];
        this.errorResumen = 'No se pudo cargar el resumen.';
        this.cargandoResumen = false;
      }
    });
  }

  /** Refresca todo lo que está en pantalla. Manual: no hay refresco automático. */
  refrescar(): void {
    this.cargarResumen();
    // Los paneles se refrescan solos al cambiar `filtros` (su @Input) — ver cada componente.
    this.filtros = { ...this.filtros };
  }

  onPeriodoChange(): void {
    // Nueva referencia para que los paneles (que leen `filtros` por @Input) recarguen.
    this.filtros = { ...this.filtros, periodo: { ...this.filtros.periodo } };
  }
}

/** Últimos 30 días, en fechas puras `YYYY-MM-DD`. */
function periodoPorDefecto(): FiltrosDashboard['periodo'] {
  const hoy = new Date();
  const desde = new Date(hoy);
  desde.setDate(desde.getDate() - 29);
  const ymd = (d: Date) => d.toISOString().slice(0, 10);
  return { desde: ymd(desde), hasta: ymd(hoy) };
}

/**
 * Tarjetas del encabezado a partir del resumen.
 *
 * Sólo se dibuja la tarjeta de una línea productiva **si esa línea tiene lotes**: una tarjeta
 * «Pollo engorde: 0» en una empresa que no maneja engorde no informa nada y ocupa lugar.
 */
function construirKpis(r: DashboardResumen): Kpi[] {
  const kpis: Kpi[] = [
    {
      etiqueta: 'Granjas',
      valor: formatearNumero(r.granjas),
      detalle: r.alcanceRestringido ? 'Tu alcance, no el total de la empresa' : 'Asignadas a tu usuario'
    }
  ];

  if (r.lotesPosturaTotal > 0) {
    kpis.push({
      etiqueta: 'Lotes de postura activos',
      valor: formatearNumero(r.lotesPosturaActivos),
      detalle: `de ${formatearNumero(r.lotesPosturaTotal)} en total`
    });
  }

  if (r.lotesEngordeTotal > 0) {
    kpis.push({
      etiqueta: 'Lotes de engorde activos',
      valor: formatearNumero(r.lotesEngordeActivos),
      detalle: `de ${formatearNumero(r.lotesEngordeTotal)} en total`
    });
  }

  return kpis;
}
