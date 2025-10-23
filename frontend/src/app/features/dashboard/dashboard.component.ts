import { Component, OnInit, OnDestroy, signal, computed, inject, HostListener, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SidebarComponent } from '../../shared/components/sidebar/sidebar.component';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartType } from 'chart.js';
import { DashboardService, 
         DashboardEstadisticasGeneralesDto, 
         ProduccionGranjaDto, 
         RegistroDiarioDto, 
         ActividadRecienteDto, 
         MortalidadDto, 
         DistribucionLotesDto, 
         InventarioEstadisticasDto, 
         MetricasRendimientoDto } from '../../core/services/dashboard/dashboard.service';
import { interval, Subscription, forkJoin, of, BehaviorSubject, debounceTime, distinctUntilChanged, fromEvent } from 'rxjs';
import { catchError, finalize, switchMap, takeUntil, throttleTime } from 'rxjs/operators';

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule, SidebarComponent, NgChartsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss']
})
export class DashboardComponent implements OnInit, OnDestroy, AfterViewInit {
  private dashboardService = inject(DashboardService);
  private elementRef = inject(ElementRef);

  // ====== ESTADO ======
  loading = signal<boolean>(true);
  error = signal<string | null>(null);
  lastUpdated = signal<Date>(new Date());
  isRealTimeEnabled = signal<boolean>(true);
  
  // ====== EFECTOS VISUALES ======
  isScrolled = signal<boolean>(false);
  isSidebarOpen = signal<boolean>(false);
  animationDelay = signal<number>(0);

  // ====== CARGA LENTA (LAZY LOADING) ======
  private loadTrigger$ = new BehaviorSubject<boolean>(false);
  private destroy$ = new BehaviorSubject<boolean>(false);
  
  // Estados de carga por sección
  loadingKPIs = signal<boolean>(true);
  loadingCharts = signal<boolean>(true);
  loadingActivities = signal<boolean>(true);
  loadingMetrics = signal<boolean>(true);
  loadingReports = signal<boolean>(true);
  
  // Control de carga progresiva
  loadedSections = signal<Set<string>>(new Set());
  loadingQueue = signal<string[]>(['kpis', 'charts', 'activities', 'metrics', 'reports']);

  // ====== DATOS ======
  estadisticasGenerales = signal<DashboardEstadisticasGeneralesDto | null>(null);
  produccionPorGranja = signal<ProduccionGranjaDto[]>([]);
  registrosDiarios = signal<RegistroDiarioDto[]>([]);
  actividadesRecientes = signal<ActividadRecienteDto[]>([]);
  mortalidades = signal<MortalidadDto[]>([]);
  distribucionLotes = signal<DistribucionLotesDto[]>([]);
  estadisticasInventario = signal<InventarioEstadisticasDto | null>(null);
  metricasRendimiento = signal<MetricasRendimientoDto | null>(null);

  // ====== CONFIGURACIÓN DE GRÁFICOS CON COLORES CORPORATIVOS ======
  public lineChartType: ChartType = 'line';
  public barChartType: ChartType = 'bar';
  public doughnutChartType: ChartType = 'doughnut';
  public pieChartType: ChartType = 'pie';

  // Colores corporativos: Amarillo suave, Rojo, Gris
  private readonly CORPORATE_COLORS = {
    primary: '#f59e0b',      // Amarillo suave
    secondary: '#ef4444',    // Rojo
    accent: '#6b7280',      // Gris
    success: '#10b981',     // Verde para éxito
    warning: '#f59e0b',     // Amarillo para advertencias
    danger: '#ef4444',      // Rojo para peligro
    info: '#3b82f6',       // Azul para información
    light: '#f9fafb',      // Gris claro
    dark: '#374151'        // Gris oscuro
  };

  // ====== OPCIONES DE GRÁFICOS CON COLORES CORPORATIVOS ======
  public chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
        labels: {
          usePointStyle: true,
          padding: 20,
          color: this.CORPORATE_COLORS.dark,
          font: {
            family: 'Inter, sans-serif',
            size: 12,
            weight: 'normal'
          }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        titleColor: 'white',
        bodyColor: 'white',
        borderColor: this.CORPORATE_COLORS.primary,
        borderWidth: 2,
        cornerRadius: 8,
        displayColors: true
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        grid: {
          color: 'rgba(107, 114, 128, 0.1)'
        },
        ticks: {
          color: this.CORPORATE_COLORS.dark,
          font: {
            family: 'Inter, sans-serif',
            size: 11
          }
        }
      },
      x: {
        grid: {
          color: 'rgba(107, 114, 128, 0.1)'
        },
        ticks: {
          color: this.CORPORATE_COLORS.dark,
          font: {
            family: 'Inter, sans-serif',
            size: 11
          }
        }
      }
    }
  };

  public doughnutOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'right',
        labels: {
          usePointStyle: true,
          padding: 20,
          color: this.CORPORATE_COLORS.dark,
          font: {
            family: 'Inter, sans-serif',
            size: 12,
            weight: 'normal'
          }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        titleColor: 'white',
        bodyColor: 'white',
        borderColor: this.CORPORATE_COLORS.primary,
        borderWidth: 2,
        cornerRadius: 8
      }
    }
  };

  // ====== DATOS DE GRÁFICOS CON COLORES CORPORATIVOS ======
  public registrosDiariosChart: ChartData<'line'> = {
    labels: [],
    datasets: [{
      label: 'Registros Diarios',
      data: [],
      borderColor: this.CORPORATE_COLORS.primary,
      backgroundColor: 'rgba(245, 158, 11, 0.1)',
      tension: 0.4,
      fill: true,
      pointBackgroundColor: this.CORPORATE_COLORS.primary,
      pointBorderColor: '#ffffff',
      pointBorderWidth: 3,
      pointRadius: 6,
      pointHoverRadius: 8
    }]
  };

  public produccionGranjaChart: ChartData<'bar'> = {
    labels: [],
    datasets: [{
      label: 'Producción por Granja',
      data: [],
      backgroundColor: [
        this.CORPORATE_COLORS.primary,
        this.CORPORATE_COLORS.secondary,
        this.CORPORATE_COLORS.accent,
        this.CORPORATE_COLORS.success,
        this.CORPORATE_COLORS.info,
        '#8b5cf6',
        '#06b6d4',
        '#84cc16'
      ],
      borderColor: [
        '#d97706',
        '#dc2626',
        '#4b5563',
        '#059669',
        '#2563eb',
        '#7c3aed',
        '#0891b2',
        '#65a30d'
      ],
      borderWidth: 2,
      borderRadius: 8,
      borderSkipped: false
    }]
  };

  public mortalidadChart: ChartData<'doughnut'> = {
    labels: [],
    datasets: [{
      label: 'Mortalidad por Fecha',
      data: [],
      backgroundColor: [
        this.CORPORATE_COLORS.secondary,
        this.CORPORATE_COLORS.primary,
        this.CORPORATE_COLORS.accent,
        this.CORPORATE_COLORS.success,
        this.CORPORATE_COLORS.info,
        '#8b5cf6',
        '#06b6d4',
        '#84cc16'
      ],
      borderWidth: 3,
      borderColor: '#ffffff'
    }]
  };

  public distribucionLotesChart: ChartData<'pie'> = {
    labels: [],
    datasets: [{
      label: 'Distribución de Lotes',
      data: [],
      backgroundColor: [
        this.CORPORATE_COLORS.primary,
        this.CORPORATE_COLORS.secondary,
        this.CORPORATE_COLORS.accent,
        this.CORPORATE_COLORS.success,
        this.CORPORATE_COLORS.info,
        '#8b5cf6'
      ],
      borderWidth: 3,
      borderColor: '#ffffff'
    }]
  };

  public inventarioChart: ChartData<'doughnut'> = {
    labels: ['Hembras', 'Machos', 'Mixtas'],
    datasets: [{
      label: 'Inventario de Aves',
      data: [],
      backgroundColor: [
        this.CORPORATE_COLORS.primary,
        this.CORPORATE_COLORS.secondary,
        this.CORPORATE_COLORS.accent
      ],
      borderWidth: 3,
      borderColor: '#ffffff'
    }]
  };

  // ====== COMPUTED PROPERTIES ======
  eficienciaPromedio = computed(() => {
    const metricas = this.metricasRendimiento();
    return metricas ? metricas.eficienciaPromedio : 0;
  });

  tasaMortalidadPromedio = computed(() => {
    const metricas = this.metricasRendimiento();
    return metricas ? metricas.tasaMortalidadPromedio : 0;
  });

  totalAves = computed(() => {
    const inventario = this.estadisticasInventario();
    return inventario ? inventario.totalAvesHembras + inventario.totalAvesMachos + inventario.totalAvesMixtas : 0;
  });

  // ====== REAL-TIME UPDATES ======
  private refreshSubscription?: Subscription;
  private readonly REFRESH_INTERVAL = 30000; // 30 segundos

  ngOnInit(): void {
    this.setupLazyLoading();
    this.loadInitialData();
    this.startRealTimeUpdates();
  }

  ngOnDestroy(): void {
    this.refreshSubscription?.unsubscribe();
    this.destroy$.next(true);
    this.destroy$.complete();
  }

  // ====== CONFIGURACIÓN DE CARGA LENTA ======
  private setupLazyLoading(): void {
    this.loadTrigger$.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(() => this.loadDataProgressively()),
      takeUntil(this.destroy$)
    ).subscribe();
  }

  // ====== CARGA INICIAL ======
  private loadInitialData(): void {
    this.loading.set(true);
    this.error.set(null);
    
    this.loadKPIsData();
    setTimeout(() => this.loadTrigger$.next(true), 100);
  }

  // ====== CARGA PROGRESIVA ======
  private loadDataProgressively() {
    const queue = this.loadingQueue();
    const loaded = this.loadedSections();
    
    if (queue.length === 0) {
      this.loading.set(false);
      return of(null);
    }

    const nextSection = queue[0];
    const newQueue = queue.slice(1);
    this.loadingQueue.set(newQueue);

    switch (nextSection) {
      case 'charts':
        this.loadChartsData();
        break;
      case 'activities':
        this.loadActivitiesData();
        break;
      case 'metrics':
        this.loadMetricsData();
        break;
      case 'reports':
        this.loadReportsData();
        break;
    }
    
    return of(null);
  }

  // ====== CARGA POR SECCIONES ======
  private loadKPIsData(): void {
    this.loadingKPIs.set(true);
    
    this.dashboardService.getEstadisticasGenerales().pipe(
      catchError(error => {
        this.error.set(this.getErrorMessage(error));
        return of(null);
      }),
      finalize(() => {
        this.loadingKPIs.set(false);
        this.markSectionLoaded('kpis');
      })
    ).subscribe(data => {
      if (data) {
        this.estadisticasGenerales.set(data);
      }
    });
  }

  private loadChartsData() {
    this.loadingCharts.set(true);
    
    return forkJoin({
      produccion: this.dashboardService.getProduccionPorGranja(),
      registros: this.dashboardService.getRegistrosDiarios(7),
      mortalidad: this.dashboardService.getEstadisticasMortalidad(30),
      distribucion: this.dashboardService.getDistribucionLotes(),
      inventario: this.dashboardService.getEstadisticasInventario()
    }).pipe(
      catchError(error => {
        this.error.set(this.getErrorMessage(error));
        return of({
          produccion: null,
          registros: null,
          mortalidad: null,
          distribucion: null,
          inventario: null
        });
      }),
      finalize(() => {
        this.loadingCharts.set(false);
        this.markSectionLoaded('charts');
        setTimeout(() => this.loadTrigger$.next(true), 200);
      })
    ).subscribe(data => {
      if (data.produccion) {
        this.produccionPorGranja.set(data.produccion);
        this.updateProduccionChart(data.produccion);
      }
      if (data.registros) {
        this.registrosDiarios.set(data.registros);
        this.updateRegistrosChart(data.registros);
      }
      if (data.mortalidad) {
        this.mortalidades.set(data.mortalidad);
        this.updateMortalidadChart(data.mortalidad);
      }
      if (data.distribucion) {
        this.distribucionLotes.set(data.distribucion);
        this.updateDistribucionChart(data.distribucion);
      }
      if (data.inventario) {
        this.estadisticasInventario.set(data.inventario);
        this.updateInventarioChart(data.inventario);
      }
    });
  }

  private loadActivitiesData() {
    this.loadingActivities.set(true);
    
    this.dashboardService.getActividadesRecientes(10).pipe(
      catchError(error => {
        this.error.set(this.getErrorMessage(error));
        return of([]);
      }),
      finalize(() => {
        this.loadingActivities.set(false);
        this.markSectionLoaded('activities');
        setTimeout(() => this.loadTrigger$.next(true), 200);
      })
    ).subscribe(data => {
      this.actividadesRecientes.set(data);
    });
  }

  private loadMetricsData() {
    this.loadingMetrics.set(true);
    
    this.dashboardService.getMetricasRendimiento().pipe(
      catchError(error => {
        this.error.set(this.getErrorMessage(error));
        return of(null);
      }),
      finalize(() => {
        this.loadingMetrics.set(false);
        this.markSectionLoaded('metrics');
        setTimeout(() => this.loadTrigger$.next(true), 200);
      })
    ).subscribe(data => {
      if (data) {
        this.metricasRendimiento.set(data);
      }
    });
  }

  private loadReportsData() {
    this.loadingReports.set(true);
    
    // Aquí se pueden cargar reportes adicionales
    setTimeout(() => {
      this.loadingReports.set(false);
      this.markSectionLoaded('reports');
      this.lastUpdated.set(new Date());
    }, 500);
  }

  private markSectionLoaded(section: string): void {
    const loaded = new Set(this.loadedSections());
    loaded.add(section);
    this.loadedSections.set(loaded);
  }

  // ====== CARGA COMPLETA (PARA REFRESH MANUAL) ======
  loadAllData(): void {
    this.loading.set(true);
    this.error.set(null);
    this.loadedSections.set(new Set());
    this.loadingQueue.set(['kpis', 'charts', 'activities', 'metrics', 'reports']);
    
    this.loadInitialData();
  }

  // ====== ACTUALIZACIÓN DE GRÁFICOS ======
  updateRegistrosChart(registros: RegistroDiarioDto[]): void {
    this.registrosDiariosChart = {
      labels: registros.map(r => this.dashboardService.formatDate(r.fecha)),
      datasets: [{
        label: 'Registros Diarios',
        data: registros.map(r => r.totalRegistros),
        borderColor: this.CORPORATE_COLORS.primary,
        backgroundColor: 'rgba(245, 158, 11, 0.1)',
        tension: 0.4,
        fill: true,
        pointBackgroundColor: this.CORPORATE_COLORS.primary,
        pointBorderColor: '#ffffff',
        pointBorderWidth: 3,
        pointRadius: 6,
        pointHoverRadius: 8
      }]
    };
  }

  updateProduccionChart(produccion: ProduccionGranjaDto[]): void {
    this.produccionGranjaChart = {
      labels: produccion.map(p => p.granjaNombre),
      datasets: [{
        label: 'Producción por Granja',
        data: produccion.map(p => p.totalHuevos),
        backgroundColor: [
          this.CORPORATE_COLORS.primary,
          this.CORPORATE_COLORS.secondary,
          this.CORPORATE_COLORS.accent,
          this.CORPORATE_COLORS.success,
          this.CORPORATE_COLORS.info,
          '#8b5cf6',
          '#06b6d4',
          '#84cc16'
        ],
        borderColor: [
          '#d97706',
          '#dc2626',
          '#4b5563',
          '#059669',
          '#2563eb',
          '#7c3aed',
          '#0891b2',
          '#65a30d'
        ],
        borderWidth: 2,
        borderRadius: 8,
        borderSkipped: false
      }]
    };
  }

  updateMortalidadChart(mortalidades: MortalidadDto[]): void {
    const agrupadas = mortalidades.reduce((acc, m) => {
      const fecha = this.dashboardService.formatDate(m.fecha);
      acc[fecha] = (acc[fecha] || 0) + m.cantidadMuertas;
      return acc;
    }, {} as Record<string, number>);

    this.mortalidadChart = {
      labels: Object.keys(agrupadas),
      datasets: [{
        label: 'Mortalidad por Fecha',
        data: Object.values(agrupadas),
        backgroundColor: [
          this.CORPORATE_COLORS.secondary,
          this.CORPORATE_COLORS.primary,
          this.CORPORATE_COLORS.accent,
          this.CORPORATE_COLORS.success,
          this.CORPORATE_COLORS.info,
          '#8b5cf6',
          '#06b6d4',
          '#84cc16'
        ],
        borderWidth: 3,
        borderColor: '#ffffff'
      }]
    };
  }

  updateDistribucionChart(distribucion: DistribucionLotesDto[]): void {
    this.distribucionLotesChart = {
      labels: distribucion.map(d => d.granjaNombre),
      datasets: [{
        label: 'Distribución de Lotes',
        data: distribucion.map(d => d.totalLotes),
        backgroundColor: [
          this.CORPORATE_COLORS.primary,
          this.CORPORATE_COLORS.secondary,
          this.CORPORATE_COLORS.accent,
          this.CORPORATE_COLORS.success,
          this.CORPORATE_COLORS.info,
          '#8b5cf6'
        ],
        borderWidth: 3,
        borderColor: '#ffffff'
      }]
    };
  }

  updateInventarioChart(inventario: InventarioEstadisticasDto): void {
    this.inventarioChart = {
      labels: ['Hembras', 'Machos', 'Mixtas'],
      datasets: [{
        label: 'Inventario de Aves',
        data: [inventario.totalAvesHembras, inventario.totalAvesMachos, inventario.totalAvesMixtas],
        backgroundColor: [
          this.CORPORATE_COLORS.primary,
          this.CORPORATE_COLORS.secondary,
          this.CORPORATE_COLORS.accent
        ],
        borderWidth: 3,
        borderColor: '#ffffff'
      }]
    };
  }

  // ====== REAL-TIME UPDATES ======
  startRealTimeUpdates(): void {
    if (this.isRealTimeEnabled()) {
      this.refreshSubscription = interval(this.REFRESH_INTERVAL)
        .subscribe(() => {
          this.loadAllData();
        });
    }
  }

  toggleRealTime(): void {
    this.isRealTimeEnabled.set(!this.isRealTimeEnabled());
    
    if (this.isRealTimeEnabled()) {
      this.startRealTimeUpdates();
    } else {
      this.refreshSubscription?.unsubscribe();
    }
  }

  // ====== UTILIDADES ======
  private getErrorMessage(error: any): string {
    return error?.error?.message || 
           error?.error?.title || 
           error?.message || 
           'Error desconocido';
  }

  formatNumber(num: number): string {
    return this.dashboardService.formatNumber(num);
  }

  formatPercentage(num: number): string {
    return this.dashboardService.formatPercentage(num);
  }

  formatDate(date: string): string {
    return this.dashboardService.formatDate(date);
  }

  formatDateTime(date: string): string {
    return this.dashboardService.formatDateTime(date);
  }

  getTimeAgo(date: string): string {
    return this.dashboardService.getTimeAgo(date);
  }

  refreshData(): void {
    this.loadAllData();
  }

  // ====== NAVEGACIÓN LAZY ======
  onDailyVisible(): void {
    // Implementar carga lazy si es necesario
  }

  onProductionVisible(): void {
    // Implementar carga lazy si es necesario
  }

  onMortalityVisible(): void {
    // Implementar carga lazy si es necesario
  }

  onActivitiesVisible(): void {
    // Implementar carga lazy si es necesario
  }

  // ====== EFECTOS VISUALES ======
  @HostListener('window:scroll', ['$event'])
  onWindowScroll(): void {
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
    this.isScrolled.set(scrollTop > 50);
  }

  @HostListener('window:resize', ['$event'])
  onWindowResize(): void {
    this.handleResponsiveLayout();
  }

  ngAfterViewInit(): void {
    this.initializeAnimations();
    this.setupScrollEffects();
    this.handleResponsiveLayout();
  }

  private initializeAnimations(): void {
    // Configurar delays escalonados para las animaciones
    const sections = ['kpis', 'charts', 'activities', 'metrics', 'reports'];
    sections.forEach((section, index) => {
      setTimeout(() => {
        this.animationDelay.set(index * 100);
      }, index * 100);
    });
  }

  private setupScrollEffects(): void {
    // Efecto de parallax sutil en el header
    fromEvent(window, 'scroll')
      .pipe(
        throttleTime(10),
        takeUntil(this.destroy$)
      )
      .subscribe(() => {
        const scrollTop = window.pageYOffset;
        const header = this.elementRef.nativeElement.querySelector('.main-header') as HTMLElement;
        if (header) {
          const parallaxOffset = scrollTop * 0.1;
          header.style.transform = `translateY(${parallaxOffset}px)`;
        }
      });
  }

  private handleResponsiveLayout(): void {
    const width = window.innerWidth;
    if (width <= 768) {
      this.isSidebarOpen.set(false);
    }
  }

  toggleSidebar(): void {
    this.isSidebarOpen.set(!this.isSidebarOpen());
  }

  // ====== EFECTOS DE HOVER Y INTERACCIÓN ======
  onCardHover(event: Event, cardType: string): void {
    const card = event.currentTarget as HTMLElement;
    if (card) {
      card.style.transform = 'translateY(-8px) scale(1.02)';
      card.style.boxShadow = '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)';
      
      // Efecto de brillo
      const glow = document.createElement('div');
      glow.className = 'card-glow';
      glow.style.cssText = `
        position: absolute;
        top: 0;
        left: 0;
        right: 0;
        bottom: 0;
        background: linear-gradient(45deg, transparent 30%, rgba(220, 38, 38, 0.1) 50%, transparent 70%);
        pointer-events: none;
        opacity: 0;
        transition: opacity 0.3s ease;
      `;
      card.appendChild(glow);
      
      setTimeout(() => {
        glow.style.opacity = '1';
      }, 10);
    }
  }

  onCardLeave(event: Event): void {
    const card = event.currentTarget as HTMLElement;
    if (card) {
      card.style.transform = 'translateY(0) scale(1)';
      card.style.boxShadow = '';
      
      const glow = card.querySelector('.card-glow') as HTMLElement;
      if (glow) {
        glow.style.opacity = '0';
        setTimeout(() => {
          glow.remove();
        }, 300);
      }
    }
  }

  // ====== EFECTOS DE NOTIFICACIÓN ======
  showNotification(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;
    notification.style.cssText = `
      position: fixed;
      top: 20px;
      right: 20px;
      background: ${type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : '#3b82f6'};
      color: white;
      padding: 1rem 1.5rem;
      border-radius: 0.5rem;
      box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1);
      z-index: 1000;
      transform: translateX(100%);
      transition: transform 0.3s ease;
    `;
    notification.textContent = message;
    
    document.body.appendChild(notification);
    
    setTimeout(() => {
      notification.style.transform = 'translateX(0)';
    }, 10);
    
    setTimeout(() => {
      notification.style.transform = 'translateX(100%)';
      setTimeout(() => {
        notification.remove();
      }, 300);
    }, 3000);
  }

  // ====== EFECTOS DE PULSO ======
  addPulseEffect(element: HTMLElement): void {
    element.style.animation = 'pulse 1s ease-in-out';
    setTimeout(() => {
      element.style.animation = '';
    }, 1000);
  }

  // ====== EFECTOS DE SHIMMER ======
  addShimmerEffect(element: HTMLElement): void {
    element.style.background = 'linear-gradient(90deg, #f3f4f6 25%, #e5e7eb 50%, #f3f4f6 75%)';
    element.style.backgroundSize = '200% 100%';
    element.style.animation = 'shimmer 1.5s infinite';
  }

  removeShimmerEffect(element: HTMLElement): void {
    element.style.background = '';
    element.style.backgroundSize = '';
    element.style.animation = '';
  }
}