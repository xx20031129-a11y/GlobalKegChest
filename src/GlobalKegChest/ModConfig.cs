namespace GlobalKegChest;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public int ProcessIntervalMinutes { get; set; } = 10;
    public bool VerboseLogging { get; set; }

    public bool Normalize()
    {
        if (ProcessIntervalMinutes is >= 10 and <= 120 && ProcessIntervalMinutes % 10 == 0)
            return false;
        ProcessIntervalMinutes = 10;
        return true;
    }
}
