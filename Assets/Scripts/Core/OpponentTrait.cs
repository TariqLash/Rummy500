namespace Rummy500.Core
{
    /// <summary>
    /// Rule modifiers that apply to a specific match in the roguelike gauntlet.
    /// These change the game for ALL players — the challenge comes from the rules, not from AI behavior.
    /// </summary>
    public enum MatchModifier
    {
        HotDeck,    // Face cards (J/Q/K) worth double points this match
        CursedCard, // The starting discard card is cursed — holding it at round end costs –25 pts
        SpeedRound, // Round ends after 10 turns if no one has gone out
    }
}
