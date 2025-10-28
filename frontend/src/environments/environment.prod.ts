// src/environments/environment.prod.ts
export const environment = {
  production: true,
  // Usar ALB para rutas /api (será redirigido al backend por el ALB)
  apiUrl: '/api'
  // Alternativas:
  // apiUrl: 'http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api' // URL directa del ALB
  // apiUrl: 'http://3.145.143.253:5002/api' // IP directa del backend (sin ALB)
};
