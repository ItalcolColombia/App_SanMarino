// src/app/features/lote/funciones/payload-lote-base.funcion.ts
import { CreateLotePosturaBaseDto, LotePosturaBaseDto } from '../services/lote-postura-base.service';

/**
 * Lo que el formulario de **Lote Base** (`baseForm`) sigue capturando en pantalla.
 *
 * `raza`, `tipoLinea` y `cantidadMixtas` **no están** acá a propósito: sus controles salieron del
 * `FormGroup` y del template. El valor de esos tres se conserva desde el registro que se edita, no
 * desde el form (ver {@link payloadLoteBaseConCamposPreservados}).
 */
export interface ValoresFormLoteBase {
  loteNombre?: string | null;
  codigoErp?: string | null;
  descripcionErp?: string | null;
  fechaEncaset?: string | null;
  cantidadHembras?: number | string | null;
  cantidadMachos?: number | string | null;
  farmId?: number | string | null;
  erpCreate?: string | null;
}

/**
 * Arma el payload de `POST`/`PUT` a `LotePosturaBase` con lo que sigue en el formulario y
 * **preservando desde `baseEnEdicion`** los tres campos que ya no se muestran: `raza`, `tipoLinea`
 * y `cantidadMixtas`.
 *
 * <p>
 * 🔴 **Por qué existe.** `raza` / `tipoLinea` eran un duplicado real de lo que ya captura —y valida
 * contra la guía genética— el formulario de **Lote**; `cantidadMixtas` es un concepto de pollo de
 * engorde que en reproductoras nunca se diligencia (TK-2026-000024). Los tres se sacaron de la
 * pantalla del Lote Base. Pero **quitar un campo de la vista no puede borrar un dato**: medido el
 * 9-sep-2026 hay 14 bases con `raza` cargada. Si el payload de edición mandara `null` porque el
 * control ya no está, la primera edición de cada una pisaría ese histórico. Por eso el valor de los
 * tres viaja desde `baseEnEdicion` —el registro tal como lo devolvió el backend—, nunca desde el
 * formulario. `lote_postura_base.raza` / `tipo_linea` no los lee ningún otro servicio, así que
 * mantenerlos intactos alcanza: no se corrigen ni se migran en este cambio.
 * </p>
 *
 * <p>
 * En un **alta** (`baseEnEdicion` nulo/indefinido) los tres salen en su neutro (`null` / `0`), que
 * es lo correcto: 16 de las 30 bases ya tienen `raza` nula. Todo lo demás —nombre, ERP, fecha de
 * encasetamiento declarada, cantidades, granja— sale del formulario con la **misma normalización**
 * que tenía el código inline previo de `saveBase()` (recorte a `null`, `Number(...) || 0`).
 * </p>
 *
 * Función pura: sin `this`, sin DI, sin estado. La usa `LoteListComponent.saveBase()`.
 */
export function payloadLoteBaseConCamposPreservados(
  valoresDelForm: ValoresFormLoteBase | null | undefined,
  baseEnEdicion: LotePosturaBaseDto | null | undefined
): CreateLotePosturaBaseDto {
  const v: ValoresFormLoteBase = valoresDelForm ?? {};
  return {
    loteNombre:      (v.loteNombre ?? '').toString().trim(),
    codigoErp:       (v.codigoErp ?? '').toString().trim() || null,
    descripcionErp:  (v.descripcionErp ?? '').toString().trim() || null,
    // raza / tipoLinea / cantidadMixtas: salieron de la pantalla del Lote Base. El valor viaja
    // desde el registro que se edita (baseEnEdicion), NUNCA desde el form —quitar el control no
    // puede borrar el dato guardado: hay 14 bases con raza cargada—. En un alta van null/0.
    raza:            baseEnEdicion?.raza      ?? null,
    tipoLinea:       baseEnEdicion?.tipoLinea ?? null,
    fechaEncaset:    v.fechaEncaset ? String(v.fechaEncaset) : null,
    cantidadHembras: Number(v.cantidadHembras) || 0,
    cantidadMachos:  Number(v.cantidadMachos)  || 0,
    cantidadMixtas:  Number(baseEnEdicion?.cantidadMixtas ?? 0) || 0,
    farmId:          v.farmId    ? Number(v.farmId)    : null,
    erpCreate:       v.erpCreate ? String(v.erpCreate) : null
  };
}
