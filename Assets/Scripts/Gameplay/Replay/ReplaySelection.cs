namespace EachOneCoin.Gameplay.Replay
{
    // Holds a validated selection only across the menu-to-arena scene transition.
    public static class ReplaySelection
    {
        public static ReplayData Pending { get; set; }
        public static bool ReturnToList { get; set; }
    }
}
