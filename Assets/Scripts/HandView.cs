using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Rummy500.Core;

public enum SortMode { None, ByRank, BySuit }

/// <summary>
/// Displays the player's hand as a fanned arc.
/// Click to select/deselect. Drag to reorder or drop externally.
/// Dragging a selected card moves ALL selected cards together visually.
/// </summary>
public class HandView : MonoBehaviour
{
    [Header("Layout")]
    public float cardSpacingMax = 160f;
    public float cardSpacingMin = 75f;
    public float cardWidth = 145f;
    public float cardHeight = 205f;

    [Header("Prefab")]
    public GameObject cardPrefab;

    public bool CanDrag { get; set; } = true;

    public event Action<List<CardView>, Vector2> OnCardsDroppedExternal;
    public event Action<bool>    OnDragStateChanged;
    public event Action<Vector2> OnDragMoved;
    public event Action<SortMode> OnSortStateChanged;

    private List<CardView> _cardViews = new List<CardView>();
    private List<RectTransform> _shadowRTs = new List<RectTransform>();
    private List<int> _selectedIndices = new List<int>();
    private List<Card> _currentHand;
    private SortMode _sortMode;

    private int _draggingIndex = -1;
    private int _ghostInsertIndex = -1;
    private RectTransform _draggingRT;
    private bool _draggingAllSelected;

    // All RTs being moved during drag + their offsets from the primary card
    private List<RectTransform> _draggingRTs     = new List<RectTransform>();
    private List<Vector3>       _draggingOffsets = new List<Vector3>();

    public List<int> SelectedIndices => new List<int>(_selectedIndices);
    public List<CardView> CardViews  => _cardViews;

    // Cards currently being dragged (dragged card first, then selected cards); null when not dragging
    public List<Card> CurrentDragCards { get; private set; }

    float GetCardSpacing(int count)
    {
        if (count <= 1) return cardSpacingMax;
        // 2–10 cards: comfortable compression down to cardSpacingMin
        if (count <= 10)
            return Mathf.Lerp(cardSpacingMin, cardSpacingMax, (10f - count) / 8f);
        // 10–20 cards: aggressive compression down to 28px (just rank + suit visible)
        return Mathf.Lerp(28f, cardSpacingMin, Mathf.Clamp01((20f - count) / 10f));
    }

    Vector3 GetSlotPosition(int slot, int totalCount, float spacing)
    {
        float startX = -(totalCount - 1) * spacing / 2f;
        float t      = totalCount > 1 ? (slot - (totalCount - 1) / 2f) / ((totalCount - 1) / 2f) : 0f;
        float archY  = -(t * t) * 40f;   // parabolic drop — edges fall ~40px below centre
        return new Vector3(startX + slot * spacing, archY, 0);
    }

    Quaternion GetSlotRotation(int slot, int totalCount)
    {
        if (totalCount <= 1) return Quaternion.identity;
        float t     = (slot - (totalCount - 1) / 2f) / Mathf.Max((totalCount - 1) / 2f, 1f); // -1 to 1
        float angle = -t * Mathf.Lerp(8f, 18f, Mathf.Abs(t)); // gentle in centre, stronger at edges
        return Quaternion.Euler(0f, 0f, angle);
    }

    RectTransform CreateShadow(Vector3 slotPos, Quaternion slotRot)
    {
        var go = new GameObject("Shadow", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(cardWidth + 6f, cardHeight + 6f);
        rt.localPosition = slotPos + new Vector3(0, -6f, 0);
        rt.localRotation = slotRot;
        var img = go.GetComponent<UnityEngine.UI.Image>();
        img.sprite = CardView.FillSprite;
        img.type   = UnityEngine.UI.Image.Type.Sliced;
        img.color  = new Color(0f, 0f, 0f, 0.30f);
        img.raycastTarget = false;
        return rt;
    }

    public void RenderHand(List<Card> hand, Card highlightCard = null)
    {
        _currentHand = hand;

        if (_sortMode != SortMode.None && hand != null && hand.Count > 0)
        {
            var sorted = _sortMode == SortMode.ByRank
                ? hand.OrderBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank).ThenBy(c => (int)c.Suit).ToList()
                : hand.OrderBy(c => (int)c.Suit).ThenBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank).ToList();
            hand.Clear();
            hand.AddRange(sorted);
        }

        _draggingIndex = -1;
        _draggingRT = null;
        _draggingAllSelected = false;
        _draggingRTs.Clear();
        _draggingOffsets.Clear();

        if (hand == null || hand.Count == 0)
        {
            foreach (var cv in _cardViews) if (cv) Destroy(cv.gameObject);
            foreach (var sr in _shadowRTs)  if (sr) Destroy(sr.gameObject);
            _cardViews.Clear();
            _shadowRTs.Clear();
            _selectedIndices.Clear();
            return;
        }

        // Map currently live cards to their views/shadows so we can reuse them
        var existingViews   = new Dictionary<Card, CardView>();
        var existingShadows = new Dictionary<Card, RectTransform>();
        for (int i = 0; i < _cardViews.Count; i++)
        {
            var cv = _cardViews[i];
            if (cv != null && cv.Card != null && !existingViews.ContainsKey(cv.Card))
            {
                existingViews[cv.Card]   = cv;
                existingShadows[cv.Card] = i < _shadowRTs.Count ? _shadowRTs[i] : null;
            }
        }

        int count     = hand.Count;
        float spacing = GetCardSpacing(count);

        var newCardViews = new List<CardView>(count);
        var newShadowRTs = new List<RectTransform>(count);
        var reused       = new HashSet<Card>();

        for (int i = 0; i < count; i++)
        {
            var card    = hand[i];
            var slotPos = GetSlotPosition(i, count, spacing);
            var slotRot = GetSlotRotation(i, count);

            CardView      cv;
            RectTransform shadowRT;

            if (existingViews.TryGetValue(card, out cv) && !reused.Contains(card))
            {
                // Card still in hand — reuse the view, tilt/idle state intact
                reused.Add(card);
                existingShadows.TryGetValue(card, out shadowRT);
                if (shadowRT == null) shadowRT = CreateShadow(slotPos, slotRot);

                cv.SetSelected(false);
                cv.AnimateTo(slotPos);
                cv.SetBaseRotation(slotRot);
                cv.SetHighlighted(highlightCard != null && card == highlightCard);
            }
            else
            {
                // New card — create fresh view and shadow
                shadowRT = CreateShadow(slotPos, slotRot);

                var go = Instantiate(cardPrefab, transform);
                cv     = go.GetComponent<CardView>();
                var rt = go.GetComponent<RectTransform>();
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cardWidth, cardHeight);
                cv.Setup(card);
                cv.SetBasePosition(slotPos);
                rt.localRotation = slotRot;
                cv.SetBaseRotation(slotRot);

                if (highlightCard != null && card == highlightCard)
                    cv.SetHighlighted(true);
            }

            newCardViews.Add(cv);
            newShadowRTs.Add(shadowRT);
        }

        // Destroy views and shadows for cards that left the hand
        foreach (var card in existingViews.Keys)
        {
            if (reused.Contains(card)) continue;
            if (existingViews[card]) Destroy(existingViews[card].gameObject);
            if (existingShadows.TryGetValue(card, out var sr) && sr) Destroy(sr.gameObject);
        }

        _cardViews = newCardViews;
        _shadowRTs = newShadowRTs;
        _selectedIndices.Clear();

        // Re-wire events, fix interleaved sibling order, re-link shadows
        for (int i = 0; i < count; i++)
        {
            var cv = _cardViews[i];
            cv.OnCardClicked   = null;
            cv.OnBeginDragCard = null;
            cv.OnDragCard      = null;
            cv.OnEndDragCard   = null;

            int capturedIndex = i;
            cv.OnCardClicked   += _ => ToggleSelection(capturedIndex);
            cv.OnBeginDragCard += OnCardBeginDrag;
            cv.OnDragCard      += OnCardDrag;
            cv.OnEndDragCard   += OnCardEndDrag;

            if (i < _shadowRTs.Count) _shadowRTs[i].SetSiblingIndex(i * 2);
            cv.transform.SetSiblingIndex(i * 2 + 1);
            if (i < _shadowRTs.Count) cv.SetShadow(_shadowRTs[i]);
        }
    }

    void ToggleSelection(int index)
    {
        if (_selectedIndices.Contains(index))
        {
            _selectedIndices.Remove(index);
            _cardViews[index].SetSelected(false);
        }
        else
        {
            _selectedIndices.Add(index);
            _cardViews[index].SetSelected(true);
        }
    }

    public SortMode CurrentSortMode => _sortMode;

    public void ToggleSortByRank()
    {
        _sortMode = _sortMode == SortMode.ByRank ? SortMode.None : SortMode.ByRank;
        OnSortStateChanged?.Invoke(_sortMode);
        ApplySortAndRender();
    }

    public void ToggleSortBySuit()
    {
        _sortMode = _sortMode == SortMode.BySuit ? SortMode.None : SortMode.BySuit;
        OnSortStateChanged?.Invoke(_sortMode);
        ApplySortAndRender();
    }

    void ApplySortAndRender()
    {
        if (_currentHand == null || _currentHand.Count == 0) return;

        // Toggling sort off — just snap re-render, no animation needed
        if (_sortMode == SortMode.None) { RenderHand(_currentHand); return; }

        // Build card → current CardView map BEFORE sorting
        var cardToView = new Dictionary<Card, CardView>();
        for (int i = 0; i < _currentHand.Count && i < _cardViews.Count; i++)
            cardToView[_currentHand[i]] = _cardViews[i];

        // Sort _currentHand in place
        var sorted = _sortMode == SortMode.ByRank
            ? _currentHand.OrderBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank).ThenBy(c => (int)c.Suit).ToList()
            : _currentHand.OrderBy(c => (int)c.Suit).ThenBy(c => c.Rank == Rank.Ace ? 14 : (int)c.Rank).ToList();
        _currentHand.Clear();
        _currentHand.AddRange(sorted);

        // Fall back to a full re-render if counts don't match (e.g. first sort after hand change)
        if (cardToView.Count != _currentHand.Count || _currentHand.Any(c => !cardToView.ContainsKey(c)))
        { RenderHand(_currentHand); return; }

        int count = _currentHand.Count;
        float spacing = GetCardSpacing(count);

        // Remap _cardViews to the new sorted order; animate each to its new slot
        var newCardViews = new List<CardView>(count);
        for (int i = 0; i < count; i++)
        {
            var cv = cardToView[_currentHand[i]];
            newCardViews.Add(cv);
            cv.AnimateTo(GetSlotPosition(i, count, spacing));
            cv.SetBaseRotation(GetSlotRotation(i, count));
        }
        _cardViews = newCardViews;

        // Restore interleaved sibling order: shadow[i] at i*2, card[i] at i*2+1
        for (int i = 0; i < _cardViews.Count; i++)
        {
            if (i < _shadowRTs.Count) _shadowRTs[i].SetSiblingIndex(i * 2);
            _cardViews[i].transform.SetSiblingIndex(i * 2 + 1);
        }

        // Re-link each shadow to its new card — CardView.Update() will keep them in sync
        for (int i = 0; i < _cardViews.Count && i < _shadowRTs.Count; i++)
            _cardViews[i].SetShadow(_shadowRTs[i]);

        // Clear selection — indices have changed
        foreach (var idx in _selectedIndices)
            if (idx < _cardViews.Count) _cardViews[idx].SetSelected(false);
        _selectedIndices.Clear();

        // Re-wire event handlers with updated indices
        for (int i = 0; i < _cardViews.Count; i++)
        {
            var cv = _cardViews[i];
            cv.OnCardClicked   = null;
            cv.OnBeginDragCard = null;
            cv.OnDragCard      = null;
            cv.OnEndDragCard   = null;

            int capturedIndex = i;
            cv.OnCardClicked   += _ => ToggleSelection(capturedIndex);
            cv.OnBeginDragCard += OnCardBeginDrag;
            cv.OnDragCard      += OnCardDrag;
            cv.OnEndDragCard   += OnCardEndDrag;
        }
    }

    public void AutoSelectCard(Card card)
    {
        if (card == null) return;
        int idx = _currentHand != null ? _currentHand.IndexOf(card) : -1;
        if (idx >= 0 && idx < _cardViews.Count && !_selectedIndices.Contains(idx))
        {
            _selectedIndices.Add(idx);
            _cardViews[idx].SetSelected(true);
        }
    }

    public void ClearSelection()
    {
        foreach (var i in _selectedIndices)
            if (i < _cardViews.Count) _cardViews[i].SetSelected(false);
        _selectedIndices.Clear();
    }

    // --- Drag ---

    void OnCardBeginDrag(CardView cv, PointerEventData eventData)
    {
        _draggingIndex = _cardViews.IndexOf(cv);
        if (_draggingIndex < 0) return;

        // Stop any in-progress sort animations so they don't fight with drag positioning
        foreach (var c in _cardViews) c.CancelAnimation();

        // Flag dragged cards so their Update skips rotation animation
        foreach (var c in _cardViews) c.IsDragging = false;
        cv.IsDragging = true;

        _draggingRT = cv.GetComponent<RectTransform>();
        _ghostInsertIndex = _draggingIndex;

        _draggingRTs.Clear();
        _draggingOffsets.Clear();

        _draggingAllSelected = CanDrag
            && _selectedIndices.Contains(_draggingIndex)
            && _selectedIndices.Count > 1;

        if (_draggingAllSelected)
        {
            // Move all selected cards together; offsets relative to the dragged card's position
            var primaryPos = _draggingRT.localPosition;
            foreach (int idx in _selectedIndices.OrderBy(i => i))
            {
                var rt = _cardViews[idx].GetComponent<RectTransform>();
                _draggingRTs.Add(rt);
                _draggingOffsets.Add(rt.localPosition - primaryPos);
                rt.SetAsLastSibling();
                _cardViews[idx].IsDragging = true;
            }
        }
        else
        {
            _draggingRTs.Add(_draggingRT);
            _draggingOffsets.Add(Vector3.zero);
            _draggingRT.SetAsLastSibling();
        }

        // Build CurrentDragCards: dragged card first, then any other selected cards
        CurrentDragCards = new List<Card>();
        if (_draggingIndex >= 0 && _draggingIndex < _cardViews.Count)
            CurrentDragCards.Add(_cardViews[_draggingIndex].Card);
        foreach (int idx in _selectedIndices)
            if (idx != _draggingIndex && idx < _cardViews.Count)
                CurrentDragCards.Add(_cardViews[idx].Card);

        if (CanDrag)
            OnDragStateChanged?.Invoke(true);
    }

    void OnCardDrag(CardView cv, PointerEventData eventData)
    {
        if (_draggingRT == null || _draggingRTs.Count == 0) return;
        OnDragMoved?.Invoke(eventData.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 local);

        var primaryPos = new Vector3(local.x, local.y, 0);
        float tilt = Mathf.Clamp(-eventData.delta.x * 0.3f, -25f, 25f);

        for (int i = 0; i < _draggingRTs.Count; i++)
        {
            _draggingRTs[i].localPosition = primaryPos + _draggingOffsets[i];
            _draggingRTs[i].localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        // Ghost reorder only makes sense when dragging a single card
        if (_draggingRTs.Count == 1)
            UpdateGhostPositions(local.x);
    }

    void OnCardEndDrag(CardView cv, PointerEventData eventData)
    {
        if (_draggingIndex < 0 || _currentHand == null) return;

        bool canDropExternal = CanDrag;

        bool insideHand = RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera);

        bool needsRerender = true;

        if (insideHand && !_draggingAllSelected)
        {
            // Reorder single card within hand
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 local);
            int insertIdx = GetInsertionIndex(local.x, _draggingIndex);
            if (insertIdx != _draggingIndex)
            {
                _sortMode = SortMode.None;
                OnSortStateChanged?.Invoke(SortMode.None);
                var card = _currentHand[_draggingIndex];
                _currentHand.RemoveAt(_draggingIndex);
                _currentHand.Insert(insertIdx, card);
            }
        }
        else if (canDropExternal)
        {
            // Dragged card is always first; append any selected cards that aren't the dragged one.
            // This lets the player select some cards and drag an additional card to complete a meld,
            // or simply drag one selected card while the rest of the selection rides along.
            var candidates = new List<CardView> { cv };
            foreach (int idx in _selectedIndices)
                if (idx != _draggingIndex && idx < _cardViews.Count)
                    candidates.Add(_cardViews[idx]);
            OnCardsDroppedExternal?.Invoke(candidates, eventData.position);
            needsRerender = false; // Refresh() inside the handler already re-rendered
        }

        foreach (var c in _cardViews) c.IsDragging = false;
        _draggingIndex = -1;
        _draggingRT = null;
        _draggingAllSelected = false;
        _draggingRTs.Clear();
        _draggingOffsets.Clear();
        CurrentDragCards = null;
        OnDragStateChanged?.Invoke(false);

        if (needsRerender)
            RenderHand(_currentHand);
    }

    void UpdateGhostPositions(float dragX)
    {
        int count = _cardViews.Count;
        if (count < 2) return;

        int newGhost = GetInsertionIndex(dragX, _draggingIndex);
        if (newGhost == _ghostInsertIndex) return;
        _ghostInsertIndex = newGhost;

        float spacing = GetCardSpacing(count);

        var availableSlots = new List<int>(count - 1);
        for (int s = 0; s < count; s++)
            if (s != _ghostInsertIndex) availableSlots.Add(s);

        int slotPtr = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == _draggingIndex) continue;

            int slot = availableSlots[slotPtr++];
            _cardViews[i].AnimateTo(GetSlotPosition(slot, count, spacing));
            _cardViews[i].SetBaseRotation(GetSlotRotation(slot, count));
        }
    }

    int GetInsertionIndex(float dragX, int draggingIdx)
    {
        int count = _cardViews.Count;
        float spacing = GetCardSpacing(count);

        // Use natural slot positions so AnimateTo on ghost cards doesn't corrupt the result
        var xs = new List<float>();
        for (int i = 0; i < count; i++)
            if (i != draggingIdx) xs.Add(GetSlotPosition(i, count, spacing).x);
        xs.Sort();

        int pos = 0;
        foreach (float x in xs)
        {
            if (dragX > x) pos++;
            else break;
        }
        return pos;
    }
}
