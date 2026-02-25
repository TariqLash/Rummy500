using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rummy500.Core;

namespace Rummy500.Core
{
    public enum GamePhase
    {
        WaitingForPlayers,
        Dealing,
        PlayerTurn_Draw,        // Player must draw (from pile or discard)
        PlayerTurn_MeldDiscard, // Player may meld/extend, then must discard
        RoundOver,
        GameOver
    }

    public enum DrawSource { DrawPile, DiscardPile }

    /// <summary>
    /// The single authoritative source of truth for the game.
    /// On the host, this is manipulated directly.
    /// Clients receive a sanitized snapshot (no other players' hands).
    /// </summary>
    public class GameState
    {
        public List<PlayerState> Players { get; private set; } = new List<PlayerState>();
        public Deck Deck { get; private set; }
        public List<Meld> TableMelds { get; private set; } = new List<Meld>(); // All melds from all players
        public GamePhase Phase { get; private set; } = GamePhase.WaitingForPlayers;
        public int CurrentPlayerIndex { get; private set; } = 0;
        public int ScoreTarget { get; private set; } = 500;
        public int CardsPerPlayer { get; private set; } = 7;

        // The card a player drew from the discard pile (must be melded this turn)
        public Card RequiredMeldCard { get; private set; } = null;
        public bool HasMeldedThisTurn { get; private set; } = false;

        public PlayerState CurrentPlayer => Players[CurrentPlayerIndex];
        public bool IsGameOver => Players.Any(p => p.Score >= ScoreTarget);

        // Events
        public event Action<PlayerState> OnTurnChanged;
        public event Action<PlayerState, Meld> OnMeldLaid;
        public event Action<PlayerState> OnRoundOver;
        public event Action<PlayerState> OnGameOver;

        public GameState(int scoreTarget = 500)
        {
            ScoreTarget = scoreTarget;
        }

        // --- Setup ---

        public void AddPlayer(int id, string name)
        {
            if (Phase != GamePhase.WaitingForPlayers)
                throw new InvalidOperationException("Cannot add players after game has started.");
            Players.Add(new PlayerState(id, name));
        }

        public void StartGame(int seed = -1)
        {
            if (Players.Count < 2 || Players.Count > 4)
                throw new InvalidOperationException("Need 2–4 players.");

            Deck = new Deck(seed);
            StartRound();
        }

        private void StartRound()
        {
            // Reset hands and table melds for new round
            TableMelds.Clear();
            foreach (var p in Players)
            {
                p.Hand.Clear();
                p.Melds.Clear();
            }

            Deck = new Deck(); // fresh deck each round
            Deck.Shuffle();

            var hands = Players.Select(p => p.Hand).ToList();
            Deck.Deal(hands, CardsPerPlayer);

            CurrentPlayerIndex = 0;
            SetPhase(GamePhase.PlayerTurn_Draw);
        }

        // --- Turn Actions ---

        /// <summary>Player draws from the draw pile.</summary>
        public bool TryDrawFromPile(int playerId)
        {
            if (!IsCurrentPlayer(playerId) || Phase != GamePhase.PlayerTurn_Draw)
                return false;

            var card = Deck.DrawFromPile();
            CurrentPlayer.AddCardToHand(card);
            RequiredMeldCard = null;
            HasMeldedThisTurn = false;
            SetPhase(GamePhase.PlayerTurn_MeldDiscard);
            return true;
        }

        /// <summary>
        /// Player draws from discard pile at a given index.
        /// The card at that index must be immediately melded.
        /// </summary>
        public bool TryDrawFromDiscard(int playerId, int discardIndex)
        {
            if (!IsCurrentPlayer(playerId) || Phase != GamePhase.PlayerTurn_Draw)
                return false;
            if (discardIndex < 0 || discardIndex >= Deck.DiscardPileCount)
                return false;

            var pickedCard = Deck.DiscardPile[discardIndex];
            Debug.Log($"Can meld {pickedCard}? {CanPlayerMeldCard(CurrentPlayer, pickedCard, discardIndex)}");
            if (!CanPlayerMeldCard(CurrentPlayer, pickedCard, discardIndex))
                return false;

            var cards = Deck.DrawFromDiscard(discardIndex);
            CurrentPlayer.AddCardsToHand(cards);
            RequiredMeldCard = pickedCard;
            HasMeldedThisTurn = false;
            SetPhase(GamePhase.PlayerTurn_MeldDiscard);
            return true;
        }

        /// <summary>Player lays a new meld from cards in their hand.</summary>
        public bool TryLayMeld(int playerId, List<Card> cards)
        {
            if (!IsCurrentPlayer(playerId) || Phase != GamePhase.PlayerTurn_MeldDiscard)
                return false;

            // If player drew from discard, RequiredMeldCard must be in this meld
            if (RequiredMeldCard != null && !cards.Contains(RequiredMeldCard))
                return false;

            if (!CurrentPlayer.TryLayMeld(cards))
                return false;

            var meld = CurrentPlayer.Melds[^1];
            TableMelds.Add(meld);
            HasMeldedThisTurn = true;
            if (RequiredMeldCard != null && cards.Contains(RequiredMeldCard))
                RequiredMeldCard = null;

            OnMeldLaid?.Invoke(CurrentPlayer, meld);
            CheckRoundOver();
            return true;
        }

        /// <summary>Player adds cards from hand onto any existing meld on the table.</summary>
        public bool TryExtendMeld(int playerId, int meldIndex, List<Card> cards)
        {
            if (!IsCurrentPlayer(playerId) || Phase != GamePhase.PlayerTurn_MeldDiscard)
                return false;
            if (meldIndex < 0 || meldIndex >= TableMelds.Count)
                return false;

            if (RequiredMeldCard != null && !cards.Contains(RequiredMeldCard))
                return false;

            if (!CurrentPlayer.TryExtendMeld(TableMelds[meldIndex], cards))
                return false;

            HasMeldedThisTurn = true;
            if (RequiredMeldCard != null && cards.Contains(RequiredMeldCard))
                RequiredMeldCard = null;

            CheckRoundOver();
            return true;
        }

        /// <summary>Player discards a card to end their turn.</summary>
        public bool TryDiscard(int playerId, Card card)
        {
            if (!IsCurrentPlayer(playerId) || Phase != GamePhase.PlayerTurn_MeldDiscard)
                return false;

            // Must have melded the required card before discarding
            if (RequiredMeldCard != null)
                return false;

            if (!CurrentPlayer.RemoveCardFromHand(card))
                return false;

            Deck.AddToDiscard(card);

            if (CheckRoundOver()) return true;

            AdvanceTurn();
            return true;
        }

        // --- Internal Helpers ---

        private bool CheckRoundOver()
        {
            if (CurrentPlayer.Hand.Count == 0)
            {
                EndRound(CurrentPlayer);
                return true;
            }
            return false;
        }

        private void EndRound(PlayerState winner)
        {
            foreach (var p in Players)
                p.ApplyRoundScore(p == winner);

            SetPhase(GamePhase.RoundOver);
            OnRoundOver?.Invoke(winner);

            if (Players.Any(p => p.Score >= ScoreTarget))
            {
                var gameWinner = Players.OrderByDescending(p => p.Score).First();
                SetPhase(GamePhase.GameOver);
                OnGameOver?.Invoke(gameWinner);
            }
        }

        public void StartNextRound()
        {
            if (Phase != GamePhase.RoundOver)
                throw new InvalidOperationException("Not in RoundOver phase.");
            StartRound();
        }

        private void AdvanceTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
            RequiredMeldCard = null;
            HasMeldedThisTurn = false;
            SetPhase(GamePhase.PlayerTurn_Draw);
            OnTurnChanged?.Invoke(CurrentPlayer);
        }

        private bool IsCurrentPlayer(int playerId) =>
            Players[CurrentPlayerIndex].PlayerId == playerId;

        private void SetPhase(GamePhase phase) => Phase = phase;

        /// <summary>
        /// Checks if the player can meld the given card with cards already in hand or on table.
        /// Used to validate discard pile draws.
        /// </summary>
        private bool CanPlayerMeldCard(PlayerState player, Card card, int discardIndex)
{
    // Build full hand: current hand + card + everything above it in discard pile
    var cardsFromDiscard = Deck.DiscardPile.Skip(discardIndex).ToList();
    var fullHand = player.Hand.Concat(cardsFromDiscard).ToList();

    // Check for valid set (3+ same rank, different suits)
    var sameRank = fullHand.Where(c => c.Rank == card.Rank).ToList();
    if (sameRank.Count >= 3)
    {
        var suits = sameRank.Select(c => c.Suit).Distinct().ToList();
        if (suits.Count >= 3) return true;
    }

    // Check if it extends any existing table meld
    foreach (var meld in TableMelds)
    {
        var test = meld.Cards.Append(card).ToList();
        if (meld.Type == MeldType.Set && Meld.IsValidSet(test)) return true;
        if (meld.Type == MeldType.Sequence && Meld.IsValidSequence(test)) return true;
    }

    // Check normal sequence (Ace = 1, e.g. A-2-3 or 5-6-7)
    var sameSuit = fullHand.Where(c => c.Suit == card.Suit)
                           .OrderBy(c => (int)c.Rank)
                           .ToList();
    for (int i = 0; i <= sameSuit.Count - 3; i++)
    {
        var subset = sameSuit.Skip(i).Take(3).ToList();
        if (Meld.IsValidSequence(subset) && subset.Contains(card))
            return true;
    }

    // Check wraparound sequence (Ace = 14, e.g. Q-K-A)
    var sameSuitWrapped = fullHand.Where(c => c.Suit == card.Suit)
                                  .OrderBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank)
                                  .ToList();
    for (int i = 0; i <= sameSuitWrapped.Count - 3; i++)
    {
        var subset = sameSuitWrapped.Skip(i).Take(3).ToList();
        if (Meld.IsValidSequence(subset) && subset.Contains(card))
            return true;
    }

    return false;
}
    }
}
