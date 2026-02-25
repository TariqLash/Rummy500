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

        // Background
        var bg = MakePanel(canvasGo.transform, "Background", BG, Vector2.zero, new Vector2(1, 1));
        bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        bg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        bg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // Table surface (slight perspective via scale/rotation trick)
        var table = MakePanel(canvasGo.transform, "TableSurface", TableGreen,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var tableRT = table.GetComponent<RectTransform>();
        tableRT.sizeDelta = new Vector2(1400, 700);
        tableRT.anchoredPosition = new Vector2(0, 50);
        // Subtle perspective skew via rotation
        table.transform.localRotation = Quaternion.Euler(10f, 0, 0);

        // Left panel — scores + info
        var leftPanel = MakePanel(canvasGo.transform, "LeftPanel", PanelDark,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f));
        var leftRT = leftPanel.GetComponent<RectTransform>();
        leftRT.sizeDelta = new Vector2(220, 600);
        leftRT.anchoredPosition = new Vector2(120, 50);
        AddVerticalLayout(leftPanel, 10, new RectOffset(12, 12, 12, 12));

        MakeLabel(leftPanel.transform, "RUMMY 500", 20, TextLight, FontStyles.Bold);
        MakeLabel(leftPanel.transform, "─────────────", 10, new Color(1,1,1,0.2f));

        var phaseLabel = MakeLabel(leftPanel.transform, "Phase", 13, AccentBlue);
        phaseLabel.name = "PhaseText";

        var turnLabel = MakeLabel(leftPanel.transform, "Turn", 15, TextLight, FontStyles.Bold);
        turnLabel.name = "CurrentPlayerText";

        MakeLabel(leftPanel.transform, "─────────────", 10, new Color(1,1,1,0.2f));
        MakeLabel(leftPanel.transform, "SCORES", 13, new Color(1,1,1,0.5f), FontStyles.Bold);

        var scoreContainer = new GameObject("ScoreContainer", typeof(RectTransform));
        scoreContainer.transform.SetParent(leftPanel.transform, false);
        AddVerticalLayout(scoreContainer, 4, new RectOffset(0,0,0,0));
        var scRT = scoreContainer.GetComponent<RectTransform>();
        scRT.sizeDelta = new Vector2(196, 200);

        var warningLabel = MakeLabel(leftPanel.transform, "", 13, new Color(1f, 0.6f, 0.2f), FontStyles.Bold);
        warningLabel.name = "WarningText";
        warningLabel.gameObject.SetActive(false);

        // Center top — draw pile + discard pile
        var centerTop = new GameObject("CenterTop", typeof(RectTransform));
        centerTop.transform.SetParent(canvasGo.transform, false);
        var ctRT = centerTop.GetComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0.5f, 1f);
        ctRT.anchorMax = new Vector2(0.5f, 1f);
        ctRT.sizeDelta = new Vector2(700, 140);
        ctRT.anchoredPosition = new Vector2(0, -160);
        AddHorizontalLayout(centerTop, 20, new RectOffset(10, 10, 10, 10));

        // Draw pile card back
        var drawPileGo = MakeCardBack(centerTop.transform, "DrawPile", "DRAW\nPILE");
        var drawPileBtn = drawPileGo.AddComponent<Button>();
        drawPileGo.name = "DrawPileCardBack";

        // Draw pile count label
        var drawCountLabel = MakeLabel(centerTop.transform, "52", 14, TextLight);
        drawCountLabel.name = "DrawPileCountText";
        drawCountLabel.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 120);

        // Discard pile container
        var discardArea = new GameObject("DiscardContainer", typeof(RectTransform));
        discardArea.transform.SetParent(centerTop.transform, false);
        var daRT = discardArea.GetComponent<RectTransform>();
        daRT.sizeDelta = new Vector2(400, 110);
        AddHorizontalLayout(discardArea, -30, new RectOffset(0,0,0,0)); // overlap cards slightly
        discardArea.name = "DiscardContainer";

        // Melds area (center of table)
        var meldsArea = new GameObject("MeldsContainer", typeof(RectTransform));
        meldsArea.transform.SetParent(canvasGo.transform, false);
        var maRT = meldsArea.GetComponent<RectTransform>();
        maRT.anchorMin = new Vector2(0.5f, 0.5f);
        maRT.anchorMax = new Vector2(0.5f, 0.5f);
        maRT.sizeDelta = new Vector2(1200, 400);
        maRT.anchoredPosition = new Vector2(0, 100);
        AddVerticalLayout(meldsArea, 8, new RectOffset(0,0,0,0));
        meldsArea.name = "MeldsContainer";

        // Discard drop zone — invisible geometric target for drag-to-discard gesture
        var discardZoneGo = new GameObject("DiscardDropZone", typeof(RectTransform), typeof(Image));
        discardZoneGo.transform.SetParent(canvasGo.transform, false);
        var dzImg = discardZoneGo.GetComponent<Image>();
        dzImg.color = Color.clear;
        dzImg.raycastTarget = false;   // transparent but geometry used for drop detection
        var dzRT = discardZoneGo.GetComponent<RectTransform>();
        dzRT.anchorMin = new Vector2(0.2f, 0.65f);
        dzRT.anchorMax = new Vector2(0.8f, 1.0f);
        dzRT.offsetMin = Vector2.zero;
        dzRT.offsetMax = Vector2.zero;

        // Game-over / round-over overlay — full-screen, shown when phase is RoundOver or GameOver
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
        overlayTxtRT.anchorMin = new Vector2(0.2f, 0.35f);
        overlayTxtRT.anchorMax = new Vector2(0.8f, 0.65f);
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

        // Hand container (bottom center)
        var handContainer = new GameObject("HandContainer", typeof(RectTransform));
        handContainer.transform.SetParent(canvasGo.transform, false);
        var hcRT = handContainer.GetComponent<RectTransform>();
        hcRT.anchorMin = new Vector2(0.5f, 0f);
        hcRT.anchorMax = new Vector2(0.5f, 0f);
        hcRT.sizeDelta = new Vector2(900, 200);
        hcRT.anchoredPosition = new Vector2(0, 320);

        var handView = handContainer.AddComponent<HandView>();
        handView.fanAngleRange = 40f;
        handView.fanRadius = 700f;
        handView.cardSpacingMax = 110f;

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

        tableUI.drawPileCardBack = drawPileGo.GetComponent<Button>();
        tableUI.drawPileCountText = GameObject.Find("DrawPileCountText")?.GetComponent<TextMeshProUGUI>();
        tableUI.phaseText = phaseLabel.GetComponent<TextMeshProUGUI>();
        tableUI.currentPlayerText = turnLabel.GetComponent<TextMeshProUGUI>();
        tableUI.warningText = warningLabel.GetComponent<TextMeshProUGUI>();
        tableUI.scoreContainer = scoreContainer.transform;
        tableUI.discardDropZone = dzRT;
        tableUI.gameOverOverlay = overlay;
        tableUI.overlayText = overlayTmp;
        tableUI.overlayButton = overlayBtn;
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
        // Card root — wider for readability
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

        // Top-left rank — anchored top-left, inset from edge
        var topRank = new GameObject("TopRank", typeof(RectTransform), typeof(TextMeshProUGUI));
        topRank.transform.SetParent(card.transform, false);
        var trRT = topRank.GetComponent<RectTransform>();
        trRT.anchorMin = new Vector2(0, 1);
        trRT.anchorMax = new Vector2(0, 1);
        trRT.pivot = new Vector2(0, 1);
        trRT.sizeDelta = new Vector2(40, 22);
        trRT.anchoredPosition = new Vector2(5, -5);
        var trTMP = topRank.GetComponent<TextMeshProUGUI>();
        trTMP.text = "A"; trTMP.fontSize = 16; trTMP.color = Color.black;
        trTMP.alignment = TextAlignmentOptions.TopLeft; trTMP.raycastTarget = false;

        // Top-left suit
        var topSuit = new GameObject("TopSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        topSuit.transform.SetParent(card.transform, false);
        var tsRT = topSuit.GetComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0, 1);
        tsRT.anchorMax = new Vector2(0, 1);
        tsRT.pivot = new Vector2(0, 1);
        tsRT.sizeDelta = new Vector2(22, 20);
        tsRT.anchoredPosition = new Vector2(5, -24);
        var tsTMP = topSuit.GetComponent<TextMeshProUGUI>();
        tsTMP.text = "♠"; tsTMP.fontSize = 14; tsTMP.color = Color.black;
        tsTMP.alignment = TextAlignmentOptions.TopLeft; tsTMP.raycastTarget = false;

        // Center suit (large) — fill entire card
        var centerSuit = new GameObject("CenterSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        centerSuit.transform.SetParent(card.transform, false);
        var csRT = centerSuit.GetComponent<RectTransform>();
        csRT.anchorMin = Vector2.zero;
        csRT.anchorMax = Vector2.one;
        csRT.offsetMin = Vector2.zero;
        csRT.offsetMax = Vector2.zero;
        var csTMP = centerSuit.GetComponent<TextMeshProUGUI>();
        csTMP.text = "♠"; csTMP.fontSize = 40; csTMP.color = Color.black;
        csTMP.alignment = TextAlignmentOptions.Center; csTMP.raycastTarget = false;

        // Bottom-right rank — anchored bottom-right
        var botRank = new GameObject("BotRank", typeof(RectTransform), typeof(TextMeshProUGUI));
        botRank.transform.SetParent(card.transform, false);
        var brRT = botRank.GetComponent<RectTransform>();
        brRT.anchorMin = new Vector2(1, 0);
        brRT.anchorMax = new Vector2(1, 0);
        brRT.pivot = new Vector2(1, 0);
        brRT.sizeDelta = new Vector2(40, 22);
        brRT.anchoredPosition = new Vector2(-5, 5);
        var brTMP = botRank.GetComponent<TextMeshProUGUI>();
        brTMP.text = "A"; brTMP.fontSize = 16; brTMP.color = Color.black;
        brTMP.alignment = TextAlignmentOptions.BottomRight; brTMP.raycastTarget = false;

        // Bottom-right suit
        var botSuit = new GameObject("BotSuit", typeof(RectTransform), typeof(TextMeshProUGUI));
        botSuit.transform.SetParent(card.transform, false);
        var bsRT = botSuit.GetComponent<RectTransform>();
        bsRT.anchorMin = new Vector2(1, 0);
        bsRT.anchorMax = new Vector2(1, 0);
        bsRT.pivot = new Vector2(1, 0);
        bsRT.sizeDelta = new Vector2(22, 20);
        bsRT.anchoredPosition = new Vector2(-5, 24);
        var bsTMP = botSuit.GetComponent<TextMeshProUGUI>();
        bsTMP.text = "♠"; bsTMP.fontSize = 14; bsTMP.color = Color.black;
        bsTMP.alignment = TextAlignmentOptions.BottomRight; bsTMP.raycastTarget = false;

        // Wire CardView
        var cv = card.GetComponent<CardView>();
        cv.cardBackground = card.GetComponent<Image>();
        cv.selectionBorder = borderImg;
        cv.topRankText = trTMP;
        cv.topSuitText = tsTMP;
        cv.centerSuitText = csTMP;
        cv.bottomRankText = brTMP;
        cv.bottomSuitText = bsTMP;

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
