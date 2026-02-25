using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rummy500.Core;

/// <summary>
/// Main UI controller — gesture-only interactions:
///   Draw from deck:    Click the draw pile card back.
///   Draw from discard: Drag a discard pile card rightward.
///   Discard:           Drag a hand card to the discard drop zone (top of screen).
///   Lay meld:          Select cards in hand, drag any selected card to the melds area.
///   Extend meld:       Drag card(s) from hand onto an existing meld row.
///   Reorder hand:      Drag a single card left/right within the hand arc.
///   Round/Game Over:   Tap the overlay.
/// </summary>
public class TableUI : MonoBehaviour
{
    [Header("Hand")]
    public HandView handView;

    [Header("Discard Pile")]
    public Transform discardContainer;
    public GameObject cardPrefab;

    [Header("Draw Pile")]
    public Button drawPileCardBack;
    public TextMeshProUGUI drawPileCountText;

    [Header("Melds Area")]
    public Transform meldsContainer;

    [Header("Drop Zones")]
    public RectTransform discardDropZone;   // drag hand card here to discard

    [Header("Overlay")]
    public GameObject gameOverOverlay;      // shown on RoundOver / GameOver
    public TextMeshProUGUI overlayText;
    public Button overlayButton;

    [Header("Info Panel")]
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI currentPlayerText;
    public TextMeshProUGUI warningText;
    public Transform scoreContainer;
    public GameObject scoreRowPrefab;

    private List<CardView> _discardViews = new List<CardView>();
    private List<RectTransform> _meldRowRTs = new List<RectTransform>();

    private int _draggingDiscardIndex = -1;
    private float _draggingDiscardStartX;

    void Start()
    {
        if (drawPileCardBack) drawPileCardBack.onClick.AddListener(OnDrawPile);
        if (overlayButton) overlayButton.onClick.AddListener(OnOverlayClicked);
        if (handView) handView.OnCardsDroppedExternal += OnHandCardsDropped;
        Refresh();
    }

    // --- Called after every game action ---

    public void Refresh()
    {
        if (GameManager.Instance == null || GameManager.Instance.Game == null) return;
        var game = GameManager.Instance.Game;
        var player = game.CurrentPlayer;

        if (phaseText) phaseText.text = PhaseFriendly(game.Phase);
        if (currentPlayerText) currentPlayerText.text = $"{player.DisplayName}'s Turn";

        if (warningText)
        {
            warningText.text = game.RequiredMeldCard != null ? $"Must meld: {game.RequiredMeldCard}" : "";
            warningText.gameObject.SetActive(game.RequiredMeldCard != null);
        }

        if (drawPileCountText) drawPileCountText.text = $"{game.Deck.DrawPileCount}";

        bool isHumanDrawPhase = game.Phase == GamePhase.PlayerTurn_Draw && player.PlayerId == 0;
        if (drawPileCardBack) drawPileCardBack.interactable = isHumanDrawPhase;

        RenderDiscardPile(game);

        var humanPlayer = game.Players.Find(p => p.PlayerId == 0);
        if (handView)
        {
            handView.CanDrag = game.Phase == GamePhase.PlayerTurn_MeldDiscard && player.PlayerId == 0;
            handView.RenderHand(humanPlayer?.Hand);
        }

        RenderTableMelds(game);
        RenderScores(game);

        bool showOverlay = game.Phase == GamePhase.RoundOver || game.Phase == GamePhase.GameOver;
        if (gameOverOverlay) gameOverOverlay.SetActive(showOverlay);
        if (overlayText && showOverlay)
        {
            if (game.Phase == GamePhase.GameOver)
            {
                var winner = game.Players.OrderByDescending(p => p.Score).First();
                overlayText.text = $"GAME OVER!\n{winner.DisplayName} wins with {winner.Score} pts\n\nTap to play again";
            }
            else
            {
                overlayText.text = "Round Over!\n\nTap for next round";
            }
        }
    }

    // --- Render helpers ---

    void RenderDiscardPile(GameState game)
    {
        if (!discardContainer) return;

        foreach (var cv in _discardViews) if (cv) Destroy(cv.gameObject);
        _discardViews.Clear();

        var layout = discardContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout) Destroy(layout);

        var pile = game.Deck.DiscardPile;
        if (pile.Count == 0) return;

        float cardWidth = 90f;
        float overlap = 20f;
        float containerWidth = discardContainer.GetComponent<RectTransform>().sizeDelta.x;
        float startX = -containerWidth / 2f + cardWidth / 2f;

        bool canDraw = game.Phase == GamePhase.PlayerTurn_Draw && game.CurrentPlayer.PlayerId == 0;

        for (int i = 0; i < pile.Count; i++)
        {
            float x = startX + i * overlap;
            var go = Instantiate(cardPrefab, discardContainer);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, 0);

            var cv = go.GetComponent<CardView>();
            cv.Setup(pile[i]);
            cv.SetBasePosition(new Vector3(x, 0, 0));

            if (canDraw)
            {
                int capturedIndex = i;
                cv.OnBeginDragCard += (c, e) => OnDiscardBeginDrag(capturedIndex, e);
                cv.OnEndDragCard += (c, e) => OnDiscardEndDrag(capturedIndex, e);
            }
            _discardViews.Add(cv);
        }
    }

    void RenderTableMelds(GameState game)
    {
        if (!meldsContainer) return;
        _meldRowRTs.Clear();

        foreach (Transform child in meldsContainer)
            Destroy(child.gameObject);

        for (int m = 0; m < game.TableMelds.Count; m++)
        {
            var meld = game.TableMelds[m];
            var row = new GameObject($"Meld_{m}", typeof(RectTransform));
            row.transform.SetParent(meldsContainer, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(600, 100);
            _meldRowRTs.Add(rowRT);

            float cardW = 60f, cardH = 88f, meldOverlap = 40f;
            float totalW = cardW + (meld.Cards.Count - 1) * meldOverlap;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < meld.Cards.Count; i++)
            {
                var go = Instantiate(cardPrefab, row.transform);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(startX + i * meldOverlap, 0);
                var cv = go.GetComponent<CardView>();
                cv.Setup(meld.Cards[i]);
                if (cv.topRankText) cv.topRankText.fontSize = 9;
                if (cv.topSuitText) cv.topSuitText.fontSize = 8;
                if (cv.centerSuitText) cv.centerSuitText.fontSize = 20;
                if (cv.bottomRankText) cv.bottomRankText.fontSize = 9;
                if (cv.bottomSuitText) cv.bottomSuitText.fontSize = 8;
            }
        }
    }

    void RenderScores(GameState game)
    {
        if (!scoreContainer) return;
        foreach (Transform child in scoreContainer)
            Destroy(child.gameObject);

        foreach (var p in game.Players)
        {
            GameObject row;
            if (scoreRowPrefab)
            {
                row = Instantiate(scoreRowPrefab, scoreContainer);
                var t = row.GetComponentInChildren<TextMeshProUGUI>();
                if (t) t.text = $"{p.DisplayName}: {p.Score} / 500";
            }
            else
            {
                row = new GameObject("ScoreRow", typeof(RectTransform), typeof(TextMeshProUGUI));
                row.transform.SetParent(scoreContainer, false);
                var t = row.GetComponent<TextMeshProUGUI>();
                t.text = $"{p.DisplayName}: {p.Score} / 500";
                t.fontSize = 16;
                t.color = Color.white;
            }
        }
    }

    // --- Discard pile drag: draw from discard ---

    void OnDiscardBeginDrag(int index, PointerEventData e)
    {
        _draggingDiscardIndex = index;
        _draggingDiscardStartX = e.position.x;
    }

    void OnDiscardEndDrag(int index, PointerEventData e)
    {
        if (_draggingDiscardIndex < 0 || index != _draggingDiscardIndex)
        {
            _draggingDiscardIndex = -1;
            return;
        }
        float dragDelta = e.position.x - _draggingDiscardStartX;
        if (dragDelta > 50f)
        {
            if (GameManager.Instance.DrawFromDiscard(_draggingDiscardIndex))
                Refresh();
        }
        _draggingDiscardIndex = -1;
    }

    // --- Hand external drop: discard / lay meld / extend meld ---

    void OnHandCardsDropped(List<CardView> draggedCards, Vector2 screenPos)
    {
        var game = GameManager.Instance.Game;
        if (game.Phase != GamePhase.PlayerTurn_MeldDiscard || game.CurrentPlayer.PlayerId != 0)
        {
            Refresh();
            return;
        }

        var hand = game.CurrentPlayer.Hand;
        var handIndices = draggedCards
            .Select(cv => hand.IndexOf(cv.Card))
            .Where(i => i >= 0)
            .ToList();

        if (handIndices.Count == 0) { Refresh(); return; }

        // 1. Discard zone
        if (discardDropZone && RectTransformUtility.RectangleContainsScreenPoint(discardDropZone, screenPos, null))
        {
            if (handIndices.Count == 1)
            {
                if (!GameManager.Instance.Discard(handIndices[0]))
                    ShowWarning("Can't discard that card — must meld it first.");
            }
            else
                ShowWarning("Select exactly 1 card to discard.");
            Refresh();
            return;
        }

        // 2. Specific meld row (extend)
        for (int m = 0; m < _meldRowRTs.Count; m++)
        {
            if (_meldRowRTs[m] && RectTransformUtility.RectangleContainsScreenPoint(_meldRowRTs[m], screenPos, null))
            {
                if (!GameManager.Instance.ExtendMeld(m, handIndices))
                    ShowWarning("Can't extend that meld with those cards.");
                Refresh();
                return;
            }
        }

        // 3. Melds container (lay new meld)
        var meldsRT = meldsContainer?.GetComponent<RectTransform>();
        if (meldsRT && RectTransformUtility.RectangleContainsScreenPoint(meldsRT, screenPos, null))
        {
            if (!GameManager.Instance.LayMeld(handIndices))
                ShowWarning("That's not a valid meld.");
            Refresh();
            return;
        }

        // Dropped nowhere meaningful — snap back
        Refresh();
    }

    // --- Draw pile button ---

    void OnDrawPile()
    {
        if (GameManager.Instance.DrawFromPile())
            Refresh();
    }

    // --- Overlay: round over / game over ---

    void OnOverlayClicked()
    {
        var game = GameManager.Instance.Game;
        if (game.Phase == GamePhase.RoundOver)
        {
            GameManager.Instance.StartNextRound();
            Refresh();
        }
        else if (game.Phase == GamePhase.GameOver)
        {
            GameManager.Instance.StartNewGame();
            Refresh();
        }
    }

    void ShowWarning(string msg)
    {
        if (warningText)
        {
            warningText.text = msg;
            warningText.gameObject.SetActive(true);
        }
        Debug.LogWarning(msg);
    }

    string PhaseFriendly(GamePhase phase) => phase switch
    {
        GamePhase.PlayerTurn_Draw => "Draw a card",
        GamePhase.PlayerTurn_MeldDiscard => "Meld or Discard",
        GamePhase.RoundOver => "Round Over",
        GamePhase.GameOver => "Game Over!",
        _ => phase.ToString()
    };
}
