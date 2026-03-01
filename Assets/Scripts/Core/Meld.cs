using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy500.Core
{
    public enum MeldType { Set, Sequence }

    /// <summary>
    /// A meld is a group of cards laid on the table by a player.
    /// Sets: 3+ cards of the same rank, different suits.
    /// Sequences: 3+ cards of the same suit in consecutive rank order.
    /// Once laid, other players can extend melds on their turn.
    /// </summary>
    [Serializable]
    public class Meld
    {
        public MeldType Type { get; private set; }
        public int OwnerId { get; private set; }       // Player index who created it
        public List<Card> Cards { get; private set; }
        public List<int> CardOwners { get; private set; } // Per-card contributor (parallel to Cards)

        public int PointValue => Cards.Sum(c => c.PointValue);

        private Meld() { }

        public static Meld CreateSet(int ownerId, List<Card> cards)
        {
            if (!IsValidSet(cards))
                throw new ArgumentException("Invalid set.");
            return new Meld
            {
                Type = MeldType.Set, OwnerId = ownerId,
                Cards = new List<Card>(cards),
                CardOwners = Enumerable.Repeat(ownerId, cards.Count).ToList()
            };
        }

        public static Meld CreateSequence(int ownerId, List<Card> cards)
        {
            if (!IsValidSequence(cards))
                throw new ArgumentException("Invalid sequence.");
            var sorted = SortByRank(cards);
            return new Meld
            {
                Type = MeldType.Sequence, OwnerId = ownerId,
                Cards = sorted,
                CardOwners = Enumerable.Repeat(ownerId, sorted.Count).ToList()
            };
        }

        /// <summary>
        /// Non-destructive check: would these cards validly extend this meld?
        /// </summary>
        public bool CanExtend(List<Card> newCards)
        {
            if (newCards == null || newCards.Count == 0) return false;
            var combined = Cards.Concat(newCards).ToList();
            if (Type == MeldType.Set)      return IsValidSet(combined);
            if (Type == MeldType.Sequence) return IsValidSequence(combined);
            return false;
        }

        /// <summary>
        /// Try to extend this meld with additional cards.
        /// Returns true if all cards were validly added.
        /// </summary>
        public bool TryExtend(List<Card> newCards, int extenderId)
        {
            var combined = Cards.Concat(newCards).ToList();

            if (Type == MeldType.Set && IsValidSet(combined))
            {
                Cards = combined;
                CardOwners = CardOwners.Concat(Enumerable.Repeat(extenderId, newCards.Count)).ToList();
                return true;
            }
            if (Type == MeldType.Sequence && IsValidSequence(combined))
            {
                var allOwners = CardOwners.Concat(Enumerable.Repeat(extenderId, newCards.Count)).ToList();
                var pairs = combined.Zip(allOwners, (c, o) => (c, o))
                                    .OrderBy(p => (int)p.c.Rank).ToList();
                Cards      = pairs.Select(p => p.c).ToList();
                CardOwners = pairs.Select(p => p.o).ToList();
                return true;
            }
            return false;
        }

        // --- Validation ---

        public static bool IsValidSet(List<Card> cards)
        {
            if (cards == null || cards.Count < 3) return false;
            var rank = cards[0].Rank;
            var suits = new HashSet<Suit>();
            foreach (var c in cards)
            {
                if (c.Rank != rank) return false;
                if (!suits.Add(c.Suit)) return false; // duplicate suit
            }
            return true;
        }

        public static bool IsValidSequence(List<Card> cards)
        {
            if (cards == null || cards.Count < 3) return false;
            var suit = cards[0].Suit;
            if (cards.Any(c => c.Suit != suit)) return false;

            var sorted = cards.OrderBy(c => (int)c.Rank).ToList();

            // Check normal sequence (e.g. A-2-3 or 5-6-7 or Q-K)
            bool normalOk = true;
            for (int i = 1; i < sorted.Count; i++)
            {
                if ((int)sorted[i].Rank != (int)sorted[i - 1].Rank + 1)
                {
                    normalOk = false;
                    break;
                }
            }
            if (normalOk) return true;

            // Check wraparound sequence (e.g. Q-K-A)
            // Treat Ace as rank 14 for this check
            var wrappedRanks = cards.Select(c =>
                c.Rank == Rank.Ace ? 14 : (int)c.Rank).OrderBy(r => r).ToList();

            for (int i = 1; i < wrappedRanks.Count; i++)
            {
                if (wrappedRanks[i] != wrappedRanks[i - 1] + 1)
                    return false;
            }
            return true;
        }

        public static bool IsValidMeld(List<Card> cards) =>
            IsValidSet(cards) || IsValidSequence(cards);

        private static List<Card> SortByRank(List<Card> cards) =>
            cards.OrderBy(c => (int)c.Rank).ToList();

        public override string ToString() =>
            $"{Type} [{string.Join(", ", Cards)}] ({PointValue}pts)";
    }
}
