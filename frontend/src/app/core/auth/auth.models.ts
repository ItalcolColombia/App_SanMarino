// src/app/core/auth/auth.models.ts
// Tipos del backend (ajustados a tu respuesta)
export interface MenuItem {
  id: number;
  label: string;
  icon?: string | null;
  route?: string | null;
  order: number;
  children: MenuItem[];
}

export interface RoleMenusLite {
  roleId: number;
  roleName: string;
  menuIds: number[];
}

export interface LoginPayload {
  email: string;
  password: string;
  companyId?: number;
}

export interface LoginResult {
  token: string;
  refreshToken?: string;

  // ===== campos adicionales que devuelve tu backend =====
  userId?: string;
  username?: string;
  firstName?: string;
  surName?: string;
  fullName?: string;
  roles?: string[];
  empresas?: string[];       // ["Agricola sanmarino", ...]
  permisos?: string[];
  menusByRole?: RoleMenusLite[]; // 👈 NUEVO
  menu?: MenuItem[];             // 👈 NUEVO (árbol efectivo)
}

export interface AuthSession {
  accessToken: string;
  refreshToken?: string;

  user: {
    id?: string;
    username?: string;
    firstName?: string;
    surName?: string;
    fullName?: string;
    roles?: string[];
    permisos?: string[];
  };

  companies: string[];       // nombres legibles
  activeCompany?: string;    // la elegida (por nombre, si así lo manejas)

  // 👇 NUEVO
  menu: MenuItem[];               // árbol efectivo para construir el sidebar
  menusByRole: RoleMenusLite[];   // ids asignados por rol (para admin)
}
