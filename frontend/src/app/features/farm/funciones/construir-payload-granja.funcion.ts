/**
 * Arma el payload que la pantalla de granjas manda al backend (POST /api/Farm y PUT /api/Farm).
 *
 * Es PURA a propósito: recibe el `getRawValue()` del formulario y devuelve el objeto a enviar.
 * Sin `this`, sin DI, sin HTTP.
 *
 * ⚠️ **Todo campo que el backend asigne SIN condicional tiene que salir de acá.**
 * `FarmService.UpdateAsync` hace `entity.X = dto.X` para los campos opcionales del
 * `UpdateFarmDto`: los que el front no manda llegan como `null` y se BORRAN en silencio.
 * Así se perdían `codigoErpEngorde` (el correlativo ERP de engorde de Panamá, que avanza +1 al
 * cerrar el ciclo) y `manejaAlimentoPorGalpon` (el override por granja del nivel de alimento) en
 * cada edición hecha desde la pestaña «Granjas» — sin error y sin aviso.
 *
 * Los campos viajan siempre, aunque el formulario no los pinte: se hidratan con lo que devuelve
 * `GET /api/Farm/{id}` al abrir la edición, y así una empresa que no los usa tampoco los borra.
 */

/** Valor crudo del formulario de granjas (`form.getRawValue()`), con todo laxo: viene del DOM. */
export interface RawFormGranja {
  name?: unknown;
  companyId?: unknown;
  status?: unknown;
  regionalOptionId?: unknown;
  departamentoId?: unknown;
  ciudadId?: unknown;
  clienteId?: unknown;
  zona?: unknown;
  certificadoGab?: unknown;
  latitud?: unknown;
  longitud?: unknown;
  manejaAlimentoPorGalpon?: unknown;
  codigoErpEngorde?: unknown;
  codigoBodega?: unknown;
  descripcionBodega?: unknown;
  centroOperacion?: unknown;
  descripcionCentroOperacion?: unknown;
  codigoInstalacion?: unknown;
  descripcionInstalacion?: unknown;
}

/** Payload de creación/actualización de granja (al de update se le agrega el `id` afuera). */
export interface PayloadGranja {
  name: string;
  companyId: number;
  status: 'A' | 'I';
  regionalId: number | null;
  departamentoId: number | null;
  ciudadId: number | null;
  clienteId: number | null;
  zona: string | null;
  certificadoGab: boolean;
  latitud: number | null;
  longitud: number | null;
  manejaAlimentoPorGalpon: boolean | null;
  codigoErpEngorde: string | null;
  codigoBodega: string | null;
  descripcionBodega: string | null;
  centroOperacion: string | null;
  descripcionCentroOperacion: string | null;
  codigoInstalacion: string | null;
  descripcionInstalacion: string | null;
}

/** Texto del form → string recortado o null (los campos opcionales no viajan como ''). */
export function textoOrNull(value: unknown): string | null {
  const texto = value == null ? '' : String(value).trim();
  return texto === '' ? null : texto;
}

/** Valor del form → number o null (el DOM devuelve '' cuando el select/input está vacío). */
function numeroOrNull(value: unknown): number | null {
  return value != null && value !== '' ? Number(value) : null;
}

/**
 * Nivel de manejo de alimento de la granja. Es TRI-ESTADO y `null` no significa «no informado»:
 * `null` = hereda el flag de la empresa · `true` = alimento sobre GALPÓN · `false` = sobre GRANJA.
 * Por eso `false` no puede colapsar a `null` (sería cambiar de nivel de inventario), y por eso el
 * fix no puede vivir en el backend con un «si viene null, conservar»: la granja se quedaría sin
 * forma de volver a heredar.
 */
function nivelAlimentoOrNull(value: unknown): boolean | null {
  return value == null || value === '' ? null : value === true || value === 'true';
}

export function construirPayloadGranja(raw: RawFormGranja): PayloadGranja {
  return {
    name: String(raw?.name ?? '').trim(),
    companyId: Number(raw?.companyId ?? 1),
    // Normaliza status a 'A' | 'I'
    status: String(raw?.status ?? 'A').toUpperCase() === 'I' ? 'I' : 'A',
    // El valor seleccionado en el select es el id de la opción (lista maestra); se envía como
    // regionalId para que se guarde
    regionalId: numeroOrNull(raw?.regionalOptionId),
    departamentoId: numeroOrNull(raw?.departamentoId),
    ciudadId: numeroOrNull(raw?.ciudadId),
    // ── Campos Panamá (null para otros países) ──────────────────────
    clienteId: numeroOrNull(raw?.clienteId),
    zona: (raw?.zona as string) || null,
    certificadoGab: (raw?.certificadoGab as boolean) ?? false,
    latitud: numeroOrNull(raw?.latitud),
    longitud: numeroOrNull(raw?.longitud),
    // ── Configuración de la granja que el backend pisa con lo que reciba ────────────
    // Se envían siempre desde el form (hidratado con lo que devuelve el backend): así una edición
    // hecha desde otro país o con el flag apagado NO los borra.
    manejaAlimentoPorGalpon: nivelAlimentoOrNull(raw?.manejaAlimentoPorGalpon),
    codigoErpEngorde: textoOrNull(raw?.codigoErpEngorde),
    // ── Códigos ERP avícolas ────────────────────────────────────────
    codigoBodega: textoOrNull(raw?.codigoBodega),
    descripcionBodega: textoOrNull(raw?.descripcionBodega),
    centroOperacion: textoOrNull(raw?.centroOperacion),
    descripcionCentroOperacion: textoOrNull(raw?.descripcionCentroOperacion),
    codigoInstalacion: textoOrNull(raw?.codigoInstalacion),
    descripcionInstalacion: textoOrNull(raw?.descripcionInstalacion),
  };
}
