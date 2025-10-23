import { Component, Input, Output, EventEmitter, OnInit, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SeguimientoLoteLevanteDto } from '../../services/seguimiento-lote-levante.service';
import { LoteDto } from '../../../lote/services/lote.service';
import { GuiaGeneticaService } from '../../../../services/guia-genetica.service';

interface IndicadorSemanal {
  semana: number;
  fechaInicio: string;
  fechaFin: string;
  avesInicioSemana: number;
  avesFinSemana: number;
  consumoReal: number;
  consumoTabla: number;
  conversionAlimenticia: number;
  gananciaSemana: number;
  gananciaDiariaAcumulada: number;
  mortalidadSem: number;
  seleccionSem: number;
  mortalidadMasSeleccion: number;
  eficiencia: number;
  ip: number;
  vpi: number;
  saldoAvesSemanal: number;
  mortalidadAcum: number;
  seleccionAcum: number;
  mortalidadMasSeleccionAcum: number;
  pisoTermicoVisible: boolean;
  pesoInicial: number;
  pesoCierre: number;
  pesoAnterior: number;
}

@Component({
  selector: 'app-tabla-lista-indicadores',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tabla-lista-indicadores.component.html',
  styleUrls: ['./tabla-lista-indicadores.component.scss']
})
export class TablaListaIndicadoresComponent implements OnInit, OnChanges {
  @Input() seguimientos: SeguimientoLoteLevanteDto[] = [];
  @Input() selectedLote: LoteDto | null = null;
  @Input() loading: boolean = false;

  // Datos calculados
  indicadoresSemanales: IndicadorSemanal[] = [];
  
  // Control de peticiones secuenciales
  private peticionesEnCola: boolean = false;
  private cacheGuiaGenetica: Map<string, any> = new Map();

  constructor(private guiaGeneticaService: GuiaGeneticaService) { }

  ngOnInit(): void {
    this.calcularIndicadores().catch(error => {
      console.error('Error calculando indicadores:', error);
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['seguimientos'] || changes['selectedLote']) {
      // Limpiar cache cuando cambie el lote
      if (changes['selectedLote']) {
        console.log('🔄 Lote cambiado, limpiando cache...');
        this.cacheGuiaGenetica.clear();
        this.peticionesEnCola = false;
      }
      
      this.calcularIndicadores().catch(error => {
        console.error('Error calculando indicadores:', error);
      });
    }
  }

  // ================== CÁLCULOS DE INDICADORES ==================
  private async calcularIndicadores(): Promise<void> {
    if (!this.seguimientos || this.seguimientos.length === 0 || !this.selectedLote) {
      this.indicadoresSemanales = [];
      return;
    }

    // Agrupar registros por semana
    const registrosPorSemana = this.agruparPorSemana(this.seguimientos);
    
    // Calcular indicadores para cada semana
    this.indicadoresSemanales = await this.calcularIndicadoresSemanales(registrosPorSemana);
    
    // 🔍 VALIDACIÓN AUTOMÁTICA: Ejecutar validación después de calcular
    console.log('🔍 Ejecutando validación automática de tabla genética...');
    await this.validarUsoTablaGenetica();
  }

  private agruparPorSemana(registros: SeguimientoLoteLevanteDto[]): Map<number, SeguimientoLoteLevanteDto[]> {
    const grupos = new Map<number, SeguimientoLoteLevanteDto[]>();
    
    registros.forEach(registro => {
      const semana = this.calcularSemana(registro.fechaRegistro);
      if (!grupos.has(semana)) {
        grupos.set(semana, []);
      }
      grupos.get(semana)!.push(registro);
    });

    // Ordenar registros dentro de cada semana por fecha
    grupos.forEach((registros, semana) => {
      registros.sort((a, b) => new Date(a.fechaRegistro).getTime() - new Date(b.fechaRegistro).getTime());
    });

    return grupos;
  }

  private calcularSemana(fechaRegistro: string | Date): number {
    if (!this.selectedLote?.fechaEncaset) return 1;
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const fechaReg = new Date(fechaRegistro);
    const diffTime = fechaReg.getTime() - fechaEncaset.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    
    return Math.max(1, Math.ceil(diffDays / 7));
  }

  private async calcularIndicadoresSemanales(grupos: Map<number, SeguimientoLoteLevanteDto[]>): Promise<IndicadorSemanal[]> {
    const indicadores: IndicadorSemanal[] = [];
    const semanas = Array.from(grupos.keys()).sort((a, b) => a - b);
    
    let avesAcumuladas = this.selectedLote?.avesEncasetadas || 0;
    let mortalidadAcumulada = 0;
    let seleccionAcumulada = 0;
    let pesoAnterior = this.selectedLote?.pesoInicialH || 0;

    // 🔧 CONTROL SECUENCIAL: Procesar semanas una por una para evitar colisiones
    console.log(`🔄 Procesando ${semanas.length} semanas secuencialmente...`);
    
    for (let i = 0; i < semanas.length; i++) {
      const semana = semanas[i];
      const registros = grupos.get(semana) || [];
      
      console.log(`📊 Procesando semana ${semana} (${i + 1}/${semanas.length})...`);
      
      // Esperar a que termine la petición anterior antes de continuar
      if (this.peticionesEnCola) {
        console.log(`⏳ Esperando que termine la petición anterior...`);
        await this.esperarPeticionAnterior();
      }
      
      const indicador = await this.calcularIndicadorSemana(semana, registros, avesAcumuladas, mortalidadAcumulada, seleccionAcumulada, pesoAnterior);
      
      indicadores.push(indicador);
      
      // Actualizar acumulados para la siguiente semana
      avesAcumuladas = indicador.avesFinSemana;
      mortalidadAcumulada += indicador.mortalidadSem;
      seleccionAcumulada += indicador.seleccionSem;
      pesoAnterior = indicador.pesoCierre;
      
      console.log(`✅ Semana ${semana} procesada exitosamente`);
    }

    console.log(`🎉 Todas las semanas procesadas secuencialmente`);
    return indicadores;
  }

  private async calcularIndicadorSemana(
    semana: number, 
    registros: SeguimientoLoteLevanteDto[], 
    avesInicio: number,
    mortalidadAcum: number,
    seleccionAcum: number,
    pesoAnterior: number
  ): Promise<IndicadorSemanal> {
    // Calcular totales de la semana
    const mortalidadTotal = registros.reduce((sum, r) => sum + (r.mortalidadHembras || 0) + (r.mortalidadMachos || 0), 0);
    const seleccionTotal = registros.reduce((sum, r) => sum + (r.selH || 0) + (r.selM || 0), 0);
    const consumoTotal = registros.reduce((sum, r) => sum + (r.consumoKgHembras || 0) + (r.consumoKgMachos || 0), 0);
    
    // Aves al final de la semana
    const avesFin = avesInicio - mortalidadTotal - seleccionTotal;
    
    // Peso promedio de la semana (usar el último registro de la semana)
    const ultimoRegistro = registros[registros.length - 1];
    const pesoPromedio = ((ultimoRegistro?.pesoPromH || 0) + (ultimoRegistro?.pesoPromM || 0)) / 2;
    
    // 🔧 MEJORA: Consumo real en gramos (convertir de kg a gramos)
    const consumoTotalGramos = consumoTotal * 1000;
    
    // 🔧 MEJORA: Consumo por ave (más preciso)
    const avesPromedio = (avesInicio + avesFin) / 2;
    const consumoRealPorAve = avesPromedio > 0 ? consumoTotalGramos / avesPromedio : 0;
    
    // 🔧 MEJORA: Consumo tabla (valor de referencia de guía genética)
    const consumoTablaPorAve = await this.obtenerConsumoTabla(semana);
    
    // 🔧 MEJORA: Conversión alimenticia (FCR) - Consumo por ave / Ganancia por ave
    const gananciaSemana = pesoPromedio - pesoAnterior;
    const conversionAlimenticia = gananciaSemana > 0 ? consumoRealPorAve / gananciaSemana : 0;
    
    // 🔧 MEJORA: Ganancia diaria acumulada
    const gananciaDiariaAcumulada = gananciaSemana / 7;
    
    // Porcentajes
    const mortalidadSem = avesInicio > 0 ? (mortalidadTotal / avesInicio) * 100 : 0;
    const seleccionSem = avesInicio > 0 ? (seleccionTotal / avesInicio) * 100 : 0;
    const mortalidadMasSeleccion = mortalidadSem + seleccionSem;
    
    // 🔧 MEJORA: Eficiencia (Ganancia por ave / Consumo por ave)
    const eficiencia = consumoRealPorAve > 0 ? gananciaSemana / consumoRealPorAve : 0;
    
    // 🔧 MEJORA: IP (Índice de Productividad) - Combinación de eficiencia y supervivencia
    const supervivencia = avesInicio > 0 ? avesFin / avesInicio : 0;
    const ip = eficiencia * supervivencia;
    
    // 🔧 MEJORA: VPI (Índice de Vitalidad) - Relación entre supervivencia y rendimiento
    const vpi = supervivencia * eficiencia;
    
    // Saldo de aves semanal
    const saldoAvesSemanal = avesFin;
    
    // Acumulados
    const mortalidadAcumTotal = mortalidadAcum + mortalidadSem;
    const seleccionAcumTotal = seleccionAcum + seleccionSem;
    const mortalidadMasSeleccionAcumTotal = mortalidadAcumTotal + seleccionAcumTotal;
    
    // 🔧 MEJORA: Piso térmico desde guía genética
    const pisoTermicoVisible = await this.obtenerPisoTermico(semana);

    return {
      semana,
      fechaInicio: this.obtenerFechaInicioSemana(semana),
      fechaFin: this.obtenerFechaFinSemana(semana),
      avesInicioSemana: avesInicio,
      avesFinSemana: avesFin,
      consumoReal: consumoRealPorAve, // 🔧 MEJORA: Ahora es por ave
      consumoTabla: consumoTablaPorAve, // 🔧 MEJORA: Ahora es por ave
      conversionAlimenticia,
      gananciaSemana,
      gananciaDiariaAcumulada,
      mortalidadSem,
      seleccionSem,
      mortalidadMasSeleccion,
      eficiencia,
      ip,
      vpi,
      saldoAvesSemanal,
      mortalidadAcum: mortalidadAcumTotal,
      seleccionAcum: seleccionAcumTotal,
      mortalidadMasSeleccionAcum: mortalidadMasSeleccionAcumTotal,
      pisoTermicoVisible,
      pesoInicial: pesoAnterior,
      pesoCierre: pesoPromedio,
      pesoAnterior
    };
  }

  // ================== VALIDACIÓN Y DEBUGGING ==================
  
  /**
   * Valida que el sistema esté usando correctamente la tabla genética
   */
  async validarUsoTablaGenetica(): Promise<void> {
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      console.warn('⚠️ No se puede validar: Lote sin raza o año tabla genética');
      return;
    }

    console.log('🔍 === VALIDACIÓN DE TABLA GENÉTICA ===');
    console.log(`📋 Lote: ${this.selectedLote.loteNombre}`);
    console.log(`🧬 Raza: ${this.selectedLote.raza}`);
    console.log(`📅 Año Tabla: ${this.selectedLote.anoTablaGenetica}`);
    console.log(`📊 Semanas a validar: ${this.indicadoresSemanales.length}`);

    let semanasValidadas = 0;
    let semanasConErrores = 0;
    let consumoPromedioDiferencia = 0;
    let pisoTermicoCorrecto = 0;

    // Validar cada semana
    for (const indicador of this.indicadoresSemanales) {
      const resultado = await this.validarSemanaIndicador(indicador);
      semanasValidadas++;
      
      if (resultado.tieneErrores) {
        semanasConErrores++;
      }
      
      consumoPromedioDiferencia += resultado.diferenciaConsumo;
      if (resultado.pisoTermicoCorrecto) {
        pisoTermicoCorrecto++;
      }
    }

    // Mostrar resumen
    console.log('\n📊 === RESUMEN DE VALIDACIÓN ===');
    console.log(`✅ Semanas validadas: ${semanasValidadas}`);
    console.log(`❌ Semanas con errores: ${semanasConErrores}`);
    console.log(`📈 Diferencia promedio de consumo: ${(consumoPromedioDiferencia / semanasValidadas).toFixed(2)}%`);
    console.log(`🔥 Piso térmico correcto: ${pisoTermicoCorrecto}/${semanasValidadas} semanas`);
    
    if (semanasConErrores === 0) {
      console.log('🎉 ¡EXCELENTE! Todas las semanas están usando correctamente la tabla genética');
    } else if (semanasConErrores <= semanasValidadas * 0.2) {
      console.log('✅ BUENO: La mayoría de semanas están correctas');
    } else {
      console.log('⚠️ ATENCIÓN: Hay problemas significativos en el uso de la tabla genética');
    }

    console.log('✅ === VALIDACIÓN COMPLETADA ===');
  }

  /**
   * Valida los datos de una semana específica
   */
  private async validarSemanaIndicador(indicador: IndicadorSemanal): Promise<{
    tieneErrores: boolean;
    diferenciaConsumo: number;
    pisoTermicoCorrecto: boolean;
  }> {
    const semana = indicador.semana;
    console.log(`\n📊 === VALIDANDO SEMANA ${semana} ===`);

    let tieneErrores = false;
    let diferenciaConsumo = 0;
    let pisoTermicoCorrecto = false;

    try {
      // Obtener datos reales de la guía genética
      const datosGuia = await this.obtenerDatosCompletosGuia(semana);
      
      if (datosGuia) {
        console.log(`✅ Datos de guía genética obtenidos para semana ${semana}:`);
        console.log(`   🍽️ Consumo Hembras: ${datosGuia.consumoHembras}g/ave/día`);
        console.log(`   🍽️ Consumo Machos: ${datosGuia.consumoMachos}g/ave/día`);
        console.log(`   ⚖️ Peso Hembras: ${datosGuia.pesoHembras}g`);
        console.log(`   ⚖️ Peso Machos: ${datosGuia.pesoMachos}g`);
        console.log(`   💀 Mortalidad Hembras: ${datosGuia.mortalidadHembras}%`);
        console.log(`   💀 Mortalidad Machos: ${datosGuia.mortalidadMachos}%`);
        console.log(`   📏 Uniformidad: ${datosGuia.uniformidad}%`);
        console.log(`   🔥 Piso Térmico: ${datosGuia.pisoTermicoRequerido ? 'Sí' : 'No'}`);

        // Validar consumo calculado vs tabla
        const consumoPromedioTabla = (datosGuia.consumoHembras + datosGuia.consumoMachos) / 2;
        diferenciaConsumo = Math.abs(indicador.consumoTabla - consumoPromedioTabla);
        const porcentajeDiferencia = (diferenciaConsumo / consumoPromedioTabla) * 100;

        console.log(`\n🔍 === COMPARACIÓN DE CONSUMO ===`);
        console.log(`   📊 Consumo Calculado: ${indicador.consumoTabla.toFixed(2)}g/ave/día`);
        console.log(`   📋 Consumo Tabla: ${consumoPromedioTabla.toFixed(2)}g/ave/día`);
        console.log(`   📈 Diferencia: ${diferenciaConsumo.toFixed(2)}g (${porcentajeDiferencia.toFixed(1)}%)`);

        if (porcentajeDiferencia <= 1) {
          console.log(`   ✅ EXCELENTE: Consumo coincide perfectamente`);
        } else if (porcentajeDiferencia <= 5) {
          console.log(`   ✅ BUENO: Consumo muy cercano a la tabla`);
        } else if (porcentajeDiferencia <= 15) {
          console.log(`   ⚠️ ACEPTABLE: Consumo dentro del rango aceptable`);
        } else {
          console.log(`   ❌ PROBLEMA: Consumo muy diferente de la tabla`);
          tieneErrores = true;
        }

        // Validar piso térmico
        console.log(`\n🔍 === VALIDACIÓN PISO TÉRMICO ===`);
        console.log(`   🔥 Piso Térmico Calculado: ${indicador.pisoTermicoVisible ? 'Sí' : 'No'}`);
        console.log(`   📋 Piso Térmico Tabla: ${datosGuia.pisoTermicoRequerido ? 'Sí' : 'No'}`);
        
        pisoTermicoCorrecto = indicador.pisoTermicoVisible === datosGuia.pisoTermicoRequerido;
        
        if (pisoTermicoCorrecto) {
          console.log(`   ✅ CORRECTO: Piso térmico coincide con la tabla`);
        } else {
          console.log(`   ❌ ERROR: Piso térmico no coincide con la tabla`);
          tieneErrores = true;
        }

        // Validar indicadores calculados
        console.log(`\n🔍 === INDICADORES CALCULADOS ===`);
        console.log(`   🍽️ Consumo Real: ${indicador.consumoReal.toFixed(2)}g/ave/día`);
        console.log(`   📈 Ganancia Semana: ${indicador.gananciaSemana.toFixed(2)}g/día`);
        console.log(`   🔄 Conversión Alimenticia: ${indicador.conversionAlimenticia.toFixed(2)}`);
        console.log(`   ⚡ Eficiencia: ${indicador.eficiencia.toFixed(2)}`);
        console.log(`   📊 IP: ${indicador.ip.toFixed(2)}`);
        console.log(`   💪 VPI: ${indicador.vpi.toFixed(2)}`);

      } else {
        console.log(`❌ No se pudieron obtener datos de guía genética para semana ${semana}`);
        console.log(`   🔄 Usando valores por defecto`);
        tieneErrores = true;
      }

    } catch (error) {
      console.error(`❌ Error validando semana ${semana}:`, error);
      tieneErrores = true;
    }

    return {
      tieneErrores,
      diferenciaConsumo,
      pisoTermicoCorrecto
    };
  }

  /**
   * Obtiene datos completos de la guía genética para una semana
   */
  private async obtenerDatosCompletosGuia(semana: number): Promise<any> {
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return null;
    }

    try {
      const response = await this.guiaGeneticaService.obtenerGuiaGenetica(
        this.selectedLote.raza,
        this.selectedLote.anoTablaGenetica,
        semana
      ).toPromise();

      return response?.datos || null;
    } catch (error) {
      console.error(`Error obteniendo datos completos de guía genética para semana ${semana}:`, error);
      return null;
    }
  }

  // ================== CONTROL DE PETICIONES SECUENCIALES ==================
  private async esperarPeticionAnterior(): Promise<void> {
    return new Promise((resolve) => {
      const checkInterval = setInterval(() => {
        if (!this.peticionesEnCola) {
          clearInterval(checkInterval);
          resolve();
        }
      }, 100); // Verificar cada 100ms
    });
  }

  private generarClaveCache(raza: string, anoTabla: number, edad: number): string {
    return `${raza}-${anoTabla}-${edad}`;
  }

  // ================== HELPERS DE CONSUMO TABLA ==================
  private async obtenerConsumoTabla(semana: number): Promise<number> {
    // 🔧 MEJORA: Obtener consumo de la guía genética real con cache y control secuencial
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return this.obtenerConsumoPorDefecto(semana);
    }

    const claveCache = this.generarClaveCache(this.selectedLote.raza, this.selectedLote.anoTablaGenetica, semana);
    
    // Verificar cache primero
    if (this.cacheGuiaGenetica.has(claveCache)) {
      console.log(`📋 Cache hit para semana ${semana}`);
      const datosCache = this.cacheGuiaGenetica.get(claveCache);
      return (datosCache.consumoHembras + datosCache.consumoMachos) / 2;
    }

    try {
      // Marcar que hay una petición en curso
      this.peticionesEnCola = true;
      console.log(`🌐 Petición a API para semana ${semana}...`);
      
      const consumoEsperado = await this.guiaGeneticaService.obtenerConsumoEsperado(
        this.selectedLote.raza,
        this.selectedLote.anoTablaGenetica,
        semana
      );

      // Guardar en cache
      if (consumoEsperado > 0) {
        this.cacheGuiaGenetica.set(claveCache, {
          consumoHembras: consumoEsperado,
          consumoMachos: consumoEsperado,
          semana: semana
        });
        console.log(`💾 Datos guardados en cache para semana ${semana}`);
      }

      // Marcar que la petición terminó
      this.peticionesEnCola = false;
      
      return consumoEsperado > 0 ? consumoEsperado : this.obtenerConsumoPorDefecto(semana);
    } catch (error) {
      console.error(`❌ Error obteniendo consumo de guía genética para semana ${semana}:`, error);
      this.peticionesEnCola = false;
      return this.obtenerConsumoPorDefecto(semana);
    }
  }

  private obtenerConsumoPorDefecto(semana: number): number {
    // Tabla de consumo por defecto (fallback)
    const tablaConsumo: { [key: number]: number } = {
      1: 15,   // Semana 1: 15g/ave/día
      2: 25,   // Semana 2: 25g/ave/día
      3: 35,   // Semana 3: 35g/ave/día
      4: 45,   // Semana 4: 45g/ave/día
      5: 55,   // Semana 5: 55g/ave/día
      6: 65,   // Semana 6: 65g/ave/día
      7: 75,   // Semana 7: 75g/ave/día
      8: 85,   // Semana 8: 85g/ave/día
      9: 95,   // Semana 9: 95g/ave/día
      10: 105, // Semana 10: 105g/ave/día
      11: 115, // Semana 11: 115g/ave/día
      12: 125, // Semana 12: 125g/ave/día
      13: 135, // Semana 13: 135g/ave/día
      14: 145, // Semana 14: 145g/ave/día
      15: 155, // Semana 15: 155g/ave/día
      16: 165, // Semana 16: 165g/ave/día
    };
    
    return tablaConsumo[semana] || 157; // Valor por defecto si no está en la tabla
  }

  private async obtenerPisoTermico(semana: number): Promise<boolean> {
    // 🔧 MEJORA: Obtener piso térmico de la guía genética real con cache
    if (!this.selectedLote?.raza || !this.selectedLote?.anoTablaGenetica) {
      return semana <= 3; // Valor por defecto
    }

    const claveCache = this.generarClaveCache(this.selectedLote.raza, this.selectedLote.anoTablaGenetica, semana);
    
    // Verificar cache primero
    if (this.cacheGuiaGenetica.has(claveCache)) {
      console.log(`📋 Cache hit para piso térmico semana ${semana}`);
      const datosCache = this.cacheGuiaGenetica.get(claveCache);
      return datosCache.pisoTermicoRequerido || semana <= 3;
    }

    try {
      const requierePisoTermico = await this.guiaGeneticaService.requierePisoTermico(
        this.selectedLote.raza,
        this.selectedLote.anoTablaGenetica,
        semana
      );

      // Guardar en cache
      this.cacheGuiaGenetica.set(claveCache, {
        ...this.cacheGuiaGenetica.get(claveCache),
        pisoTermicoRequerido: requierePisoTermico
      });

      return requierePisoTermico;
    } catch (error) {
      console.error(`❌ Error obteniendo piso térmico para semana ${semana}:`, error);
      return semana <= 3; // Valor por defecto
    }
  }

  // ================== VALIDACIONES Y ALERTAS ==================
  validarConsumo(consumoReal: number, consumoTabla: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    const diferencia = Math.abs(consumoReal - consumoTabla);
    const porcentajeDiferencia = consumoTabla > 0 ? (diferencia / consumoTabla) * 100 : 0;
    
    // Rangos más específicos basados en la guía genética
    if (porcentajeDiferencia <= 5) {
      return {
        esValido: true,
        mensaje: `Consumo óptimo (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'success'
      };
    } else if (porcentajeDiferencia <= 15) {
      return {
        esValido: true,
        mensaje: `Consumo aceptable (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'success'
      };
    } else if (porcentajeDiferencia <= 25) {
      return {
        esValido: false,
        mensaje: `Consumo ${consumoReal < consumoTabla ? 'bajo' : 'alto'} (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Consumo ${consumoReal < consumoTabla ? 'muy bajo' : 'muy alto'} (${porcentajeDiferencia.toFixed(1)}% diferencia)`,
        tipo: 'error'
      };
    }
  }

  validarGanancia(gananciaSemana: number, semana: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    // Rangos de ganancia esperada según la edad
    const gananciaEsperadaPorSemana: { [key: number]: { min: number; max: number; ideal: number } } = {
      1: { min: 8, max: 15, ideal: 12 },   // Semana 1: 8-15g/día, ideal 12g
      2: { min: 12, max: 20, ideal: 16 }, // Semana 2: 12-20g/día, ideal 16g
      3: { min: 15, max: 25, ideal: 20 },  // Semana 3: 15-25g/día, ideal 20g
      4: { min: 18, max: 28, ideal: 23 },  // Semana 4: 18-28g/día, ideal 23g
      5: { min: 20, max: 30, ideal: 25 },  // Semana 5: 20-30g/día, ideal 25g
      6: { min: 22, max: 32, ideal: 27 }, // Semana 6: 22-32g/día, ideal 27g
      7: { min: 24, max: 34, ideal: 29 }, // Semana 7: 24-34g/día, ideal 29g
      8: { min: 26, max: 36, ideal: 31 },  // Semana 8: 26-36g/día, ideal 31g
    };

    const rangoEsperado = gananciaEsperadaPorSemana[semana] || { min: 20, max: 35, ideal: 28 };
    
    if (gananciaSemana >= rangoEsperado.min && gananciaSemana <= rangoEsperado.max) {
      const diferenciaDelIdeal = Math.abs(gananciaSemana - rangoEsperado.ideal);
      const porcentajeDelIdeal = (diferenciaDelIdeal / rangoEsperado.ideal) * 100;
      
      if (porcentajeDelIdeal <= 10) {
        return {
          esValido: true,
          mensaje: `Ganancia óptima (${gananciaSemana.toFixed(1)}g/día)`,
          tipo: 'success'
        };
      } else {
        return {
          esValido: true,
          mensaje: `Ganancia aceptable (${gananciaSemana.toFixed(1)}g/día)`,
          tipo: 'success'
        };
      }
    } else if (gananciaSemana > 0) {
      return {
        esValido: false,
        mensaje: `Ganancia ${gananciaSemana < rangoEsperado.min ? 'baja' : 'alta'} (${gananciaSemana.toFixed(1)}g/día, esperado: ${rangoEsperado.min}-${rangoEsperado.max}g)`,
        tipo: 'warning'
      };
    } else if (gananciaSemana === 0) {
      return {
        esValido: false,
        mensaje: 'Sin ganancia de peso',
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Pérdida de peso: ${Math.abs(gananciaSemana).toFixed(2)}g/día`,
        tipo: 'error'
      };
    }
  }

  validarMortalidad(mortalidadSemana: number, semana: number): { 
    esValido: boolean; 
    mensaje: string; 
    tipo: 'success' | 'warning' | 'error' 
  } {
    // Mortalidad esperada según la edad (porcentaje semanal)
    const mortalidadEsperadaPorSemana: { [key: number]: { min: number; max: number; ideal: number } } = {
      1: { min: 0.1, max: 0.5, ideal: 0.3 },   // Semana 1: 0.1-0.5%, ideal 0.3%
      2: { min: 0.1, max: 0.4, ideal: 0.25 },  // Semana 2: 0.1-0.4%, ideal 0.25%
      3: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 3: 0.1-0.3%, ideal 0.2%
      4: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 4: 0.1-0.3%, ideal 0.2%
      5: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 5: 0.1-0.3%, ideal 0.2%
      6: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 6: 0.1-0.3%, ideal 0.2%
      7: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 7: 0.1-0.3%, ideal 0.2%
      8: { min: 0.1, max: 0.3, ideal: 0.2 },   // Semana 8: 0.1-0.3%, ideal 0.2%
    };

    const rangoEsperado = mortalidadEsperadaPorSemana[semana] || { min: 0.1, max: 0.3, ideal: 0.2 };
    
    if (mortalidadSemana >= rangoEsperado.min && mortalidadSemana <= rangoEsperado.max) {
      return {
        esValido: true,
        mensaje: `Mortalidad normal (${mortalidadSemana.toFixed(2)}%)`,
        tipo: 'success'
      };
    } else if (mortalidadSemana < rangoEsperado.min) {
      return {
        esValido: true,
        mensaje: `Mortalidad baja (${mortalidadSemana.toFixed(2)}%)`,
        tipo: 'success'
      };
    } else if (mortalidadSemana <= rangoEsperado.max * 1.5) {
      return {
        esValido: false,
        mensaje: `Mortalidad alta (${mortalidadSemana.toFixed(2)}%, esperado: ${rangoEsperado.min}-${rangoEsperado.max}%)`,
        tipo: 'warning'
      };
    } else {
      return {
        esValido: false,
        mensaje: `Mortalidad crítica (${mortalidadSemana.toFixed(2)}%, esperado: ${rangoEsperado.min}-${rangoEsperado.max}%)`,
        tipo: 'error'
      };
    }
  }

  // ================== HELPERS DE FECHA ==================
  private obtenerFechaInicioSemana(semana: number): string {
    if (!this.selectedLote?.fechaEncaset) return '';
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const diasASumar = (semana - 1) * 7;
    const fechaInicio = new Date(fechaEncaset.getTime() + (diasASumar * 24 * 60 * 60 * 1000));
    
    return fechaInicio.toISOString().split('T')[0];
  }

  private obtenerFechaFinSemana(semana: number): string {
    if (!this.selectedLote?.fechaEncaset) return '';
    
    const fechaEncaset = new Date(this.selectedLote.fechaEncaset);
    const diasASumar = (semana * 7) - 1;
    const fechaFin = new Date(fechaEncaset.getTime() + (diasASumar * 24 * 60 * 60 * 1000));
    
    return fechaFin.toISOString().split('T')[0];
  }

  // ================== FORMATO ==================
  formatNumber = (value: number, decimals: number = 2): string => {
    return value.toFixed(decimals);
  };

  formatPercentage = (value: number, decimals: number = 2): string => {
    return `${value.toFixed(decimals)}%`;
  };

  formatDate = (date: string): string => {
    return new Date(date).toLocaleDateString('es-ES');
  };
}
