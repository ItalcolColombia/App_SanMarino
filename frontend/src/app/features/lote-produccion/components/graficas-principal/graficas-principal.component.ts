import { Component, Input, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NgChartsModule } from 'ng2-charts';
import { LoteProduccionDto } from '../../services/lote-produccion.service';
import { LoteDto } from '../../../lote/services/lote.service';

@Component({
  selector: 'app-graficas-principal',
  standalone: true,
  imports: [CommonModule, NgChartsModule],
  templateUrl: './graficas-principal.component.html',
  styleUrls: ['./graficas-principal.component.scss']
})
export class GraficasPrincipalComponent implements OnInit, OnChanges {
  @Input() registros: LoteProduccionDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  // Datos para gráficos
  mortalidadData: any = null;
  consumoData: any = null;
  produccionData: any = null;
  eficienciaData: any = null;

  // Opciones de gráficos
  lineOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
        labels: {
          color: '#6b7280',
          font: {
            weight: 'normal'
          }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        titleColor: '#fff',
        bodyColor: '#fff',
        borderColor: '#f59e0b',
        borderWidth: 1
      }
    },
    scales: {
      x: {
        ticks: {
          color: '#6b7280'
        },
        grid: {
          color: '#e5e7eb'
        }
      },
      y: {
        ticks: {
          color: '#6b7280'
        },
        grid: {
          color: '#e5e7eb'
        }
      }
    }
  };

  barOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'top',
        labels: {
          color: '#6b7280',
          font: {
            weight: 'normal'
          }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        titleColor: '#fff',
        bodyColor: '#fff',
        borderColor: '#f59e0b',
        borderWidth: 1
      }
    },
    scales: {
      x: {
        ticks: {
          color: '#6b7280'
        },
        grid: {
          color: '#e5e7eb'
        }
      },
      y: {
        ticks: {
          color: '#6b7280'
        },
        grid: {
          color: '#e5e7eb'
        }
      }
    }
  };

  doughnutOptions: any = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          color: '#6b7280',
          font: {
            weight: 'normal'
          }
        }
      },
      tooltip: {
        backgroundColor: 'rgba(0, 0, 0, 0.8)',
        titleColor: '#fff',
        bodyColor: '#fff',
        borderColor: '#f59e0b',
        borderWidth: 1
      }
    }
  };

  constructor() { }

  ngOnInit(): void {
    this.prepararDatosGraficos();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['registros'] || changes['selectedLote']) {
      this.prepararDatosGraficos();
    }
  }

  prepararDatosGraficos(): void {
    if (!this.registros.length) {
      this.limpiarDatos();
      return;
    }

    // Ordenar registros por fecha
    const registrosOrdenados = [...this.registros].sort((a, b) => 
      new Date(a.fecha).getTime() - new Date(b.fecha).getTime()
    );

    const fechas = registrosOrdenados.map(r => this.formatearFecha(r.fecha));
    const edades = registrosOrdenados.map(r => this.calcularEdadDias(r.fecha));

    // Gráfico de Mortalidad
    this.mortalidadData = {
      labels: fechas,
      datasets: [
        {
          label: 'Mortalidad Hembras',
          data: registrosOrdenados.map(r => r.mortalidadH),
          borderColor: '#ef4444',
          backgroundColor: 'rgba(239, 68, 68, 0.1)',
          tension: 0.4
        },
        {
          label: 'Mortalidad Machos',
          data: registrosOrdenados.map(r => r.mortalidadM),
          borderColor: '#dc2626',
          backgroundColor: 'rgba(220, 38, 38, 0.1)',
          tension: 0.4
        }
      ]
    };

    // Gráfico de Consumo
    this.consumoData = {
      labels: fechas,
      datasets: [
        {
          label: 'Consumo Kg Hembras',
          data: registrosOrdenados.map(r => r.consKgH),
          borderColor: '#f59e0b',
          backgroundColor: 'rgba(245, 158, 11, 0.1)',
          tension: 0.4
        },
        {
          label: 'Consumo Kg Machos',
          data: registrosOrdenados.map(r => r.consKgM),
          borderColor: '#d97706',
          backgroundColor: 'rgba(217, 119, 6, 0.1)',
          tension: 0.4
        }
      ]
    };

    // Gráfico de Producción
    this.produccionData = {
      labels: fechas,
      datasets: [
        {
          label: 'Huevos Totales',
          data: registrosOrdenados.map(r => r.huevoTot),
          borderColor: '#22c55e',
          backgroundColor: 'rgba(34, 197, 94, 0.1)',
          tension: 0.4
        },
        {
          label: 'Huevos Incubables',
          data: registrosOrdenados.map(r => r.huevoInc),
          borderColor: '#16a34a',
          backgroundColor: 'rgba(22, 163, 74, 0.1)',
          tension: 0.4
        }
      ]
    };

    // Gráfico de Eficiencia (Doughnut)
    const totalHuevos = registrosOrdenados.reduce((sum, r) => sum + r.huevoTot, 0);
    const totalIncubables = registrosOrdenados.reduce((sum, r) => sum + r.huevoInc, 0);
    const noIncubables = totalHuevos - totalIncubables;

    this.eficienciaData = {
      labels: ['Incubables', 'No Incubables'],
      datasets: [
        {
          data: [totalIncubables, noIncubables],
          backgroundColor: [
            'rgba(34, 197, 94, 0.8)',
            'rgba(239, 68, 68, 0.8)'
          ],
          borderColor: [
            '#22c55e',
            '#ef4444'
          ],
          borderWidth: 2
        }
      ]
    };
  }

  calcularEdadDias(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 0;
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(1, diffDays);
  }

  formatearFecha(fecha: string | Date): string {
    const d = new Date(fecha);
    return d.toLocaleDateString('es-ES', { 
      day: '2-digit', 
      month: '2-digit' 
    });
  }

  limpiarDatos(): void {
    this.mortalidadData = null;
    this.consumoData = null;
    this.produccionData = null;
    this.eficienciaData = null;
  }

  getTotalRegistros(): number {
    return this.registros.length;
  }

  getPromedioHuevos(): number {
    if (!this.registros.length) return 0;
    const total = this.registros.reduce((sum, r) => sum + r.huevoTot, 0);
    return total / this.registros.length;
  }

  getPromedioIncubables(): number {
    if (!this.registros.length) return 0;
    const total = this.registros.reduce((sum, r) => sum + r.huevoInc, 0);
    return total / this.registros.length;
  }

  getEficienciaPromedio(): number {
    if (!this.registros.length) return 0;
    const totalHuevos = this.registros.reduce((sum, r) => sum + r.huevoTot, 0);
    const totalIncubables = this.registros.reduce((sum, r) => sum + r.huevoInc, 0);
    return totalHuevos > 0 ? (totalIncubables / totalHuevos) * 100 : 0;
  }
}
