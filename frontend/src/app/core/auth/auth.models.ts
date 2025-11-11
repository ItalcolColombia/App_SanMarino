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

export interface EmailQueueStatus {
  id: number;
  status: 'pending' | 'processing' | 'sent' | 'failed';
  toEmail: string;
  emailType: string;
  errorMessage?: string | null;
  errorType?: string | null;
  retryCount: number;
  createdAt: string;
  sentAt?: string | null;
  failedAt?: string | null;
}

export interface LoginPayload {
  email: string;
  password: string;
  companyId?: number;
  recaptchaToken?: string | null;
}

export interface CompanyPais {
  companyId: number;
  companyName: string;
  paisId: number;
  paisNombre: string;
  isDefault: boolean;
}

export interface LoginResult {
  token: string;
  refreshToken?: string;

  // ===== campos que devuelve el backend (AuthResponseDto) =====
  userId?: string; // Guid desde el backend se serializa como string en JSON
  username?: string;
  firstName?: string | null;
  surName?: string | null;
  fullName?: string;
  roles?: string[];
  empresas?: string[];       // ["Agricola sanmarino", ...] - legacy
  companyPaises?: CompanyPais[]; // ← NUEVO: combinaciones empresa-país
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
    firstName?: string | null;
    surName?: string | null;
    fullName?: string;
    roles?: string[];
    permisos?: string[];
  };

  companies: string[];       // nombres legibles (legacy)
  activeCompany?: string;    // la elegida (por nombre, si así lo manejas) - legacy

  // ← NUEVO: información de empresa-país
  companyPaises?: CompanyPais[];  // todas las combinaciones empresa-país disponibles
  activeCompanyId?: number;        // ID de la empresa activa
  activePaisId?: number;           // ID del país activo

  // 👇 NUEVO
  menu: MenuItem[];               // árbol efectivo para construir el sidebar
  menusByRole: RoleMenusLite[];   // ids asignados por rol (para admin)
}
