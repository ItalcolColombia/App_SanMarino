// src/environments/environment.ts
export const environment = {
  production: false,
  //apiUrl: 'https://mi-dominio.com/api'
  apiUrl: 'http://localhost:5002/api',
  //apiUrl: 'http://alb-sanmarino-1757251809.us-east-2.elb.amazonaws.com/api'
  // apiUrl: '/api' // Usar proxy en desarrollo

  // reCAPTCHA (deshabilitado en desarrollo)
  recaptcha: {
    enabled: false,
    siteKey: ''
  },

  // Llaves de encriptación para comunicación segura con el backend
  encryptionKeys: {
    // Llave para encriptar datos enviados al backend (remitente frontend)
    remitenteFrontend: 'pR7@xW2!dN#9mZ$eH8&',
    // Llave para desencriptar datos recibidos del backend (remitente backend)
    remitenteBackend: 'Q5#vF1@pG*0bT$yK9!r'
  },
  // Llaves SECRET_UP para identificación de plataforma (24 caracteres cada una)
  platformSecret: {
    // SECRET_UP del frontend (identifica peticiones desde el frontend)
    secretUpFrontend: 'Fr0nt#SeCr3t!SanM@r1n0X2',
    // SECRET_UP del backend (para uso futuro si el backend necesita identificarse al frontend)
    secretUpBackend: 'Back!SeC2024#SanM@r!N8Y7',
    // Llave única para encriptar/desencriptar el SECRET_UP
    encryptionKey: 'EncKey#SANMARINO2024!xZ9'
  }
};
