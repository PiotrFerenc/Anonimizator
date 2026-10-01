using System.Text.RegularExpressions;
using Anonymizer.Core;
using Xunit;

namespace Anonymizer.Tests;

public class WindowsPathAnonymizerTests
{
    private readonly WindowsPathAnonymizer _a = new();

    private bool Match(string text, out PathMatch m, int cursor = 0)
        => _a.TryMatch(text, text.IndexOf(':'), cursor, out m);

    private string? Run(string text, ReplacementMap? map = null)
        => Match(text, out var m) ? _a.Replace(m, text, map ?? new ReplacementMap(new Random(1))) : null;

    [Theory]
    [InlineData(@"C:\Users\Jan\a.txt", @"^C:\\[a-z]{4}\\a\.txt$")]
    [InlineData(@"c:\x\y.cs", @"^c:\\[a-z]{4}\\y\.cs$")]
    [InlineData(@"C:\a\b\c.txt", @"^C:\\[a-z]{4}\\c\.txt$")]
    public void Replaces(string text, string pattern) => Assert.Matches(pattern, Run(text)!);

    [Theory]
    [InlineData(@"C:\a.txt")]
    [InlineData(@"C:\dir\")]
    [InlineData(@"C:\\a.txt")]
    [InlineData(@"file:C:\x\y")]
    [InlineData(@"abC:\x\y")]
    [InlineData(@"C:/x/y.txt")]
    [InlineData(@"C:x\y.txt")]
    [InlineData(@"1:\x\y.txt")]
    public void Skips(string text) => Assert.False(Match(text, out _));

    [Fact]
    public void TrimsQuoteAndPunctuation()
    {
        var text = "otworz \"C:\\Users\\Jan\\a.txt\", potem.";
        Assert.True(Match(text, out var m));
        Assert.Equal(@"C:\Users\Jan\a.txt", text.Substring(m.Start, m.Length));
        Assert.Equal(@"Users\Jan\", text.Substring(m.DirStart, m.DirLength));
        var t2 = @"zobacz C:\a\b.cs.";
        Assert.True(Match(t2, out m));
        Assert.Equal(@"C:\a\b.cs", t2.Substring(m.Start, m.Length));
    }

    [Fact]
    public void RespectsCursor() => Assert.False(Match(@"C:\a\b.txt", out _, cursor: 1));

    [Fact]
    public void DirectoryCaseInsensitiveSameToken()
    {
        var map = new ReplacementMap(new Random(1));
        var r1 = Run(@"C:\Users\Jan\a.txt", map)!;
        var r2 = Run(@"D:\USERS\JAN\b.txt", map)!;
        Assert.Equal(r1.Split('\\')[1], r2.Split('\\')[1]);
        Assert.StartsWith(@"D:\", r2);
        Assert.Matches(@"^D:\\[a-z]{4}\\b\.txt$", r2);
    }
}
