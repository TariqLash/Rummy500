using System;

namespace Rummy500.Core
{
    public enum Suit { Spades, Hearts, Diamonds, Clubs }

    public enum Rank
    {
        Ace = 1,
        Two = 2, Three = 3, Four = 4, Five = 5,
        Six = 6, Seven = 7, Eight = 8, Nine = 9,
        Ten = 10, Jack = 11, Queen = 12, King = 13
    }

    [Serializable]
    public class Card
    {
        public Suit Suit { get; private set; }
        public Rank Rank { get; private set; }

        // Unique ID for networking (0–51)
        public int Id { get; private set; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
            Id = (int)suit * 13 + (int)rank - 1;
        }

        /// <summary>
        /// Point value of the card for scoring.
        /// Ace = 15, Ten through King = 10, Two through Nine = 5.
        /// </summary>
        public int PointValue
        {
            get
            {
                if (Rank == Rank.Ace)      return 15;
                if ((int)Rank >= 10)       return 10;  // Ten, Jack, Queen, King
                return 5;                              // Two through Nine
            }
        }

        public override string ToString() => $"{Rank} of {Suit}";

        public override bool Equals(object obj) =>
            obj is Card other && other.Id == Id;

        public override int GetHashCode() => Id;
    }
}
