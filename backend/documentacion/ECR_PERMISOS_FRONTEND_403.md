# Permisos ECR para deploy Frontend - Error 403 Forbidden

## Problema
El push al repositorio `sanmarino/zootecnia/granjas/frontend` falla con **403 Forbidden** después de subir las capas. El backend despliega correctamente.

## Usuario afectado
- **IAM User:** `moisesmurillo@sanmarino.com.co`
- **Account:** 196080479890
- **Región:** us-east-2

## Acción faltante (muy probable)
`ecr:BatchGetImage` — Docker la usa para verificar el push tras subir el manifest.

## Política IAM sugerida

El usuario necesita esta política en el repo del frontend. Si usa una política por repositorio, agregar:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:BatchCheckLayerAvailability",
        "ecr:PutImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload"
      ],
      "Resource": "arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/frontend"
    }
  ]
}
```

## Cómo verificar
Si el usuario tiene una política managed, el administrador debe asegurar que incluya `ecr:BatchGetImage` sobre el recurso del frontend.

## Nota
El repo `sanmarino/zootecnia/granjas/backend` funciona. Conviene comparar las políticas que aplican a cada repo para identificar la diferencia.
