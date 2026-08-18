/**
 * Roles — quién ve los catálogos globales (tabs Permisos y Menús).
 *
 * Espejo de `backend/.../Calculos/CatalogoGlobalAutorizacionCalculos.cs`: el front decide qué se
 * MUESTRA, el back decide qué se PUEDE. Si cambia una regla, cambian las dos.
 */
import {
  esAdminDeAplicacion, puedeVerTab, tabPorDefecto, TABS_SOLO_ADMIN
} from '../app/features/config/role-management/funciones/catalogos-globales.funcion';

describe('catalogos-globales · esAdminDeAplicacion', () => {
  it('reconoce el rol Admin de la aplicación sin importar mayúsculas ni espacios', () => {
    expect(esAdminDeAplicacion(['Admin'])).toBeTrue();
    expect(esAdminDeAplicacion(['ADMIN'])).toBeTrue();
    expect(esAdminDeAplicacion(['  admin  '])).toBeTrue();
    expect(esAdminDeAplicacion(['Administrador'])).toBeTrue();
    expect(esAdminDeAplicacion(['Consulta', 'Admin'])).toBeTrue();
  });

  it('NO toma por admin de la aplicación a los administradores de empresa', () => {
    // Todos existen hoy en la base: son administradores de SU empresa, no del sistema.
    for (const rol of ['Admin Panama', 'Admin Demo', 'Ecuador Administrador',
                       'Santa Reyes Administrador', 'ADMINISTRADOR DE GRANJA',
                       'Administrador de Empresa']) {
      expect(esAdminDeAplicacion([rol])).withContext(rol).toBeFalse();
    }
  });

  it('fail-closed: sin roles, vacío, null o undefined ⇒ no es admin', () => {
    expect(esAdminDeAplicacion([])).toBeFalse();
    expect(esAdminDeAplicacion(null)).toBeFalse();
    expect(esAdminDeAplicacion(undefined)).toBeFalse();
    expect(esAdminDeAplicacion([null, undefined, ''])).toBeFalse();
  });
});

describe('catalogos-globales · puedeVerTab', () => {
  it('Roles se le muestra a todo el mundo: es la razón de ser del módulo', () => {
    expect(puedeVerTab('roles', false)).toBeTrue();
    expect(puedeVerTab('roles', true)).toBeTrue();
  });

  it('Permisos y Menús solo para el admin de la aplicación', () => {
    for (const tab of TABS_SOLO_ADMIN) {
      expect(puedeVerTab(tab, false)).withContext(`${tab} sin admin`).toBeFalse();
      expect(puedeVerTab(tab, true)).withContext(`${tab} con admin`).toBeTrue();
    }
  });

  it('el tab por defecto siempre está permitido', () => {
    expect(puedeVerTab(tabPorDefecto(), false)).toBeTrue();
  });
});
