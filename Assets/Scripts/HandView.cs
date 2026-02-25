using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Rummy500.Core;

/// <summary>
/// Displays a player's hand as a fanned arc at the bottom of the screen.
/// Supports single-card reorder drag (within hand) and multi-card external drop (meld/discard).
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

    // Fired when card(s) are dropped outside the hand area.
    // Parameters: dragged CardViews, screen drop position.
    public event Action<List<CardView>, Vector2> OnCardsDroppedExternal;

    private List<CardView> _cardViews = new List<CardView>();
    private List<int> _selectedIndices = new List<int>();
    private List<Card> _currentHand;

    private int _draggingIndex = -1;
    private RectTransform _draggingRT;
    private List<RectTransform> _draggingRTs = new List<RectTransform>();
    private List<Vector3> _draggingOffsets = new List<Vector3>();

    public List<int> SelectedIndices => new List<int>(_selectedIndices);
    public List<CardView> CardViews => _cardViews;

    public void RenderHand(List<Card> hand)
    {
        foreach (var cv in _cardViews)
            if (cv != null) Destroy(cv.gameObject);
        _cardViews.Clear();
        _selectedIndices.Clear();

        _currentHand = hand;
        _draggingIndex = -1;
        _draggingRT = null;
        _draggingRTs.Clear();
        _draggingOffsets.Clear();

        if (hand == null || hand.Count == 0) return;

        int count = hand.Count;
        float angleStep = count > 1 ? Mathf.Min(fanAngleRange / (count - 1), 8f) : 0f;
        float startAngle = -(angleStep * (count - 1)) / 2f;

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(cardPrefab, transform);
            var cv = go.GetComponent<CardView>();
            var rt = go.GetComponent<RectTransform>();

            rt.pivot = new Vector2(0.5f, 0f);
            cv.Setup(hand[i]);

            float angle = startAngle + i * angleStep;
            float rad = angle * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad) * fanRadius;
            float y = Mathf.Cos(rad) * fanRadius - fanRadius;

            var basePos = new Vector3(x, y, 0);
            cv.SetBasePosition(basePos);
            rt.localRotation = Quaternion.Euler(0, 0, -angle);

            int capturedIndex = i;
            cv.OnCardClicked += _ => ToggleSelection(capturedIndex);
            cv.OnBeginDragCard += OnCardBeginDrag;
            cv.OnDragCard += OnCardDrag;
            cv.OnEndDragCard += OnCardEndDrag;

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

    // --- Drag handlers ---

    void OnCardBeginDrag(CardView cv, PointerEventData eventData)
    {
        if (!CanDrag) return;
        _draggingIndex = _cardViews.IndexOf(cv);
        if (_draggingIndex < 0) return;

        _draggingRT = cv.GetComponent<RectTransform>();
        _draggingRTs.Clear();
        _draggingOffsets.Clear();

        bool isDraggingSelected = _selectedIndices.Contains(_draggingIndex);

        if (isDraggingSelected && _selectedIndices.Count > 1)
        {
            // Multi-card drag: move all selected cards together
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
    }

    void OnCardDrag(CardView cv, PointerEventData eventData)
    {
        if (_draggingRT == null || _draggingRTs.Count == 0) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera, out Vector2 local);

        var primaryPos = new Vector3(local.x, local.y, 0);
        float tilt = Mathf.Clamp(-eventData.delta.x * 0.3f, -25f, 25f);

        for (int i = 0; i < _draggingRTs.Count; i++)
        {
            _draggingRTs[i].localPosition = primaryPos + _draggingOffsets[i];
            _draggingRTs[i].localRotation = Quaternion.Euler(0f, 0f, tilt);
        }
    }

    void OnCardEndDrag(CardView cv, PointerEventData eventData)
    {
        if (_draggingIndex < 0 || _currentHand == null) return;

        bool isMultiDrag = _draggingRTs.Count > 1;
        bool insideHand = RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera);

        if (insideHand && !isMultiDrag)
        {
            // Single card dropped within hand — reorder
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
        else
        {
            // External drop — single card to discard/extend, or multi-card to lay meld
            var draggedCards = isMultiDrag
                ? _selectedIndices.Select(i => _cardViews[i]).ToList()
                : new List<CardView> { cv };
            OnCardsDroppedExternal?.Invoke(draggedCards, eventData.position);
        }

        _draggingIndex = -1;
        _draggingRT = null;
        _draggingRTs.Clear();
        _draggingOffsets.Clear();
        RenderHand(_currentHand);
    }

    int GetInsertionIndex(float dragX, int draggingIdx)
    {
        var xs = new List<float>();
        for (int i = 0; i < _cardViews.Count; i++)
        {
            if (i != draggingIdx)
                xs.Add(_cardViews[i].BasePosition.x);
        }
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
