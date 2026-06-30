.PHONY: help docker-up docker-down docker-logs docker-clean docker-rebuild docker-ps docker-stop docker-start

help: ## Display this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

# Secrets Management
vault-init: ## Initialize Vault with development secrets
	@echo "🔐 Initializing Vault with secrets..."
	@powershell -NoProfile -ExecutionPolicy Bypass -File "scripts/Initialize-Vault.ps1" -Environment dev
	@echo ""
	@echo "✅ Vault initialized! Access: http://localhost:8200/ui"
	@echo "   Token: agile_dev_token_12345"

vault-list: ## List all secrets in Vault
	@echo "📋 Secrets in Vault (dev):"
	@powershell -Command "$$env:VAULT_ADDR='http://localhost:8200'; $$env:VAULT_TOKEN='agile_dev_token_12345'; vault kv list secret/dev"

vault-read: ## Read a specific secret (usage: make vault-read SECRET=postgres)
	@powershell -Command "$$env:VAULT_ADDR='http://localhost:8200'; $$env:VAULT_TOKEN='agile_dev_token_12345'; vault kv get secret/dev/$(SECRET)"

vault-ui: ## Open Vault UI in browser
	@powershell -Command "Start-Process 'http://localhost:8200/ui'"
	@echo "✅ Opening Vault UI..."

# Docker Compose Commands
docker-up: ## Start all docker-compose services
	docker-compose up -d
	@echo ""
	@echo "✅ Services started!"
	@echo ""
	@echo "Available access:"
	@echo "  RabbitMQ:  http://localhost:15672 (agile_user / agile_rabbitmq_password_123)"
	@echo "  Vault:     http://localhost:8200 (Token: agile_dev_token_12345)"
	@echo "  Jaeger:    http://localhost:16686"
	@echo "  Prometheus: http://localhost:9090"
	@echo "  Grafana:   http://localhost:3000 (admin / agile_grafana_password_123)"
	@echo ""

docker-down: ## Stop all services
	docker-compose down

docker-clean: ## Remove all services AND volumes (WARNING - deletes data!)
	docker-compose down -v
	@echo "✅ Containers and volumes removed"

docker-rebuild: ## Rebuild all services from scratch
	docker-compose down -v
	docker-compose up -d --build
	@echo "✅ Services rebuilt"

docker-ps: ## Show status of all services
	docker-compose ps

docker-logs: ## Show logs of all services (Ctrl+C to stop)
	docker-compose logs -f

docker-logs-postgres: ## Show PostgreSQL logs
	docker-compose logs -f postgres

docker-logs-rabbitmq: ## Show RabbitMQ logs
	docker-compose logs -f rabbitmq

docker-logs-redis: ## Show Redis logs
	docker-compose logs -f redis

docker-logs-vault: ## Show Vault logs
	docker-compose logs -f vault

docker-logs-jaeger: ## Show Jaeger logs
	docker-compose logs -f jaeger

docker-logs-prometheus: ## Show Prometheus logs
	docker-compose logs -f prometheus

docker-logs-grafana: ## Show Grafana logs
	docker-compose logs -f grafana

docker-stop: ## Stop all services (without removing)
	docker-compose stop

docker-start: ## Restart all stopped services
	docker-compose start

docker-shell-postgres: ## Access PostgreSQL shell
	docker-compose exec postgres psql -U agile_admin -d agile_lancamentos

docker-shell-redis: ## Access Redis CLI
	docker-compose exec redis redis-cli -a agile_redis_password_123

docker-shell-rabbitmq: ## Access RabbitMQ container shell
	docker-compose exec rabbitmq /bin/bash

# Utilities
docker-health: ## Check health of all services
	@echo "Services status:"
	docker-compose ps --format "table {{.Service}}\t{{.Status}}\t{{.State}}"

docker-network: ## List docker-compose network
	docker network ls | grep agile

docker-volume: ## List docker-compose volumes
	docker volume ls | grep agile_workers

docker-prune: ## Remove unused images, containers and networks
	docker system prune -f

docker-clean-env: ## Remove .env.docker file (never commit!)
	@if exist .env.docker (del .env.docker & echo "✅ File .env.docker removed")
	@echo ""
	@echo "⚠️  IMPORTANT:"
	@echo "    - Always use .env.docker.example as template"
	@echo "    - REAL credentials should be in Vault"
	@echo "    - Execute: make vault-init"

db-backup: ## Backup PostgreSQL
	docker-compose exec postgres pg_dump -U agile_admin agile_lancamentos > backup_lancamentos_$$(date +%Y%m%d_%H%M%S).sql
	@echo "✅ Backup completed"

db-restore: ## Restore backup (usage: make db-restore FILE=backup_file.sql)
	docker-compose exec -T postgres psql -U agile_admin agile_lancamentos < $(FILE)
	@echo "✅ Backup restored"

# Monitoring
monitor: docker-up ## Start services and open dashboards (Linux/Mac)
	@echo "Opening dashboards..."
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:3000 || open http://localhost:3000
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:16686 || open http://localhost:16686
	@echo "✅ Dashboards opened"

# Status
status: docker-ps health-check

health-check: ## Check health of services with curl
	@echo "🔍 Checking service health..."
	@echo ""
	@echo "PostgreSQL (localhost:5432):"
	@docker-compose exec -T postgres pg_isready -U agile_admin -d agile_lancamentos || echo "❌ PostgreSQL unavailable"
	@echo ""
	@echo "Redis (localhost:6379):"
	@docker-compose exec -T redis redis-cli -a agile_redis_password_123 ping || echo "❌ Redis unavailable"
	@echo ""
	@echo "RabbitMQ (localhost:5672):"
	@docker-compose exec -T rabbitmq rabbitmq-diagnostics -q ping || echo "❌ RabbitMQ unavailable"
	@echo ""
	@echo "Vault (http://localhost:8200):"
	@curl -s http://localhost:8200/v1/sys/health >/dev/null && echo "✅ Vault healthy" || echo "❌ Vault unavailable"
	@echo ""
	@echo "Jaeger (http://localhost:16686):"
	@curl -s http://localhost:16686/ >/dev/null && echo "✅ Jaeger healthy" || echo "❌ Jaeger unavailable"
	@echo ""
	@echo "Prometheus (http://localhost:9090):"
	@curl -s http://localhost:9090/-/healthy >/dev/null && echo "✅ Prometheus healthy" || echo "❌ Prometheus unavailable"
	@echo ""
	@echo "Grafana (http://localhost:3000):"
	@curl -s http://localhost:3000/api/health >/dev/null && echo "✅ Grafana healthy" || echo "❌ Grafana unavailable"
	@echo ""
