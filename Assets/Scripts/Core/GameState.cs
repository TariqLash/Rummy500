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

        // --- Match modifiers ---
        public List<MatchModifier> ActiveModifiers { get; private set; } = new List<MatchModifier>();

        // --- Relics ---
        private Dictionary<int, List<RelicId>> _playerRelics = new Dictionary<int, List<RelicId>>();

        private bool PlayerHasRelic(int playerId, RelicId relic) =>
            _playerRelics.TryGetValue(playerId, out var relics) && relics.Contains(relic);
        /// <summary>The starting discard card when CursedCard modifier is active. Null otherwise.</summary>
        public Card CursedCard { get; private set; } = null;
        /// <summary>Total turns taken this round (increments once per player turn).</summary>
        public int TurnCount { get; private set; } = 0;
        public const int SpeedRoundTurnLimit = 10;

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

        /// <summary>Call before StartGame to apply relics accumulated during the run.</summary>
        public void SetRelics(Dictionary<int, List<RelicId>> relics)
        {
            _playerRelics = relics ?? new Dictionary<int, List<RelicId>>();
        }

        public void StartGame(int seed = -1, List<MatchModifier> modifiers = null)
        {
            if (Players.Count < 2 || Players.Count > 4)
                throw new InvalidOperationException("Need 2–4 players.");

            ActiveModifiers = modifiers ?? new List<MatchModifier>();
            Deck = new Deck(seed);
            StartRound();
        }

        private void StartRound()
        {
            // Reset hands and table melds for new round
            TableMelds.Clear();
            foreach (var p in Players)
            {
                p.CaptureScoreForRound();
                p.Hand.Clear();
                p.Melds.Clear();
            }

            Deck = new Deck(); // fresh deck each round
            Deck.Shuffle();

            var hands = Players.Select(p => p.Hand).ToList();
            Deck.Deal(hands, CardsPerPlayer);

            // Loaded relic: deal one extra card to each player who has it
            foreach (var p in Players)
                if (PlayerHasRelic(p.PlayerId, RelicId.Loaded))
                    p.AddCardToHand(Deck.DrawFromPile());

            // CursedCard: designate the starting discard card as cursed for this round.
            // It's visible to all players from the start, adding strategic tension.
            CursedCard = ActiveModifiers.Contains(MatchModifier.CursedCard) ? Deck.TopDiscard : null;
            TurnCount  = 0;

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

            var pickedCard  = Deck.DiscardPile[discardIndex];
            bool isScavenger = PlayerHasRelic(playerId, RelicId.Scavenger);

            if (!isScavenger)
            {
                Debug.Log($"Can meld {pickedCard}? {CanPlayerMeldCard(CurrentPlayer, pickedCard, discardIndex)}");
                if (!CanPlayerMeldCard(CurrentPlayer, pickedCard, discardIndex))
                    return false;
            }

            var cards = Deck.DrawFromDiscard(discardIndex);
            CurrentPlayer.AddCardsToHand(cards);
            RequiredMeldCard = isScavenger ? null : pickedCard;
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

            // Relic: SetSpecialist / RunSpecialist — bonus per card in matching meld type
            int pid = CurrentPlayer.PlayerId;
            if (meld.Type == MeldType.Set && PlayerHasRelic(pid, RelicId.SetSpecialist))
                CurrentPlayer.AddRelicBonus(meld.Cards.Count * 3);
            else if (meld.Type == MeldType.Sequence && PlayerHasRelic(pid, RelicId.RunSpecialist))
                CurrentPlayer.AddRelicBonus(meld.Cards.Count * 3);

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

            // When a player drew from the discard pile, they must lay a NEW meld —
            // extending an existing meld does not satisfy that requirement.
            if (RequiredMeldCard != null)
                return false;

            // Player must have laid at least one meld of their own before extending
            if (CurrentPlayer.Melds.Count == 0)
                return false;

            if (!CurrentPlayer.TryExtendMeld(TableMelds[meldIndex], cards, CurrentPlayer.PlayerId))
                return false;

            // Relic: Extender — bonus each time you extend any meld
            if (PlayerHasRelic(CurrentPlayer.PlayerId, RelicId.Extender))
                CurrentPlayer.AddRelicBonus(5);

            HasMeldedThisTurn = true;
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
            {
                bool wentOut  = winner != null && p == winner;
                int meldedPts = p.Melds.Sum(m => m.Cards.Sum(c => GetCardPoints(c)));
                int handPts   = p.Hand.Sum(c => GetHandPenalty(c, p.PlayerId));

                // CursedCard: holding the cursed card at round end adds a 25-pt penalty
                if (!wentOut && CursedCard != null && p.Hand.Contains(CursedCard))
                    handPts += 25;

                // Relic: SafetyNet — hand penalty halved
                if (!wentOut && PlayerHasRelic(p.PlayerId, RelicId.SafetyNet))
                    handPts = handPts / 2;

                // Relic: Insurance — hand penalty capped at 30
                if (!wentOut && PlayerHasRelic(p.PlayerId, RelicId.Insurance))
                    handPts = System.Math.Min(handPts, 30);

                // Relic: ExitBonus — bonus when you go out
                if (wentOut && PlayerHasRelic(p.PlayerId, RelicId.ExitBonus))
                    p.AddRelicBonus(20);

                // Relic: Tempo — bonus for going out fast (within 4 total player-turns)
                if (wentOut && TurnCount <= 4 && PlayerHasRelic(p.PlayerId, RelicId.Tempo))
                    p.AddRelicBonus(20);

                // Relic: RoyalTreatment — +5 per J/Q/K in your laid melds
                if (PlayerHasRelic(p.PlayerId, RelicId.RoyalTreatment))
                {
                    int royals = p.Melds.Sum(m => m.Cards.Count(c => (int)c.Rank >= 11));
                    p.AddRelicBonus(royals * 5);
                }

                // Relic: AceKicker — +10 per Ace in your laid melds
                if (PlayerHasRelic(p.PlayerId, RelicId.AceKicker))
                {
                    int aces = p.Melds.Sum(m => m.Cards.Count(c => c.Rank == Rank.Ace));
                    p.AddRelicBonus(aces * 10);
                }

                p.ApplyRoundScore(wentOut, meldedPts, handPts);
            }

            // One round = one match. Go straight to GameOver — no multi-round loop.
            OnRoundOver?.Invoke(winner);
            var gameWinner = Players.OrderByDescending(p => p.Score).First();
            SetPhase(GamePhase.GameOver);
            OnGameOver?.Invoke(gameWinner);
        }

        /// <summary>
        /// Returns the point value of a card for scoring laid melds, applying match modifiers.
        /// HotDeck: J/Q/K worth double (20 pts instead of 10).
        /// </summary>
        private int GetCardPoints(Card c)
        {
            int pts = c.PointValue;
            if (ActiveModifiers.Contains(MatchModifier.HotDeck) && (int)c.Rank >= 11)
                pts *= 2;
            return pts;
        }

        /// <summary>
        /// Returns the hand-penalty value of a card for a specific player, applying relics.
        /// DeadwoodCutter: cards ranked 2–5 count as 0.
        /// </summary>
        private int GetHandPenalty(Card c, int playerId)
        {
            if (PlayerHasRelic(playerId, RelicId.DeadwoodCutter)
                && (int)c.Rank >= 2 && (int)c.Rank <= 5)
                return 0;
            int pts = c.PointValue;
            if (ActiveModifiers.Contains(MatchModifier.HotDeck) && (int)c.Rank >= 11)
                pts *= 2;
            return pts;
        }


        private void AdvanceTurn()
        {
            TurnCount++;

            // SpeedRound: force-end the round when the turn limit is hit.
            // Nobody gets the "went out" bonus — everyone scores melds minus hand.
            if (ActiveModifiers.Contains(MatchModifier.SpeedRound) && TurnCount >= SpeedRoundTurnLimit)
            {
                EndRound(null);
                return;
            }

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
            // Build full hand: current hand + the picked card + everything above it in the discard pile.
            // The meld must contain the picked card AND at least one card from the player's actual hand —
            // the player cannot take cards that form a meld entirely from the pile without contributing.
            var cardsFromDiscard = Deck.DiscardPile.Skip(discardIndex).ToList();
            var fullHand = player.Hand.Concat(cardsFromDiscard).ToList();

            // A valid meld requires at least 3 cards, so the player must have at least 4 total
            // after drawing — 3 to meld the required card, and 1 left over to discard.
            if (fullHand.Count < 4)
                return false;

            // Check for a valid set (3+ same rank, different suits, ≥1 from player's hand)
            var sameRank = fullHand.Where(c => c.Rank == card.Rank).ToList();
            if (sameRank.Count >= 3
                && sameRank.Select(c => c.Suit).Distinct().Count() >= 3
                && sameRank.Any(c => player.Hand.Contains(c)))
                return true;

            // Check normal sequence (Ace = 1), ≥1 card from player's hand.
            // Try all window sizes (3..N) so that e.g. [7♥ 8♥ 9♥ 10♥] is found
            // even when the 3-card sub-window [7♥ 8♥ 9♥] has no hand card.
            var sameSuit = fullHand.Where(c => c.Suit == card.Suit)
                                   .OrderBy(c => (int)c.Rank)
                                   .ToList();
            for (int len = 3; len <= sameSuit.Count; len++)
            {
                for (int i = 0; i <= sameSuit.Count - len; i++)
                {
                    var subset = sameSuit.Skip(i).Take(len).ToList();
                    if (Meld.IsValidSequence(subset)
                        && subset.Contains(card)
                        && subset.Any(c => player.Hand.Contains(c)))
                        return true;
                }
            }

            // Check wraparound sequence (Ace = 14, e.g. Q-K-A), ≥1 from player's hand
            var sameSuitWrapped = fullHand.Where(c => c.Suit == card.Suit)
                                          .OrderBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank)
                                          .ToList();
            for (int len = 3; len <= sameSuitWrapped.Count; len++)
            {
                for (int i = 0; i <= sameSuitWrapped.Count - len; i++)
                {
                    var subset = sameSuitWrapped.Skip(i).Take(len).ToList();
                    if (Meld.IsValidSequence(subset)
                        && subset.Contains(card)
                        && subset.Any(c => player.Hand.Contains(c)))
                        return true;
                }
            }

            return false;
        }
    }
}
