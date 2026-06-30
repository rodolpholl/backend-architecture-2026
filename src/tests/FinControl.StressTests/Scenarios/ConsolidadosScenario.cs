using System.Globalization;
using System.Net.Http.Headers;
using NBomber.Contracts;
using NBomber.CSharp;

namespace FinControl.StressTests.Scenarios;

/// <summary>
/// Cenário principal — valida o NFR de 50 req/s no endpoint de saldo consolidado.
/// Tresholds: p95 &lt; 500ms, taxa de erro &lt; 5%
/// </summary>
internal static class ConsolidadosScenario
{
    public static ScenarioProps Create(string baseUrl, string token, int sustainedSeconds)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var today = DateOnly.FromDateTime(DateTime.Today)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endpoint = $"/consolidados/saldo?transaction-date={today}";

        return Scenario.Create("consolidados_saldo_50rps", async ctx =>
        {
            try
            {
                using var response = await client.GetAsync(endpoint);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return Response.Ok();

                // Loga status + primeiros 300 chars do body para facilitar diagnóstico
                ctx.Logger.Warning(
                    "[FAIL] {StatusCode} {ReasonPhrase} | {Endpoint} | body: {Body}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    endpoint,
                    body[..Math.Min(300, body.Length)]);

                return Response.Fail();
            }
            catch (Exception ex)
            {
                ctx.Logger.Error(ex, "[FAIL] Excecao ao chamar {Endpoint}", endpoint);
                return Response.Fail();
            }
        })
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 50,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(20)),
            Simulation.Inject(rate: 50,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(sustainedSeconds)),
            Simulation.RampingInject(rate: 0,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(10))
        );
    }
}
