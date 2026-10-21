namespace GlobalKegChest.Core;

/// <summary>Which client is allowed to act on the shared automation network.</summary>
public enum CommandRoute
{
    /// <summary>This client owns the network and can rescan immediately.</summary>
    RunLocally,

    /// <summary>Another client owns the network; ask it over a multiplayer message.</summary>
    AskHost,

    /// <summary>No authority reachable; report that the host must run it.</summary>
    Reject
}

public static class CommandRouting
{
    /// <summary>
    /// Automation state lives on the main player's first screen, so only that client may
    /// rescan directly. Farmhands in a multiplayer session can still ask the host.
    /// </summary>
    public static CommandRoute Rescan(bool isMainPlayer, int screenId, bool isMultiplayer)
    {
        if (isMainPlayer && screenId == 0)
            return CommandRoute.RunLocally;
        return isMultiplayer ? CommandRoute.AskHost : CommandRoute.Reject;
    }
}
