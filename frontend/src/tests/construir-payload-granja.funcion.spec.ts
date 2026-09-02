/**
 * Granjas — el payload que la pantalla manda al backend.
 *
 * `FarmService.UpdateAsync` asigna los campos opcionales del `UpdateFarmDto` SIN condicional
 * (`entity.X = dto.X`): lo que el front no manda llega como `null` y se borra en silencio. Este
 * spec es el guardián de ese contrato — si alguien vuelve a sacar un campo del payload, revienta acá
 * y no en la base de un cliente.
 *
 * Origen: 1-sep-2026. Cada edición de granja desde `/config/farm-management` borraba
 * `codigo_erp_engorde` (el correlativo ERP de engorde de Panamá) y `maneja_alimento_por_galpon`
 * (el override por granja del nivel de alimento).
 */
import { construirPayloadGranja } from '../app/features/farm/funciones/construir-payload-granja.funcion';

/** Form de una granja de Panamá con los dos campos que se perdían, ya cargados. */
const formGranjaPanama = {
  name: '  Granja La Esperanza  ',
  companyId: 7,
  status: 'A',
  regionalOptionId: 42,
  departamentoId: 11,
  ciudadId: 110,
  clienteId: 3,
  zona: 'Zona 1',
  certificadoGab: true,
  latitud: 8.9824,
  longitud: -79.5199,
  manejaAlimentoPorGalpon: true,
  codigoErpEngorde: '4001017',
  codigoBodega: 'B0601',
  descripcionBodega: 'Bodega Granja La Esperanza',
  centroOperacion: '830',
  descripcionCentroOperacion: 'Centro de operación Buga',
  codigoInstalacion: 'B06',
  descripcionInstalacion: 'Instalación granja',
};

describe('construir-payload-granja · los 2 campos que se borraban', () => {
  it('manda el código ERP de engorde tal cual: editar el nombre no puede perder el correlativo', () => {
    // Éste ES el defecto: el payload viejo ni siquiera tenía la clave.
    expect(construirPayloadGranja(formGranjaPanama).codigoErpEngorde).toBe('4001017');
  });

  it('manda el nivel de manejo de alimento en sus TRES estados, sin colapsar `false` en `null`', () => {
    // null = hereda la empresa · true = sobre galpón · false = sobre granja.
    // Si `false` viajara como `null`, la granja pasaría a heredar y el inventario cambiaría de nivel.
    const nivel = (v: unknown) =>
      construirPayloadGranja({ ...formGranjaPanama, manejaAlimentoPorGalpon: v }).manejaAlimentoPorGalpon;

    expect(nivel(true)).toBeTrue();
    expect(nivel(false)).toBeFalse();
    expect(nivel(null)).toBeNull();
  });

  it('el `<select>` que devuelve strings del DOM no rompe el tri-estado', () => {
    const nivel = (v: unknown) =>
      construirPayloadGranja({ ...formGranjaPanama, manejaAlimentoPorGalpon: v }).manejaAlimentoPorGalpon;

    expect(nivel('true')).toBeTrue();
    expect(nivel('false')).toBeFalse();
    expect(nivel('')).toBeNull();
  });

  it('sin valor en el form, el nivel viaja como null (hereda), nunca como undefined', () => {
    // `undefined` se serializa fuera del JSON y el backend lo recibe como null igual, pero acá se
    // deja explícito para que el contrato sea legible en la request.
    const payload = construirPayloadGranja({ ...formGranjaPanama, manejaAlimentoPorGalpon: undefined });
    expect(payload.manejaAlimentoPorGalpon).toBeNull();
    expect('manejaAlimentoPorGalpon' in payload).toBeTrue();
  });

  it('recorta el código ERP y manda null cuando queda vacío (borrado explícito)', () => {
    const codigo = (v: unknown) =>
      construirPayloadGranja({ ...formGranjaPanama, codigoErpEngorde: v }).codigoErpEngorde;

    expect(codigo('  4001017  ')).toBe('4001017');
    expect(codigo('')).toBeNull();
    expect(codigo('   ')).toBeNull();
    expect(codigo(null)).toBeNull();
    expect(codigo(undefined)).toBeNull();
  });
});

describe('construir-payload-granja · el resto del payload no cambió', () => {
  it('arma el mismo objeto que armaba `save()` inline', () => {
    expect(construirPayloadGranja(formGranjaPanama)).toEqual({
      name: 'Granja La Esperanza',       // trim
      companyId: 7,
      status: 'A',
      regionalId: 42,                    // el id de la opción de lista maestra viaja como regionalId
      departamentoId: 11,
      ciudadId: 110,
      clienteId: 3,
      zona: 'Zona 1',
      certificadoGab: true,
      latitud: 8.9824,
      longitud: -79.5199,
      manejaAlimentoPorGalpon: true,
      codigoErpEngorde: '4001017',
      codigoBodega: 'B0601',
      descripcionBodega: 'Bodega Granja La Esperanza',
      centroOperacion: '830',
      descripcionCentroOperacion: 'Centro de operación Buga',
      codigoInstalacion: 'B06',
      descripcionInstalacion: 'Instalación granja',
    });
  });

  it('los selects vacíos del DOM (`\'\'`) viajan como null, no como NaN', () => {
    const payload = construirPayloadGranja({
      ...formGranjaPanama,
      regionalOptionId: '',
      departamentoId: '',
      ciudadId: '',
      clienteId: '',
      latitud: '',
      longitud: '',
    });

    expect(payload.regionalId).toBeNull();
    expect(payload.departamentoId).toBeNull();
    expect(payload.ciudadId).toBeNull();
    expect(payload.clienteId).toBeNull();
    expect(payload.latitud).toBeNull();
    expect(payload.longitud).toBeNull();
  });

  it('los numéricos que llegan como string del DOM se mandan como number', () => {
    const payload = construirPayloadGranja({
      ...formGranjaPanama,
      departamentoId: '11',
      ciudadId: '110',
      latitud: '8.9824',
    });

    expect(payload.departamentoId).toBe(11);
    expect(payload.ciudadId).toBe(110);
    expect(payload.latitud).toBe(8.9824);
  });

  it("normaliza el status a 'A' | 'I' y cae en 'A' ante cualquier otra cosa", () => {
    const status = (v: unknown) => construirPayloadGranja({ ...formGranjaPanama, status: v }).status;

    expect(status('i')).toBe('I');
    expect(status('I')).toBe('I');
    expect(status('a')).toBe('A');
    expect(status(null)).toBe('A');
    expect(status('cualquier cosa')).toBe('A');
  });

  it('los códigos ERP avícolas siguen viajando siempre (la defensa que ya existía)', () => {
    // Una empresa sin el flag `manejaCodigosErpAvicola` no los pinta, pero el form los tiene
    // hidratados desde el backend: editar esa granja no puede borrarlos.
    const payload = construirPayloadGranja(formGranjaPanama);

    expect(payload.codigoBodega).toBe('B0601');
    expect(payload.centroOperacion).toBe('830');
    expect(payload.codigoInstalacion).toBe('B06');
  });

  it('la zona vacía viaja como null', () => {
    expect(construirPayloadGranja({ ...formGranjaPanama, zona: '' }).zona).toBeNull();
  });
});
