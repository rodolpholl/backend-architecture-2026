.PHONY: help docker-up docker-down docker-logs docker-clean docker-rebuild docker-ps docker-stop docker-start

help: ## Mostra este help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-30s\033[0m %s\n", $$1, $$2}'

# Secrets Management
vault-init: ## Inicializa Vault com secrets de desenvolvimento
	@echo "🔐 Inicializando Vault com secrets..."
	@powershell -NoProfile -ExecutionPolicy Bypass -File "scripts/Initialize-Vault.ps1" -Environment dev
	@echo ""
	@echo "✅ Vault inicializado! Acesse: http://localhost:8200/ui"
	@echo "   Token: agile_dev_token_12345"

vault-list: ## Lista todos os secrets no Vault
	@echo "📋 Secrets no Vault (dev):"
	@powershell -Command "$$env:VAULT_ADDR='http://localhost:8200'; $$env:VAULT_TOKEN='agile_dev_token_12345'; vault kv list secret/dev"

vault-read: ## Lê um secret específico (uso: make vault-read SECRET=postgres)
	@powershell -Command "$$env:VAULT_ADDR='http://localhost:8200'; $$env:VAULT_TOKEN='agile_dev_token_12345'; vault kv get secret/dev/$(SECRET)"

vault-ui: ## Abre Vault UI no navegador
	@powershell -Command "Start-Process 'http://localhost:8200/ui'"
	@echo "✅ Abrindo Vault UI..."

# Docker Compose Commands
docker-up: ## Inicia todos os serviços do docker-compose
	docker-compose up -d
	@echo ""
	@echo "✅ Serviços iniciados!"
	@echo ""
	@echo "Acessos disponíveis:"
	@echo "  RabbitMQ:  http://localhost:15672 (agile_user / agile_rabbitmq_password_123)"
	@echo "  Vault:     http://localhost:8200 (Token: agile_dev_token_12345)"
	@echo "  Jaeger:    http://localhost:16686"
	@echo "  Prometheus: http://localhost:9090"
	@echo "  Grafana:   http://localhost:3000 (admin / agile_grafana_password_123)"
	@echo ""

docker-down: ## Para todos os serviços
	docker-compose down

docker-clean: ## Remove todos os serviços E volumes (CUIDADO - apaga dados!)
	docker-compose down -v
	@echo "✅ Contêiners e volumes removidos"

docker-rebuild: ## Reconstrói todos os serviços do zero
	docker-compose down -v
	docker-compose up -d --build
	@echo "✅ Serviços reconstruídos"

docker-ps: ## Mostra status de todos os serviços
	docker-compose ps

docker-logs: ## Mostra logs de todos os serviços (Ctrl+C para parar)
	docker-compose logs -f

docker-logs-postgres: ## Mostra logs do PostgreSQL
	docker-compose logs -f postgres

docker-logs-rabbitmq: ## Mostra logs do RabbitMQ
	docker-compose logs -f rabbitmq

docker-logs-redis: ## Mostra logs do Redis
	docker-compose logs -f redis

docker-logs-vault: ## Mostra logs do Vault
	docker-compose logs -f vault

docker-logs-jaeger: ## Mostra logs do Jaeger
	docker-compose logs -f jaeger

docker-logs-prometheus: ## Mostra logs do Prometheus
	docker-compose logs -f prometheus

docker-logs-grafana: ## Mostra logs do Grafana
	docker-compose logs -f grafana

docker-stop: ## Para todos os serviços (sem remover)
	docker-compose stop

docker-start: ## Reinicia todos os serviços parados
	docker-compose start

docker-shell-postgres: ## Acessa shell do PostgreSQL
	docker-compose exec postgres psql -U agile_admin -d agile_lancamentos

docker-shell-redis: ## Acessa CLI do Redis
	docker-compose exec redis redis-cli -a agile_redis_password_123

docker-shell-rabbitmq: ## Acessa shell do container RabbitMQ
	docker-compose exec rabbitmq /bin/bash

# Utilidades
docker-health: ## Verifica saúde de todos os serviços
	@echo "Status dos serviços:"
	docker-compose ps --format "table {{.Service}}\t{{.Status}}\t{{.State}}"

docker-network: ## Lista a rede do docker-compose
	docker network ls | grep agile

docker-volume: ## Lista os volumes do docker-compose
	docker volume ls | grep agile_workers

docker-prune: ## Remove imagens, contêiners e redes não utilizados
	docker system prune -f

docker-clean-env: ## Remove arquivo .env.docker (nunca commitar!)
	@if exist .env.docker (del .env.docker & echo "✅ Arquivo .env.docker removido")
	@echo ""
	@echo "⚠️  IMPORTANTE:"
	@echo "    - Sempre use .env.docker.example como template"
	@echo "    - Credenciais REAIS devem estar no Vault"
	@echo "    - Execute: make vault-init"

db-backup: ## Faz backup do PostgreSQL
	docker-compose exec postgres pg_dump -U agile_admin agile_lancamentos > backup_lancamentos_$$(date +%Y%m%d_%H%M%S).sql
	@echo "✅ Backup realizado"

db-restore: ## Restaura backup (uso: make db-restore FILE=backup_file.sql)
	docker-compose exec -T postgres psql -U agile_admin agile_lancamentos < $(FILE)
	@echo "✅ Backup restaurado"

# Monitoramento
monitor: docker-up ## Inicia os serviços e abre dashboards (Linux/Mac)
	@echo "Abrindo dashboards..."
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:3000 || open http://localhost:3000
	@command -v xdg-open >/dev/null 2>&1 && xdg-open http://localhost:16686 || open http://localhost:16686
	@echo "✅ Dashboards abertos"

# Status
status: docker-ps health-check

health-check: ## Verifica saúde dos serviços com curl
	@echo "🔍 Verificando saúde dos serviços..."
	@echo ""
	@echo "PostgreSQL (localhost:5432):"
	@docker-compose exec -T postgres pg_isready -U agile_admin -d agile_lancamentos || echo "❌ PostgreSQL indisponível"
	@echo ""
	@echo "Redis (localhost:6379):"
	@docker-compose exec -T redis redis-cli -a agile_redis_password_123 ping || echo "❌ Redis indisponível"
	@echo ""
	@echo "RabbitMQ (localhost:5672):"
	@docker-compose exec -T rabbitmq rabbitmq-diagnostics -q ping || echo "❌ RabbitMQ indisponível"
	@echo ""
	@echo "Vault (http://localhost:8200):"
	@curl -s http://localhost:8200/v1/sys/health >/dev/null && echo "✅ Vault healthy" || echo "❌ Vault indisponível"
	@echo ""
	@echo "Jaeger (http://localhost:16686):"
	@curl -s http://localhost:16686/ >/dev/null && echo "✅ Jaeger healthy" || echo "❌ Jaeger indisponível"
	@echo ""
	@echo "Prometheus (http://localhost:9090):"
	@curl -s http://localhost:9090/-/healthy >/dev/null && echo "✅ Prometheus healthy" || echo "❌ Prometheus indisponível"
	@echo ""
	@echo "Grafana (http://localhost:3000):"
	@curl -s http://localhost:3000/api/health >/dev/null && echo "✅ Grafana healthy" || echo "❌ Grafana indisponível"
	@echo ""
