using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FinControl.StressTests.Fakers;
using NBomber.Contracts;
using NBomber.CSharp;

namespace FinControl.StressTests.Scenarios;

/// <summary>
/// Cenário secundário — simula carga de escrita (registrar lançamentos) em paralelo.
/// Taxa alvo: 10 req/s (carga de fundo realista enquanto leitura é testada).
/// Tresholds: p95 &lt; 1000ms, taxa de erro &lt; 5%
/// </summary>
internal static class TransactionsScenario
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ScenarioProps Create(string baseUrl, string token, int sustainedSeconds)
    {
        var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return Scenario.Create("Entries_registrar_10rps", async ctx =>
        {
            try
            {
                var payload = JsonSerializer.Serialize(TransactionFaker.Next(), JsonOpts);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await client.PostAsync("/Entries/registrar", content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return Response.Ok();

                ctx.Logger.Warning(
                    "[FAIL] {StatusCode} {ReasonPhrase} | /Entries/registrar | body: {Body}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    body[..Math.Min(300, body.Length)]);

                return Response.Fail();
            }
            catch (Exception ex)
            {
                ctx.Logger.Error(ex, "[FAIL] Excecao ao chamar /Entries/registrar");
                return Response.Fail();
            }
        })
        .WithLoadSimulations(
            Simulation.RampingInject(rate: 10,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(20)),
            Simulation.Inject(rate: 10,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(sustainedSeconds)),
            Simulation.RampingInject(rate: 0,
                interval: TimeSpan.FromSeconds(1),
                during: TimeSpan.FromSeconds(10))
        );
    }
}

