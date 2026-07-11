.PHONY: up down open logs restart rebuild build-angular check-port help deploy-backend deploy-frontend deploy-all dev dev-back dev-front

help:
	@echo "🧰 Opciones disponibles:"
	@echo ""
	@echo "📦 Desarrollo Local (nativo, sin Docker):"
	@echo "  make dev            👉 Levanta BACK (:5002) + FRONT (:4200) en ventanas separadas"
	@echo "  make dev-back       👉 Solo Backend .NET 10 (:5002)  [usa .dotnet user-local]"
	@echo "  make dev-front      👉 Solo Frontend Angular 22 (:4200)  [usa Node portable]"
	@echo ""
	@echo "📦 Desarrollo Local (Docker):"
	@echo "  make up             👉 Levanta los servicios Docker (compila frontend)"
	@echo "  make down           👉 Detiene y elimina los contenedores"
	@echo "  make rebuild        👉 Fuerza reconstrucción completa"
	@echo "  make restart        👉 Reinicia los contenedores activos"
	@echo "  make logs           👉 Muestra logs en tiempo real"
	@echo "  make open           👉 Abre navegador en frontend y API"
	@echo "  make build-angular  👉 Compila Angular en modo producción"
	@echo "  make check-port     👉 Verifica si el puerto 5050 está libre"
	@echo ""
	@echo "🚀 Despliegue AWS ECS:"
	@echo "  make deploy-backend  👉 Despliega solo el Backend a AWS"
	@echo "  make deploy-frontend 👉 Despliega solo el Frontend a AWS"
	@echo "  make deploy-all      👉 Despliega Backend y Frontend a AWS"

# ==========================================
# Desarrollo local NATIVO (sin Docker)
#   Resuelve el PATH: back -> .NET 10 user-local, front -> Node portable.
#   Detalle en dev-back.ps1 / dev-front.ps1 / dev.ps1 (raiz del repo).
# ==========================================

dev:
	powershell -NoProfile -ExecutionPolicy Bypass -File dev.ps1

dev-back:
	powershell -NoProfile -ExecutionPolicy Bypass -File dev-back.ps1

dev-front:
	powershell -NoProfile -ExecutionPolicy Bypass -File dev-front.ps1

up: check-port build-angular
	docker-compose up -d --build
	@echo "⏳ Esperando que los servicios estén listos..."
	@sleep 10
	@$(MAKE) open

down:
	docker-compose down

rebuild: down clean-dangling build-angular
	docker-compose up -d --build
	@$(MAKE) open

restart:
	docker-compose restart
	@$(MAKE) open

logs:
	docker-compose logs -f

open:
	@echo "🌐 Abriendo navegador..."
	@open http://localhost:4200
	@open http://localhost:4200/api/weatherforecast

build-angular:
	@echo "🛠️ Compilando Angular en frontend..."
	cd frontend && npm install && npm run build -- --configuration=production

check-port:
	@echo "🔎 Verificando puerto 5050..."
	@if lsof -i :5050 >/dev/null; then \
		echo "❌ El puerto 5050 ya está en uso. Libéralo antes de continuar."; \
		exit 1; \
	else \
		echo "✅ Puerto 5050 disponible."; \
	fi

clean-dangling:
	@echo "🧹 Limpiando contenedores en conflicto..."
	-docker rm -f sanmarinoapp-backend sanmarinoapp-db sanmarinoapp-frontend >/dev/null 2>&1 || true
	@echo "🧹 Limpiando imágenes huérfanas..."

# ==========================================
# Comandos de Despliegue AWS ECS
# ==========================================

deploy-backend:
	@echo "🚀 Desplegando Backend a AWS ECS..."
	@cd backend && bash scripts/deploy-backend-ecs.sh

deploy-frontend:
	@echo "🚀 Desplegando Frontend a AWS ECS..."
	@cd frontend && bash scripts/deploy-frontend-ecs.sh

deploy-all: deploy-backend deploy-frontend
	@echo ""
	@echo "✅ Despliegue completo finalizado"
	@echo "🌐 Obteniendo URLs de acceso..."
	@aws elbv2 describe-load-balancers --region us-east-2 --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text 2>/dev/null | head -1 | xargs -I {} echo "Frontend: http://{}" || echo "No se pudo obtener la URL del ALB"