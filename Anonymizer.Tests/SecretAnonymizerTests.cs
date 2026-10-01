using System.Text.RegularExpressions;
using Anonymizer.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Anonymizer.Tests;

public class SecretAnonymizerTests
{
    private readonly SecretAnonymizer _sut = new();

    private string? Match(string text)
    {
        for (var i = 0; i < text.Length; i++)
            if (_sut.Triggers.Contains(text[i]) && _sut.TryMatch(text, i, 0, out var m))
                return text.Substring(m.Start, m.Length);
        return null;
    }

    [Theory]
    [InlineData("Authorization: Bearer abc.def-123", "abc.def-123")]
    [InlineData("authorization: basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
    [InlineData("Authorization: Token t0k3n", "t0k3n")]
    [InlineData("\"Authorization\": \"Bearer abc123\"", "abc123")]
    [InlineData("token glpat-abcdefghij1234567890 end", "glpat-abcdefghij1234567890")]
    [InlineData("password=hunter2", "hunter2")]
    [InlineData("curl 'http://h/x?a=1&passwd=s3cr3t&b=2'", "s3cr3t")]
    [InlineData("GITLAB_TOKEN=abc123 make", "abc123")]
    [InlineData("secret=\"quoted value\"", "quoted")]
    [InlineData("API_KEY=k1", "k1")]
    [InlineData("apikey=k2", "k2")]
    [InlineData("access_key=k3", "k3")]
    [InlineData("Server=db;User Id=sa;Password=P@ss w0rd;Port=1", "P@ss")]
    [InlineData("Server=db;Password=Pa55;Port=1", "Pa55")]
    [InlineData("mail jan.kowalski@example.com ok", "jan.kowalski@example.com")]
    [InlineData("write to a@b.pl.", "a@b.pl")]
    [InlineData("host 192.168.0.12 down", "192.168.0.12")]
    [InlineData("(10.0.0.1)", "10.0.0.1")]
    public void Matches(string text, string expected) => Assert.Equal(expected, Match(text));

    [Theory]
    [InlineData("zwykły tekst. Nic tu nie ma, 12 testów.")]
    [InlineData("version 1.2.3 released")]
    [InlineData("v1.2.3.4")]
    [InlineData("1.2.3.4.5")]
    [InlineData("300.1.1.1")]
    [InlineData("Authorization: none")]
    [InlineData("tokens=3")]
    [InlineData("password=")]
    [InlineData("glpat-short")]
    [InlineData("@user mention")]
    [InlineData("a@b")]
    [InlineData("a=1 b=2")]
    [InlineData("Time: 12:30:00")]
    public void DoesNotMatch(string text) => Assert.Null(Match(text));

    [Fact]
    public void Replace_SameSecretSameToken_DifferentSecretDifferent()
    {
        var map = new ReplacementMap(new Random(1));
        string R(string t) { Assert.True(_sut.TryMatch(t, t.IndexOf('='), 0, out var m)); return _sut.Replace(m, t, map); }
        var a = R("password=aaa");
        var b = R("token=aaa");
        var c = R("secret=bbb");
        Assert.Matches(new Regex("^[a-z]{4}$"), a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    private static IAnonymizationService Create() =>
        new ServiceCollection().AddAnonymizer().BuildServiceProvider().GetRequiredService<IAnonymizationService>();

    [Fact]
    public void Service_KeepsKeysReplacesValues_CoexistsWithPaths()
    {
        const string text = "Authorization: Bearer tok123\nurl=http://h/?password=hunter2 user a@b.com ip 10.1.2.3 token=hunter2\nfile /etc/projekty/main.cs";
        var r = Create().Anonymize(text);
        var d = r.AnonymizedDocument;
        Assert.Contains("Authorization: Bearer ", d);
        Assert.Contains("password=", d);
        foreach (var s in new[] { "tok123", "hunter2", "a@b.com", "10.1.2.3", "/etc/projekty/" })
            Assert.DoesNotContain(s, d);
        Assert.Contains("/main.cs", d);
        Assert.Contains("secrets", r.Anonymizers);
        Assert.Contains("linux_path", r.Anonymizers);
        var t = Regex.Matches(d, "(?:password|token)=([a-z]+)");
        Assert.Equal(2, t.Count);
        Assert.Equal(t[0].Groups[1].Value, t[1].Groups[1].Value);
    }

    [Fact]
    public void Service_PlainTextUntouched()
    {
        const string t = "Build 1.2.3 ok, see docs. Done: yes";
        Assert.Equal(t, Create().Anonymize(t).AnonymizedDocument);
    }
}
