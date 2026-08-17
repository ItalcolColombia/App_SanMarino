# Informe técnico — La plataforma no puede enviar correos desde producción

**Fecha:** 12 de agosto de 2026 · **cifras actualizadas al 17 de agosto de 2026**
**Sistema afectado:** ItalGranja / Zootécnico San Marino (`https://zootecnico.sanmarino.com.co`)
**Buzón emisor:** `zootecnico@sanmarino.com.co`
**Impacto:** ningún correo automático sale desde producción desde el **3 de junio de 2026** — a hoy,
**75 días y 71 correos sin entregar**: 20 recuperaciones de contraseña, 16 altas de usuario y 35
notificaciones de tickets. El último intento fallido es del **15 de agosto de 2026**, así que el
problema sigue activo.

---

## 1. Resumen para quien decide

La aplicación **no tiene un problema de software**. El mismo código, con las mismas credenciales y la
misma configuración, **envía correctamente** cuando se ejecuta desde la red corporativa, y es
**rechazado** cuando se ejecuta desde el servidor de producción en AWS.

El servidor de Microsoft 365 responde textualmente:

> `535 5.7.139 Authentication unsuccessful, the request did not meet the criteria to be authenticated successfully. Contact your administrator.`

«Contact your administrator» no es una figura retórica: es una **decisión administrativa del tenant**
la que rechaza la conexión, y solo un administrador de Microsoft 365 puede levantarla.

**Se necesita una acción de dos áreas:**

| Área | Qué se pide | Por qué |
|---|---|---|
| Administración Microsoft 365 | Permitir la autenticación SMTP del buzón `zootecnico@sanmarino.com.co` desde la aplicación | Es quien puede levantar la política que bloquea |
| Administración AWS | Asignar una **IP de salida fija** (NAT Gateway con IP elástica) al servicio ECS | Hoy la IP cambia en cada despliegue, así que no se puede autorizar de forma estable |

---

## 2. Qué se probó y qué resultado dio

Todas las pruebas se hicieron el 12-ago-2026, contra el tenant de producción y con las credenciales
que hoy usa el sistema.

| # | Prueba | Resultado |
|---|---|---|
| 1 | Diálogo SMTP manual desde la red corporativa: `EHLO` → `STARTTLS` → `AUTH LOGIN` | ✅ `235 Authentication successful` |
| 2 | Envío real de un correo con el mismo código del sistema (.NET 10) | ✅ **Entregado** a un buzón de Gmail |
| 3 | Flujo completo de la aplicación (solicitud de recuperación → cola → envío) | ✅ `sent` en **18 segundos**, sin reintentos |
| 4 | Configuración desplegada en producción (definición de tarea ECS, revisión 154) | ✅ **Idéntica** a la usada en las pruebas 1-3 |
| 5 | Comportamiento en producción (registro de la cola de correos) | ❌ **71 correos fallidos** desde el 3-jun-2026 (medido el 17-ago) |

**Conclusión:** credenciales correctas ✅ · código correcto ✅ · protocolo correcto ✅ ·
configuración de producción correcta ✅. La única variable que difiere entre el caso que funciona y
el que falla es **el origen de la conexión**.

## 3. Datos técnicos exactos

**Conexión que la aplicación intenta** (idéntica en ambos entornos):

| Parámetro | Valor |
|---|---|
| Servidor | `smtp.office365.com` |
| Puerto | `587` |
| Cifrado | **STARTTLS** (TLS 1.2 negociado y verificado) |
| Autenticación | `AUTH LOGIN` (usuario y contraseña) |
| Usuario / remitente | `zootecnico@sanmarino.com.co` |

**Origen que funciona:** red corporativa, IP pública `186.86.52.65`.

**Origen que falla:** contenedor en AWS ECS, región `us-east-2` (Ohio), cuenta `196080479890`,
servicio `sanmarino-back-task-service-75khncfa`. La IP pública observada el 12-ago-2026 fue
`3.137.144.7`, **pero no es estable** (ver §4).

**Error completo registrado en producción:**

```
5.7.57 Client not authenticated to send mail.
Error: 535 5.7.139 Authentication unsuccessful, the request did not meet the criteria
to be authenticated successfully. Contact your administrator.
[BN0PR03CA0016.namprd03.prod.outlook.com]
```

**Nota para quien revise el registro:** el sistema guarda además un código interno
`MustIssueStartTlsFirst`. Es un artefacto de la librería (mapea el `530` que llega **después** del
fallo de autenticación) y **no significa que falte cifrado**: el cifrado STARTTLS se negocia
correctamente, como muestra la prueba 1.

## 4. El detalle que condiciona la solución: la IP de salida no es fija

El servicio en AWS corre con `assignPublicIp: ENABLED` sobre cuatro subredes y **sin IP elástica
asignada**. Cada tarea que arranca toma una IP pública del conjunto de AWS, así que **la dirección
de salida cambia en cada despliegue o reinicio**.

Consecuencia práctica: **autorizar la IP actual no resuelve nada de forma duradera** — se rompería
en el próximo despliegue. De ahí que la solución tenga dos caminos posibles:

- **Camino A (preferido, sin depender de la IP):** que Microsoft 365 permita la autenticación SMTP de
  este buzón sin condicionarla a la ubicación de origen.
- **Camino B (si la política debe seguir atada a ubicaciones):** crear en AWS un NAT Gateway con IP
  elástica para que el sistema salga **siempre por la misma dirección**, y autorizar esa dirección
  en Microsoft 365. Requiere trabajo del área de AWS **y** del área de Microsoft 365.

## 5. Qué verificar del lado de Microsoft 365

1. **Acceso condicional / Valores predeterminados de seguridad**: ¿existe una directiva que bloquee
   la *autenticación heredada* (legacy authentication) según la ubicación o la IP de origen? Es la
   causa más probable: coincide con el mensaje `did not meet the criteria` y con que el mismo usuario
   y contraseña sí autentiquen desde la red corporativa.

2. **SMTP AUTH habilitado en los dos niveles.** Ambos comandos deben devolver `False`:

   ```powershell
   Get-CASMailbox 'zootecnico@sanmarino.com.co' | Select-Object SmtpClientAuthenticationDisabled
   Get-TransportConfig | Select-Object SmtpClientAuthenticationDisabled
   ```

3. **Registros de inicio de sesión** (Entra ID → Inicios de sesión), filtrando por el usuario
   `zootecnico@sanmarino.com.co` y protocolo/aplicación *Authenticated SMTP*. Ahí aparece la directiva
   concreta que produce el rechazo y la IP de origen del intento.

4. **Qué cambió alrededor del 3 de junio de 2026.** El último correo salió ese día y el software no se
   modificó entre esa fecha y hoy: el cambio ocurrió en el tenant.

## 6. Alternativas si la política no se puede levantar

| Alternativa | Qué implica | Observación |
|---|---|---|
| **OAuth 2.0 (Microsoft Graph)** | Registrar una aplicación en Entra ID con permiso `Mail.Send` y desarrollo en el sistema | No usa contraseña, así que no lo alcanzan las políticas de autenticación heredada. Es además el camino obligado antes del retiro definitivo de la autenticación básica |
| **Amazon SES** | Verificar el dominio en AWS y enviar desde ahí | Evita Microsoft 365 por completo; requiere configurar SPF/DKIM del dominio |
| **Retransmisión SMTP interna** | Enviar a un servidor de la organización que reenvíe | Depende de la infraestructura disponible |

---

### Anexo · Cómo reproducir la prueba que funciona

Diálogo SMTP manual (no envía ningún correo, solo verifica la autenticación):

```
1. Abrir conexión TCP a smtp.office365.com puerto 587
2. EHLO  → el servidor anuncia STARTTLS
3. STARTTLS → 220 SMTP server ready
4. Negociar TLS 1.2
5. EHLO → el servidor anuncia AUTH LOGIN XOAUTH2
6. AUTH LOGIN con usuario y contraseña en base64
   → desde la red corporativa: 235 Authentication successful
   → desde el servidor de producción: 535 5.7.139
```
