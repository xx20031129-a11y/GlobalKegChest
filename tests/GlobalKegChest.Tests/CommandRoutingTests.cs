using GlobalKegChest.Core;

namespace GlobalKegChest.Tests;

public sealed class CommandRoutingTests
{
    [Theory]
    [InlineData(true, 0, false, CommandRoute.RunLocally)]  // host, single player
    [InlineData(true, 0, true, CommandRoute.RunLocally)]   // host in a multiplayer session
    [InlineData(false, 0, true, CommandRoute.AskHost)]     // farmhand
    [InlineData(false, 1, true, CommandRoute.AskHost)]     // farmhand on a second screen
    [InlineData(true, 1, false, CommandRoute.Reject)]      // split-screen guest with no session
    [InlineData(false, 0, false, CommandRoute.Reject)]     // no session at all
    public void RescanRoutesToTheOnlyAuthority(bool isMainPlayer, int screenId, bool isMultiplayer, CommandRoute expected)
    {
        Assert.Equal(expected, CommandRouting.Rescan(isMainPlayer, screenId, isMultiplayer));
    }

    [Fact]
    public void RunLocallyOnlyHappensForTheHostFirstScreen()
    {
        for (int screen = 0; screen <= 1; screen++)
        for (int host = 0; host <= 1; host++)
        for (int multiplayer = 0; multiplayer <= 1; multiplayer++)
        {
            bool expected = host == 1 && screen == 0;
            Assert.Equal(expected, CommandRouting.Rescan(host == 1, screen, multiplayer == 1) == CommandRoute.RunLocally);
        }
    }
}
