using System.Collections.Generic;
using UnityEngine;
using Rummy500.Core;

/// <summary>
/// Temporary debug UI to test the game loop.
/// Attach to the same GameObject as GameManager.
/// Remove or disable when you build real UI later.
/// </summary>
public class DebugUI : MonoBehaviour
{
    // Cards selected in hand (by index)
    private List<int> _selectedHandIndices = new List<int>();
    private int _selectedMeldIndex = -1;
    private Vector2 _scrollPos;

    void OnGUI()
    {
        if (GameManager.Instance == null || GameManager.Instance.Game == null) return;

        var game = GameManager.Instance.Game;
        var player = game.CurrentPlayer;
        bool isYourTurn = player.PlayerId == 0;

        // Main window
        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20));
        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        GUILayout.Label($"=== RUMMY 500 ===", HeaderStyle());
        GUILayout.Label($"Phase: {game.Phase}");
        GUILayout.Label($"Turn: {player.DisplayName}{(isYourTurn ? " (YOU)" : "")}");
        GUILayout.Space(5);

        // Scores
        GUILayout.Label("--- Scores ---", BoldStyle());
        foreach (var p in game.Players)
            GUILayout.Label($"  {p.DisplayName}: {p.Score} pts (melded: {p.MeldedPoints})");
        GUILayout.Space(5);

        // Discard pile
        GUILayout.Label("--- Discard Pile (bottom → top) ---", BoldStyle());
        var discard = game.Deck.DiscardPile;
        for (int i = 0; i < discard.Count; i++)
        {
            if (GUILayout.Button($"[{i}] {discard[i]} — draw from here"))
                GameManager.Instance.DrawFromDiscard(i);
        }
        GUILayout.Space(5);

        // Draw actions
        if (game.Phase == GamePhase.PlayerTurn_Draw)
        {
            GUILayout.Label("--- Draw ---", BoldStyle());
            if (GUILayout.Button("Draw from pile"))
                GameManager.Instance.DrawFromPile();
        }

        // Hand
        GUILayout.Label($"--- Your Hand ({player.Hand.Count} cards) ---", BoldStyle());
        if (game.RequiredMeldCard != null)
            GUILayout.Label($"⚠️ Must meld: {game.RequiredMeldCard}", WarningStyle());

        for (int i = 0; i < player.Hand.Count; i++)
        {
            bool selected = _selectedHandIndices.Contains(i);
            string label = selected ? $"✓ [{i}] {player.Hand[i]}" : $"  [{i}] {player.Hand[i]}";
            if (GUILayout.Button(label, selected ? SelectedStyle() : GUI.skin.button))
            {
                if (selected) _selectedHandIndices.Remove(i);
                else _selectedHandIndices.Add(i);
            }
        }
        GUILayout.Space(5);

        // Meld / Extend / Discard actions
        if (game.Phase == GamePhase.PlayerTurn_MeldDiscard)
        {
            GUILayout.Label("--- Actions (select cards above first) ---", BoldStyle());

            if (GUILayout.Button($"Lay Meld ({_selectedHandIndices.Count} selected)"))
            {
                if (GameManager.Instance.LayMeld(_selectedHandIndices))
                    _selectedHandIndices.Clear();
            }

            // Table melds for extending
            if (game.TableMelds.Count > 0)
            {
                GUILayout.Label("--- Table Melds (select meld + cards to extend) ---", BoldStyle());
                for (int i = 0; i < game.TableMelds.Count; i++)
                {
                    bool meldSelected = _selectedMeldIndex == i;
                    string mlabel = meldSelected ? $"✓ [{i}] {game.TableMelds[i]}" : $"  [{i}] {game.TableMelds[i]}";
                    if (GUILayout.Button(mlabel, meldSelected ? SelectedStyle() : GUI.skin.button))
                        _selectedMeldIndex = meldSelected ? -1 : i;
                }

                if (_selectedMeldIndex >= 0)
                {
                    if (GUILayout.Button($"Extend Meld [{_selectedMeldIndex}] with {_selectedHandIndices.Count} selected cards"))
                    {
                        if (GameManager.Instance.ExtendMeld(_selectedMeldIndex, _selectedHandIndices))
                        {
                            _selectedHandIndices.Clear();
                            _selectedMeldIndex = -1;
                        }
                    }
                }
            }

            GUILayout.Space(5);
            GUILayout.Label("--- Discard (select ONE card) ---", BoldStyle());
            if (_selectedHandIndices.Count == 1)
            {
                if (GUILayout.Button($"Discard {player.Hand[_selectedHandIndices[0]]}"))
                {
                    if (GameManager.Instance.Discard(_selectedHandIndices[0]))
                        _selectedHandIndices.Clear();
                }
            }
            else
            {
                GUILayout.Label("(Select exactly 1 card to discard)");
            }
        }

        // Round over
        if (game.Phase == GamePhase.RoundOver)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- ROUND OVER ---", HeaderStyle());
            foreach (var p in game.Players)
                GUILayout.Label($"  {p.DisplayName}: {p.Score} pts");
            if (GUILayout.Button("Start Next Round"))
            {
                GameManager.Instance.StartNextRound();
                _selectedHandIndices.Clear();
                _selectedMeldIndex = -1;
            }
        }

        // Game over
        if (game.Phase == GamePhase.GameOver)
        {
            GUILayout.Space(10);
            GUILayout.Label("--- GAME OVER ---", HeaderStyle());
            foreach (var p in game.Players)
                GUILayout.Label($"  {p.DisplayName}: {p.Score} pts");
            if (GUILayout.Button("Play Again"))
            {
                GameManager.Instance.StartNewGame();
                _selectedHandIndices.Clear();
                _selectedMeldIndex = -1;
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // --- Styles ---

    GUIStyle HeaderStyle()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontStyle = FontStyle.Bold;
        s.fontSize = 14;
        return s;
    }

    GUIStyle BoldStyle()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.fontStyle = FontStyle.Bold;
        return s;
    }

    GUIStyle SelectedStyle()
    {
        var s = new GUIStyle(GUI.skin.button);
        s.normal.textColor = Color.green;
        s.fontStyle = FontStyle.Bold;
        return s;
    }

    GUIStyle WarningStyle()
    {
        var s = new GUIStyle(GUI.skin.label);
        s.normal.textColor = Color.yellow;
        s.fontStyle = FontStyle.Bold;
        return s;
    }
}
