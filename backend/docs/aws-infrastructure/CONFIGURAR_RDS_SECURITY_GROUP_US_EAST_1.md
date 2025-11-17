# 🔒 Configurar Security Group del RDS en us-east-1

## 🚨 Error Actual

```
Failed to connect to 10.4.6.6:5432
System.TimeoutException: The operation has timed out.
```

## ✅ Solución

### Security Group de ECS (Ya creado)
- **ID**: `sg-0c6a91db2ba4b872f`
- **Nombre**: sanmarino-ecs-sg
- **Región**: us-east-1

### Pasos a Seguir en Consola de AWS

1. Ve a **EC2 Console** → **Security Groups** en **us-east-1**
2. Busca el Security Group asociado al RDS: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
3. Selecciona el Security Group y ve a la pestaña **Inbound rules**
4. Haz clic en **Edit inbound rules**
5. Haz clic en **Add rule**
6. Configura la regla:
   - **Type**: PostgreSQL
   - **Protocol**: TCP
   - **Port range**: 5432
   - **Source**: Custom → Busca `sg-0c6a91db2ba4b872f`
   - **Description**: Allow ECS tasks from San Marino backend
7. Haz clic en **Save rules**

---

## 📋 Información del RDS

- **Endpoint**: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **Database**: sanmarinoappdev
- **Usuario**: repropesa01
- **Puerto**: 5432

---

## 🎯 Después de Configurar

Una vez configurado el Security Group:

1. El backend automáticamente intentará conectarse
2. Deberías poder hacer login sin errores
3. Verifica con:

```bash
curl -X POST http://44.203.245.250:5002/api/Auth/login \
  -H 'accept: application/json' \
  -H 'Content-Type: application/json' \
  -d '{"email": "moiesbbuga@gmail.com", "password": "Moi$3177120174", "companyId": 0}'
```

---

## ✅ Estado Actual

- **Backend en us-east-1**: ✅ ACTIVO (IP: 44.203.245.250)
- **Health Check**: ✅ OK
- **Conexión a RDS**: ⏳ PENDIENTE - Necesita configuración de Security Group

---

**Acción requerida**: Configurar Security Group del RDS manualmente en la consola de AWS.

