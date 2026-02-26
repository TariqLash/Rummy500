using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Rummy500.Core;

/// <summary>
/// Main UI controller.
///   Draw:         Click draw pile button OR click a discard card.
///   Select cards: Click hand cards to toggle selection (for melds).
///   Discard:      Drag 1 hand card to the discard drop zone (at the pile's next slot).
///   Lay meld:     Select cards, drag any selected card to the meld drop zone (left-middle).
///   Extend meld:  Same as meld — the system tries extend if lay fails.
///   Reorder:      Drag a card within the hand arc.
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
    public RectTransform discardDropZone;   // card-sized; repositioned to pile's next slot each render
    public RectTransform meldDropZone;      // 2-card-wide; left-middle of screen

    [Header("Overlay")]
    public GameObject gameOverOverlay;
    public TextMeshProUGUI overlayText;
    public Button overlayButton;

    [Header("Info Panel")]
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI currentPlayerText;
    public TextMeshProUGUI warningText;
    public Transform scoreContainer;
    public GameObject scoreRowPrefab;

    private List<CardView> _discardViews = new List<CardView>();
    private Card _lastDrawnCard = null;

    private Image _discardDropImage;
    private Image _meldDropImage;

    // Per-meld-row hover data
    private List<RectTransform> _meldRowRTs     = new List<RectTransform>();
    private List<Image>         _meldRowOverlays = new List<Image>();

    private static readonly Color MeldRowExtendable = new Color(0.22f, 0.95f, 0.45f, 0.55f);
    private static readonly Color MeldRowNormal      = new Color(0f, 0f, 0f, 0f);
    private static readonly Color MeldNormal    = new Color(0.18f, 0.75f, 0.35f, 0.45f);
    private static readonly Color MeldHover     = new Color(0.22f, 0.95f, 0.45f, 0.75f);
    private static readonly Color DiscardNormal = new Color(1.00f, 0.55f, 0.20f, 0.45f);
    private static readonly Color DiscardHover  = new Color(1.00f, 0.65f, 0.25f, 0.75f);

    void Start()
    {
        if (drawPileCardBack) drawPileCardBack.onClick.AddListener(OnDrawPile);
        if (overlayButton)    overlayButton.onClick.AddListener(OnOverlayClicked);
        if (handView)
        {
            handView.OnCardsDroppedExternal += OnHandCardsDropped;
            handView.OnDragStateChanged     += OnHandDragStateChanged;
            handView.OnDragMoved            += OnHandDragMoved;
        }

        Canvas.ForceUpdateCanvases();   // ensure layout is resolved before Refresh
        InitDropZones();
        Refresh();
    }

    // --- Called after every game action ---

    public void Refresh()
    {
        if (GameManager.Instance == null || GameManager.Instance.Game == null) return;
        var game   = GameManager.Instance.Game;
        var player = game.CurrentPlayer;

        if (phaseText)         phaseText.text         = PhaseFriendly(game.Phase);
        if (currentPlayerText) currentPlayerText.text = $"{player.DisplayName}'s Turn";

        if (warningText)
        {
            warningText.text = game.RequiredMeldCard != null ? $"Must meld: {game.RequiredMeldCard}" : "";
            warningText.gameObject.SetActive(game.RequiredMeldCard != null);
        }

        if (drawPileCountText) drawPileCountText.text = $"{game.Deck.DrawPileCount}";

        bool isHumanTurn = player.PlayerId == 0;
        if (drawPileCardBack)
            drawPileCardBack.interactable = isHumanTurn && game.Phase == GamePhase.PlayerTurn_Draw;

        RenderDiscardPile(game);

        var humanPlayer = game.Players.Find(p => p.PlayerId == 0);
        if (handView)
        {
            handView.CanDrag = isHumanTurn && game.Phase == GamePhase.PlayerTurn_MeldDiscard;
            handView.RenderHand(humanPlayer?.Hand, _lastDrawnCard);
        }
        _lastDrawnCard = null;

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

        float cardW  = 90f, overlap = 20f;
        float contW  = ((RectTransform)discardContainer).sizeDelta.x;
        float startX = -contW / 2f + cardW / 2f;

        bool canDraw = game.Phase == GamePhase.PlayerTurn_Draw && game.CurrentPlayer.PlayerId == 0;

        for (int i = 0; i < pile.Count; i++)
        {
            float x  = startX + i * overlap;
            var go   = Instantiate(cardPrefab, discardContainer);
            var rt   = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, 0);

            var cv = go.GetComponent<CardView>();
            cv.Setup(pile[i]);
            cv.SetBasePosition(new Vector3(x, 0, 0));

            if (canDraw)
            {
                int capturedIndex = i;
                cv.OnCardClicked += _ => OnDiscardClicked(capturedIndex);
            }
            _discardViews.Add(cv);
        }

        // Position discard drop zone to cover the entire pile container (slightly oversized)
        // Use a fixed size — rect.width is 0 at startup before layout runs
        if (discardDropZone && discardContainer)
        {
            var contRT = (RectTransform)discardContainer;
            discardDropZone.position  = contRT.TransformPoint(Vector3.zero);
            discardDropZone.sizeDelta = new Vector2(494f, 130f);
        }
    }

    void RenderTableMelds(GameState game)
    {
        if (!meldsContainer) return;

        foreach (Transform child in meldsContainer)
            Destroy(child.gameObject);
        _meldRowRTs.Clear();
        _meldRowOverlays.Clear();

        string[] rankLabels  = {"","A","2","3","4","5","6","7","8","9","10","J","Q","K"};
        string[] suitSymbols = {"♠","♥","♦","♣"};

        for (int m = 0; m < game.TableMelds.Count; m++)
        {
            var meld = game.TableMelds[m];
            var row  = new GameObject($"Meld_{m}", typeof(RectTransform));
            row.transform.SetParent(meldsContainer, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(600, 34);

            // Hover overlay — covers entire row, hidden by default
            var overlayGO = new GameObject("HoverOverlay", typeof(RectTransform), typeof(Image));
            overlayGO.transform.SetParent(row.transform, false);
            var overlayRT = overlayGO.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImg = overlayGO.GetComponent<Image>();
            overlayImg.color         = MeldRowNormal;
            overlayImg.raycastTarget = false;

            _meldRowRTs.Add(rowRT);
            _meldRowOverlays.Add(overlayImg);

            float cardW = 38f, cardH = 28f, meldOverlap = 30f;
            float totalW = cardW + (meld.Cards.Count - 1) * meldOverlap;
            float startX = -totalW / 2f + cardW / 2f;

            for (int i = 0; i < meld.Cards.Count; i++)
            {
                var card = meld.Cards[i];
                bool isRed = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;
                Color suitColor = isRed ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.1f, 0.1f, 0.1f);

                var tile = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image));
                tile.transform.SetParent(row.transform, false);
                tile.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.96f);
                var tileRT = tile.GetComponent<RectTransform>();
                tileRT.sizeDelta        = new Vector2(cardW, cardH);
                tileRT.anchoredPosition = new Vector2(startX + i * meldOverlap, 0);

                var lbl = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                lbl.transform.SetParent(tile.transform, false);
                var lblRT = lbl.GetComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = new Vector2(2, 1); lblRT.offsetMax = new Vector2(-2, -1);
                var lblTMP = lbl.GetComponent<TextMeshProUGUI>();
                lblTMP.text = rankLabels[(int)card.Rank] + suitSymbols[(int)card.Suit];
                lblTMP.fontSize = 14; lblTMP.color = suitColor;
                lblTMP.alignment = TextAlignmentOptions.Center;
                lblTMP.raycastTarget = false;
            }
        }
    }

    void RenderScores(GameState game)
    {
        if (!scoreContainer) return;
        foreach (Transform child in scoreContainer) Destroy(child.gameObject);

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
                t.color    = Color.white;
            }
        }
    }

    // --- Drop zone setup ---

    void InitDropZones()
    {
        if (discardDropZone)
        {
            _discardDropImage = discardDropZone.GetComponent<Image>();
            if (!_discardDropImage)
            {
                _discardDropImage = discardDropZone.gameObject.AddComponent<Image>();
            }
            _discardDropImage.color        = DiscardNormal;
            _discardDropImage.raycastTarget = false;
            EnsureZoneLabel(discardDropZone, "Discard");
            discardDropZone.gameObject.SetActive(false);
        }

        if (meldDropZone)
        {
            _meldDropImage = meldDropZone.GetComponent<Image>();
            if (!_meldDropImage)
            {
                _meldDropImage = meldDropZone.gameObject.AddComponent<Image>();
            }
            _meldDropImage.color        = MeldNormal;
            _meldDropImage.raycastTarget = false;
            EnsureZoneLabel(meldDropZone, "Lay Meld");
            meldDropZone.gameObject.SetActive(false);
        }
    }

    void EnsureZoneLabel(RectTransform zone, string text)
    {
        var existing = zone.GetComponentInChildren<TextMeshProUGUI>();
        if (existing) { existing.text = text; return; }

        var lbl   = new GameObject("ZoneLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        lbl.transform.SetParent(zone, false);
        var lblRT = lbl.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
        var tmp = lbl.GetComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = 13;
        tmp.color         = new Color(1f, 1f, 1f, 0.80f);
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.raycastTarget = false;
    }

    // --- Drag state handlers ---

    void OnHandDragStateChanged(bool dragging)
    {
        var game   = GameManager.Instance?.Game;
        bool active = dragging
            && game?.Phase == GamePhase.PlayerTurn_MeldDiscard
            && game?.CurrentPlayer.PlayerId == 0;

        if (discardDropZone) discardDropZone.gameObject.SetActive(active);
        if (meldDropZone)    meldDropZone.gameObject.SetActive(active);

        if (dragging && meldDropZone)
        {
            // +1 for the dragged card itself (which may not be in SelectedIndices)
            int count = handView ? handView.SelectedIndices.Count + 1 : 1;
            string countLabel = count >= 3 ? $"Lay Meld ({count})" : $"Lay Meld\nNeed {3 - count} more";
            EnsureZoneLabel(meldDropZone, countLabel);
        }
        else if (!dragging)
        {
            if (_discardDropImage) _discardDropImage.color = DiscardNormal;
            if (_meldDropImage)    _meldDropImage.color    = MeldNormal;
            if (meldDropZone) EnsureZoneLabel(meldDropZone, "Lay Meld");
            foreach (var img in _meldRowOverlays) if (img) img.color = MeldRowNormal;
        }
    }

    void OnHandDragMoved(Vector2 screenPos)
    {
        if (_discardDropImage && discardDropZone)
            _discardDropImage.color = RectTransformUtility.RectangleContainsScreenPoint(
                discardDropZone, screenPos, null) ? DiscardHover : DiscardNormal;

        if (_meldDropImage && meldDropZone)
            _meldDropImage.color = RectTransformUtility.RectangleContainsScreenPoint(
                meldDropZone, screenPos, null) ? MeldHover : MeldNormal;

        // Highlight individual meld rows if dragged cards can extend them
        var game      = GameManager.Instance?.Game;
        var dragCards = handView ? handView.CurrentDragCards : null;
        for (int m = 0; m < _meldRowRTs.Count && m < _meldRowOverlays.Count; m++)
        {
            bool hovering  = RectTransformUtility.RectangleContainsScreenPoint(_meldRowRTs[m], screenPos, null);
            bool canExtend = dragCards != null && dragCards.Count > 0
                             && game != null && m < game.TableMelds.Count
                             && game.TableMelds[m].CanExtend(dragCards);
            _meldRowOverlays[m].color = (hovering && canExtend) ? MeldRowExtendable : MeldRowNormal;
        }
    }

    // --- Drop handler ---

    void OnHandCardsDropped(List<CardView> draggedCards, Vector2 screenPos)
    {
        var game = GameManager.Instance.Game;
        if (game.Phase != GamePhase.PlayerTurn_MeldDiscard || game.CurrentPlayer.PlayerId != 0)
        { Refresh(); return; }

        // Extract Card objects directly — no index round-trip
        var hand  = game.CurrentPlayer.Hand;
        var cards = draggedCards
            .Select(cv => cv.Card)
            .Where(c => c != null && hand.Contains(c))
            .ToList();

        if (cards.Count == 0) { Refresh(); return; }

        // Discard zone — always discard only the dragged card (cards[0]), ignore selection
        if (discardDropZone && RectTransformUtility.RectangleContainsScreenPoint(discardDropZone, screenPos, null))
        {
            if (!game.TryDiscard(0, cards[0]))
                ShowWarning("Can't discard — meld the drawn card first.");
            Refresh();
            return;
        }

        // Meld zone — try lay new meld, then try extending any existing meld
        // Checked BEFORE meld rows because the zone can overlap a row visually
        if (meldDropZone && RectTransformUtility.RectangleContainsScreenPoint(meldDropZone, screenPos, null))
        {
            if (cards.Count < 2)
            {
                ShowWarning("Select cards first, then drag to Lay Meld.");
                Refresh();
                return;
            }
            Debug.Log($"[Meld] Trying: {string.Join(", ", cards)}  IsValidSet={Meld.IsValidSet(cards)}  IsValidSeq={Meld.IsValidSequence(cards)}");

            if (game.TryLayMeld(0, cards))
            {
                handView.ClearSelection();
                Refresh();
                return;
            }
            for (int m = 0; m < game.TableMelds.Count; m++)
            {
                if (game.TryExtendMeld(0, m, cards))
                {
                    handView.ClearSelection();
                    Refresh();
                    return;
                }
            }
            ShowWarning("Not a valid meld.");
            Refresh();
            return;
        }

        // Drop directly on a meld row (outside the meld zone) — extend that specific meld
        for (int m = 0; m < _meldRowRTs.Count; m++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_meldRowRTs[m], screenPos, null))
            {
                if (m < game.TableMelds.Count && game.TryExtendMeld(0, m, cards))
                {
                    handView.ClearSelection();
                    Refresh();
                    return;
                }
                ShowWarning("Can't extend that meld.");
                Refresh();
                return;
            }
        }

        // Dropped nowhere — snap back
        Refresh();
    }

    // --- Draw ---

    void OnDrawPile()
    {
        if (GameManager.Instance.DrawFromPile())
        {
            _lastDrawnCard = GameManager.Instance.Game.CurrentPlayer.Hand[0];
            Refresh();
        }
    }

    void OnDiscardClicked(int index)
    {
        if (GameManager.Instance.DrawFromDiscard(index))
        {
            _lastDrawnCard = GameManager.Instance.Game.CurrentPlayer.Hand[0];
            Refresh();
        }
    }

    // --- Overlay ---

    void OnOverlayClicked()
    {
        var game = GameManager.Instance.Game;
        if      (game.Phase == GamePhase.RoundOver) { GameManager.Instance.StartNextRound(); Refresh(); }
        else if (game.Phase == GamePhase.GameOver)  { GameManager.Instance.StartNewGame();   Refresh(); }
    }

    void ShowWarning(string msg)
    {
        if (warningText) { warningText.text = msg; warningText.gameObject.SetActive(true); }
        Debug.LogWarning(msg);
    }

    string PhaseFriendly(GamePhase phase) => phase switch
    {
        GamePhase.PlayerTurn_Draw        => "Draw a card",
        GamePhase.PlayerTurn_MeldDiscard => "Meld or Discard",
        GamePhase.RoundOver              => "Round Over",
        GamePhase.GameOver               => "Game Over!",
        _                                => phase.ToString()
    };
}
