using FinControl.StressTests;
using FinControl.StressTests.Scenarios;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// ─────────────────────────────────────────────────────────────────────────────
//  FinControl — Stress Test  (NBomber)
//
//  Valida o NFR de 50 req/s no endpoint de saldo consolidado (GET /consolidados/saldo)
//  e aplica carga de fundo de escrita (POST /lancamentos/registrar) em paralelo.
//
//  PRÉ-REQUISITOS
//  ──────────────
//  1. Docker Compose no ar:
//       docker compose up -d
//  2. Kong e Keycloak saudáveis (aguardar ~2 min após o compose)
//  3. Token JWT será obtido automaticamente do Keycloak
//     OU definir a variável STRESS_JWT_TOKEN para pular o login.
//
//  EXECUÇÃO
//  ────────
//  dotnet run --project src/tests/FinControl.StressTests
//
//  EXECUÇÃO RÁPIDA (smoke test de 15 segundos)
//  ────────────────────────────────────────────
//  STRESS_DURATION=15 dotnet run --project src/tests/FinControl.StressTests
//
//  VARIÁVEIS DE AMBIENTE DISPONÍVEIS
//  ──────────────────────────────────
//  STRESS_BASE_URL      URL base do Kong      (padrão: http://localhost:8000)
//  STRESS_KEYCLOAK_URL  URL do Keycloak       (padrão: http://localhost:8081)
//  STRESS_USERNAME      Usuário para o token  (padrão: admin.fincontrol)
//  STRESS_PASSWORD      Senha do usuário      (padrão: Admin@123456)
//  STRESS_JWT_TOKEN     Token JWT pronto       (ignora login automático)
//  STRESS_DURATION      Segundos do estágio   (padrão: 60)
//
//  RELATÓRIOS
//  ──────────
//  Gerados em stress-reports/ (HTML + Markdown) após o término do teste.
// ─────────────────────────────────────────────────────────────────────────────

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║   FinControl — Stress Test  (NBomber)        ║");
Console.WriteLine("║   NFR: 50 req/s  ·  p95 < 500ms  ·  erro < 5% ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// 1. Carrega configuração (env vars com fallback para defaults locais)
var config = StressConfig.FromEnvironment();

Console.WriteLine("  Configuração:");
Console.WriteLine($"  • Kong URL:     {config.BaseUrl}");
Console.WriteLine($"  • Keycloak URL: {config.KeycloakUrl}");
Console.WriteLine($"  • Usuário:      {config.Username}");
Console.WriteLine($"  • Duração:      {config.SustainedDurationSeconds}s (estágio sustentado)");
Console.WriteLine();

// 2. Obtém JWT (auto-fetch do Keycloak ou via STRESS_JWT_TOKEN)
var token = await AuthHelper.FetchTokenAsync(config);

Console.WriteLine();
Console.WriteLine("  Cenários:");
Console.WriteLine($"  • consolidados_saldo_50rps  → GET  /consolidados/saldo  → ramp 20s + {config.SustainedDurationSeconds}s @ 50 req/s");
Console.WriteLine($"  • lancamentos_registrar_10rps → POST /lancamentos/registrar → ramp 20s + {config.SustainedDurationSeconds}s @ 10 req/s");
Console.WriteLine();
Console.WriteLine("  Iniciando...");
Console.WriteLine();

// 3. Cria os dois cenários
var consolidados = ConsolidadosScenario.Create(config.BaseUrl, token, config.SustainedDurationSeconds);
var lancamentos  = TransactionsScenario.Create(config.BaseUrl, token, config.SustainedDurationSeconds);

// 4. Executa (os cenários correm em paralelo)
NBomberRunner
    .RegisterScenarios(consolidados, lancamentos)
    .WithReportFileName("fincontrol_stress_report")
    .WithReportFolder("stress-reports")
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md)
    .Run();
