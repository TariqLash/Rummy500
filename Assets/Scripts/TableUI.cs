using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    private Card _lastDrawnCard  = null;
    private TextMeshProUGUI _deckCountLabel;

    // Relic pick buttons (shown in overlay when human wins a round)
    private List<GameObject> _relicChoiceButtons = new List<GameObject>();

    private Image _discardDropImage;
    private Image _meldDropImage;
    private Image _overlayImage;

    private Image _sortRankBtnImg;
    private Image _sortSuitBtnImg;

    private static readonly Color SortOnColor  = new Color(0.18f, 0.55f, 0.90f, 0.95f);
    private static readonly Color SortOffColor = new Color(0.15f, 0.15f, 0.18f, 0.88f);

    // Drop zone tints — very low opacity so they're felt rather than seen
    private static readonly Color DiscardRest  = new Color(1f, 0.2f, 0.2f, 0.07f);
    private static readonly Color DiscardHover = new Color(1f, 0.2f, 0.2f, 0.18f);
    private static readonly Color MeldRest     = new Color(0.2f, 0.9f, 0.3f, 0.07f);
    private static readonly Color MeldHover    = new Color(0.2f, 0.9f, 0.3f, 0.18f);

    // Per-meld-row hit rects + subtle hover overlays
    private List<RectTransform> _meldRowRTs     = new List<RectTransform>();
    private List<Image>         _meldRowOverlays = new List<Image>();

    // Meld row cache: reuse row GOs across Refresh() to keep IdleTilt running smoothly
    private Dictionary<Meld, (GameObject rowGO, List<RectTransform> tileRTs, List<RectTransform> barRTs)> _meldRowCache
        = new Dictionary<Meld, (GameObject rowGO, List<RectTransform> tileRTs, List<RectTransform> barRTs)>();

    private static readonly Color MeldRowRest      = new Color(0f, 0f, 0f, 0f);
    private static readonly Color MeldRowExtendable = new Color(0.25f, 0.90f, 0.50f, 0.18f);

    void Update()
    {
        if (handView == null || handView.SelectedIndices.Count == 0) return;
        if (handView.CurrentDragCards != null) return; // mid-drag — don't clear

        if (Input.GetMouseButtonDown(0))
        {
            var pointerData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, hits);
            bool hitHandCard = hits.Any(r => r.gameObject.transform.IsChildOf(handView.transform));
            if (!hitHandCard)
                handView.ClearSelection();
        }
    }

    void Start()
    {
        if (drawPileCardBack) drawPileCardBack.onClick.AddListener(OnDrawPile);
        if (overlayButton)    overlayButton.onClick.AddListener(OnOverlayClicked);
        if (handView)
        {
            handView.OnCardsDroppedExternal += OnHandCardsDropped;
            handView.OnDragStateChanged     += OnHandDragStateChanged;
            handView.OnDragMoved            += OnHandDragMoved;
            handView.OnSortStateChanged += _ => UpdateSortButtonVisual();

            // Card size + hand position
            handView.cardWidth      = 130f;
            handView.cardHeight     = 182f;
            handView.cardSpacingMax = 135f;
            handView.cardSpacingMin = 48f;
            var handRT = handView.GetComponent<RectTransform>();
            if (handRT) handRT.anchoredPosition = new Vector2(0f, 200f);
        }

        // Style draw pile button to look like a face-down card
        if (drawPileCardBack && handView)
        {
            var rt = drawPileCardBack.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(handView.cardWidth, handView.cardHeight);

            var img = drawPileCardBack.GetComponent<Image>();
            img.sprite = CardView.FillSprite;
            img.type   = Image.Type.Sliced;
            img.color  = new Color(0.13f, 0.20f, 0.42f, 1f); // navy card back

            // Count label — centered on the card face
            var countGO = new GameObject("DeckCount", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGO.transform.SetParent(drawPileCardBack.transform, false);
            var countRT = countGO.GetComponent<RectTransform>();
            countRT.anchorMin = new Vector2(1f, 0f);
            countRT.anchorMax = new Vector2(1f, 0f);
            countRT.pivot     = new Vector2(1f, 0f);
            countRT.offsetMin = Vector2.zero;
            countRT.offsetMax = Vector2.zero;
            countRT.anchoredPosition = new Vector2(-6f, 6f);
            _deckCountLabel = countGO.GetComponent<TextMeshProUGUI>();
            _deckCountLabel.alignment     = TextAlignmentOptions.BottomRight;
            _deckCountLabel.fontSize      = 14;
            _deckCountLabel.color         = new Color(1f, 1f, 1f, 0.45f);
            _deckCountLabel.raycastTarget = false;

            // Add border overlay
            var borderGO = new GameObject("CardBorder", typeof(RectTransform), typeof(Image));
            borderGO.transform.SetParent(drawPileCardBack.transform, false);
            var brt = borderGO.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bImg = borderGO.GetComponent<Image>();
            bImg.sprite        = CardView.OutlineSprite;
            bImg.type          = Image.Type.Sliced;
            bImg.color         = new Color(0.28f, 0.28f, 0.32f, 1f);
            bImg.raycastTarget = false;
        }

        // Update table background to bright felt green at runtime
        var leftPanel = GameObject.Find("LeftPanel");
        if (leftPanel) { var img = leftPanel.GetComponent<Image>(); if (img) img.color = new Color(0.28f, 0.60f, 0.18f); }

        CreateSortButton();
        Canvas.ForceUpdateCanvases();   // ensure layout is resolved before Refresh
        SetupScoreAutoResize();
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
        if (currentPlayerText) currentPlayerText.text =
            player.PlayerId == 0 ? "Your Turn" : $"{player.DisplayName}'s Turn";

        if (warningText)
        {
            var msgs = new System.Collections.Generic.List<string>();

            // RequiredMeldCard is shown via auto-selection in hand, not as text

            if (game.CursedCard != null)
            {
                bool holding = game.Players.Find(p => p.PlayerId == 0)?.Hand.Contains(game.CursedCard) ?? false;
                msgs.Add(holding
                    ? $"You hold the cursed card! ({game.CursedCard})  –25 pts"
                    : $"Cursed: {game.CursedCard}  (–25 pts if held at round end)");
            }

            if (game.ActiveModifiers.Contains(MatchModifier.SpeedRound))
            {
                int left = GameState.SpeedRoundTurnLimit - game.TurnCount;
                msgs.Add($"Speed Round: {left} turn{(left == 1 ? "" : "s")} remaining");
            }

            warningText.text = string.Join("\n", msgs);
            warningText.gameObject.SetActive(msgs.Count > 0);
        }

        if (drawPileCountText) drawPileCountText.text = $"{game.Deck.DrawPileCount}";
        if (_deckCountLabel)   _deckCountLabel.text   = $"{game.Deck.DrawPileCount}/52";

        bool isHumanTurn = player.PlayerId == 0;
        if (drawPileCardBack)
            drawPileCardBack.interactable = isHumanTurn && game.Phase == GamePhase.PlayerTurn_Draw;

        RenderDiscardPile(game);

        var humanPlayer = game.Players.Find(p => p.PlayerId == 0);
        if (handView)
        {
            handView.CanDrag = isHumanTurn && game.Phase == GamePhase.PlayerTurn_MeldDiscard;
            handView.RenderHand(humanPlayer?.Hand, _lastDrawnCard);
            if (game.RequiredMeldCard != null)
                handView.AutoSelectCard(game.RequiredMeldCard);
        }
        _lastDrawnCard = null;

        RenderTableMelds(game);
        RenderScores(game);
        UpdateSortButtonVisual();

        bool showOverlay = game.Phase == GamePhase.GameOver;
        if (gameOverOverlay) gameOverOverlay.SetActive(showOverlay);

        // Always rebuild the relic picker (Refresh() is the single source of truth)
        ClearRelicPicker();

        if (showOverlay)
        {
            var run        = GameManager.Instance?.RunState;
            int humanScore = run?.GetScore(0) ?? 0;
            int oppScore   = run?.GetScore(1) ?? 0;

            bool relicPickPending = run != null && run.PendingRelicWinnerId == 0;
            bool humanWonRound    = GameManager.Instance.LastMatchWonByHuman;

            // Round delta: how much the human scored this round
            var human      = game.Players.Find(p => p.PlayerId == 0);
            int roundDelta = human != null ? human.Score - human.ScoreBeforeRound : 0;
            string deltaStr = roundDelta >= 0 ? $"+{roundDelta}" : $"{roundDelta}";

            // overlayButton IS the overlay panel — never call SetActive on it here;
            // overlay visibility is fully controlled by gameOverOverlay.SetActive above.
            // OnOverlayClicked guards against advancing while a relic pick is pending.

            if (relicPickPending)
            {
                // Human won — show relic picker; clicking overlay background does nothing (guarded)
                if (overlayText) overlayText.text = $"You won the round! ({deltaStr} pts)\nPick a relic:";
                if (_overlayImage) _overlayImage.color = new Color(0.08f, 0.22f, 0.10f, 0.92f); // dark green
                ShowRelicPicker(run.PendingRelicChoices);
            }
            else
            {
                if (run != null && run.IsRunComplete)
                {
                    bool humanWonRun = humanScore >= RunState.ScoreTarget
                                   || (humanScore > oppScore && oppScore < RunState.ScoreTarget);
                    if (humanWonRun)
                    {
                        if (_overlayImage) _overlayImage.color = new Color(0.55f, 0.42f, 0.05f, 0.95f); // gold
                        if (overlayText)  overlayText.text = $"YOU WIN!\n{humanScore} — {oppScore}\n\nTap to start a new run";
                    }
                    else
                    {
                        if (_overlayImage) _overlayImage.color = new Color(0.10f, 0.05f, 0.05f, 0.95f); // dark
                        if (overlayText)  overlayText.text = $"Opponent wins!\n{humanScore} — {oppScore}\n\nTap to start a new run";
                    }
                }
                else if (humanWonRound)
                {
                    // Won the round but already have 3 relics
                    if (_overlayImage) _overlayImage.color = new Color(0.08f, 0.22f, 0.10f, 0.92f); // dark green
                    if (overlayText)  overlayText.text = $"Round won! {deltaStr} pts\n{humanScore} / {RunState.ScoreTarget}\n\nTap for next round";
                }
                else
                {
                    // Lost the round
                    if (_overlayImage) _overlayImage.color = new Color(0.28f, 0.05f, 0.05f, 0.95f); // dark red
                    if (overlayText)  overlayText.text = $"Round lost! {deltaStr} pts\n{humanScore} / {RunState.ScoreTarget}\n\nTap for next round";
                }
            }
        }
    }

    // --- Render helpers ---

    void RenderDiscardPile(GameState game)
    {
        if (!discardContainer) return;

        var layout = discardContainer.GetComponent<HorizontalLayoutGroup>();
        if (layout) Destroy(layout);

        var pile = game.Deck.DiscardPile;

        float cardW  = handView ? handView.cardWidth  : 145f;
        float cardH  = handView ? handView.cardHeight : 205f;

        // Use rect.width (actual rendered size) — sizeDelta is 0 on stretch-anchored containers.
        var contRT = (RectTransform)discardContainer;
        float contW  = contRT.rect.width > 50f ? contRT.rect.width : 800f;

        // Spacing shrinks as more cards are added so the pile always fits within the container.
        int   count      = pile.Count;
        float maxSpacing = 55f;
        float minSpacing = 15f;
        float spacing    = count <= 1
            ? 0f
            : Mathf.Clamp((contW - cardW) / (count - 1), minSpacing, maxSpacing);

        float startX = -contW / 2f + cardW / 2f;

        bool canDraw = game.Phase == GamePhase.PlayerTurn_Draw && game.CurrentPlayer.PlayerId == 0;

        // Build existing Card→CardView map to reuse views across refreshes
        var existingMap = new Dictionary<Card, CardView>();
        foreach (var cv in _discardViews)
            if (cv != null && cv.Card != null && !existingMap.ContainsKey(cv.Card))
                existingMap[cv.Card] = cv;

        var newViews  = new List<CardView>(count);
        var usedCards = new HashSet<Card>();

        for (int i = 0; i < pile.Count; i++)
        {
            var card   = pile[i];
            float x    = startX + i * spacing;
            var target = new Vector3(x, 0, 0);

            CardView cv;
            if (existingMap.TryGetValue(card, out cv))
            {
                // Reuse existing view — animate smoothly to new position
                usedCards.Add(card);
                cv.AnimateTo(target);
                cv.OnCardClicked = null;
            }
            else
            {
                // New card — create a fresh view
                var go = Instantiate(cardPrefab, discardContainer);
                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta        = new Vector2(cardW, cardH);
                rt.anchoredPosition = new Vector2(x, 0);
                cv = go.GetComponent<CardView>();
                cv.Setup(card);
                cv.SetBasePosition(target);
                cv.HoverEnabled = false;
            }

            if (canDraw)
            {
                int capturedIndex = i;
                cv.OnCardClicked += _ => OnDiscardClicked(capturedIndex);
            }

            cv.transform.SetSiblingIndex(i);
            newViews.Add(cv);
        }

        // Destroy views for cards that are no longer in the pile
        foreach (var kv in existingMap)
            if (!usedCards.Contains(kv.Key) && kv.Value != null)
                Destroy(kv.Value.gameObject);

        _discardViews = newViews;

        // Position discard drop zone — centered over full pile container
        if (discardDropZone && discardContainer)
        {
            float dzW  = contW * 0.9f;
            float dzX  = -contW / 2f + dzW / 2f - 10f;
            discardDropZone.position  = contRT.TransformPoint(new Vector3(dzX, 0f, 0f));
            discardDropZone.sizeDelta = new Vector2(dzW, cardH + 20f);
        }
    }

    void RenderTableMelds(GameState game)
    {
        if (!meldsContainer) return;

        string[] rankLabels  = {"","A","2","3","4","5","6","7","8","9","10","J","Q","K"};
        string[] suitSymbols = {"♠","♥","♦","♣"};
        Color[]  playerColors =
        {
            new Color(0.25f, 0.55f, 0.95f, 1f),
            new Color(0.95f, 0.50f, 0.15f, 1f),
            new Color(0.75f, 0.25f, 0.90f, 1f),
            new Color(0.25f, 0.80f, 0.40f, 1f),
        };

        const float meldCardW = 44f, meldCardH = 62f, meldGap = 7f, meldPadding = 6f;
        float meldStep = meldCardW + meldGap;

        // Destroy rows for melds that are no longer on the table
        var activeMelds = new HashSet<Meld>(game.TableMelds);
        var stale = new List<Meld>();
        foreach (var key in _meldRowCache.Keys)
            if (!activeMelds.Contains(key)) stale.Add(key);
        foreach (var key in stale)
        {
            Destroy(_meldRowCache[key].rowGO);
            _meldRowCache.Remove(key);
        }

        _meldRowRTs.Clear();
        _meldRowOverlays.Clear();

        for (int m = 0; m < game.TableMelds.Count; m++)
        {
            var   meld     = game.TableMelds[m];
            float contentW = meldCardW + (meld.Cards.Count - 1) * meldStep;
            float startX   = -contentW / 2f + meldCardW / 2f;

            GameObject          rowGO;
            List<RectTransform> tileRTs;
            List<RectTransform> barRTs;

            if (_meldRowCache.TryGetValue(meld, out var cached))
            {
                rowGO   = cached.rowGO;
                tileRTs = cached.tileRTs;
                barRTs  = cached.barRTs;

                var rowRT = rowGO.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(contentW + 2 * meldPadding, meldCardH + 2 * meldPadding);

                // Reposition all existing bars and tiles (startX shifts when meld grows)
                // Also resize bars: only the last bar omits the gap extension
                for (int i = 0; i < barRTs.Count; i++)
                {
                    if (!barRTs[i]) continue;
                    bool isLast = (i == meld.Cards.Count - 1);
                    float bW = isLast ? meldCardW : meldStep;
                    float bX = isLast ? startX + i * meldStep : startX + i * meldStep + meldGap * 0.5f;
                    barRTs[i].anchoredPosition = new Vector2(bX, 0f);
                    barRTs[i].sizeDelta        = new Vector2(bW, barRTs[i].sizeDelta.y);
                }
                for (int i = 0; i < tileRTs.Count; i++)
                    if (tileRTs[i]) tileRTs[i].anchoredPosition = new Vector2(startX + i * meldStep, 0f);

                // Add bar + tile for each newly extended card
                for (int i = tileRTs.Count; i < meld.Cards.Count; i++)
                {
                    int ownerId = i < meld.CardOwners.Count ? meld.CardOwners[i] : meld.OwnerId;
                    bool isLast = (i == meld.Cards.Count - 1);
                    float bW = isLast ? meldCardW : meldStep;
                    float bX = isLast ? startX + i * meldStep : startX + i * meldStep + meldGap * 0.5f;
                    barRTs.Add(CreateColorBar(rowGO.transform, ownerId, barRTs.Count,
                                              bX, bW, playerColors));
                    tileRTs.Add(CreateMeldTile(rowGO.transform, meld.Cards[i], i, startX, meldStep,
                                               meldCardW, meldCardH, rankLabels, suitSymbols));
                }

                _meldRowCache[meld] = (rowGO, tileRTs, barRTs);
            }
            else
            {
                // Brand-new meld — create a fresh row
                rowGO = new GameObject($"Meld_{m}", typeof(RectTransform), typeof(Image));
                rowGO.transform.SetParent(meldsContainer, false);
                rowGO.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 0.55f);
                var rowRT = rowGO.GetComponent<RectTransform>();
                rowRT.sizeDelta = new Vector2(contentW + 2 * meldPadding, meldCardH + 2 * meldPadding);

                // Hover overlay (index 0)
                var overlayGO = new GameObject("HoverOverlay", typeof(RectTransform), typeof(Image));
                overlayGO.transform.SetParent(rowGO.transform, false);
                var overlayRT = overlayGO.GetComponent<RectTransform>();
                overlayRT.anchorMin = Vector2.zero; overlayRT.anchorMax = Vector2.one;
                overlayRT.offsetMin = Vector2.zero; overlayRT.offsetMax = Vector2.zero;
                var overlayImg = overlayGO.GetComponent<Image>();
                overlayImg.color         = MeldRowRest;
                overlayImg.raycastTarget = false;

                // Pass 1: color bars — added first so they render behind tiles
                barRTs  = new List<RectTransform>();
                for (int i = 0; i < meld.Cards.Count; i++)
                {
                    int ownerId = i < meld.CardOwners.Count ? meld.CardOwners[i] : meld.OwnerId;
                    bool isLast = (i == meld.Cards.Count - 1);
                    float bW = isLast ? meldCardW : meldStep;
                    float bX = isLast ? startX + i * meldStep : startX + i * meldStep + meldGap * 0.5f;
                    barRTs.Add(CreateColorBar(rowGO.transform, ownerId, barRTs.Count, bX, bW, playerColors));
                }

                // Pass 2: tiles on top
                tileRTs = new List<RectTransform>();
                for (int i = 0; i < meld.Cards.Count; i++)
                {
                    tileRTs.Add(CreateMeldTile(rowGO.transform, meld.Cards[i], i, startX, meldStep,
                                               meldCardW, meldCardH, rankLabels, suitSymbols));
                }

                _meldRowCache[meld] = (rowGO, tileRTs, barRTs);
            }

            rowGO.transform.SetSiblingIndex(m);

            _meldRowRTs.Add(rowGO.GetComponent<RectTransform>());
            _meldRowOverlays.Add(rowGO.transform.Find("HoverOverlay")?.GetComponent<Image>());
        }
    }

    RectTransform CreateColorBar(Transform rowParent, int ownerIdx, int existingCount,
        float barX, float barW, Color[] playerColors)
    {
        var bar    = new GameObject("ColorBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(rowParent, false);
        var barRT  = bar.GetComponent<RectTransform>();
        barRT.sizeDelta        = new Vector2(barW, 5f);
        barRT.anchoredPosition = new Vector2(barX, 0f);
        var barImg = bar.GetComponent<Image>();
        barImg.color         = playerColors[ownerIdx % playerColors.Length];
        barImg.raycastTarget = false;
        // Slot this bar after HoverOverlay + existing bars, just before the first tile
        bar.transform.SetSiblingIndex(existingCount + 1);
        return barRT;
    }

    RectTransform CreateMeldTile(Transform parent, Card card, int index, float startX, float meldStep,
        float meldCardW, float meldCardH, string[] rankLabels, string[] suitSymbols)
    {
        var miniSprite = CardView.GetMiniSprite(card);

        var tile    = new GameObject($"Card_{index}", typeof(RectTransform), typeof(Image));
        tile.transform.SetParent(parent, false);
        var tileRT  = tile.GetComponent<RectTransform>();
        var tileImg = tile.GetComponent<Image>();
        tileRT.sizeDelta        = new Vector2(meldCardW, meldCardH);
        tileRT.anchoredPosition = new Vector2(startX + index * meldStep, 0);

        if (miniSprite != null)
        {
            tileImg.sprite = miniSprite;
            tileImg.type   = Image.Type.Simple;
            tileImg.color  = Color.white;
        }
        else
        {
            tileImg.color = new Color(0.97f, 0.97f, 0.96f);
            bool isRed    = card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;
            var lbl       = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lbl.transform.SetParent(tile.transform, false);
            var lblRT  = lbl.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(2, 1); lblRT.offsetMax = new Vector2(-2, -1);
            var lblTMP = lbl.GetComponent<TextMeshProUGUI>();
            lblTMP.text          = rankLabels[(int)card.Rank] + suitSymbols[(int)card.Suit];
            lblTMP.fontSize      = 18;
            lblTMP.color         = isRed ? new Color(0.85f, 0.15f, 0.15f) : new Color(0.1f, 0.1f, 0.1f);
            lblTMP.alignment     = TextAlignmentOptions.Center;
            lblTMP.raycastTarget = false;
        }

        // Continuous idle tilt animation
        tile.AddComponent<IdleTilt>();

        return tileRT;
    }

    void RenderScores(GameState game)
    {
        if (!scoreContainer) return;
        foreach (Transform child in scoreContainer) Destroy(child.gameObject);

        var run = GameManager.Instance?.RunState;

        // Run HUD — round number
        if (run != null)
            SpawnScoreRow($"Round {run.RoundNumber}", new Color(1f, 0.85f, 0.35f));

        foreach (var p in game.Players)
        {
            // During GameOver the cumulative already includes this round (banked in OnMatchOver).
            // During active play add in-progress round score to show running total.
            int banked = run != null ? run.GetScore(p.PlayerId) : 0;
            int total  = game.Phase == GamePhase.GameOver ? banked : banked + p.Score;
            SpawnScoreRow($"{p.DisplayName}: {total} / {RunState.ScoreTarget}", Color.white);

            // Show each relic with its description
            var relics = run?.GetRelics(p.PlayerId);
            if (relics != null)
                foreach (var r in relics)
                {
                    var def = RelicPool.All[r];
                    SpawnScoreRow($"  {def.Name} — {def.Description}", new Color(0.80f, 0.70f, 1.00f));
                }
        }

        // Push the melds container down to clear whatever height the info panel now needs
        SyncMeldsTop();
    }

    // --- Info-panel / melds-container auto-resize ---

    // Adds ContentSizeFitters so the info panel grows with its content (relic rows).
    void SetupScoreAutoResize()
    {
        if (!scoreContainer) return;

        // Let the score container grow to fit however many rows are spawned
        var scoreCsf = scoreContainer.GetComponent<ContentSizeFitter>()
                    ?? scoreContainer.gameObject.AddComponent<ContentSizeFitter>();
        scoreCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Let the whole info panel (parent) grow to contain the score container
        var infoPanelRT = scoreContainer.transform.parent?.GetComponent<RectTransform>();
        if (!infoPanelRT) return;
        var infoCsf = infoPanelRT.GetComponent<ContentSizeFitter>()
                   ?? infoPanelRT.gameObject.AddComponent<ContentSizeFitter>();
        infoCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // Repositions the melds container so it always starts directly below the info panel.
    void SyncMeldsTop()
    {
        if (!meldsContainer || !scoreContainer) return;
        var infoPanelRT = scoreContainer.transform.parent?.GetComponent<RectTransform>();
        if (!infoPanelRT) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanelRT);
        float h = infoPanelRT.sizeDelta.y;
        if (h < 10f) return;

        var meldsRT = meldsContainer.GetComponent<RectTransform>();
        if (meldsRT)
            meldsRT.offsetMax = new Vector2(meldsRT.offsetMax.x, -h);
    }

    void SpawnScoreRow(string text, Color color)
    {
        if (scoreRowPrefab)
        {
            var row = Instantiate(scoreRowPrefab, scoreContainer);
            var t   = row.GetComponentInChildren<TextMeshProUGUI>();
            if (t) { t.text = text; t.color = color; }
        }
        else
        {
            var row = new GameObject("ScoreRow", typeof(RectTransform), typeof(TextMeshProUGUI));
            row.transform.SetParent(scoreContainer, false);
            var t   = row.GetComponent<TextMeshProUGUI>();
            t.text     = text;
            t.fontSize = 16;
            t.color    = color;
        }
    }

    // --- Drop zone setup ---

    void InitDropZones()
    {
        if (gameOverOverlay)
        {
            _overlayImage = gameOverOverlay.GetComponent<Image>();
            if (!_overlayImage) _overlayImage = gameOverOverlay.AddComponent<Image>();
        }

        if (discardDropZone)
        {
            _discardDropImage = discardDropZone.GetComponent<Image>();
            if (!_discardDropImage) _discardDropImage = discardDropZone.gameObject.AddComponent<Image>();
            _discardDropImage.sprite        = CardView.FillSprite;
            _discardDropImage.type          = Image.Type.Sliced;
            _discardDropImage.color         = DiscardRest;
            _discardDropImage.raycastTarget = false;
            discardDropZone.gameObject.SetActive(false);
        }

        if (meldDropZone)
        {
            _meldDropImage = meldDropZone.GetComponent<Image>();
            if (!_meldDropImage) _meldDropImage = meldDropZone.gameObject.AddComponent<Image>();
            _meldDropImage.color         = MeldRest;
            _meldDropImage.raycastTarget = false;
            meldDropZone.gameObject.SetActive(false);
        }
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

        if (!dragging)
        {
            if (_discardDropImage) _discardDropImage.color = DiscardRest;
            if (_meldDropImage)    _meldDropImage.color    = MeldRest;
            foreach (var img in _meldRowOverlays) if (img) img.color = MeldRowRest;
        }
    }

    void OnHandDragMoved(Vector2 screenPos)
    {
        if (_discardDropImage && discardDropZone)
            _discardDropImage.color = RectTransformUtility.RectangleContainsScreenPoint(
                discardDropZone, screenPos, null) ? DiscardHover : DiscardRest;

        if (_meldDropImage && meldDropZone)
            _meldDropImage.color = RectTransformUtility.RectangleContainsScreenPoint(
                meldDropZone, screenPos, null) ? MeldHover : MeldRest;

        // Subtly highlight meld rows the dragged cards can extend
        var game      = GameManager.Instance?.Game;
        var dragCards = handView ? handView.CurrentDragCards : null;
        for (int m = 0; m < _meldRowRTs.Count && m < _meldRowOverlays.Count; m++)
        {
            bool hovering  = RectTransformUtility.RectangleContainsScreenPoint(_meldRowRTs[m], screenPos, null);
            bool canExtend = dragCards != null && dragCards.Count > 0
                             && game != null && m < game.TableMelds.Count
                             && game.TableMelds[m].CanExtend(dragCards);
            _meldRowOverlays[m].color = (hovering && canExtend) ? MeldRowExtendable : MeldRowRest;
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
            // Try to lay a new meld (requires 3+ cards)
            if (cards.Count >= 3 && game.TryLayMeld(0, cards))
            {
                handView.ClearSelection();
                Refresh();
                return;
            }
            // Try to extend any existing meld (works with 1+ cards)
            for (int m = 0; m < game.TableMelds.Count; m++)
            {
                if (game.TryExtendMeld(0, m, cards))
                {
                    handView.ClearSelection();
                    Refresh();
                    return;
                }
            }
            ShowWarning(cards.Count < 3
                ? "Need 3+ cards to lay a new meld, or drag onto a meld to extend it."
                : "Not a valid meld.");
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
        var run  = GameManager.Instance.RunState;

        // Don't advance while a relic pick is pending for the human
        if (run != null && run.PendingRelicWinnerId == 0) return;

        if (game.Phase == GamePhase.GameOver)
        {
            if (run == null || run.IsRunComplete)
                GameManager.Instance.StartRun();
            else
                GameManager.Instance.AdvanceToNextMatch();
        }

        Refresh();
    }

    // --- Relic picker ---

    void ShowRelicPicker(List<RelicId> choices)
    {
        if (!gameOverOverlay || choices == null) return;

        float btnW = 340f, btnH = 80f, gap = 12f;
        float totalH = choices.Count * btnH + (choices.Count - 1) * gap;
        float startY = totalH / 2f - btnH / 2f;

        for (int i = 0; i < choices.Count; i++)
        {
            var relicId = choices[i];
            var def     = RelicPool.All[relicId];

            var go = new GameObject($"RelicBtn_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(gameOverOverlay.transform, false);
            _relicChoiceButtons.Add(go);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.sizeDelta        = new Vector2(btnW, btnH);
            rt.anchoredPosition = new Vector2(0f, startY - i * (btnH + gap) - 60f);

            var img = go.GetComponent<Image>();
            img.sprite = MakeRoundedSprite(40, 40, 8);
            img.type   = Image.Type.Sliced;
            img.color  = new Color(0.18f, 0.12f, 0.30f, 0.95f); // dark purple

            // Name label (top of button)
            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(go.transform, false);
            var nameRT = nameGO.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0.5f); nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = new Vector2(10f, 2f);  nameRT.offsetMax = new Vector2(-10f, -4f);
            var nameTMP = nameGO.GetComponent<TextMeshProUGUI>();
            nameTMP.text          = def.Name;
            nameTMP.fontSize      = 17;
            nameTMP.fontStyle     = FontStyles.Bold;
            nameTMP.color         = new Color(0.85f, 0.70f, 1.00f);
            nameTMP.alignment     = TextAlignmentOptions.Left;
            nameTMP.raycastTarget = false;

            // Description label (bottom of button)
            var descGO = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGO.transform.SetParent(go.transform, false);
            var descRT = descGO.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0f, 0f); descRT.anchorMax = new Vector2(1f, 0.5f);
            descRT.offsetMin = new Vector2(10f, 4f); descRT.offsetMax = new Vector2(-10f, -2f);
            var descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.text          = def.Description;
            descTMP.fontSize      = 13;
            descTMP.color         = new Color(0.85f, 0.85f, 0.85f);
            descTMP.alignment     = TextAlignmentOptions.Left;
            descTMP.raycastTarget = false;

            var capturedId = relicId;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                GameManager.Instance.AcceptRelicPick(capturedId);
                Refresh();
            });
        }
    }

    void ClearRelicPicker()
    {
        foreach (var go in _relicChoiceButtons)
            if (go) Destroy(go);
        _relicChoiceButtons.Clear();
    }

    void ShowWarning(string msg)
    {
        if (warningText) { warningText.text = msg; warningText.gameObject.SetActive(true); }
        Debug.LogWarning(msg);
    }

    void CreateSortButton()
    {
        if (!handView) return;

        var parent = handView.transform.parent;
        _sortRankBtnImg = MakeSortBtn(parent, "Rank", -55f, () => handView.ToggleSortByRank());
        _sortSuitBtnImg = MakeSortBtn(parent, "Suit",  55f, () => handView.ToggleSortBySuit());
    }

    Image MakeSortBtn(Transform parent, string label, float offsetX, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(offsetX, 20f);
        rt.sizeDelta        = new Vector2(105f, 40f);

        var img = go.GetComponent<Image>();
        img.sprite = MakeRoundedSprite(40, 40, 8);
        img.type   = Image.Type.Sliced;
        img.color  = SortOffColor;

        go.GetComponent<Button>().onClick.AddListener(() => { onClick(); UpdateSortButtonVisual(); });

        var lblGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        lblGO.transform.SetParent(go.transform, false);
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblGO.GetComponent<TextMeshProUGUI>();
        lbl.text          = label;
        lbl.fontSize      = 13;
        lbl.color         = Color.white;
        lbl.alignment     = TextAlignmentOptions.Center;
        lbl.fontStyle     = FontStyles.Bold;
        lbl.raycastTarget = false;

        return img;
    }

    void UpdateSortButtonVisual()
    {
        if (!handView) return;
        var mode = handView.CurrentSortMode;
        if (_sortRankBtnImg) _sortRankBtnImg.color = mode == SortMode.ByRank ? SortOnColor : SortOffColor;
        if (_sortSuitBtnImg) _sortSuitBtnImg.color = mode == SortMode.BySuit ? SortOnColor : SortOffColor;
    }

    static Sprite MakeRoundedSprite(int w, int h, int r)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var buf = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float qx = Mathf.Max(0, Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - r));
            float qy = Mathf.Max(0, Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - r));
            buf[y * w + x] = new Color32(255, 255, 255, (byte)(qx * qx + qy * qy <= r * r ? 255 : 0));
        }
        tex.SetPixels32(buf);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    string PhaseFriendly(GamePhase phase) => phase switch
    {
        GamePhase.PlayerTurn_Draw        => "Draw a card",
        GamePhase.PlayerTurn_MeldDiscard => "Meld or Discard",

        GamePhase.GameOver               => "Game Over!",
        _                                => phase.ToString()
    };
}
