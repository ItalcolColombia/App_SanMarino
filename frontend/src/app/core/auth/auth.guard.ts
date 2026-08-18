// src/app/core/auth/auth.guard.ts
import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';
import { TokenStorageService } from './token-storage.service';
import { TRABAJO_PENDIENTE_OFFLINE } from './session-timeout.service';
import { ConexionService } from '../pwa/conexion.service';
import { ToastService } from '../../shared/services/toast.service';
import { evaluarAccesoOffline, mensajeAccesoDenegado } from './funciones/politica-sesion.funcion';
import { estaVencido, leerMarcasDelToken, ultimoContactoSegunToken } from './funciones/marcas-del-token.funcion';

/**
 * Puerta de las rutas protegidas.
 *
 * Orquestador delgado: lee el token y el estado, y **la decisión la toma la política pura**
 * (`evaluarAccesoOffline`, con sus tests). Antes decidía acá mismo —token vencido ⇒ `logout()`— y eso
 * anulaba la jornada offline de 16 h de la decisión D4: el JWT dura 60 minutos, así que un operario
 * sin señal quedaba deslogueado **y con la caché borrada** al minuto 61, sin red para volver a entrar.
 *
 * La regla nueva en una línea: **sin red no se purga nunca**. Se deja trabajar mientras dure la
 * jornada y, agotada, se niega el paso sin destruir nada.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const storage = inject(TokenStorageService);
  const conexion = inject(ConexionService);
  const toast = inject(ToastService);
  const trabajo = inject(TRABAJO_PENDIENTE_OFFLINE, { optional: true });
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  const ahora = Date.now();
  const marcas = leerMarcasDelToken(storage.getToken());

  const acceso = evaluarAccesoOffline({
    tokenVencido: estaVencido(marcas, ahora),
    // `hayConexionReal` es pesimista a propósito: el wifi del galpón levantado pero sin salida
    // cuenta como sin red. Ante la duda, el camino que no purga.
    enLinea: conexion.hayConexionReal(),
    ahora,
    ultimoContactoOk: ultimoContactoSegunToken(marcas) ?? 0,
    operacionesPendientes: trabajo?.operacionesPendientes() ?? 0
  });

  if (acceso === 'permitir') {
    return true;
  }

  // El único camino que purga, y solo se llega con red: el usuario puede volver a entrar ahí mismo.
  if (acceso === 'cerrar_sesion') {
    auth.logout();
  }

  const aviso = mensajeAccesoDenegado(acceso);
  if (aviso) {
    // Sin esto, el operario aterriza en un login que sin señal no puede completar y sin saber por qué.
    toast.warning(aviso);
  }

  router.navigate(['/login']);
  return false;
};
