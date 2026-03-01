using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Rummy500.Core;

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
    public bool HoverEnabled { get; set; } = true;
    public bool IsDragging  { get; set; } = false;

    // Events
    public Action<CardView> OnCardClicked;
    public Action<CardView, PointerEventData> OnBeginDragCard;
    public Action<CardView, PointerEventData> OnDragCard;
    public Action<CardView, PointerEventData> OnEndDragCard;

    public Vector3 BasePosition => _basePosition;

    private Vector3 _basePosition;
    private Vector3 _liveBase;
    private bool _isAnimatingPos;
    private Quaternion _baseRotation = Quaternion.identity;
    private float _idlePhase;
    private bool _isHovered;
    private bool _isHighlighted;
    private RectTransform _rect;
    private RectTransform _shadowRT;
    private Image _faceImage;   // dedicated layer for card art — sits above background, below text
    private static readonly Color HighlightColor = new Color(1f, 0.94f, 0.55f);

    const float IdleMaxX  = 18f;   // idle pitch amplitude
    const float IdleMaxY  = 28f;   // idle yaw amplitude
    const float HoverMaxX = 18f;   // cursor-tracked pitch amplitude
    const float HoverMaxY = 28f;   // cursor-tracked yaw amplitude
    const float TiltSpeed = 10f;   // slerp speed
    const float MoveSpeed =  9f;   // position animation speed

    const float ScaleNormal = 1.00f;
    const float ScaleHover  = 1.03f;
    const float ScaleSelect = 1.10f;
    const float ScaleDrag   = 1.22f;
    const float ScaleSpeed  = 14f;

    // Suit unicode symbols
    static readonly string[] SuitSymbols = { "♠", "♥", "♦", "♣" };
    static readonly string[] RankLabels =
    {
        "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"
    };

    // ── Card art sprite cache (loaded once from Resources/Cards/) ──────────
    // Sprites are mapped by grid position in the tilesheet, not by name,
    // so this works regardless of how Unity names the sliced sub-assets.

    // Sheet layout (13 cols × 4 rows) — standard deck, no jokers/backs
    const int SheetCols = 13;
    const int SheetRows = 4;

    // Row 0→3 suit order (top to bottom)
    static readonly string[] SheetSuitByRow = { "Hearts", "Diamonds", "Spades", "Clubs" };
    // Col 0→12 rank order (left to right)
    static readonly string[] SheetRankByCol = { "A","2","3","4","5","6","7","8","9","10","J","Q","K" };

    static Dictionary<string, Sprite> s_CardSprites;

    static void EnsureSpritesLoaded()
    {
        if (s_CardSprites != null) return;
        s_CardSprites = new Dictionary<string, Sprite>();

        var all = Resources.LoadAll<Sprite>("Cards/PlayingCards");
        if (all.Length == 0)
        {
            Debug.LogWarning("CardView: no sprites found in Resources/Cards/PlayingCards/ — card art won't show.");
            return;
        }

        var tex   = all[0].texture;
        float cellW = tex.width  / (float)SheetCols;
        float cellH = tex.height / (float)SheetRows;

        Debug.Log($"CardView: texture {tex.width}x{tex.height}, expecting {SheetCols}x{SheetRows} grid → cell {cellW:F1}x{cellH:F1}px. Total sprites: {all.Length}");

        int skipped = 0;
        foreach (var sprite in all)
        {
            var rect    = sprite.textureRect;
            int col     = Mathf.FloorToInt(rect.x / cellW + 0.01f);
            int rowFlip = Mathf.FloorToInt(rect.y / cellH + 0.01f);
            int row     = SheetRows - 1 - rowFlip;

            string key = null;

            if (row >= 0 && row < SheetRows && col >= 0 && col < SheetCols)
                key = $"card_{SheetRankByCol[col]}_{SheetSuitByRow[row]}";
            // out-of-bounds cells → skip

            if (key != null)
                s_CardSprites[key] = sprite;
            else
            {
                if (skipped < 5) Debug.Log($"CardView: skipped '{sprite.name}' rect=({rect.x:F0},{rect.y:F0}) → col={col} row={row}");
                skipped++;
            }
        }

        Debug.Log($"CardView: loaded {s_CardSprites.Count} card sprites, skipped {skipped} from tilesheet.");
    }

    // Suit order in Card.cs: Spades=0, Hearts=1, Diamonds=2, Clubs=3
    static readonly string[] SuitNames = { "Spades", "Hearts", "Diamonds", "Clubs" };

    public static Sprite GetFaceSprite(Card card)
    {
        EnsureSpritesLoaded();
        var key = $"card_{RankLabels[(int)card.Rank]}_{SuitNames[(int)card.Suit]}";
        s_CardSprites.TryGetValue(key, out var sprite);
        return sprite;
    }

    // ── Mini card sprites (Resources/Cards/MiniCards/) ─────────────────────
    static Dictionary<string, Sprite> s_MiniSprites;

    static void EnsureMiniSpritesLoaded()
    {
        if (s_MiniSprites != null) return;
        s_MiniSprites = new Dictionary<string, Sprite>();

        var all = Resources.LoadAll<Sprite>("Cards/MiniCards");
        if (all.Length == 0) { Debug.LogWarning("CardView: no sprites in Resources/Cards/MiniCards/"); return; }

        var tex   = all[0].texture;
        float cellW = tex.width  / (float)SheetCols;
        float cellH = tex.height / (float)SheetRows;

        foreach (var sprite in all)
        {
            var rect    = sprite.textureRect;
            int col     = Mathf.FloorToInt(rect.x / cellW + 0.01f);
            int rowFlip = Mathf.FloorToInt(rect.y / cellH + 0.01f);
            int row     = SheetRows - 1 - rowFlip;

            if (row >= 0 && row < SheetRows && col >= 0 && col < SheetCols)
                s_MiniSprites[$"card_{SheetRankByCol[col]}_{SheetSuitByRow[row]}"] = sprite;
        }
        Debug.Log($"CardView: loaded {s_MiniSprites.Count} mini sprites.");
    }

    public static Sprite GetMiniSprite(Card card)
    {
        EnsureMiniSpritesLoaded();
        var key = $"card_{RankLabels[(int)card.Rank]}_{SuitNames[(int)card.Suit]}";
        s_MiniSprites.TryGetValue(key, out var sprite);
        return sprite;
    }

    public static Sprite GetNamedSprite(string name)
    {
        EnsureSpritesLoaded();
        s_CardSprites.TryGetValue(name, out var sprite);
        return sprite;
    }

    // --- Rounded-card sprites (generated once, shared by all cards) ---

    static Sprite s_Fill;
    static Sprite s_Outline;
    static Sprite s_Mask;
    public static Sprite FillSprite    { get { return s_Fill    ?? (s_Fill    = MakeCardSprite(false));       } }
    public static Sprite OutlineSprite { get { return s_Outline ?? (s_Outline = MakeCardSprite(true));        } }
    static Sprite MaskSprite           { get { return s_Mask    ?? (s_Mask    = MakeCardSprite(false, TMR)); } }

    const int TX = 64, TY = 90, TR = 10, TB = 2;   // texture w/h, corner radius, border px
    const int TMR = 16;  // mask corner radius — tighter than card art so gray AA edges are clipped

    static Sprite MakeCardSprite(bool outlineOnly, int radius = TR)
    {
        var tex = new Texture2D(TX, TY, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var buf = new Color32[TX * TY];
        for (int y = 0; y < TY; y++)
        for (int x = 0; x < TX; x++)
        {
            bool outer = InRR(x + 0.5f, y + 0.5f, TX, TY, radius);
            bool inner = InRR(x + 0.5f - TB, y + 0.5f - TB, TX - TB * 2f, TY - TB * 2f, radius - TB);
            bool visible = outer && (!outlineOnly || !inner);
            buf[y * TX + x] = new Color32(255, 255, 255, (byte)(visible ? 255 : 0));
        }
        tex.SetPixels32(buf);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, TX, TY), new Vector2(0.5f, 0.5f), 100f, 0,
                             SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    static bool InRR(float px, float py, float w, float h, float r)
    {
        if (px < 0 || py < 0 || px > w || py > h) return false;
        float qx = Mathf.Max(0, Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r));
        float qy = Mathf.Max(0, Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r));
        return qx * qx + qy * qy <= r * r;
    }

    // ---------------------------------------------------------------

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _idlePhase = UnityEngine.Random.value * 100f;

        // cardBackground provides the white rounded fill and acts as the mask shape.
        // The root Mask clips all children (face art, labels, border) to the rounded card boundary.
        // TMR (mask radius) is larger than TR so anti-aliased corner pixels of the card art
        // are clipped before they're visible, giving clean rounded corners.
        if (cardBackground)
        {
            cardBackground.sprite = MaskSprite;   // tighter radius for clipping
            cardBackground.type   = Image.Type.Sliced;
            var mask = gameObject.GetComponent<Mask>() ?? gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;           // still draw the white background
        }
        if (selectionBorder) { selectionBorder.sprite = FillSprite; selectionBorder.type = Image.Type.Sliced; }

        // Dedicated face-art layer — direct child, clipped by the root Mask automatically
        var faceGO = new GameObject("CardFace", typeof(RectTransform), typeof(Image));
        faceGO.transform.SetParent(transform, false);
        faceGO.transform.SetSiblingIndex(1);   // just above cardBackground
        var faceRT = faceGO.GetComponent<RectTransform>();
        faceRT.anchorMin = Vector2.zero; faceRT.anchorMax = Vector2.one;
        faceRT.offsetMin = Vector2.zero; faceRT.offsetMax = Vector2.zero;
        _faceImage = faceGO.GetComponent<Image>();
        _faceImage.raycastTarget = false;
        _faceImage.gameObject.SetActive(false);

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
    ApplyPosition();
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
    _liveBase = pos;
    _isAnimatingPos = false;
    ApplyPosition();
}

// Smoothly moves the card to pos rather than snapping
public void AnimateTo(Vector3 pos)
{
    if (_rect == null) _rect = GetComponent<RectTransform>();
    _basePosition = pos;
    _liveBase = _rect.localPosition - (IsSelected ? new Vector3(0, 30f, 0) : Vector3.zero);
    _isAnimatingPos = true;
}

public void CancelAnimation()
{
    _isAnimatingPos = false;
}

void ApplyPosition()
{
    if (_rect == null) return;
    var pos = _liveBase;
    if (IsSelected) pos.y += 30f;
    _rect.localPosition = pos;
}

public void SetBaseRotation(Quaternion rot)
{
    _baseRotation = rot;
}

public void SetShadow(RectTransform shadowRT)
{
    _shadowRT = shadowRT;
}

void Update()
{
    if (_rect == null) return;

    // Position animation
    if (_isAnimatingPos)
    {
        _liveBase = Vector3.Lerp(_liveBase, _basePosition, Time.deltaTime * MoveSpeed);
        ApplyPosition();
        if ((_liveBase - _basePosition).sqrMagnitude < 0.25f)
        {
            _liveBase = _basePosition;
            _isAnimatingPos = false;
            ApplyPosition();
        }
    }

    // Scale: drag > selected > hovered > normal
    float targetScale = IsDragging  ? ScaleDrag   :
                        IsSelected  ? ScaleSelect  :
                        (_isHovered && HoverEnabled) ? ScaleHover : ScaleNormal;
    float curScale = _rect.localScale.x;
    float newScale = Mathf.Lerp(curScale, targetScale, Time.deltaTime * ScaleSpeed);
    _rect.localScale = Vector3.one * newScale;

    // Shadow always follows — offset and fade increase with scale to sell the "raised" height
    if (_shadowRT != null)
    {
        float raise       = (newScale - 1f) / (ScaleDrag - 1f);   // 0 at rest, 1 at full drag scale
        float shadowY     = Mathf.Lerp(-6f, -42f, raise);
        float shadowAlpha = Mathf.Lerp(0.55f, 0.25f, raise);      // fainter when higher up
        _shadowRT.localPosition = _rect.localPosition + new Vector3(0, shadowY, 0);
        _shadowRT.localRotation = _rect.localRotation;
        var img = _shadowRT.GetComponent<UnityEngine.UI.Image>();
        if (img) img.color = new Color(0f, 0f, 0f, shadowAlpha);
    }

    // Rotation: drag code controls rotation directly — skip tilt animation
    if (IsDragging) return;

    // Cursor tilt on hover, frozen when selected, idle drift otherwise
    Quaternion target;
    if (IsSelected && HoverEnabled)
    {
        target = _baseRotation;
    }
    else if (_isHovered && HoverEnabled)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, Input.mousePosition, null, out Vector2 local);
        float nx = Mathf.Clamp(local.x / (_rect.sizeDelta.x * 0.5f), -1f, 1f);
        float ny = Mathf.Clamp(local.y / (_rect.sizeDelta.y * 0.5f), -1f, 1f);
        target = Quaternion.Euler(-ny * HoverMaxX, nx * HoverMaxY, 0f) * _baseRotation;
    }
    else
    {
        float tx = (Mathf.PerlinNoise(Time.time * 0.42f + _idlePhase, 0.5f) - 0.5f) * 2f * IdleMaxX;
        float ty = (Mathf.PerlinNoise(0.5f, Time.time * 0.32f + _idlePhase) - 0.5f) * 2f * IdleMaxY;
        target = Quaternion.Euler(tx, ty, 0f) * _baseRotation;
    }

    _rect.localRotation = Quaternion.Slerp(_rect.localRotation, target, Time.deltaTime * TiltSpeed);
}

    void UpdateVisuals()
    {
        if (Card == null) return;

        var faceSprite = GetFaceSprite(Card);
        bool hasArt    = faceSprite != null;

        if (hasArt && _faceImage != null)
        {
            // Show the tilesheet sprite on the dedicated face layer; leave cardBackground untouched
            _faceImage.sprite  = faceSprite;
            _faceImage.type    = Image.Type.Simple;
            _faceImage.color   = Color.white;
            _faceImage.gameObject.SetActive(true);

            // Art has rank/suit baked in — hide the TMP labels
            if (topRankText)    topRankText.gameObject.SetActive(false);
            if (topSuitText)    topSuitText.gameObject.SetActive(false);
            if (centerSuitText) centerSuitText.gameObject.SetActive(false);
            if (bottomRankText) bottomRankText.gameObject.SetActive(false);
            if (bottomSuitText) bottomSuitText.gameObject.SetActive(false);
        }
        else
        {
            // Fallback: procedural text card (no art imported yet)
            if (_faceImage != null) _faceImage.gameObject.SetActive(false);

            bool isRed = Card.Suit == Suit.Hearts || Card.Suit == Suit.Diamonds;
            Color suitColor = isRed ? redSuitColor : blackSuitColor;
            string rank = RankLabels[(int)Card.Rank];
            string suit = SuitSymbols[(int)Card.Suit];

            if (topRankText)    { topRankText.gameObject.SetActive(true);    topRankText.text    = rank; topRankText.color    = suitColor; topRankText.fontSize    = 26; }
            if (topSuitText)    { topSuitText.gameObject.SetActive(true);    topSuitText.text    = suit; topSuitText.color    = suitColor; topSuitText.fontSize    = 24; topSuitText.margin = new Vector4(0, 6, 0, 0); }
            if (centerSuitText) { centerSuitText.gameObject.SetActive(true); centerSuitText.text = suit; centerSuitText.color = suitColor; centerSuitText.fontSize = 72; }
            if (bottomRankText) { bottomRankText.gameObject.SetActive(true); bottomRankText.text = rank; bottomRankText.color = suitColor; bottomRankText.fontSize = 26; }
            if (bottomSuitText) { bottomSuitText.gameObject.SetActive(true); bottomSuitText.text = suit; bottomSuitText.color = suitColor; bottomSuitText.fontSize = 24; }

            if (cardBackground) cardBackground.color = normalColor;
        }

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
    if (!HoverEnabled) return;
    _isHovered = true;
}

public void OnPointerExit(PointerEventData eventData)
{
    if (!HoverEnabled) return;
    _isHovered = false;
}
}
