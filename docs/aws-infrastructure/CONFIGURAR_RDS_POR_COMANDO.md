# 🔧 Configurar RDS por Comando AWS CLI

## ⚠️ No se pudo identificar automáticamente el Security Group del RDS

El RDS `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com` no tiene un Security Group con nombre descriptivo.

---

## 🔍 Opción 1: Obtener el ID desde Consola Web

1. Ve a: **https://console.aws.amazon.com/rds/home?region=us-east-2#databases:**
2. Busca: **sanmarinoapp**
3. Click en la base de datos
4. Ve a la pestaña **"Connectivity & security"**
5. Anota el **"VPC security groups"** ID (ejemplo: `sg-xxxxxxxxx`)

---

## 🎯 Opción 2: Probar con el Security Group "default"

Es posible que el RDS esté usando el Security Group default `sg-8f1ff7fe`.

**Puedes intentar agregar la regla directamente**:

```bash
aws ec2 authorize-security-group-ingress \
  --group-id sg-8f1ff7fe \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-2 \
  --group-owner-id 196080479890
```

---

## 🔍 Opción 3: Buscar todos los Security Groups manualmente

```bash
aws ec2 describe-security-groups --region us-east-2 \
  --query 'SecurityGroups[*].[GroupId,GroupName,Description]' \
  --output table
```

Luego revisa cuál podría ser el del RDS.

---

## ✅ Comando Final (Una vez tengas el ID)

Reemplaza `<ID_DEL_RDS>` con el Security Group ID que obtuviste:

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <ID_DEL_RDS> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-2
```

Este comando agregará una regla para permitir que el backend ECS (`sg-8f1ff7fe`) se conecte al RDS en el puerto 5432.

---

## 📝 Nota

Si ya existe una regla similar, verás un error. Puedes ignorarlo o verificar las reglas existentes con:

```bash
aws ec2 describe-security-groups \
  --group-ids <ID_DEL_RDS> \
  --region us-east-2 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]'
```

