using Anonymizer.Core;

namespace Anonymizer.Tests;

public class ReplacementMapTests
{
    private sealed class SeqRandom(params int[] values) : Random
    {
        private int _i;
        public override int Next(int maxValue) => values[_i++ % values.Length];
    }

    [Fact]
    public void SameKey_SameToken_DifferentKeys_DifferentTokens_Length4_AZ()
    {
        var map = new ReplacementMap(new Random(1));
        var a = map.GetOrCreate("x", "/a/");
        Assert.Equal(a, map.GetOrCreate("x", "/a/"));
        Assert.Equal(4, a.Length);
        Assert.All(a, c => Assert.InRange(c, 'a', 'z'));

        var seen = new HashSet<string> { a };
        for (var i = 0; i < 500; i++) Assert.True(seen.Add(map.GetOrCreate("x", "/k" + i + "/")));
    }

    [Fact]
    public void AnonymizerNamesAreSeparateNamespaces_ButTokensStayUnique()
    {
        var map = new ReplacementMap(new Random(2));
        Assert.NotEqual(map.GetOrCreate("a", "k"), map.GetOrCreate("b", "k"));
    }

    [Fact]
    public void Collision_Retries()
    {
        // 0,0,0,0 -> "aaaa"; then "aaaa" again (collision), then 1,1,1,1 -> "bbbb"
        var map = new ReplacementMap(new SeqRandom(0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1));
        Assert.Equal("aaaa", map.GetOrCreate("x", "1"));
        Assert.Equal("bbbb", map.GetOrCreate("x", "2"));
    }

    [Fact]
    public void LengthGrows_AfterEightFailedAttempts()
    {
        // always 0 -> "aaaa" taken; 8 collisions -> length 5 "aaaaa"
        var map = new ReplacementMap(new SeqRandom(0));
        Assert.Equal("aaaa", map.GetOrCreate("x", "1"));
        Assert.Equal("aaaaa", map.GetOrCreate("x", "2"));
    }
}
