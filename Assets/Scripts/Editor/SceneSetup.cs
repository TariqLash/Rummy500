using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Run this once from the menu to build the entire game scene automatically.
/// Menu: Rummy500 → Build Game Scene
/// </summary>
public class SceneSetup : MonoBehaviour
{
    // Color palette — clean modern dark theme
    static Color BG          = new Color(0.08f, 0.11f, 0.14f);   // near-black blue
    static Color TableGreen  = new Color(0.10f, 0.22f, 0.18f);   // dark teal felt
    static Color CardWhite   = new Color(0.97f, 0.97f, 0.96f);
    static Color AccentBlue  = new Color(0.25f, 0.55f, 0.95f);
    static Color AccentGreen = new Color(0.18f, 0.75f, 0.45f);
    static Color TextLight   = new Color(0.90f, 0.90f, 0.88f);
    static Color PanelDark   = new Color(0.05f, 0.08f, 0.11f, 0.9f);

    [MenuItem("Rummy500/Build Game Scene")]
    public static void BuildScene()
    {
        // Clear scene
        var roots = new System.Collections.Generic.List<GameObject>();
        foreach (var go in FindObjectsOfType<GameObject>())
            if (go.transform.parent == null && go.name != "Main Camera")
                roots.Add(go);
        foreach (var go in roots)
            DestroyImmediate(go);

        // Camera
        var cam = Camera.main;
        if (!cam) { var c = new GameObject("Main Camera"); cam = c.AddComponent<Camera>(); c.tag = "MainCamera"; }
        cam.backgroundColor = BG;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.transform.position = new Vector3(0, 0, -10);

        // EventSystem
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Root Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // Background — full screen
        var bg = MakePanel(canvasGo.transform, "Background", BG, Vector2.zero, Vector2.one);
        bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        bg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        bg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // GameArea — inset from screen edges; background shows as margin border
        const float marginH = 250f;  // left + right
        const float marginV = 150f;  // top + bottom
        var gameArea = new GameObject("GameArea", typeof(RectTransform));
        gameArea.transform.SetParent(canvasGo.transform, false);
        var gaRT = gameArea.GetComponent<RectTransform>();
        gaRT.anchorMin = Vector2.zero;
        gaRT.anchorMax = Vector2.one;
        gaRT.offsetMin = new Vector2( marginH,  marginV);
        gaRT.offsetMax = new Vector2(-marginH, -marginV);

        // ── LEFT PANEL (70%) — game area ─────────────────────────────────────
        var leftPanel = new GameObject("LeftPanel", typeof(RectTransform), typeof(Image));
        leftPanel.transform.SetParent(gameArea.transform, false);
        leftPanel.GetComponent<Image>().color = TableGreen;
        var lpRT = leftPanel.GetComponent<RectTransform>();
        lpRT.anchorMin = Vector2.zero;
        lpRT.anchorMax = new Vector2(0.7f, 1f);
        lpRT.offsetMin = Vector2.zero;
        lpRT.offsetMax = Vector2.zero;

        // ── RIGHT PANEL (30%) — info + melds ─────────────────────────────────
        var rightPanel = new GameObject("RightPanel", typeof(RectTransform), typeof(Image));
        rightPanel.transform.SetParent(gameArea.transform, false);
        rightPanel.GetComponent<Image>().color = PanelDark;
        var rpRT = rightPanel.GetComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0.7f, 0f);
        rpRT.anchorMax = Vector2.one;
        rpRT.offsetMin = Vector2.zero;
        rpRT.offsetMax = Vector2.zero;

        // Thin divider between panels
        var divider = new GameObject("PanelDivider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(gameArea.transform, false);
        divider.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        var dividerRT = divider.GetComponent<RectTransform>();
        dividerRT.anchorMin = new Vector2(0.7f, 0f);
        dividerRT.anchorMax = new Vector2(0.7f, 1f);
        dividerRT.offsetMin = new Vector2(-1f, 0f);
        dividerRT.offsetMax = new Vector2( 1f, 0f);

        // ── LEFT: Deck + Discard row at top ───────────────────────────────────
        var deckArea = new GameObject("DeckArea", typeof(RectTransform));
        deckArea.transform.SetParent(leftPanel.transform, false);
        AddHorizontalLayout(deckArea, 10, new RectOffset(14, 14, 14, 14));
        var deckAreaRT = deckArea.GetComponent<RectTransform>();
        deckAreaRT.anchorMin        = new Vector2(0f, 1f);
        deckAreaRT.anchorMax        = new Vector2(1f, 1f);
        deckAreaRT.pivot            = new Vector2(0.5f, 1f);
        deckAreaRT.sizeDelta        = new Vector2(0f, 158f);
        deckAreaRT.anchoredPosition = Vector2.zero;

        var drawPileGo = MakeCardBack(deckArea.transform, "DrawPile", "DRAW\nPILE");
        drawPileGo.GetComponent<RectTransform>().sizeDelta = new Vector2(90, 130);
        drawPileGo.AddComponent<LayoutElement>().preferredWidth = 90;
        var drawPileBtn = drawPileGo.AddComponent<Button>();
        drawPileGo.name = "DrawPileCardBack";

        var drawCountLabel = MakeLabel(deckArea.transform, "52", 13, TextLight);
        drawCountLabel.name = "DrawPileCountText";
        drawCountLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(32, 130);
        drawCountLabel.AddComponent<LayoutElement>().preferredWidth = 32;

        var discardArea = new GameObject("DiscardContainer", typeof(RectTransform));
        discardArea.transform.SetParent(deckArea.transform, false);
        discardArea.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 130);
        var discardLE = discardArea.AddComponent<LayoutElement>();
        discardLE.flexibleWidth = 1;   // take remaining horizontal space
        discardLE.preferredHeight = 130;
        AddHorizontalLayout(discardArea, -25, new RectOffset(0, 0, 0, 0));
        discardArea.name = "DiscardContainer";

        // ── LEFT: Player hand at bottom (flat, no arc) ────────────────────────
        var handContainer = new GameObject("HandContainer", typeof(RectTransform));
        handContainer.transform.SetParent(leftPanel.transform, false);
        var hcRT = handContainer.GetComponent<RectTransform>();
        hcRT.anchorMin        = new Vector2(0.5f, 0f);
        hcRT.anchorMax        = new Vector2(0.5f, 0f);
        hcRT.pivot            = new Vector2(0.5f, 0f);
        hcRT.sizeDelta        = new Vector2(1200f, 170f);
        hcRT.anchoredPosition = new Vector2(0f, 40f);   // 40px off the bottom edge

        var handView = handContainer.AddComponent<HandView>();
        handView.flatLayout    = true;
        handView.cardSpacingMax = 70f;

        // ── RIGHT: Info panel at top ──────────────────────────────────────────
        var infoPanel = new GameObject("InfoPanel", typeof(RectTransform));
        infoPanel.transform.SetParent(rightPanel.transform, false);
        AddVerticalLayout(infoPanel, 5, new RectOffset(14, 14, 14, 10));
        var ipRT = infoPanel.GetComponent<RectTransform>();
        ipRT.anchorMin        = new Vector2(0f, 1f);
        ipRT.anchorMax        = new Vector2(1f, 1f);
        ipRT.pivot            = new Vector2(0.5f, 1f);
        ipRT.sizeDelta        = new Vector2(0f, 220f);
        ipRT.anchoredPosition = Vector2.zero;

        MakeLabel(infoPanel.transform, "RUMMY 500", 13, TextLight, FontStyles.Bold);
        var phaseLabel = MakeLabel(infoPanel.transform, "Phase", 13, AccentBlue);
        phaseLabel.name = "PhaseText";
        var turnLabel = MakeLabel(infoPanel.transform, "Turn", 14, TextLight, FontStyles.Bold);
        turnLabel.name = "CurrentPlayerText";
        MakeLabel(infoPanel.transform, "SCORES", 11, new Color(1, 1, 1, 0.5f), FontStyles.Bold);

        var scoreContainer = new GameObject("ScoreContainer", typeof(RectTransform));
        scoreContainer.transform.SetParent(infoPanel.transform, false);
        AddVerticalLayout(scoreContainer, 2, new RectOffset(0, 0, 0, 0));
        scoreContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 70);

        var warningLabel = MakeLabel(infoPanel.transform, "", 12, new Color(1f, 0.6f, 0.2f), FontStyles.Bold);
        warningLabel.name = "WarningText";
        warningLabel.gameObject.SetActive(false);

        // ── RIGHT: Melds below info, fills rest of right panel ────────────────
        var meldsArea = new GameObject("MeldsContainer", typeof(RectTransform));
        meldsArea.transform.SetParent(rightPanel.transform, false);
        AddVerticalLayout(meldsArea, 10, new RectOffset(10, 10, 10, 10));
        var maRT = meldsArea.GetComponent<RectTransform>();
        maRT.anchorMin = Vector2.zero;
        maRT.anchorMax = Vector2.one;
        maRT.offsetMin = Vector2.zero;
        maRT.offsetMax = new Vector2(0f, -220f);   // leave 220px for info at top
        meldsArea.name = "MeldsContainer";

        // ── Game-over overlay ─────────────────────────────────────────────────
        var overlay = MakePanel(canvasGo.transform, "GameOverOverlay", new Color(0f, 0f, 0f, 0.72f),
            Vector2.zero, Vector2.one);
        var overlayRT = overlay.GetComponent<RectTransform>();
        overlayRT.anchorMin = Vector2.zero;
        overlayRT.anchorMax = Vector2.one;
        overlayRT.offsetMin = Vector2.zero;
        overlayRT.offsetMax = Vector2.zero;
        var overlayBtn = overlay.AddComponent<Button>();

        var overlayTxtGo = new GameObject("OverlayText", typeof(RectTransform), typeof(TextMeshProUGUI));
        overlayTxtGo.transform.SetParent(overlay.transform, false);
        var overlayTxtRT = overlayTxtGo.GetComponent<RectTransform>();
        overlayTxtRT.anchorMin = new Vector2(0.1f, 0.35f);
        overlayTxtRT.anchorMax = new Vector2(0.7f, 0.65f);
        overlayTxtRT.offsetMin = Vector2.zero;
        overlayTxtRT.offsetMax = Vector2.zero;
        var overlayTmp = overlayTxtGo.GetComponent<TextMeshProUGUI>();
        overlayTmp.text = "Round Over!\nTap to continue";
        overlayTmp.fontSize = 36;
        overlayTmp.color = Color.white;
        overlayTmp.fontStyle = FontStyles.Bold;
        overlayTmp.alignment = TextAlignmentOptions.Center;
        overlayTmp.raycastTarget = false;
        overlay.SetActive(false);

        // ── DROP ZONES ────────────────────────────────────────────────────────

        // Discard drop zone — repositioned by TableUI each render to cover the pile
        var discardZoneGo = new GameObject("DiscardDropZone", typeof(RectTransform), typeof(Image));
        discardZoneGo.transform.SetParent(canvasGo.transform, false);
        var dzImg = discardZoneGo.GetComponent<Image>();
        dzImg.color         = new Color(1f, 0.55f, 0.20f, 0.45f);
        dzImg.raycastTarget = false;
        var dzRT = discardZoneGo.GetComponent<RectTransform>();
        dzRT.anchorMin = new Vector2(0.5f, 0.5f);
        dzRT.anchorMax = new Vector2(0.5f, 0.5f);
        dzRT.pivot     = new Vector2(0.5f, 0.5f);
        dzRT.sizeDelta = new Vector2(494f, 158f);
        discardZoneGo.SetActive(false);

        // Meld drop zone — entire right panel below the info area
        var meldZoneGo = new GameObject("MeldDropZone", typeof(RectTransform), typeof(Image));
        meldZoneGo.transform.SetParent(gameArea.transform, false);
        var mzImg = meldZoneGo.GetComponent<Image>();
        mzImg.color         = new Color(0.18f, 0.75f, 0.35f, 0.18f);
        mzImg.raycastTarget = false;
        var mzRT = meldZoneGo.GetComponent<RectTransform>();
        mzRT.anchorMin = new Vector2(0.7f, 0f);
        mzRT.anchorMax = new Vector2(1f,   1f);
        mzRT.offsetMin = Vector2.zero;
        mzRT.offsetMax = new Vector2(0f, -220f);   // exclude info panel at top
        meldZoneGo.SetActive(false);

        // Card prefab
        var cardPrefab = BuildCardPrefab();

        handView.cardPrefab = cardPrefab;

        // GameManager
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        // TableUI
        var tableUI = gmGo.AddComponent<TableUI>();
        tableUI.handView = handView;
        tableUI.cardPrefab = cardPrefab;

        // Wire up references by name (find them in hierarchy)
        tableUI.discardContainer = discardArea.transform;
        tableUI.meldsContainer = meldsArea.transform;

        tableUI.drawPileCardBack  = drawPileGo.GetComponent<Button>();
        tableUI.drawPileCountText = GameObject.Find("DrawPileCountText")?.GetComponent<TextMeshProUGUI>();
        tableUI.phaseText         = phaseLabel.GetComponent<TextMeshProUGUI>();
        tableUI.currentPlayerText = turnLabel.GetComponent<TextMeshProUGUI>();
        tableUI.warningText       = warningLabel.GetComponent<TextMeshProUGUI>();
        tableUI.scoreContainer    = scoreContainer.transform;
        tableUI.discardDropZone   = dzRT;
        tableUI.meldDropZone      = mzRT;
        tableUI.gameOverOverlay   = overlay;
        tableUI.overlayText       = overlayTmp;
        tableUI.overlayButton     = overlayBtn;
        Debug.Log("✅ Scene built! Hit Play to start.");
    }

    // --- Helpers ---

    static GameObject MakePanel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    static void AddVerticalLayout(GameObject go, float spacing, RectOffset padding)
    {
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.padding = padding;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
    }

    static void AddHorizontalLayout(GameObject go, float spacing, RectOffset padding)
    {
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = spacing;
        hlg.padding = padding;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;
    }

    static GameObject MakeLabel(Transform parent, string text, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Label_" + text.Substring(0, Mathf.Min(8, text.Length)),
            typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(196, fontSize * 2f);
        return go;
    }

    static GameObject MakeButton(Transform parent, string label, Color color)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(176, 40);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        var tRT = textGo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero;
        tRT.offsetMax = Vector2.zero;

        return go;
    }

    static GameObject MakeCardBack(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.15f, 0.25f, 0.55f);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(75, 105);

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 11;
        tmp.color = new Color(1,1,1,0.7f);
        tmp.alignment = TextAlignmentOptions.Center;
        var tRT = textGo.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = Vector2.zero;
        tRT.offsetMax = Vector2.zero;

        return go;
    }

    static GameObject BuildCardPrefab()
    {
        // Full standard playing card
        var card = new GameObject("CardPrefab", typeof(RectTransform), typeof(Image), typeof(CardView));
        var rt = card.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(90, 130);
        card.GetComponent<Image>().color = new Color(0.97f, 0.97f, 0.96f);

        // Selection border
        var border = new GameObject("SelectionBorder", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(card.transform, false);
        var borderImg = border.GetComponent<Image>();
        borderImg.color = Color.clear;
        var borderRT = border.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-3, -3);
        borderRT.offsetMax = new Vector2(3, 3);

        // Top-left rank
        var topRank = new GameObject("TopRank", typeof(RectTransform), typeof(TextMeshProUGUI));
        topRank.transform.SetParent(card.transform, false);
        var trRT = topRank.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0, 1); trRT.anchorMax = new Vector2(0, 1);
        trRT.pivot = new Vector2(0, 1); trRT.sizeDelta = new Vector2(40, 22);
        trRT.anchoredPosition = new Vector2(5, -5);
        var trTMP = topRank.GetComponent<TextMeshProUGUI>();
        trTMP.text = "A"; trTMP.fontSize = 16; trTMP.color = Color.black;
        trTMP.alignment = TextAlignmentOptions.TopLeft; trTMP.raycastTarget = false;

        // Top-left suit
        var topSuit = new GameObject("TopSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        topSuit.transform.SetParent(card.transform, false);
        var tsRT = topSuit.GetComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0, 1); tsRT.anchorMax = new Vector2(0, 1);
        tsRT.pivot = new Vector2(0, 1); tsRT.sizeDelta = new Vector2(22, 20);
        tsRT.anchoredPosition = new Vector2(5, -24);
        var tsTMP = topSuit.GetComponent<TextMeshProUGUI>();
        tsTMP.text = "♠"; tsTMP.fontSize = 14; tsTMP.color = Color.black;
        tsTMP.alignment = TextAlignmentOptions.TopLeft; tsTMP.raycastTarget = false;

        // Center suit (large)
        var centerSuit = new GameObject("CenterSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        centerSuit.transform.SetParent(card.transform, false);
        var csRT = centerSuit.GetComponent<RectTransform>();
        csRT.anchorMin = Vector2.zero; csRT.anchorMax = Vector2.one;
        csRT.offsetMin = Vector2.zero; csRT.offsetMax = Vector2.zero;
        var csTMP = centerSuit.GetComponent<TextMeshProUGUI>();
        csTMP.text = "♠"; csTMP.fontSize = 40; csTMP.color = Color.black;
        csTMP.alignment = TextAlignmentOptions.Center; csTMP.raycastTarget = false;

        // Bottom-right rank
        var botRank = new GameObject("BotRank", typeof(RectTransform), typeof(TextMeshProUGUI));
        botRank.transform.SetParent(card.transform, false);
        var brRT = botRank.GetComponent<RectTransform>();
        brRT.anchorMin = new Vector2(1, 0); brRT.anchorMax = new Vector2(1, 0);
        brRT.pivot = new Vector2(1, 0); brRT.sizeDelta = new Vector2(40, 22);
        brRT.anchoredPosition = new Vector2(-5, 5);
        var brTMP = botRank.GetComponent<TextMeshProUGUI>();
        brTMP.text = "A"; brTMP.fontSize = 16; brTMP.color = Color.black;
        brTMP.alignment = TextAlignmentOptions.BottomRight; brTMP.raycastTarget = false;

        // Bottom-right suit
        var botSuit = new GameObject("BotSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        botSuit.transform.SetParent(card.transform, false);
        var bsRT = botSuit.GetComponent<RectTransform>();
        bsRT.anchorMin = new Vector2(1, 0); bsRT.anchorMax = new Vector2(1, 0);
        bsRT.pivot = new Vector2(1, 0); bsRT.sizeDelta = new Vector2(22, 20);
        bsRT.anchoredPosition = new Vector2(-5, 24);
        var bsTMP = botSuit.GetComponent<TextMeshProUGUI>();
        bsTMP.text = "♠"; bsTMP.fontSize = 14; bsTMP.color = Color.black;
        bsTMP.alignment = TextAlignmentOptions.BottomRight; bsTMP.raycastTarget = false;

        // Wire CardView
        var cv = card.GetComponent<CardView>();
        cv.cardBackground  = card.GetComponent<Image>();
        cv.selectionBorder = borderImg;
        cv.topRankText     = trTMP;
        cv.topSuitText     = tsTMP;
        cv.centerSuitText  = csTMP;
        cv.bottomRankText  = brTMP;
        cv.bottomSuitText  = bsTMP;

        card.SetActive(true);
var prefab = PrefabUtility.SaveAsPrefabAsset(card, "Assets/Prefabs/Card.prefab");
DestroyImmediate(card);
return prefab;
    }

    static GameObject MakeCardText(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.black;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return go;
    }
}
#endif
