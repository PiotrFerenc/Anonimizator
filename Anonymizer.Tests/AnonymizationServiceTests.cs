using System.Buffers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Anonymizer.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Anonymizer.Tests;

public class AnonymizationServiceTests
{
    private const string Example =
        "To jest ścieżka: /etc/projekty/main.cs\nTo jest też ścieżka: /etc/projekty/test.cs\nścieżka z innego miejsca /bin/controller.cs";

    private sealed class SecretAnonymizer : IAnonymizer
    {
        public string Name => "secret";
        public string Triggers => "S";
        public bool TryMatch(ReadOnlySpan<char> text, int triggerIndex, int cursor, out PathMatch match)
        {
            if (text[triggerIndex..].StartsWith("SECRET")) { match = new(triggerIndex, 6, triggerIndex, 6); return true; }
            match = default;
            return false;
        }
        public string Replace(in PathMatch match, ReadOnlySpan<char> text, ReplacementMap map) => "***";
    }

    private static IAnonymizationService Create(int seed = 1, Action<IServiceCollection>? extra = null)
    {
        var s = new ServiceCollection();
        s.AddAnonymizer();
        s.AddSingleton(new Random(seed));
        extra?.Invoke(s);
        return s.BuildServiceProvider().GetRequiredService<IAnonymizationService>();
    }

    [Fact]
    public void Example_FromSpec()
    {
        var r = Create().Anonymize(Example);
        Assert.Equal(Example, r.Original);
        Assert.Equal(["linux_path"], r.Anonymizers);
        Assert.Equal(3, r.Anonymized.Count);
        Assert.Matches(@"^To jest ścieżka: /[a-z]{4}/main\.cs\nTo jest też ścieżka: /[a-z]{4}/test\.cs\nścieżka z innego miejsca /[a-z]{4}/controller\.cs$", r.AnonymizedDocument);
        Assert.Equal("/etc/projekty/main.cs", r.Anonymized[0].Original);
        Assert.Equal("/bin/controller.cs", r.Anonymized[2].Original);
        Assert.Equal(r.Anonymized[0].Anonymized[..6], r.Anonymized[1].Anonymized[..6]);
        Assert.NotEqual(r.Anonymized[0].Anonymized[..6], r.Anonymized[2].Anonymized[..6]);
        foreach (var it in r.Anonymized)
        {
            Assert.Contains(it.Original, Example);
            Assert.Contains(it.Anonymized, r.AnonymizedDocument);
            Assert.Equal("linux_path", it.Anonymizer);
        }
    }

    [Fact]
    public void Mixed_LinuxAndWindows()
    {
        var r = Create().Anonymize("a /etc/x/f.cs b C:\\Users\\Jan\\g.txt c");
        Assert.Equal(["linux_path", "windows_path"], r.Anonymizers);
        Assert.Matches(@"^a /[a-z]{4}/f\.cs b C:\\[a-z]{4}\\g\.txt c$", r.AnonymizedDocument);
        var toks = Regex.Matches(r.AnonymizedDocument, "[a-z]{4}(?=[/\\\\])").Select(m => m.Value).ToList();
        Assert.Equal(2, toks.Distinct().Count());
    }

    [Fact]
    public void NoMatches_SameInstance()
    {
        var text = new string('x', 10) + " http://a/b.html and/or 1/2/2024";
        var r = Create().Anonymize(text);
        Assert.Same(text, r.AnonymizedDocument);
        Assert.Empty(r.Anonymized);
        Assert.Empty(r.Anonymizers);
    }

    [Fact]
    public void Empty_And_Null()
    {
        var r = Create().Anonymize("");
        Assert.Equal("", r.AnonymizedDocument);
        Assert.Empty(r.Anonymized);
        Assert.Throws<ArgumentNullException>(() => Create().Anonymize(null!));
    }

    [Fact]
    public void NewlinesAndSurroundingTextPreserved()
    {
        var r = Create().Anonymize("x\r\n/a/b.cs\r\ny\n");
        Assert.Matches(@"^x\r\n/[a-z]{4}/b\.cs\r\ny\n$", r.AnonymizedDocument);
    }

    [Fact]
    public void CustomAnonymizer_UsedWithoutServiceChanges()
    {
        var r = Create(extra: s => s.AddAnonymizer<SecretAnonymizer>()).Anonymize("pass SECRET in /a/b.cs");
        Assert.Equal(["secret", "linux_path"], r.Anonymizers);
        Assert.StartsWith("pass *** in /", r.AnonymizedDocument);
    }

    [Fact]
    public void DuplicateName_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AnonymizationService([new SecretAnonymizer(), new SecretAnonymizer()], new Random(1)));
    }

    [Fact]
    public void Json_IsSnakeCase()
    {
        var r = Create().Anonymize("/a/b.cs");
        var json = JsonSerializer.Serialize(r, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("original", out _));
        Assert.True(root.TryGetProperty("anonymized_document", out _));
        Assert.True(root.TryGetProperty("anonymizers", out _));
        var item = root.GetProperty("anonymized")[0];
        Assert.True(item.TryGetProperty("anonymizer", out _));
        Assert.True(item.TryGetProperty("original", out _));
    }
}
