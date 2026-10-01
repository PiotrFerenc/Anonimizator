using System.Text.Encodings.Web;
using System.Text.Json;
using Anonymizer.Core;
using Microsoft.Extensions.DependencyInjection;

try
{
    using var provider = new ServiceCollection().AddAnonymizer().BuildServiceProvider();
    var result = provider.GetRequiredService<IAnonymizationService>().Anonymize(Console.In.ReadToEnd());
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    Console.Out.WriteLine(JsonSerializer.Serialize(result, options));
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
