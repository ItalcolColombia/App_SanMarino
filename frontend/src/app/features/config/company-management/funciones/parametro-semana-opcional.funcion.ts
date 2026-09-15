// Sentinel de borrado (0) para los parámetros opcionales «semana» de Configuración → Empresas
// (`huevoPrimeraPosturaHastaSemana`, `huevosLevanteDesdeSemana`).
//
// El backend interpreta `null` como «el cliente no mandó el campo, conservá lo que había» — la
// misma convención de todos los flags de UpdateCompanyDto, para que un formulario que solo toca
// datos de contacto no apague algo en silencio. Pero ESTE formulario sí administra el parámetro:
// si el admin vacía el campo para BORRAR un límite ya configurado, el valor que viaja también es
// `null`, y el backend lo lee como «no lo toques» — el límite queda pegado para siempre.
//
// Espejo de `ParametroEmpresaOpcionalCalculos.ResolverEnteroOpcional` (backend): al EDITAR, un campo
// vacío manda el sentinel `0` (fuera del rango válido, que arranca en 1) para pedir el borrado
// explícito. Al CREAR no aplica — el alta siempre fija el valor tal cual y `0` se guardaría literal.

/** Función PURA: sin `this`, sin DI, sin estado. */
export function resolverSemanaOpcionalParaGuardar(valor: unknown, editando: boolean): number | null {
  const n = Number(valor);
  if (Number.isFinite(n) && n > 0) return n;
  return editando ? 0 : null;
}
