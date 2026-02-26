using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Rummy500.Core;

/// <summary>
/// Displays the player's hand as a fanned arc.
/// Click to select/deselect. Drag to reorder or drop externally.
/// Dragging a selected card moves ALL selected cards together visually.
/// </summary>
public class HandView : MonoBehaviour
{
    [Header("Fan Settings")]
    public float fanAngleRange = 40f;
    public float fanRadius = 700f;
    public float cardSpacingMax = 120f;
    public float cardWidth = 100f;
    public float cardHeight = 150f;

    [Header("Prefab")]
    public GameObject cardPrefab;

    public bool CanDrag { get; set; } = true;
    public bool flatLayout = false;

    public event Action<List<CardView>, Vector2> OnCardsDroppedExternal;
    public event Action<bool>    OnDragStateChanged;
    public event Action<Vector2> OnDragMoved;

    private List<CardView> _cardViews = new List<CardView>();
    private List<int> _selectedIndices = new List<int>();
    private List<Card> _currentHand;

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

    public void RenderHand(List<Card> hand, Card highlightCard = null)
    {
        foreach (var cv in _cardViews)
            if (cv != null) Destroy(cv.gameObject);
        _cardViews.Clear();
        _selectedIndices.Clear();

        _currentHand = hand;
        _draggingIndex = -1;
        _draggingRT = null;
        _draggingAllSelected = false;
        _draggingRTs.Clear();
        _draggingOffsets.Clear();

        if (hand == null || hand.Count == 0) return;

        int count = hand.Count;
        float angleStep = count > 1 ? Mathf.Min(fanAngleRange / (count - 1), 5f) : 0f;
        float startAngle = -(angleStep * (count - 1)) / 2f;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(cardPrefab, transform);
            var cv = go.GetComponent<CardView>();
            var rt = go.GetComponent<RectTransform>();

            rt.pivot = new Vector2(0.5f, 0f);
            cv.Setup(hand[i]);

            if (flatLayout)
            {
                float startX = -(count - 1) * cardSpacingMax / 2f;
                cv.SetBasePosition(new Vector3(startX + i * cardSpacingMax, 0, 0));
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                float angle = startAngle + i * angleStep;
                float rad = angle * Mathf.Deg2Rad;
                float x = Mathf.Sin(rad) * fanRadius;
                float y = Mathf.Cos(rad) * fanRadius - fanRadius;
                cv.SetBasePosition(new Vector3(x, y, 0));
                rt.localRotation = Quaternion.Euler(0, 0, -angle);
            }

            if (highlightCard != null && hand[i] == highlightCard)
                cv.SetHighlighted(true);

            int capturedIndex = i;
            cv.OnCardClicked   += _ => ToggleSelection(capturedIndex);
            cv.OnBeginDragCard += OnCardBeginDrag;
            cv.OnDragCard      += OnCardDrag;
            cv.OnEndDragCard   += OnCardEndDrag;

            _cardViews.Add(cv);
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

        float angleStep = count > 1 ? Mathf.Min(fanAngleRange / (count - 1), 5f) : 0f;
        float startAngle = -(angleStep * (count - 1)) / 2f;

        var availableSlots = new List<int>(count - 1);
        for (int s = 0; s < count; s++)
            if (s != _ghostInsertIndex) availableSlots.Add(s);

        int slotPtr = 0;
        for (int i = 0; i < count; i++)
        {
            if (i == _draggingIndex) continue;

            int slot = availableSlots[slotPtr++];
            var rt = _cardViews[i].GetComponent<RectTransform>();

            if (flatLayout)
            {
                float startX = -(count - 1) * cardSpacingMax / 2f;
                rt.localPosition = new Vector3(startX + slot * cardSpacingMax, 0, 0);
                rt.localRotation = Quaternion.identity;
            }
            else
            {
                float angle = startAngle + slot * angleStep;
                float rad   = angle * Mathf.Deg2Rad;
                float x     = Mathf.Sin(rad) * fanRadius;
                float y     = Mathf.Cos(rad) * fanRadius - fanRadius;
                rt.localPosition = new Vector3(x, y, 0);
                rt.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }
    }

    int GetInsertionIndex(float dragX, int draggingIdx)
    {
        var xs = new List<float>();
        for (int i = 0; i < _cardViews.Count; i++)
            if (i != draggingIdx) xs.Add(_cardViews[i].BasePosition.x);
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
