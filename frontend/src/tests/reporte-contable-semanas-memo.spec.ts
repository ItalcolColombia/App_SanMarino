/**
 * Reporte Contable — el getter de semanas del sub-lote no puede devolver un array nuevo por ciclo.
 *
 * Por qué existe este spec (17-ago-2026): `semanasParaSubloteActual` proyectaba las semanas en cada
 * lectura, así que devolvía un array NUEVO de objetos NUEVOS en cada ciclo de change detection. La
 * plantilla lo recorre con `@for`, de modo que Angular destruía y volvía a crear los tabs de semana
 * y el panel de la semana activa —donde vive la sección BULTO y el aviso de alcance— una y otra vez.
 * Medido en pantalla: `NG0956` («track by identity caused re-creation of the entire collection»),
 * `NG0100`, y el tab de la semana quedaba **sin rótulo** y no se podía usar.
 *
 * El invariante que fija este archivo es el de CLAUDE.md: mismas entradas ⇒ **misma referencia**.
 * Y, como el arreglo es un refactor, también verifica que los números proyectados no se movieron.
 */
import {
  ReporteContableCompletoDto,
  ReporteContableSemanalDto,
  DatoDiarioContableDto
} from '../app/features/reporte-contable/services/reporte-contable.service';
import { ReporteContableMainComponent } from '../app/features/reporte-contable/pages/reporte-contable-main/reporte-contable-main.component';

function diario(loteNombre: string, fecha: string, extra: Partial<DatoDiarioContableDto> = {}) {
  return {
    fecha, loteId: 1, loteNombre,
    entradasHembras: 0, entradasMachos: 0,
    mortalidadHembras: 0, mortalidadMachos: 0,
    seleccionHembras: 0, seleccionMachos: 0,
    ventasHembras: 0, ventasMachos: 0,
    trasladosHembras: 0, trasladosMachos: 0,
    saldoHembras: 0, saldoMachos: 0,
    consumoAlimentoHembras: 0, consumoAlimentoMachos: 0,
    consumoAgua: 0, consumoMedicamento: 0, consumoVacuna: 0,
    saldoBultosAnterior: 0, trasladosBultos: 0, entradasBultos: 0, retirosBultos: 0,
    consumoBultosHembras: 0, consumoBultosMachos: 0, saldoBultos: 0,
    ...extra
  } as unknown as DatoDiarioContableDto;
}

function semana(n: number, datosDiarios: DatoDiarioContableDto[]) {
  return {
    semanaContable: n,
    fechaInicio: '2026-08-13T00:00:00',
    fechaFin: '2026-08-19T00:00:00',
    lotePadreId: 114,
    lotePadreNombre: 'A374A',
    sublotes: ['A374A', 'A374B'],
    datosDiarios,
    consumosDiarios: []
  } as unknown as ReporteContableSemanalDto;
}

function reporteCon(semanas: ReporteContableSemanalDto[]) {
  return {
    lotePadreId: 114, lotePadreNombre: 'A374A',
    granjaId: 20, granjaNombre: 'LA ESMERALDA',
    nucleoId: '819014', nucleoNombre: 'Modulo II',
    fechaPrimeraLlegada: '2025-10-16T00:00:00',
    semanaContableActual: 44,
    fechaInicioSemanaActual: '2026-08-13T00:00:00',
    fechaFinSemanaActual: '2026-08-19T00:00:00',
    reportesSemanales: semanas,
    lotesPadreEnGranja: 4,
    advertenciaBultos: 'Estos movimientos de alimento son de la GRANJA «LA ESMERALDA»…'
  } as unknown as ReporteContableCompletoDto;
}

function componenteCon(reporte: ReporteContableCompletoDto | null, sublote: string | null) {
  // No se renderiza la plantilla: sólo interesa el getter, así que los servicios no se usan
  // (el 2.º es ActiveCompanyConfigService, que el componente solo consulta en ngOnInit).
  const c = new ReporteContableMainComponent({} as never, {} as never);
  c.reporte.set(reporte);
  c.selectedSublote = sublote;
  return c;
}

describe('Reporte Contable · semanasParaSubloteActual — estabilidad de la referencia', () => {

  it('con las MISMAS entradas devuelve la MISMA referencia (es lo que rompía los tabs)', () => {
    const c = componenteCon(reporteCon([semana(44, [diario('A374A', '2026-08-13')])]), 'A374A');

    const primera = c.semanasParaSubloteActual;
    const segunda = c.semanasParaSubloteActual;
    const tercera = c.semanasParaSubloteActual;

    expect(segunda).toBe(primera);
    expect(tercera).toBe(primera);
    // y los elementos también, que es lo que mira `@for` al recorrer la colección
    expect(segunda[0]).toBe(primera[0]);
  });

  it('sin sub-lote elegido devuelve el array del reporte tal cual, sin proyectar', () => {
    const semanas = [semana(44, [])];
    const c = componenteCon(reporteCon(semanas), null);

    expect(c.semanasParaSubloteActual).toBe(semanas);
    expect(c.semanasParaSubloteActual).toBe(semanas);
  });

  it('sin reporte devuelve vacío y no revienta', () => {
    const c = componenteCon(null, null);
    expect(c.semanasParaSubloteActual).toEqual([]);
  });

  it('al cambiar el sub-lote recalcula: referencia nueva y datos del sub-lote nuevo', () => {
    const datos = [diario('A374A', '2026-08-13'), diario('A374B', '2026-08-13')];
    const c = componenteCon(reporteCon([semana(44, datos)]), 'A374A');

    const deA = c.semanasParaSubloteActual;
    expect(deA[0].datosDiarios.map(d => d.loteNombre)).toEqual(['A374A']);

    c.selectedSublote = 'A374B';
    const deB = c.semanasParaSubloteActual;

    expect(deB).not.toBe(deA);
    expect(deB[0].datosDiarios.map(d => d.loteNombre)).toEqual(['A374B']);
  });

  it('al cambiar el reporte recalcula, aunque el sub-lote sea el mismo', () => {
    const c = componenteCon(reporteCon([semana(44, [diario('A374A', '2026-08-13')])]), 'A374A');
    const antes = c.semanasParaSubloteActual;

    c.reporte.set(reporteCon([semana(45, [diario('A374A', '2026-08-20')])]));
    const despues = c.semanasParaSubloteActual;

    expect(despues).not.toBe(antes);
    expect(despues[0].semanaContable).toBe(45);
  });

  it('memorizar NO cambió los números: la proyección sigue arrastrando el saldo entre semanas', () => {
    // El saldo de la semana N+1 arranca del último día CON dato del sub-lote en la semana N.
    const s44 = semana(44, [
      diario('A374A', '2026-08-13', { saldoHembras: 100, saldoMachos: 10 }),
      diario('A374B', '2026-08-13', { saldoHembras: 999, saldoMachos: 99 })
    ]);
    const s45 = semana(45, [diario('A374A', '2026-08-20', { saldoHembras: 90, saldoMachos: 9 })]);
    const c = componenteCon(reporteCon([s44, s45]), 'A374A');

    const proyectadas = c.semanasParaSubloteActual;

    expect(proyectadas.length).toBe(2);
    // la primera arranca en 0 y cierra en el saldo del sub-lote (no en el del hermano)
    expect(proyectadas[0].saldoAnteriorHembras).toBe(0);
    expect(proyectadas[0].saldoFinHembras).toBe(100);
    expect(proyectadas[0].saldoFinMachos).toBe(10);
    // la segunda hereda el cierre de la primera
    expect(proyectadas[1].saldoAnteriorHembras).toBe(100);
    expect(proyectadas[1].saldoAnteriorMachos).toBe(10);
    expect(proyectadas[1].saldoFinHembras).toBe(90);
  });
});
