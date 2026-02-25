# Requisitos IAM para usuario ECR - Acciones para el administrador AWS

**Usuario afectado:** `moisesmurillo@sanmarino.com.co`  
**ARN:** `arn:aws:iam::196080479890:user/moisesmurillo@sanmarino.com.co`  
**Problema:** 403 Forbidden al hacer push de imágenes al repositorio ECR del frontend.

---

## 1. Validaciones realizadas (sin permisos de administrador)

| Acción | Resultado |
|--------|-----------|
| Listar políticas IAM del usuario | ❌ AccessDenied |
| Simular políticas IAM | ❌ AccessDenied |
| Política de repositorio ECR aplicada | ✅ Aplicada (no resolvió el 403) |

**Conclusión:** No es posible ver ni modificar las políticas IAM del usuario desde esta cuenta. Solo un administrador puede hacerlo.

---

## 2. Política de repositorio actual

Se aplicó una política en `sanmarino/zootecnia/granjas/frontend` que otorga al usuario:

- `ecr:GetDownloadUrlForLayer`
- `ecr:BatchGetImage`
- `ecr:BatchCheckLayerAvailability`
- `ecr:PutImage`
- `ecr:InitiateLayerUpload`
- `ecr:UploadLayerPart`
- `ecr:CompleteLayerUpload`

El error 403 persiste, lo que sugiere que hay una restricción a nivel **IAM** (posible `Deny` explícito o recurso no incluido en las políticas).

---

## 3. Política IAM sugerida para el administrador

Adjuntar una política gestionada o crear una inline que otorgue permisos ECR sobre ambos repositorios:

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

## 4. Alternativa: política gestionada de AWS

Si se prefiere usar políticas gestionadas:

- **AmazonEC2ContainerRegistryPowerUser** (o política similar con permisos ECR amplios)

Si el usuario usa una política con recurso específico (por ejemplo, solo backend), hay que extender el recurso para incluir el frontend:

```
arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/*
```

---

## 5. Comandos para el administrador

### Ver políticas del usuario

```bash
aws iam list-attached-user-policies --user-name moisesmurillo@sanmarino.com.co
aws iam list-groups-for-user --user-name moisesmurillo@sanmarino.com.co
```

### Adjuntar política inline (ejemplo)

```bash
aws iam put-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-name ECR-SanMarino-Backend-Frontend \
  --policy-document file://ecr-policy-iam.json
```

### Crear y adjuntar política gestionada

```bash
# Crear la política
aws iam create-policy \
  --policy-name ECR-SanMarino-PowerUser \
  --policy-document file://ecr-policy-iam.json

# Adjuntar al usuario
aws iam attach-user-policy \
  --user-name moisesmurillo@sanmarino.com.co \
  --policy-arn arn:aws:iam::196080479890:policy/ECR-SanMarino-PowerUser
```

---

## 6. Verificación post-cambio

Después de ajustar las políticas IAM, el usuario debe ejecutar:

```bash
aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com
cd /Users/chelsycardona/Documents/App_SanMarino
make deploy-frontend
```
