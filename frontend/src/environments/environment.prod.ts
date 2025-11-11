// src/environments/environment.prod.ts
export const environment = {
  production: true,
  // Usar ALB para rutas /api (será redirigido al backend por el ALB)
  apiUrl: '/api',
  // Alternativas:
  // apiUrl: 'http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api' // URL directa del ALB
  // apiUrl: 'http://3.145.143.253:5002/api' // IP directa del backend (sin ALB)

  // Llaves de encriptación para comunicación segura con el backend
  encryptionKeys: {
    // Llave para encriptar datos enviados al backend (remitente frontend)
    remitenteFrontend: 'pR7@xW2!dN#9mZ$eH8&',
    // Llave para desencriptar datos recibidos del backend (remitente backend)
    remitenteBackend: 'Q5#vF1@pG*0bT$yK9!r'
  },
  // Llaves SECRET_UP para identificación de plataforma
  platformSecret: {
    // SECRET_UP del frontend (identifica peticiones desde el frontend)
    secretUpFrontend: 'Fr0nt#SeCr3t!SanM@r1n0X2',
    // SECRET_UP del backend (para uso futuro si el backend necesita identificarse al frontend)
    secretUpBackend: 'Back!SeC2024#SanM@r!N8Y7',
    // Llave única para encriptar/desencriptar el SECRET_UP
    encryptionKey: 'EncKey#SANMARINO2024!xZ9'
  },
  // reCAPTCHA (habilitado en producción)
  recaptcha: {
    enabled: true,
    siteKey: '6LdjOggsAAAAAGA_2g3nm8822e9pOs4D07QpWOZA'
  }
};
