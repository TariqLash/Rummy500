using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy500.Core
{
    /// <summary>
    /// Manages the draw pile and discard pile.
    /// The discard pile is ordered: index 0 = bottom, last index = top.
    /// </summary>
    public class Deck
    {
        private List<Card> _drawPile = new List<Card>();
        private List<Card> _discardPile = new List<Card>();
        private Random _rng;

        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;

        // Read-only view of discard pile (bottom to top)
        public IReadOnlyList<Card> DiscardPile => _discardPile.AsReadOnly();

        // The top card of the discard pile
        public Card TopDiscard => _discardPile.Count > 0 ? _discardPile[^1] : null;

        public Deck(int seed = -1)
        {
            _rng = seed >= 0 ? new Random(seed) : new Random();
            BuildDeck();
        }

        private void BuildDeck()
        {
            _drawPile.Clear();
            foreach (Suit suit in Enum.GetValues(typeof(Suit)))
                foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    _drawPile.Add(new Card(suit, rank));
        }

        public void Shuffle()
        {
            // Fisher-Yates shuffle
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>
        /// Draws the top card from the draw pile.
        /// Automatically recycles discard pile if draw pile is empty.
        /// </summary>
        public Card DrawFromPile()
        {
            if (_drawPile.Count == 0)
                RecycleDiscardIntoDraw();

            if (_drawPile.Count == 0)
                throw new InvalidOperationException("No cards left to draw.");

            var card = _drawPile[^1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            return card;
        }

        /// <summary>
        /// Takes a card from the discard pile by index (0 = bottom, Count-1 = top).
        /// Returns that card AND all cards above it (they all go to the player's hand).
        /// The picked card must be melded immediately — enforced by GameManager.
        /// </summary>
        public List<Card> DrawFromDiscard(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _discardPile.Count)
                throw new ArgumentOutOfRangeException(nameof(cardIndex));

            // Take the card at cardIndex and everything above it
            int count = _discardPile.Count - cardIndex;
            var taken = _discardPile.GetRange(cardIndex, count);
            _discardPile.RemoveRange(cardIndex, count);
            return taken;
        }

        /// <summary>Adds a card to the top of the discard pile.</summary>
        public void AddToDiscard(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            _discardPile.Add(card);
        }

        private void RecycleDiscardIntoDraw()
        {
            if (_discardPile.Count == 0) return;

            // Keep the top card, recycle the rest
            var topCard = _discardPile[^1];
            _discardPile.RemoveAt(_discardPile.Count - 1);

            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            _discardPile.Add(topCard);

            // Shuffle the new draw pile
            for (int i = _drawPile.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_drawPile[i], _drawPile[j]) = (_drawPile[j], _drawPile[i]);
            }
        }

        /// <summary>Deal n cards to each player hand list provided.</summary>
        public void Deal(List<List<Card>> playerHands, int cardsPerPlayer)
        {
            for (int i = 0; i < cardsPerPlayer; i++)
                foreach (var hand in playerHands)
                    hand.Add(DrawFromPile());

            // Flip one card to start the discard pile
            AddToDiscard(DrawFromPile());
        }
    }
}
