using System;
using System.Collections.Generic;
using System.Linq;
using Rummy500.Core;

/// <summary>
/// Static AI decision helper for a medium-difficulty opponent.
/// No MonoBehaviour — pure logic only.
/// </summary>
public static class AIPlayer
{
    /// <summary>
    /// Decides whether to draw the top card of the discard pile or draw from the deck.
    /// Returns (useDiscardPile, discardIndex). discardIndex is -1 when drawing from deck.
    /// </summary>
    public static (bool useDiscardPile, int discardIndex) GetDrawDecision(GameState game, PlayerState player)
    {
        if (game.Deck.DiscardPile.Count == 0)
            return (false, -1);

        var topCard = game.Deck.TopDiscard;
        int topIdx = game.Deck.DiscardPile.Count - 1;
        var hand = player.Hand;

        // Check if top discard card would form a set with hand cards (need 2+ same rank, distinct suits)
        var sameRank = hand.Where(c => c.Rank == topCard.Rank).ToList();
        if (sameRank.Count >= 2)
        {
            var allSuits = sameRank.Select(c => c.Suit).Concat(new[] { topCard.Suit }).Distinct();
            if (allSuits.Count() >= 3)
                return (true, topIdx);
        }

        // Check if top discard card extends any existing table meld
        foreach (var meld in game.TableMelds)
        {
            var test = meld.Cards.Concat(new[] { topCard }).ToList();
            if (meld.Type == MeldType.Set && Meld.IsValidSet(test))
                return (true, topIdx);
            if (meld.Type == MeldType.Sequence && Meld.IsValidSequence(test))
                return (true, topIdx);
        }

        // Check if top discard card would form a sequence with hand cards
        var sameSuit = hand.Where(c => c.Suit == topCard.Suit).ToList();
        var combined = sameSuit.Concat(new[] { topCard }).OrderBy(c => (int)c.Rank).ToList();
        for (int i = 0; i <= combined.Count - 3; i++)
        {
            var subset = combined.Skip(i).Take(3).ToList();
            if (Meld.IsValidSequence(subset) && subset.Contains(topCard))
                return (true, topIdx);
        }

        return (false, -1);
    }

    /// <summary>
    /// Finds all melds the player can lay from their hand.
    /// Prefers sets first, then finds sequences from remaining cards.
    /// Cards are deduplicated across returned melds.
    /// </summary>
    public static List<List<Card>> FindMeldsToLay(PlayerState player)
    {
        var result = new List<List<Card>>();
        var used = new HashSet<Card>();

        // First: find sets (3+ same rank, distinct suits)
        var byRank = player.Hand.GroupBy(c => c.Rank);
        foreach (var group in byRank)
        {
            var distinctSuitCards = new List<Card>();
            var seenSuits = new HashSet<Suit>();
            foreach (var c in group)
            {
                if (seenSuits.Add(c.Suit))
                    distinctSuitCards.Add(c);
            }
            if (distinctSuitCards.Count >= 3)
            {
                result.Add(distinctSuitCards);
                foreach (var c in distinctSuitCards) used.Add(c);
            }
        }

        // Second: find sequences from remaining cards
        var remaining = player.Hand.Where(c => !used.Contains(c)).ToList();
        var bySuit = remaining.GroupBy(c => c.Suit);
        foreach (var group in bySuit)
        {
            var sorted = group.OrderBy(c => (int)c.Rank).ToList();
            int start = 0;
            while (start < sorted.Count)
            {
                var run = new List<Card> { sorted[start] };
                int i = start + 1;
                while (i < sorted.Count && (int)sorted[i].Rank == (int)sorted[i - 1].Rank + 1)
                {
                    run.Add(sorted[i]);
                    i++;
                }
                if (run.Count >= 3)
                {
                    result.Add(run);
                    foreach (var c in run) used.Add(c);
                    start = i;
                }
                else
                {
                    start++;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Finds cards in the player's hand that can extend existing table melds.
    /// Returns a list of (meldIndex, cards) pairs. Cards are deduplicated across entries.
    /// </summary>
    public static List<(int meldIndex, List<Card> cards)> FindExtensions(GameState game, PlayerState player)
    {
        var extensions = new List<(int, List<Card>)>();
        var usedCards = new HashSet<Card>();

        for (int i = 0; i < game.TableMelds.Count; i++)
        {
            var meld = game.TableMelds[i];
            var ext = new List<Card>();

            foreach (var card in player.Hand)
            {
                if (usedCards.Contains(card)) continue;
                var test = meld.Cards.Concat(new[] { card }).ToList();
                bool valid = meld.Type == MeldType.Set
                    ? Meld.IsValidSet(test)
                    : Meld.IsValidSequence(test);
                if (valid)
                    ext.Add(card);
            }

            if (ext.Count > 0)
            {
                extensions.Add((i, ext));
                foreach (var c in ext) usedCards.Add(c);
            }
        }

        return extensions;
    }

    /// <summary>
    /// Chooses which card from the hand to discard.
    /// Picks the card with the lowest meld-potential score.
    /// Ties are broken by highest point value (discard high-value deadwood).
    /// </summary>
    public static int ChooseDiscard(PlayerState player, GameState game)
    {
        if (player.Hand.Count == 0) return -1;

        int bestIdx = 0;
        int lowestScore = int.MaxValue;
        int highestPoints = -1;

        for (int i = 0; i < player.Hand.Count; i++)
        {
            var card = player.Hand[i];
            int rankVal = (int)card.Rank;
            int score = 0;

            // Count hand cards sharing the same rank (set potential)
            score += player.Hand.Count(c => c != card && c.Rank == card.Rank);

            // Count hand cards adjacent in rank with same suit (sequence potential)
            score += player.Hand.Count(c => c != card && c.Suit == card.Suit &&
                Math.Abs((int)c.Rank - rankVal) == 1);

            if (score < lowestScore || (score == lowestScore && card.PointValue > highestPoints))
            {
                lowestScore = score;
                highestPoints = card.PointValue;
                bestIdx = i;
            }
        }

        return bestIdx;
    }
}
