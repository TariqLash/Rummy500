using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy500.Core
{
    public enum RelicId
    {
        Loaded,          // +1 starting card each round
        ExitBonus,       // +20 pts when you go out
        Tempo,           // +20 pts if you go out within first 4 turns
        SetSpecialist,   // +3 pts per card in every set meld you lay
        RunSpecialist,   // +3 pts per card in every sequence meld you lay
        Extender,        // +5 pts each time you extend any meld
        RoyalTreatment,  // +5 pts per J/Q/K in your melds at round end
        AceKicker,       // +10 pts per Ace in your melds at round end
        SafetyNet,       // Hand penalty halved at round end
        Insurance,       // Hand penalty capped at 30 pts
        DeadwoodCutter,  // Cards ranked 2–5 in hand count as 0 penalty
        Scavenger,       // Drawing from discard pile skips the meld obligation
    }

    public class RelicDef
    {
        public RelicId Id          { get; }
        public string  Name        { get; }
        public string  Description { get; }

        public RelicDef(RelicId id, string name, string description)
        {
            Id          = id;
            Name        = name;
            Description = description;
        }
    }

    public static class RelicPool
    {
        public static readonly IReadOnlyDictionary<RelicId, RelicDef> All =
            new Dictionary<RelicId, RelicDef>
            {
                [RelicId.Loaded]         = new RelicDef(RelicId.Loaded,         "Loaded",          "Start each round with 1 extra card."),
                [RelicId.ExitBonus]      = new RelicDef(RelicId.ExitBonus,      "Exit Bonus",      "+20 pts when you go out."),
                [RelicId.Tempo]          = new RelicDef(RelicId.Tempo,          "Tempo",           "+20 pts if you go out within the first 4 turns."),
                [RelicId.SetSpecialist]  = new RelicDef(RelicId.SetSpecialist,  "Set Specialist",  "+3 pts per card in every set meld you lay."),
                [RelicId.RunSpecialist]  = new RelicDef(RelicId.RunSpecialist,  "Run Specialist",  "+3 pts per card in every sequence meld you lay."),
                [RelicId.Extender]       = new RelicDef(RelicId.Extender,       "Extender",        "+5 pts each time you extend any meld on the table."),
                [RelicId.RoyalTreatment] = new RelicDef(RelicId.RoyalTreatment, "Royal Treatment", "+5 pts per J, Q, or K in your melds at round end."),
                [RelicId.AceKicker]      = new RelicDef(RelicId.AceKicker,      "Ace Kicker",      "+10 pts per Ace in your melds at round end."),
                [RelicId.SafetyNet]      = new RelicDef(RelicId.SafetyNet,      "Safety Net",      "Your hand penalty is halved when someone else goes out."),
                [RelicId.Insurance]      = new RelicDef(RelicId.Insurance,      "Insurance",       "Your hand penalty is capped at 30 pts."),
                [RelicId.DeadwoodCutter] = new RelicDef(RelicId.DeadwoodCutter, "Deadwood Cutter", "Cards ranked 2–5 in your hand count as 0 penalty."),
                [RelicId.Scavenger]      = new RelicDef(RelicId.Scavenger,      "Scavenger",       "Pick up discard cards freely — no meld obligation."),
            };

        /// <summary>
        /// Returns up to 3 random relic choices the player doesn't already own.
        /// </summary>
        public static List<RelicId> GetChoices(List<RelicId> existing, Random rng = null)
        {
            rng ??= new Random();
            var available = All.Keys.Where(r => !existing.Contains(r)).ToList();

            // Fisher-Yates partial shuffle
            for (int i = available.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (available[i], available[j]) = (available[j], available[i]);
            }

            return available.Take(3).ToList();
        }
    }
}
