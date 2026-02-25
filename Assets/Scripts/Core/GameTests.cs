using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rummy500.Core;

/// <summary>
/// Lightweight test runner for Unity.
/// Call GameTests.RunAll() from an Editor script or from Start() in a MonoBehaviour.
/// Results appear in the Unity Console window.
/// </summary>
public static class GameTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    public static void RunAll()
    {
        _passed = _failed = 0;

        Test_CardPointValues();
        Test_DeckDeal();
        Test_DiscardPilePickup();
        Test_MeldValidation_Set();
        Test_MeldValidation_Sequence();
        Test_MeldExtension();
        Test_GameFlow_BasicTurn();
        Test_Score_RoundEnd();

        Debug.Log($"=== Results: {_passed} passed, {_failed} failed ===");
    }

    // --- Tests ---

    static void Test_CardPointValues()
    {
        Assert("Ace = 15pts", new Card(Suit.Spades, Rank.Ace).PointValue == 15);
        Assert("King = 10pts", new Card(Suit.Hearts, Rank.King).PointValue == 10);
        Assert("7 = 7pts", new Card(Suit.Clubs, Rank.Seven).PointValue == 7);
        Assert("2 = 2pts", new Card(Suit.Diamonds, Rank.Two).PointValue == 2);
    }

    static void Test_DeckDeal()
    {
        var deck = new Deck(42);
        deck.Shuffle();
        var hands = new List<List<Card>> { new(), new() };
        deck.Deal(hands, 7);

        Assert("Player 1 has 7 cards", hands[0].Count == 7);
        Assert("Player 2 has 7 cards", hands[1].Count == 7);
        Assert("Discard pile has 1 card", deck.DiscardPileCount == 1);
        Assert("Draw pile has 37 cards", deck.DrawPileCount == 37);
    }

    static void Test_DiscardPilePickup()
    {
        var deck = new Deck();
        deck.AddToDiscard(new Card(Suit.Spades, Rank.Two));
        deck.AddToDiscard(new Card(Suit.Hearts, Rank.Three));
        deck.AddToDiscard(new Card(Suit.Clubs, Rank.Four)); // top

        // Picking up index 1 (Hearts 3) should also return Clubs 4
        var taken = deck.DrawFromDiscard(1);
        Assert("Drew 2 cards from discard", taken.Count == 2);
        Assert("First taken is Hearts 3", taken[0].Rank == Rank.Three);
        Assert("Second taken is Clubs 4", taken[1].Rank == Rank.Four);
        Assert("Discard pile has 1 card left", deck.DiscardPileCount == 1);
    }

    static void Test_MeldValidation_Set()
    {
        var validSet = new List<Card>
        {
            new Card(Suit.Spades, Rank.King),
            new Card(Suit.Hearts, Rank.King),
            new Card(Suit.Clubs, Rank.King)
        };
        Assert("3 Kings is valid set", Meld.IsValidSet(validSet));

        var duplicateSuit = new List<Card>
        {
            new Card(Suit.Spades, Rank.King),
            new Card(Suit.Spades, Rank.King),
            new Card(Suit.Clubs, Rank.King)
        };
        Assert("Duplicate suit is invalid set", !Meld.IsValidSet(duplicateSuit));

        var tooFew = new List<Card>
        {
            new Card(Suit.Spades, Rank.King),
            new Card(Suit.Hearts, Rank.King)
        };
        Assert("2 cards is invalid set", !Meld.IsValidSet(tooFew));
    }

    static void Test_MeldValidation_Sequence()
    {
        var validSeq = new List<Card>
        {
            new Card(Suit.Hearts, Rank.Five),
            new Card(Suit.Hearts, Rank.Six),
            new Card(Suit.Hearts, Rank.Seven)
        };
        Assert("5-6-7 Hearts is valid sequence", Meld.IsValidSequence(validSeq));

        var mixedSuit = new List<Card>
        {
            new Card(Suit.Hearts, Rank.Five),
            new Card(Suit.Spades, Rank.Six),
            new Card(Suit.Hearts, Rank.Seven)
        };
        Assert("Mixed suit is invalid sequence", !Meld.IsValidSequence(mixedSuit));

        var gap = new List<Card>
        {
            new Card(Suit.Hearts, Rank.Five),
            new Card(Suit.Hearts, Rank.Seven),
            new Card(Suit.Hearts, Rank.Eight)
        };
        Assert("Gap in ranks is invalid sequence", !Meld.IsValidSequence(gap));
    }

    static void Test_MeldExtension()
    {
        var seqCards = new List<Card>
        {
            new Card(Suit.Spades, Rank.Four),
            new Card(Suit.Spades, Rank.Five),
            new Card(Suit.Spades, Rank.Six)
        };
        var meld = Meld.CreateSequence(0, seqCards);

        var extension = new List<Card> { new Card(Suit.Spades, Rank.Seven) };
        Assert("Can extend sequence with next rank", meld.TryExtend(extension));
        Assert("Meld now has 4 cards", meld.Cards.Count == 4);

        var badExtension = new List<Card> { new Card(Suit.Spades, Rank.Nine) };
        Assert("Cannot extend sequence with non-consecutive rank", !meld.TryExtend(badExtension));
    }

    static void Test_GameFlow_BasicTurn()
    {
        var game = new GameState();
        game.AddPlayer(0, "Alice");
        game.AddPlayer(1, "Bob");
        game.StartGame(seed: 99);

        Assert("Phase is Draw at start", game.Phase == GamePhase.PlayerTurn_Draw);
        Assert("Current player is index 0", game.CurrentPlayerIndex == 0);

        bool drew = game.TryDrawFromPile(0);
        Assert("Player 0 can draw", drew);
        Assert("Phase is MeldDiscard after draw", game.Phase == GamePhase.PlayerTurn_MeldDiscard);
        Assert("Player 0 has 8 cards", game.Players[0].Hand.Count == 8);

        // Discard a card
        var discard = game.Players[0].Hand[0];
        bool discarded = game.TryDiscard(0, discard);
        Assert("Player 0 can discard", discarded);
        Assert("Phase back to Draw", game.Phase == GamePhase.PlayerTurn_Draw);
        Assert("Current player is now index 1", game.CurrentPlayerIndex == 1);
    }

    static void Test_Score_RoundEnd()
    {
        var player = new PlayerState(0, "Alice");

        var tenOfSpades = new Card(Suit.Spades, Rank.Ten);
        var kingHearts = new Card(Suit.Hearts, Rank.King);
        var kingClubs = new Card(Suit.Clubs, Rank.King);
        var kingDiamonds = new Card(Suit.Diamonds, Rank.King);

        // Add ALL cards to hand first, then meld using the same references
        player.AddCardToHand(tenOfSpades);
        player.AddCardToHand(kingHearts);
        player.AddCardToHand(kingClubs);
        player.AddCardToHand(kingDiamonds);

        var meldCards = new List<Card> { kingHearts, kingClubs, kingDiamonds };
        bool result = player.TryLayMeld(meldCards);

        Assert("TryLayMeld succeeded", result);
        Assert("Melded points = 30", player.MeldedPoints == 30);
        Assert("Hand points = 10", player.HandPoints == 10);

        player.ApplyRoundScore(false);
        Assert("Score = 30 - 10 = 20", player.Score == 20);
    }

    // --- Helpers ---

    static void Assert(string label, bool condition)
    {
        if (condition)
            Debug.Log($"  ✅ {label}");
        else
            Debug.LogError($"  ❌ FAIL: {label}");

        if (condition) _passed++; else _failed++;
    }
}
