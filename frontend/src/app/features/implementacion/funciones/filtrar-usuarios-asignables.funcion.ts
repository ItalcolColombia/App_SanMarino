import type { ImplementacionUsuarioAsignableDto } from '../models/implementacion.models';

/**
 * Filtra la lista de usuarios asignables por texto libre y por rol.
 *
 * ## Por qué el rol filtra en vez de seleccionar
 *
 * En una entrega los participantes son "los auxiliares de granja", no una lista de nombres que
 * alguien tiene que acordarse. Pero elegir el rol **no** puede marcar a nadie solo: los roles de la
 * empresa incluyen gente que no estuvo en esa capacitación, y una firma de más es una persona
 * afirmando algo que no vio. Así que el rol acota la lista y el visto lo pone una persona — con el
 * botón de "marcar todos los visibles" para cuando efectivamente son todos.
 *
 * `rolId` null (o 0) = sin filtro de rol. Los usuarios sin rol en la empresa activa tienen `rolIds`
 * vacío y solo aparecen sin filtro, que es lo correcto: no pertenecen a ningún grupo.
 */
export function filtrarUsuariosAsignables(
  usuarios: readonly ImplementacionUsuarioAsignableDto[],
  busqueda: string,
  rolId: number | null
): ImplementacionUsuarioAsignableDto[] {
  const q = (busqueda ?? '').trim().toLowerCase();

  return usuarios.filter((u) => {
    if (rolId && !(u.rolIds ?? []).includes(rolId)) return false;
    if (!q) return true;
    return [u.nombre, u.cedula, u.email].some((v) => (v ?? '').toLowerCase().includes(q));
  });
}
