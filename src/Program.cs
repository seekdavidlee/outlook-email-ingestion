// See https://aka.ms/new-console-template for more information
using Azure.Identity;
using EmailIngestion;
using Microsoft.Graph;

var options = new InteractiveBrowserCredentialOptions
{
    ClientId = Environment.GetEnvironmentVariable("ClientId"),
    TenantId = "common",
    RedirectUri = new Uri("http://localhost"),
    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
    LoginHint = Environment.GetEnvironmentVariable("EmailUsername"),
};

using var graphClient = new GraphServiceClient(new InteractiveBrowserCredential(options), ["https://graph.microsoft.com/.default"]);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true; // Prevent the process from terminating.
    cts.Cancel();
    Console.WriteLine("Cancellation requested...");
};

using var httpClient = new HttpClient();
httpClient.BaseAddress = new Uri(Environment.GetEnvironmentVariable("BaseUri")!);

var procesor = new Processor(httpClient, graphClient, cts.Token);
await procesor.ProcessMessagesAsync();
