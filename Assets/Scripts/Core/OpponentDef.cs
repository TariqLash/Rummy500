using System.Collections.Generic;

namespace Rummy500.Core
{
    /// <summary>
    /// Defines a single match in the roguelike gauntlet: a name and which rule modifiers are active.
    /// Modifiers apply to all players equally — the challenge is in the rules, not the opponent.
    /// </summary>
    public class MatchDef
    {
        public string Name { get; }
        public List<MatchModifier> Modifiers { get; }

        public MatchDef(string name, params MatchModifier[] modifiers)
        {
            Name      = name;
            Modifiers = new List<MatchModifier>(modifiers);
        }

        public bool HasModifier(MatchModifier m) => Modifiers.Contains(m);

        /// <summary>The default 5-match gauntlet, escalating in difficulty.</summary>
        public static List<MatchDef> DefaultGauntlet() => new List<MatchDef>
        {
            new MatchDef("The Opening"),
            new MatchDef("Hot Deck",     MatchModifier.HotDeck),
            new MatchDef("The Curse",    MatchModifier.CursedCard),
            new MatchDef("Speed Round",  MatchModifier.SpeedRound),
            new MatchDef("The Gauntlet", MatchModifier.HotDeck, MatchModifier.CursedCard),
        };
    }
}
