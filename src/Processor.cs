using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Text.Json;

namespace EmailIngestion;

public class Processor(HttpClient fileSystemClient, GraphServiceClient client, CancellationToken cancellationToken)
{
    public async Task ProcessMessagesAsync()
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var res = await client.Me.Messages.GetAsync(cancellationToken: cancellationToken);
                if (res is not null)
                {
                    await ProcessResponseAsync(res);
                }

                await Task.Delay(3000);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{DateTime.UtcNow} - error: {e.Message}");
                return;
            }
        }
    }

    private async Task ProcessResponseAsync(MessageCollectionResponse response)
    {
        if (response.Value is null)
        {
            return;
        }

        foreach (var msg in response.Value)
        {
            await ProcessMessageAsync(msg);
        }

        if (response.OdataNextLink is null)
        {
            return;
        }

        var nextPageResponse = await client.Me.Messages.WithUrl(response.OdataNextLink)
            .GetAsync(cancellationToken: cancellationToken);
        if (nextPageResponse is null)
        {
            return;
        }
        await ProcessResponseAsync(nextPageResponse);
    }

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string PATH_PREFIX = $"{Environment.GetEnvironmentVariable("FileSystemApi")}/storage/files/object?path=prod/emails/{Environment.GetEnvironmentVariable("EmailUsername")}";

    private async Task ProcessMessageAsync(Message message)
    {
        // save raw email to persistance storage and then remove email
        var json = JsonSerializer.Serialize(message, jsonOptions);

        var id = message.CreatedDateTime!.Value.Ticks;

        int year = message.CreatedDateTime.Value.Year;
        int month = message.CreatedDateTime.Value.Month;
        int day = message.CreatedDateTime.Value.Day;

        var response = await fileSystemClient.PutAsync($"{PATH_PREFIX}/{year}/{month}/{day}/{id}.json", new StringContent(json));

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"{DateTime.UtcNow} - error: {content}, http status: {response.StatusCode}");
        }
        else
        {
            await client.Me.Messages[message.Id].DeleteAsync(cancellationToken: cancellationToken);
            Console.WriteLine($"processed: {message.Subject}, sent on {message.SentDateTime}");
        }
    }
}
