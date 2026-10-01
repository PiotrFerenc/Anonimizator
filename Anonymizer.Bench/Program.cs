using System.Diagnostics;
using Anonymizer.Core;
using Microsoft.Extensions.DependencyInjection;

var svc = new ServiceCollection().AddAnonymizer().BuildServiceProvider().GetRequiredService<IAnonymizationService>();
var lorem = string.Concat(Enumerable.Repeat("Lorem ipsum dolor sit amet, consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore: et dolore magna aliqua.\n", 400));
var p = "To jest sciezka: /etc/projekty/main.cs oraz C:\\Users\\Jan\\a.txt koniec\n";
var sparse = string.Concat(Enumerable.Repeat(lorem + p, 1000));   // ~50MB, 2000 matches
var dense = string.Concat(Enumerable.Repeat(p, 500_000));          // ~34MB, 1M matches
var nopath = string.Concat(Enumerable.Repeat(lorem, 1000));        // ~50MB, 0 matches (':' triggers only)

foreach (var (name, text) in new[] { ("no matches", nopath), ("sparse", sparse), ("dense", dense) })
{
    svc.Anonymize(text); GC.Collect();
    var times = new List<double>(); long alloc0 = GC.GetAllocatedBytesForCurrentThread();
    for (var i = 0; i < 5; i++)
    {
        var sw = Stopwatch.StartNew(); var r = svc.Anonymize(text); sw.Stop();
        times.Add(sw.Elapsed.TotalSeconds);
        if (i == 0) Console.Write($"{name}: matches={r.Anonymized.Count} ");
    }
    var best = times.Min(); var mb = text.Length * 2 / 1e6;
    Console.WriteLine($"{text.Length / 1e6:F0}M chars  best {best * 1000:F0} ms  {text.Length / 1e6 / best:F0} Mchars/s  alloc/run {(GC.GetAllocatedBytesForCurrentThread() - alloc0) / 5 / 1e6:F0} MB");
}
