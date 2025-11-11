# ---------- deps ----------
FROM node:20-alpine AS deps
WORKDIR /app

# Usa Yarn si ya está en la imagen; si no existe, lo instala
RUN if command -v yarn >/dev/null 2>&1; then yarn --version; else npm i -g yarn@1.22.22; fi

# Copiamos manifests (mejor caché)
COPY package.json ./

# Instala dependencias con o sin lockfile
RUN if [ -f yarn.lock ]; then \
      yarn install --frozen-lockfile --non-interactive; \
    else \
      yarn install --non-interactive; \
    fi

# ---------- build ----------
FROM node:20-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .

# Build de producción (builder "application" => dist/browser)
RUN yarn build --configuration docker


# ---------- runtime ----------
FROM nginx:1.27-alpine AS runtime
WORKDIR /usr/share/nginx/html
RUN apk add --no-cache curl

# NGINX config (SPA con fallback a index.html)
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copia artefactos del build (Angular 17 application => dist/browser)
COPY --from=build /app/dist/browser .

EXPOSE 80
HEALTHCHECK CMD curl -fsS http://localhost/ || exit 1
