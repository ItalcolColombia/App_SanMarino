# Comandos para el administrador AWS – Solución 403 ECR Frontend

**Usuario:** `moisesmurillo@sanmarino.com.co`  
**Cuenta:** 196080479890  
**Región:** us-east-2  
**Problema:** El usuario no puede hacer push al repositorio ECR del frontend (403 Forbidden).

---

## 1. Ver políticas actuales del usuario (diagnóstico)

```bash
# Políticas gestionadas adjuntas
aws iam list-attached-user-policies --user-name moisesmurillo@sanmarino.com.co

# Políticas inline
aws iam list-user-policies --user-name moisesmurillo@sanmarino.com.co

# Grupos del usuario (y políticas de esos grupos)
aws iam list-groups-for-user --user-name moisesmurillo@sanmarino.com.co
```

---

## 2. Opción A – Adjuntar política gestionada nueva

Desde la raíz del proyecto (donde está `backend/documentacion/ecr-policy-iam-admin.json`):

```bash
# Crear la política en IAM
aws iam create-policy \
  --policy-name ECR-SanMarino-Backend-Frontend \
  --policy-document file://backend/documentacion/ecr-policy-iam-admin.json \
  --description "ECR push/pull para backend y frontend San Marino"

# Adjuntar la política al usuario (reemplazar ACCOUNT_ID si es distinto)
aws iam attach-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-arn arn:aws:iam::196080479890:policy/ECR-SanMarino-Backend-Frontend
```

Si la política ya existe y solo quieres adjuntarla:

```bash
aws iam attach-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-arn arn:aws:iam::196080479890:policy/ECR-SanMarino-Backend-Frontend
```

---

## 3. Opción B – Política inline en el usuario

Si prefieres una política inline (sin crear política gestionada):

```bash
aws iam put-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-name ECR-SanMarino-ECR \
  --policy-document file://backend/documentacion/ecr-policy-iam-admin.json
```

*(El archivo `ecr-policy-iam-admin.json` debe estar en la ruta indicada o usar la ruta absoluta.)*

---

## 4. Opción C – Usar política gestionada de AWS

Dar permisos ECR amplios con la política de AWS:

```bash
aws iam attach-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-arn arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryPowerUser
```

*(Con esto el usuario tendrá acceso a todos los repos ECR de la cuenta en esa región.)*

---

## 5. Comprobar que la política está aplicada (opcional)

```bash
aws iam list-attached-user-policies --user-name moisesmurillo@sanmarino.com.co
```

---

## 6. Verificación por el usuario (después de aplicar la política)

El usuario debe ejecutar:

```bash
aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com
cd /ruta/al/App_SanMarino
make deploy-frontend
```

---

## Contenido de `ecr-policy-iam-admin.json`

Por si el administrador quiere revisar o crear el archivo a mano:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ECRGetAuth",
      "Effect": "Allow",
      "Action": "ecr:GetAuthorizationToken",
      "Resource": "*"
    },
    {
      "Sid": "ECRBackendFrontend",
      "Effect": "Allow",
      "Action": [
        "ecr:BatchCheckLayerAvailability",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload"
      ],
      "Resource": [
        "arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/backend",
        "arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend"
      ]
    }
  ]
}
```

---

**Repositorio que falla:** `sanmarino/zootecnia/granjas/frontend`
