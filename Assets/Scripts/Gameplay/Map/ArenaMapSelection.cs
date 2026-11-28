namespace EachOneCoin.Gameplay
{
    // Single-player selection persists through a scene reload; network and replay ignore it.
    public static class ArenaMapSelection
    {
        public static ArenaMapDefinition Selected { get; set; }
    }
}
