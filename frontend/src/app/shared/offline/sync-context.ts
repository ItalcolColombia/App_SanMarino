import { HttpContextToken } from '@angular/common/http';

/**
 * Marca las peticiones que genera el propio push, para que el interceptor de offline no intente
 * encolarlas. Sin esto, un push que falla por falta de red **se encolaría a sí mismo**.
 *
 * Vive en su propio archivo, y no dentro de `sync.service.ts`, para que el interceptor —que está en
 * el bundle inicial— no arrastre el servicio de sincronización entero solo por usar el token.
 */
export const ES_PUSH_DE_SYNC = new HttpContextToken<boolean>(() => false);
