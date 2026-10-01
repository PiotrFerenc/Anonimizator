using System.Text.RegularExpressions;
using Anonymizer.Core;

namespace Anonymizer.Tests;

public class LinuxPathAnonymizerTests
{
    private readonly LinuxPathAnonymizer _sut = new();

    private string? Match(string text)
    {
        for (var i = 0; i < text.Length; i++)
            if (text[i] == '/' && _sut.TryMatch(text, i, 0, out var m))
                return text.Substring(m.Start, m.Length);
        return null;
    }

    [Theory]
    [InlineData("/etc/projekty/main.cs", "/etc/projekty/main.cs")]
    [InlineData("/bin/controller.cs", "/bin/controller.cs")]
    [InlineData("plik \"/a/b.cs\" tu", "/a/b.cs")]
    [InlineData("(/a/b.cs)", "/a/b.cs")]
    [InlineData("zobacz /etc/a/b.cs.", "/etc/a/b.cs")]
    [InlineData("x=/a/b.cs, dalej", "/a/b.cs")]
    public void Matches(string text, string expected) => Assert.Equal(expected, Match(text));

    [Theory]
    [InlineData("/etc")]
    [InlineData("/etc/projekty/")]
    [InlineData("https://host/a/b.html")]
    [InlineData("and/or")]
    [InlineData("1/2/2024")]
    [InlineData("a//b.cs")]
    public void DoesNotMatch(string text) => Assert.Null(Match(text));

    [Fact]
    public void Replace_KeepsFileName_ReplacesDirectory_SameDirSameToken()
    {
        var map = new ReplacementMap(new Random(1));
        var a = Replace("/etc/projekty/main.cs", map);
        var b = Replace("/etc/projekty/test.cs", map);
        var c = Replace("/bin/controller.cs", map);

        Assert.Matches(new Regex("^/[a-z]{4}/main\\.cs$"), a);
        Assert.Equal(a[..6], b[..6]);
        Assert.NotEqual(a[..6], c[..6]);
        Assert.Matches(new Regex("^/[a-z]{4}/controller\\.cs$"), c);
    }

    private string Replace(string text, ReplacementMap map)
    {
        Assert.True(_sut.TryMatch(text, 0, 0, out var m));
        return _sut.Replace(m, text, map);
    }
}
