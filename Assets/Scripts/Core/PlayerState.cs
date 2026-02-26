using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy500.Core
{
    [Serializable]
    public class PlayerState
    {
        public int PlayerId { get; private set; }
        public string DisplayName { get; set; }
        public List<Card> Hand { get; private set; } = new List<Card>();
        public List<Meld> Melds { get; private set; } = new List<Meld>();
        public int Score { get; private set; } = 0;

        // Points currently on the table (laid melds this game)
        public int MeldedPoints => Melds.Sum(m => m.PointValue);

        // Points still in hand (penalty if someone else goes out)
        public int HandPoints => Hand.Sum(c => c.PointValue);

        public PlayerState(int playerId, string displayName)
        {
            PlayerId = playerId;
            DisplayName = displayName;
        }

        public void AddCardToHand(Card card) => Hand.Insert(0, card);
        public void AddCardsToHand(IEnumerable<Card> cards) => Hand.InsertRange(0, cards.ToList());

        public bool RemoveCardFromHand(Card card) => Hand.Remove(card);

        public bool RemoveCardsFromHand(IEnumerable<Card> cards)
        {
            var toRemove = cards.ToList();
            if (!toRemove.All(c => Hand.Contains(c))) return false;
            foreach (var c in toRemove) Hand.Remove(c);
            return true;
        }

        /// <summary>
        /// Lay a new meld from cards in hand. Validates and moves cards from hand to melds.
        /// </summary>
        public bool TryLayMeld(List<Card> cards)
        {
            if (!Meld.IsValidMeld(cards)) return false;

            // Snapshot before removing, so the meld gets the full card list
            var snapshot = new List<Card>(cards);

            if (!RemoveCardsFromHand(snapshot)) return false;

            Meld meld = Meld.IsValidSet(snapshot)
                ? Meld.CreateSet(PlayerId, snapshot)
                : Meld.CreateSequence(PlayerId, snapshot);

            Melds.Add(meld);
            return true;
        }

        /// <summary>
        /// Add cards from hand onto an existing meld (any player's meld on the table).
        /// </summary>
        public bool TryExtendMeld(Meld targetMeld, List<Card> cards)
        {
            if (!RemoveCardsFromHand(cards)) return false;
            if (!targetMeld.TryExtend(cards))
            {
                // Rollback
                AddCardsToHand(cards);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called at the end of a round. Adds melded points, subtracts hand points.
        /// </summary>
        public void ApplyRoundScore(bool wentOut)
        {
            int gained = MeldedPoints;
            int penalty = wentOut ? 0 : HandPoints;
            Score += gained - penalty;
        }

        public bool HasCard(Card card) => Hand.Contains(card);

        public override string ToString() =>
            $"{DisplayName} | Score: {Score} | Hand: {Hand.Count} cards | Melds: {MeldedPoints}pts";
    }
}
