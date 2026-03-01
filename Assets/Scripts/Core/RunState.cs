using System;
using System.Collections.Generic;
using System.Linq;

namespace Rummy500.Core
{
    /// <summary>
    /// Tracks the state of a run: cumulative scores toward 500 and each player's relics.
    /// This is a "race to 500" — there is no gauntlet, just continuous rounds.
    /// </summary>
    public class RunState
    {
        public const int ScoreTarget = 500;
        public const int MaxRelics   = 3;

        /// <summary>1-indexed round number.</summary>
        public int RoundNumber { get; private set; }

        // Scores banked from completed rounds, keyed by PlayerId
        private readonly Dictionary<int, int> _cumulativeScores = new Dictionary<int, int>();

        // Relics owned by each player, keyed by PlayerId
        private readonly Dictionary<int, List<RelicId>> _playerRelics = new Dictionary<int, List<RelicId>>();

        // Shared RNG for relic choices (consistent within a run)
        private readonly Random _rng = new Random();

        /// <summary>Run is complete when any player hits the score target.</summary>
        public bool IsRunComplete => _cumulativeScores.Values.Any(s => s >= ScoreTarget);

        // --- Pending relic pick ---

        /// <summary>PlayerId of the player who needs to pick a relic, or -1 if none.</summary>
        public int PendingRelicWinnerId { get; private set; } = -1;

        /// <summary>The 3 relic choices currently being offered.</summary>
        public List<RelicId> PendingRelicChoices { get; private set; } = new List<RelicId>();

        public RunState()
        {
            RoundNumber = 1;
        }

        // --- Score tracking ---

        /// <summary>Returns the cumulative score banked so far for a given player.</summary>
        public int GetScore(int playerId) =>
            _cumulativeScores.TryGetValue(playerId, out int s) ? s : 0;

        /// <summary>
        /// Called when a round ends. Banks each player's score and advances to the next round.
        /// </summary>
        public void OnMatchComplete(IEnumerable<PlayerState> players)
        {
            foreach (var p in players)
            {
                if (!_cumulativeScores.ContainsKey(p.PlayerId))
                    _cumulativeScores[p.PlayerId] = 0;
                _cumulativeScores[p.PlayerId] += p.Score;
            }
            RoundNumber++;
        }

        // --- Relic management ---

        /// <summary>Returns the relics owned by a player (creates empty list on first access).</summary>
        public List<RelicId> GetRelics(int playerId)
        {
            if (!_playerRelics.ContainsKey(playerId))
                _playerRelics[playerId] = new List<RelicId>();
            return _playerRelics[playerId];
        }

        public bool HasRelic(int playerId, RelicId relic) => GetRelics(playerId).Contains(relic);

        public bool CanPickRelic(int playerId) =>
            !IsRunComplete && GetRelics(playerId).Count < MaxRelics;

        /// <summary>
        /// Returns a snapshot of all player relics (used to initialise GameState each round).
        /// </summary>
        public Dictionary<int, List<RelicId>> GetAllRelics() =>
            _playerRelics.ToDictionary(kv => kv.Key, kv => new List<RelicId>(kv.Value));

        /// <summary>
        /// Offer 3 relic choices to the round winner. Call after OnMatchComplete.
        /// Does nothing if the player already has 3 relics or the run is complete.
        /// </summary>
        public void OfferRelicChoice(int winnerId)
        {
            if (!CanPickRelic(winnerId)) return;
            PendingRelicWinnerId = winnerId;
            PendingRelicChoices  = RelicPool.GetChoices(GetRelics(winnerId), _rng);
        }

        /// <summary>Accept a relic pick for the pending winner. Returns false if invalid.</summary>
        public bool AcceptRelic(int playerId, RelicId relic)
        {
            if (PendingRelicWinnerId != playerId) return false;
            if (!PendingRelicChoices.Contains(relic)) return false;
            GetRelics(playerId).Add(relic);
            PendingRelicWinnerId = -1;
            PendingRelicChoices.Clear();
            return true;
        }

        /// <summary>Dismiss any pending relic pick without making a choice.</summary>
        public void ClearPendingRelic()
        {
            PendingRelicWinnerId = -1;
            PendingRelicChoices.Clear();
        }
    }
}
