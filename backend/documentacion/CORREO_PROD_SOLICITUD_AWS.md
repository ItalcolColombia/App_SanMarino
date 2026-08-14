# Correo para el administrador de AWS

> Copiá el texto de abajo y enviálo. Completá el destinatario y tu firma.
> **Enviá este correo solo si el área de Microsoft 365 responde que la autorización debe ir atada a
> una IP de origen** (Opción B de la solicitud a M365). Si aceptan habilitar el envío sin condicionar
> por ubicación, este cambio de infraestructura no hace falta.
> Detalle completo en [`CORREO_PROD_INFORME_TECNICO.md`](CORREO_PROD_INFORME_TECNICO.md).

---

**Asunto:** Solicitud: IP de salida fija (NAT Gateway + Elastic IP) para el servicio ECS sanmarino-back

---

Hola,

Necesitamos un cambio en la salida a internet del backend de ItalGranja en AWS. Va el contexto y el
pedido concreto.

## Por qué

La plataforma **no puede enviar correos desde el 3 de junio de 2026** (quedaron sin funcionar la
recuperación de contraseña, el alta de usuarios y las notificaciones de tickets). Ya verificamos que
no es un problema de la aplicación: el mismo código y las mismas credenciales **envían correctamente
desde la red corporativa** y son rechazados cuando la conexión sale del servidor de producción.

El servidor de Microsoft 365 rechaza la conexión **según su origen**. Si el área de Microsoft 365
necesita autorizar nuestra dirección de salida, hoy no puede hacerlo de forma estable, y ese es el
problema que les traemos.

## El problema de infraestructura

El servicio corre con `assignPublicIp: ENABLED` sobre cuatro subredes y **sin Elastic IP**, así que
cada tarea toma una IP pública del conjunto de AWS. La dirección **cambia en cada despliegue o
reinicio** (el 12 de agosto era `3.137.144.7`, y no se mantiene).

Consecuencia: cualquier autorización basada en nuestra IP se rompería en la siguiente actualización
del sistema.

## Qué pedimos

Que el tráfico saliente del servicio salga **siempre por la misma dirección IP pública**. La forma
estándar es:

1. Crear un **NAT Gateway** con una **Elastic IP** asociada.
2. Mover las tareas del servicio a **subredes privadas** cuya tabla de rutas apunte al NAT Gateway
   (`0.0.0.0/0 → nat-gateway`).
3. Cambiar el servicio a `assignPublicIp: DISABLED`.
4. Informarnos la Elastic IP resultante, para pasarla al área de Microsoft 365.

## Recursos afectados

| Dato | Valor |
|---|---|
| Cuenta | `196080479890` |
| Región | `us-east-2` (Ohio) |
| Clúster ECS | `devSanmarinoZoo` |
| Servicio | `sanmarino-back-task-service-75khncfa` |
| Definición de tarea actual | `sanmarino-back-task:154` |
| Subredes actuales | `subnet-16cbb35a`, `subnet-ebdfcf91`, `subnet-89cf15e2`, `subnet-0068701d28ed1c03c` |
| Grupo de seguridad | `sg-8f1ff7fe` |
| Configuración actual | `assignPublicIp: ENABLED`, sin Elastic IP |

## Qué tener en cuenta

- **El servicio queda detrás de un balanceador (ALB)**, así que dejar de tener IP pública no afecta el
  acceso de los usuarios: el tráfico entrante sigue llegando por el balanceador. El cambio es solo
  para el tráfico **saliente**.
- **Verificar que el acceso a la base de datos siga funcionando** después del cambio de subredes
  (RDS PostgreSQL) — es la comprobación principal tras la migración.
- **Costo**: un NAT Gateway tiene costo por hora y por GB procesado. Si esto resultara un
  inconveniente, avísennos: existe la alternativa de resolverlo por el lado de Microsoft 365, sin
  tocar la infraestructura.
- **Ventana**: el cambio implica reiniciar las tareas del servicio, así que conviene hacerlo en
  horario de baja operación.

Quedamos atentos. Cuando tengamos la Elastic IP se la pasamos al administrador de Microsoft 365 para
que autorice el envío.

Gracias,
