# ✅ Configuración por Comandos AWS CLI

## 📋 Resumen de lo que hicimos

### 1. ✅ Agregamos una regla al Security Group default

```bash
aws ec2 authorize-security-group-ingress \
  --group-id sg-8f1ff7fe \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-2
```

**Resultado**: Regla agregada, pero el login sigue fallando. Esto significa que el RDS probablemente está en un Security Group diferente.

---

## ⚠️ Problema Actual

El RDS `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com` **NO está en el Security Group default** que configuramos.

---

## 🔧 Solución: Obtener el Security Group Real del RDS

### Paso 1: Obtener el ID desde la consola web

1. Ve a: **https://console.aws.amazon.com/rds/home?region=us-east-2#databases:**
2. Busca: **sanmarinoapp**
3. Click en la base de datos
4. Ve a **"Connectivity & security"**
5. Anota el **"VPC security groups"** ID

### Paso 2: Ejecutar el comando

Una vez que tengas el ID (ejemplo: `sg-xxxxxxxxx`), ejecuta:

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <ID_DEL_RDS_REAL> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-2
```

**Reemplaza** `<ID_DEL_RDS_REAL>` con el ID que obtuviste de la consola.

---

## ✅ Después de ejecutar el comando

El login debería funcionar. Prueba:

```bash
curl -X POST http://3.137.143.15:5002/api/Auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email": "moiesbbuga@gmail.com", "password": "Moi$3177120174", "companyId": 0}'
```

Debería devolver un token JWT.

---

## 📝 Nota

La regla que agregamos al Security Group default (`sg-8f1ff7fe`) no causará problemas, así que puedes dejarla. Solo necesitas agregar la misma regla al Security Group real del RDS.

