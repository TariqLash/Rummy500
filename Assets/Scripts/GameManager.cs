using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rummy500.Core;

/// <summary>
/// MonoBehaviour wrapper around GameState.
/// Attach this to a GameObject in your scene.
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState Game { get; private set; }

    // How many human players (rest will be skipped for now)
    [SerializeField] private int playerCount = 2;

    private Coroutine _aiCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        StartNewGame();
    }

    public void StartNewGame()
    {
        if (_aiCoroutine != null)
        {
            StopCoroutine(_aiCoroutine);
            _aiCoroutine = null;
        }

        Game = new GameState(scoreTarget: 500);

        for (int i = 0; i < playerCount; i++)
            Game.AddPlayer(i, i == 0 ? "You" : $"Player {i + 1}");

        Game.OnTurnChanged += OnTurnChanged;
        Game.OnMeldLaid += (p, m) => Debug.Log($"{p.DisplayName} laid: {m}");
        Game.OnRoundOver += p => Debug.Log($"Round over! {p.DisplayName} went out. Scores: {GetScoreSummary()}");
        Game.OnGameOver += p => Debug.Log($"GAME OVER! {p.DisplayName} wins with {p.Score} points!");

        Game.StartGame();
        Debug.Log("Game started! It's your turn.");
        LogGameState();
    }

    private void OnTurnChanged(PlayerState player)
    {
        Debug.Log($"--- {player.DisplayName}'s turn ---");
        if (player.PlayerId != 0)
        {
            if (_aiCoroutine != null) StopCoroutine(_aiCoroutine);
            _aiCoroutine = StartCoroutine(ExecuteAITurn(player.PlayerId));
        }
    }

    private IEnumerator ExecuteAITurn(int playerId)
    {
        yield return new WaitForSeconds(0.6f);

        var player = Game.Players.Find(p => p.PlayerId == playerId);
        if (player == null || Game.Phase != GamePhase.PlayerTurn_Draw)
        {
            _aiCoroutine = null;
            yield break;
        }

        // Step 1: Draw
        var (useDiscard, discardIdx) = AIPlayer.GetDrawDecision(Game, player);
        bool drew = false;
        if (useDiscard && discardIdx >= 0)
            drew = Game.TryDrawFromDiscard(playerId, discardIdx);
        if (!drew)
            drew = Game.TryDrawFromPile(playerId);

        FindAnyObjectByType<TableUI>()?.Refresh();
        yield return new WaitForSeconds(0.4f);

        if (Game.Phase != GamePhase.PlayerTurn_MeldDiscard)
        {
            _aiCoroutine = null;
            yield break;
        }

        // Step 2: Lay new melds
        var melds = AIPlayer.FindMeldsToLay(player);
        foreach (var meldCards in melds)
        {
            if (Game.Phase != GamePhase.PlayerTurn_MeldDiscard) break;
            Game.TryLayMeld(playerId, meldCards);
            FindAnyObjectByType<TableUI>()?.Refresh();
        }

        // Step 3: Extend existing melds
        var extensions = AIPlayer.FindExtensions(Game, player);
        foreach (var (meldIdx, cards) in extensions)
        {
            if (Game.Phase != GamePhase.PlayerTurn_MeldDiscard) break;
            Game.TryExtendMeld(playerId, meldIdx, cards);
            FindAnyObjectByType<TableUI>()?.Refresh();
        }

        yield return new WaitForSeconds(0.4f);

        if (Game.Phase != GamePhase.PlayerTurn_MeldDiscard)
        {
            _aiCoroutine = null;
            yield break;
        }

        // Step 4: Discard
        if (player.Hand.Count > 0)
        {
            int discardHandIdx = AIPlayer.ChooseDiscard(player, Game);
            if (discardHandIdx >= 0 && discardHandIdx < player.Hand.Count)
            {
                var cardToDiscard = player.Hand[discardHandIdx];
                Game.TryDiscard(playerId, cardToDiscard);
                FindAnyObjectByType<TableUI>()?.Refresh();
            }
        }

        _aiCoroutine = null;
    }

    // --- Actions (called by DebugUI) ---

    public bool DrawFromPile()
    {
        bool ok = Game.TryDrawFromPile(CurrentPlayerId());
        if (ok) LogGameState();
        else Debug.LogWarning("Can't draw from pile right now.");
        return ok;
    }

    public bool DrawFromDiscard(int index)
    {
        bool ok = Game.TryDrawFromDiscard(CurrentPlayerId(), index);
        if (ok) LogGameState();
        else Debug.LogWarning($"Can't draw discard at index {index} — you may not be able to meld that card.");
        return ok;
    }

    public bool LayMeld(List<int> handIndices)
    {
        var cards = IndicesToCards(handIndices);
        if (cards == null) return false;

        Debug.Log($"Cards to meld: {string.Join(", ", cards)} | IsValidSet: {Meld.IsValidSet(cards)} | IsValidSeq: {Meld.IsValidSequence(cards)}");

        bool ok = Game.TryLayMeld(CurrentPlayerId(), cards);
        if (ok) LogGameState();
        else Debug.LogWarning("Invalid meld — check your card selection.");
        return ok;
    }

    public bool ExtendMeld(int meldIndex, List<int> handIndices)
    {
        var cards = IndicesToCards(handIndices);
        if (cards == null) return false;

        bool ok = Game.TryExtendMeld(CurrentPlayerId(), meldIndex, cards);
        if (ok) LogGameState();
        else Debug.LogWarning("Can't extend that meld with those cards.");
        return ok;
    }

    public bool Discard(int handIndex)
    {
        var hand = Game.CurrentPlayer.Hand;
        if (handIndex < 0 || handIndex >= hand.Count)
        {
            Debug.LogWarning("Invalid hand index.");
            return false;
        }

        bool ok = Game.TryDiscard(CurrentPlayerId(), hand[handIndex]);
        if (ok) LogGameState();
        else Debug.LogWarning("Can't discard right now — did you draw from the discard pile without melding that card?");
        return ok;
    }

    public void StartNextRound()
    {
        Game.StartNextRound();
        Debug.Log("New round started!");
        LogGameState();
    }

    // --- Helpers ---

    int CurrentPlayerId() => Game.CurrentPlayer.PlayerId;

    List<Card> IndicesToCards(List<int> indices)
    {
        var hand = Game.CurrentPlayer.Hand;
        var cards = new List<Card>();
        foreach (int i in indices)
        {
            if (i < 0 || i >= hand.Count)
            {
                Debug.LogWarning($"Hand index {i} out of range.");
                return null;
            }
            cards.Add(hand[i]);
        }
        return cards;
    }

    void LogGameState()
    {
        var p = Game.CurrentPlayer;
        Debug.Log($"=== Phase: {Game.Phase} | {p.DisplayName}'s turn ===");
        Debug.Log($"Your hand ({p.Hand.Count} cards): {HandToString(p.Hand)}");
        Debug.Log($"Discard pile top: {(Game.Game_DiscardTop() != null ? Game.Game_DiscardTop().ToString() : "empty")}");
        Debug.Log($"Discard pile ({Game.Deck.DiscardPileCount} cards): {DiscardToString()}");
        if (Game.TableMelds.Count > 0)
        {
            Debug.Log($"Table melds ({Game.TableMelds.Count}):");
            for (int i = 0; i < Game.TableMelds.Count; i++)
                Debug.Log($"  [{i}] {Game.TableMelds[i]}");
        }
        if (Game.RequiredMeldCard != null)
            Debug.Log($"⚠️ You must meld: {Game.RequiredMeldCard}");
    }

    string HandToString(List<Card> hand)
    {
        var parts = new List<string>();
        for (int i = 0; i < hand.Count; i++)
            parts.Add($"[{i}]{hand[i]}");
        return string.Join(", ", parts);
    }

    string DiscardToString()
    {
        var pile = Game.Deck.DiscardPile;
        var parts = new List<string>();
        for (int i = 0; i < pile.Count; i++)
            parts.Add($"[{i}]{pile[i]}");
        return string.Join(", ", parts);
    }

    string GetScoreSummary()
    {
        var parts = new List<string>();
        foreach (var p in Game.Players)
            parts.Add($"{p.DisplayName}: {p.Score}");
        return string.Join(" | ", parts);
    }
}

// Small extension to expose discard top without breaking encapsulation
public static class GameStateExtensions
{
    public static Card Game_DiscardTop(this GameState gs) => gs.Deck.TopDiscard;
}
