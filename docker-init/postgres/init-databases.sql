-- Script de inicialização do PostgreSQL
-- Cria databases e schemas para diferentes contextos da aplicação
-- Este script é idempotente (seguro rodar múltiplas vezes)

-- Database para Lançamentos (débitos e créditos)
SELECT 'CREATE DATABASE fincontrol_lancamentos WITH ENCODING UTF8 LC_COLLATE en_US.UTF-8 LC_CTYPE en_US.UTF-8 TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'fincontrol_lancamentos')\gexec

-- Database para Consolidado (diário, mensal)
SELECT 'CREATE DATABASE fincontrol_consolidado WITH ENCODING UTF8 LC_COLLATE en_US.UTF-8 LC_CTYPE en_US.UTF-8 TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'fincontrol_consolidado')\gexec

-- Database para Outbox Pattern (garantia de entrega de eventos)
SELECT 'CREATE DATABASE fincontrol_outbox WITH ENCODING UTF8 LC_COLLATE en_US.UTF-8 LC_CTYPE en_US.UTF-8 TEMPLATE template0'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'fincontrol_outbox')\gexec

-- Conectar ao database fincontrol_lancamentos para criar schemas
\c fincontrol_lancamentos

-- Schema para domínio de Lançamentos
CREATE SCHEMA IF NOT EXISTS lancamentos
  AUTHORIZATION fincontrol_admin;

-- Schema para infraestrutura (auditoria, logs)
CREATE SCHEMA IF NOT EXISTS infraestrutura
  AUTHORIZATION fincontrol_admin;

-- Conectar ao database fincontrol_consolidado
\c fincontrol_consolidado

-- Schema para Consolidado Diário
CREATE SCHEMA IF NOT EXISTS consolidado_diario
  AUTHORIZATION fincontrol_admin;

-- Schema para Consolidado Mensal
CREATE SCHEMA IF NOT EXISTS consolidado_mensal
  AUTHORIZATION fincontrol_admin;

-- Conectar ao database fincontrol_outbox
\c fincontrol_outbox

-- Schema para Outbox (garantia de entrega)
CREATE SCHEMA IF NOT EXISTS outbox
  AUTHORIZATION fincontrol_admin;

-- Schema para Inbox (deduplicação)
CREATE SCHEMA IF NOT EXISTS inbox
  AUTHORIZATION fincontrol_admin;

-- Permissões
GRANT CONNECT ON DATABASE fincontrol_lancamentos TO fincontrol_admin;
GRANT CONNECT ON DATABASE fincontrol_consolidado TO fincontrol_admin;
GRANT CONNECT ON DATABASE fincontrol_outbox TO fincontrol_admin;

GRANT USAGE ON SCHEMA lancamentos TO fincontrol_admin;
GRANT USAGE ON SCHEMA infraestrutura TO fincontrol_admin;
GRANT USAGE ON SCHEMA consolidado_diario TO fincontrol_admin;
GRANT USAGE ON SCHEMA consolidado_mensal TO fincontrol_admin;
GRANT USAGE ON SCHEMA outbox TO fincontrol_admin;
GRANT USAGE ON SCHEMA inbox TO fincontrol_admin;

GRANT CREATE ON SCHEMA lancamentos TO fincontrol_admin;
GRANT CREATE ON SCHEMA infraestrutura TO fincontrol_admin;
GRANT CREATE ON SCHEMA consolidado_diario TO fincontrol_admin;
GRANT CREATE ON SCHEMA consolidado_mensal TO fincontrol_admin;
GRANT CREATE ON SCHEMA outbox TO fincontrol_admin;
GRANT CREATE ON SCHEMA inbox TO fincontrol_admin;

-- Exibir databases e schemas criados
\l
