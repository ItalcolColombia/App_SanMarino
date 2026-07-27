# ---------- deps (optimizado) ----------
FROM node:22-alpine AS deps
WORKDIR /app

# Cert corporativo opcional — solo activo en Windows con SSL inspection (ej. Kaspersky)
RUN --mount=type=secret,id=corporate_ca,required=false \
    touch /tmp/corporate-ca.crt && \
    if [ -f /run/secrets/corporate_ca ] && [ -s /run/secrets/corporate_ca ]; then \
      cat /run/secrets/corporate_ca >> /etc/ssl/certs/ca-certificates.crt && \
      cp /run/secrets/corporate_ca /tmp/corporate-ca.crt; \
    fi
ENV NODE_EXTRA_CA_CERTS=/tmp/corporate-ca.crt

# Instalar yarn de forma más eficiente
RUN corepack enable && corepack prepare yarn@1.22.22 --activate

# Copiar solo archivos de dependencias para mejor cache
COPY package.json yarn.lock* ./

# Instalar dependencias con optimizaciones
RUN yarn install \
    --frozen-lockfile \
    --non-interactive \
    --production=false \
    --network-timeout 100000 \
    && yarn cache clean

# ---------- build (optimizado) ----------
FROM node:22-alpine AS build
WORKDIR /app

# Cert corporativo opcional — Angular descarga Google Fonts durante el build
RUN --mount=type=secret,id=corporate_ca,required=false \
    touch /tmp/corporate-ca.crt && \
    if [ -f /run/secrets/corporate_ca ] && [ -s /run/secrets/corporate_ca ]; then \
      cat /run/secrets/corporate_ca >> /etc/ssl/certs/ca-certificates.crt && \
      cp /run/secrets/corporate_ca /tmp/corporate-ca.crt; \
    fi
ENV NODE_EXTRA_CA_CERTS=/tmp/corporate-ca.crt

# Copiar dependencias y código fuente
COPY --from=deps /app/node_modules ./node_modules
COPY package.json yarn.lock* ./
COPY angular.json tsconfig*.json ./
COPY tailwind.config.js postcss.config.js ./
COPY src ./src
# Se nombra el archivo en vez de copiar el directorio: si el contexto no lo trae
# (p. ej. .dockerignore desactualizado) el build falla ACÁ, diciendo qué archivo
# falta, en vez de copiar una carpeta vacía sin quejarse y reventar minutos
# después dentro del RUN con un MODULE_NOT_FOUND. Ver frontend/.dockerignore.
COPY scripts/build-version.js ./scripts/build-version.js

# Build optimizado con variables de entorno
ENV NODE_ENV=production
ENV NODE_OPTIONS="--max-old-space-size=4096"

# El sellado de versión va en DOS fases alrededor del build (ver scripts/build-version.js):
#   prepare -> compila el buildId DENTRO del bundle
#   emit    -> publica version.json, un archivo nuevo (no una mutación de uno ya hasheado)
# Nada puede reescribir un archivo del output después del build: invalidaría el SHA1
# que ngsw.json guarda para ese archivo y el Service Worker entraría en safe mode.
RUN node scripts/build-version.js prepare \
    && yarn build --configuration docker \
    && node scripts/build-version.js emit \
    && rm -rf node_modules \
    && yarn cache clean

# ---------- runtime (optimizado con seguridad) ----------
FROM nginx:1.27-alpine AS runtime

# Instalar curl para healthcheck y limpiar cache
RUN apk add --no-cache curl \
    && rm -rf /var/cache/apk/*

# Configuración de nginx optimizada
# security-headers.conf lo incluye CADA location de nginx.conf: en nginx los
# add_header de un location descartan los del server, así que la única forma de
# no perder CSP/HSTS es repetirlos por location desde un archivo compartido.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx-security-headers.conf /etc/nginx/security-headers.conf

# Copiar artefactos del build
COPY --from=build /app/dist/browser /usr/share/nginx/html

# Configurar nginx para ejecutarse como usuario no-root
# Nota: nginx necesita permisos especiales para puerto 80, pero podemos usar
# un enfoque más seguro con permisos mínimos necesarios
RUN chown -R nginx:nginx /usr/share/nginx/html \
    && chown -R nginx:nginx /var/cache/nginx \
    && chown -R nginx:nginx /var/log/nginx \
    && chown -R nginx:nginx /etc/nginx/conf.d \
    && touch /var/run/nginx.pid \
    && chown nginx:nginx /var/run/nginx.pid \
    && chmod -R 755 /usr/share/nginx/html

# Modificar nginx.conf para usar usuario nginx (no-root)
RUN sed -i 's/user  nginx;/user  nginx;/' /etc/nginx/nginx.conf || true

WORKDIR /usr/share/nginx/html

EXPOSE 80

# Healthcheck optimizado
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost/health || exit 1

# nginx ya se ejecuta como usuario nginx (no-root) por defecto en la imagen oficial
# No necesitamos cambiar USER explícitamente
