using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rummy500.Core;
using System;

/// <summary>
/// Visual representation of a single card.
/// Attach to the Card prefab (a UI Panel with child Text elements).
/// </summary>
public class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    public TextMeshProUGUI topRankText;
    public TextMeshProUGUI topSuitText;
    public TextMeshProUGUI centerSuitText;
    public TextMeshProUGUI bottomRankText;
    public TextMeshProUGUI bottomSuitText;
    public Image cardBackground;
    public Image selectionBorder;

    [Header("Colors")]
    public Color redSuitColor = new Color(0.85f, 0.15f, 0.15f);
    public Color blackSuitColor = new Color(0.1f, 0.1f, 0.1f);
    public Color selectedColor = new Color(0.2f, 0.75f, 0.4f);
    public Color hoverColor = new Color(0.95f, 0.95f, 1f);
    public Color normalColor = Color.white;

    // The card data this view represents
    public Card Card { get; private set; }
    public bool IsSelected { get; private set; }

    // Events
    public Action<CardView> OnCardClicked;
    public Action<CardView, PointerEventData> OnBeginDragCard;
    public Action<CardView, PointerEventData> OnDragCard;
    public Action<CardView, PointerEventData> OnEndDragCard;

    public Vector3 BasePosition => _basePosition;

    private Vector3 _basePosition;
    private bool _isHovered;
    private bool _isHighlighted;
    private RectTransform _rect;
    private static readonly Color HighlightColor = new Color(1f, 0.94f, 0.55f);

    // Suit unicode symbols
    static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    static readonly string[] RankLabels =
    {
        "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"
    };

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void Setup(Card card)
    {
        Card = card;
        IsSelected = false;
        UpdateVisuals();
    }


public void SetSelected(bool selected)
{
    if (_rect == null) _rect = GetComponent<RectTransform>();
    IsSelected = selected;
    if (selectionBorder != null)
        selectionBorder.color = selected ? selectedColor : Color.clear;
    if (!selected && cardBackground)
        cardBackground.color = _isHighlighted ? HighlightColor : normalColor;

    var pos = _basePosition;
    if (selected) pos.y += 30f;
    _rect.localPosition = pos;
}

public void SetHighlighted(bool highlighted)
{
    _isHighlighted = highlighted;
    if (!IsSelected && cardBackground)
        cardBackground.color = highlighted ? HighlightColor : normalColor;
}

public void SetBasePosition(Vector3 pos)
{
    if (_rect == null) _rect = GetComponent<RectTransform>();
    _basePosition = pos;
    _rect.localPosition = pos;
}

    void UpdateVisuals()
    {
        if (Card == null) return;

        bool isRed = Card.Suit == Suit.Hearts || Card.Suit == Suit.Diamonds;
        Color suitColor = isRed ? redSuitColor : blackSuitColor;
        string rank = RankLabels[(int)Card.Rank];
        string suit = SuitSymbols[(int)Card.Suit];

        if (topRankText) { topRankText.text = rank; topRankText.color = suitColor; }
        if (topSuitText) { topSuitText.text = suit; topSuitText.color = suitColor; }
        if (centerSuitText) { centerSuitText.text = suit; centerSuitText.color = suitColor; centerSuitText.fontSize = 24; }
        if (bottomRankText) { bottomRankText.text = rank; bottomRankText.color = suitColor; }
        if (bottomSuitText) { bottomSuitText.text = suit; bottomSuitText.color = suitColor; }

        if (cardBackground) cardBackground.color = normalColor;
        if (selectionBorder) selectionBorder.color = Color.clear;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnCardClicked?.Invoke(this);
    }

    public void OnBeginDrag(PointerEventData eventData) => OnBeginDragCard?.Invoke(this, eventData);
    public void OnDrag(PointerEventData eventData)      => OnDragCard?.Invoke(this, eventData);
    public void OnEndDrag(PointerEventData eventData)   => OnEndDragCard?.Invoke(this, eventData);

public void OnPointerEnter(PointerEventData eventData)
{
    if (!IsSelected && cardBackground)
        cardBackground.color = hoverColor;
}

public void OnPointerExit(PointerEventData eventData)
{
    if (!IsSelected && cardBackground)
        cardBackground.color = _isHighlighted ? HighlightColor : normalColor;
}
}
