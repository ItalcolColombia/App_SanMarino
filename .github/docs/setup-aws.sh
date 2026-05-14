#!/usr/bin/env bash
# =============================================================================
# Script de configuración AWS para CI/CD con GitHub Actions OIDC
# =============================================================================
# Ejecutar UNA SOLA VEZ desde una máquina con AWS CLI configurado y permisos
# de administrador. Luego de ejecutarlo el pipeline de GitHub Actions funcionará
# automáticamente en cada push a main-produccion.
#
# Requisitos previos:
#   - AWS CLI v2 instalado y configurado (aws configure)
#   - Permisos de administrador IAM en la cuenta 196080479890
#   - jq instalado (brew install jq / apt install jq)
#
# Uso: bash setup-aws.sh
# =============================================================================

set -euo pipefail

# ── Configuración ─────────────────────────────────────────────────────────────
AWS_ACCOUNT_ID="196080479890"
AWS_REGION="us-east-2"
GITHUB_ORG="ItalcolColombia"
GITHUB_REPO="App_SanMarino"
ROLE_NAME="github-actions-deploy"
OIDC_URL="https://token.actions.githubusercontent.com"
OIDC_AUDIENCE="sts.amazonaws.com"

echo "============================================================"
echo " Configuración AWS CI/CD — San Marino App"
echo " Cuenta: ${AWS_ACCOUNT_ID} | Región: ${AWS_REGION}"
echo "============================================================"

# ── PASO 1: Crear OIDC Provider ───────────────────────────────────────────────
echo ""
echo "[1/4] Configurando OIDC Provider para GitHub Actions..."

# Obtener thumbprint del certificado de GitHub
THUMBPRINT=$(openssl s_client -servername token.actions.githubusercontent.com \
  -connect token.actions.githubusercontent.com:443 </dev/null 2>/dev/null \
  | openssl x509 -fingerprint -noout -sha1 2>/dev/null \
  | sed 's/SHA1 Fingerprint=//' | tr -d ':' | tr '[:upper:]' '[:lower:]')

# Verificar si el OIDC provider ya existe
if aws iam get-open-id-connect-provider \
    --open-id-connect-provider-arn "arn:aws:iam::${AWS_ACCOUNT_ID}:oidc-provider/token.actions.githubusercontent.com" \
    &>/dev/null; then
  echo "  ✓ OIDC Provider ya existe — saltando creación."
else
  aws iam create-open-id-connect-provider \
    --url "${OIDC_URL}" \
    --client-id-list "${OIDC_AUDIENCE}" \
    --thumbprint-list "${THUMBPRINT}"
  echo "  ✓ OIDC Provider creado exitosamente."
fi

# ── PASO 2: Crear Trust Policy ────────────────────────────────────────────────
echo ""
echo "[2/4] Creando trust policy para el rol IAM..."

TRUST_POLICY=$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "GitHubActionsOIDC",
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::${AWS_ACCOUNT_ID}:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "${OIDC_AUDIENCE}",
          "token.actions.githubusercontent.com:sub": "repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/main-produccion"
        }
      }
    }
  ]
}
EOF
)

# ── PASO 3: Crear rol IAM ─────────────────────────────────────────────────────
echo ""
echo "[3/4] Creando rol IAM ${ROLE_NAME}..."

if aws iam get-role --role-name "${ROLE_NAME}" &>/dev/null; then
  echo "  ✓ Rol ya existe — actualizando trust policy..."
  aws iam update-assume-role-policy \
    --role-name "${ROLE_NAME}" \
    --policy-document "${TRUST_POLICY}"
else
  aws iam create-role \
    --role-name "${ROLE_NAME}" \
    --assume-role-policy-document "${TRUST_POLICY}" \
    --description "Rol para GitHub Actions CI/CD — San Marino App"
  echo "  ✓ Rol creado: arn:aws:iam::${AWS_ACCOUNT_ID}:role/${ROLE_NAME}"
fi

# ── Adjuntar política de permisos ─────────────────────────────────────────────
INLINE_POLICY=$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ECRAuthToken",
      "Effect": "Allow",
      "Action": ["ecr:GetAuthorizationToken"],
      "Resource": "*"
    },
    {
      "Sid": "ECRPushPull",
      "Effect": "Allow",
      "Action": [
        "ecr:BatchCheckLayerAvailability",
        "ecr:GetDownloadUrlForLayer",
        "ecr:BatchGetImage",
        "ecr:InitiateLayerUpload",
        "ecr:UploadLayerPart",
        "ecr:CompleteLayerUpload",
        "ecr:PutImage",
        "ecr:DescribeImages",
        "ecr:ListImages"
      ],
      "Resource": [
        "arn:aws:ecr:${AWS_REGION}:${AWS_ACCOUNT_ID}:repository/sanmarino/zootecnia/granjas/backend",
        "arn:aws:ecr:${AWS_REGION}:${AWS_ACCOUNT_ID}:repository/sanmarino/zootecnia/granjas/frontend"
      ]
    },
    {
      "Sid": "ECSDescribe",
      "Effect": "Allow",
      "Action": ["ecs:DescribeTaskDefinition", "ecs:DescribeServices"],
      "Resource": "*"
    },
    {
      "Sid": "ECSRegisterAndDeploy",
      "Effect": "Allow",
      "Action": ["ecs:RegisterTaskDefinition", "ecs:UpdateService"],
      "Resource": "*"
    },
    {
      "Sid": "IAMPassRole",
      "Effect": "Allow",
      "Action": ["iam:PassRole"],
      "Resource": "arn:aws:iam::${AWS_ACCOUNT_ID}:role/ecsTaskExecutionRole",
      "Condition": {
        "StringLike": {"iam:PassedToService": "ecs-tasks.amazonaws.com"}
      }
    }
  ]
}
EOF
)

aws iam put-role-policy \
  --role-name "${ROLE_NAME}" \
  --policy-name "GitHubActionsDeployPolicy" \
  --policy-document "${INLINE_POLICY}"

echo "  ✓ Política de permisos adjuntada al rol."

# ── PASO 4: Verificar repositorios ECR ───────────────────────────────────────
echo ""
echo "[4/4] Verificando repositorios ECR..."

for REPO in "sanmarino/zootecnia/granjas/backend" "sanmarino/zootecnia/granjas/frontend"; do
  if aws ecr describe-repositories --repository-names "${REPO}" --region "${AWS_REGION}" &>/dev/null; then
    echo "  ✓ ECR repo existe: ${REPO}"
  else
    echo "  ✗ ECR repo NO existe: ${REPO}"
    echo "    Crea el repositorio en la consola de ECR o con:"
    echo "    aws ecr create-repository --repository-name ${REPO} --region ${AWS_REGION}"
  fi
done

# ── Resumen final ─────────────────────────────────────────────────────────────
echo ""
echo "============================================================"
echo " Configuración completada"
echo "============================================================"
echo ""
echo " Rol IAM creado:"
echo "   arn:aws:iam::${AWS_ACCOUNT_ID}:role/${ROLE_NAME}"
echo ""
echo " Siguiente paso — NO se requieren secrets en GitHub."
echo " El pipeline usa OIDC y solo necesitas que el rol IAM"
echo " exista. Verifica el workflow en:"
echo "   .github/workflows/deploy-production.yml"
echo ""
echo " Para disparar el pipeline crea la rama main-produccion:"
echo "   git checkout -b main-produccion"
echo "   git push origin main-produccion"
echo ""
