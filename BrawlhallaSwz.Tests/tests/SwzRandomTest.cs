using Xunit;

namespace BrawlhallaSwz.Tests;

public class SwzRandomTests
{
    [Fact]
    public void ExpectedPrngResults718257Test()
    {
        SwzRandom rand = new(718257);
        Assert.Equal(183398404u, rand.Next());
        Assert.Equal(842980627u, rand.Next());
        Assert.Equal(1183201367u, rand.Next());
        Assert.Equal(2903004275u, rand.Next());
        Assert.Equal(1341849766u, rand.Next());
        Assert.Equal(295299457u, rand.Next());
        Assert.Equal(3204342103u, rand.Next());
        Assert.Equal(589678211u, rand.Next());
        Assert.Equal(1013056089u, rand.Next());
    }
}