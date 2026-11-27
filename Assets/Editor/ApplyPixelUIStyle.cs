using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class ApplyPixelUIStyle
{
    const string SpritesDir = "Assets/Textures/UI/";
    const string FontPath = "Assets/Fonts/PressStart2P-SDF.asset";

    [MenuItem("LarpSeek/Apply Pixel UI Style")]
    public static void ApplyStyle()
    {
        var coralNormal = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Coral_Normal.png");
        var coralHover = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Coral_Hover.png");
        var coralPressed = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Coral_Pressed.png");

        var quitNormal = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Quit_Normal.png");
        var quitHover = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Quit_Hover.png");
        var quitPressed = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBtn_Quit_Pressed.png");

        var panelCard = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelPanel_Card.png");
        var badgeFrame = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBadge_Frame.png");
        var barBg = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBar_Bg.png");
        var barFill = AssetDatabase.LoadAssetAtPath<Sprite>(SpritesDir + "PixelBar_Fill.png");

        var pixelFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        if (coralNormal == null || panelCard == null || pixelFont == null)
        {
            Debug.LogError("[ApplyPixelUIStyle] Missing sprites or font asset! Please run GeneratePixelUISprites first.");
            return;
        }

        var canvas = GameObject.Find("MultiplayerCanvas");
        if (canvas == null)
        {
            Debug.LogError("[ApplyPixelUIStyle] MultiplayerCanvas not found!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvas, "Apply Pixel UI Style");

        // 1. STYLE PAUSE PANEL
        var pausePanel = canvas.transform.Find("PausePanel");
        if (pausePanel != null)
        {
            var overlayImg = pausePanel.GetComponent<UnityEngine.UI.Image>();
            if (overlayImg != null)
            {
                overlayImg.color = new Color(0.04f, 0.01f, 0.03f, 0.75f);
            }

            var card = pausePanel.Find("PausePanel_Card");
            if (card != null)
            {
                // Remove obsolete outline/shadow if any
                var oldOutline = card.GetComponent<Outline>();
                if (oldOutline != null) Object.DestroyImmediate(oldOutline);
                var oldShadow = card.GetComponent<Shadow>();
                if (oldShadow != null) Object.DestroyImmediate(oldShadow);

                var cardRt = card.GetComponent<RectTransform>();
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.sizeDelta = new Vector2(400f, 430f);

                var cardImg = card.GetComponent<UnityEngine.UI.Image>();
                if (cardImg != null)
                {
                    cardImg.sprite = panelCard;
                    cardImg.type = UnityEngine.UI.Image.Type.Sliced;
                    cardImg.color = Color.white;
                }

                // Header Banner
                var header = card.Find("Header");
                if (header != null)
                {
                    header.SetSiblingIndex(0);
                    var headerRt = header.GetComponent<RectTransform>();
                    headerRt.anchorMin = new Vector2(0.5f, 1f);
                    headerRt.anchorMax = new Vector2(0.5f, 1f);
                    headerRt.pivot = new Vector2(0.5f, 1f);
                    headerRt.anchoredPosition = new Vector2(0f, -22f);
                    headerRt.sizeDelta = new Vector2(340f, 52f);

                    var headerImg = header.GetComponent<UnityEngine.UI.Image>();
                    if (headerImg != null)
                    {
                        headerImg.sprite = coralNormal;
                        headerImg.type = UnityEngine.UI.Image.Type.Sliced;
                        headerImg.color = Color.white;
                    }

                    var headerTxt = header.GetComponentInChildren<TMP_Text>();
                    if (headerTxt != null)
                    {
                        headerTxt.font = pixelFont;
                        headerTxt.text = "||  PAUSE  ||";
                        headerTxt.fontSize = 17f;
                        headerTxt.fontStyle = FontStyles.Bold;
                        headerTxt.color = Color.white;
                        headerTxt.alignment = TextAlignmentOptions.Center;

                        var hTxtRt = headerTxt.GetComponent<RectTransform>();
                        hTxtRt.anchorMin = Vector2.zero;
                        hTxtRt.anchorMax = Vector2.one;
                        hTxtRt.offsetMin = new Vector2(0f, 4f);
                        hTxtRt.offsetMax = new Vector2(0f, 2f);
                    }
                }

                // Ensure PauseGraphicsButton exists
                var pauseGraphicsBtn = card.Find("PauseGraphicsButton");
                if (pauseGraphicsBtn == null)
                {
                    var resumeBtn = card.Find("ResumeButton");
                    if (resumeBtn != null)
                    {
                        var newBtn = Object.Instantiate(resumeBtn.gameObject, card, false);
                        newBtn.name = "PauseGraphicsButton";
                        pauseGraphicsBtn = newBtn.transform;
                    }
                }

                // Layout all 5 buttons
                float startY = -88f;
                float btnSpacing = 62f;
                int idx = 0;

                // 1. Resume
                var resumeT = card.Find("ResumeButton");
                if (resumeT != null)
                {
                    resumeT.SetSiblingIndex(1);
                    StyleButton(resumeT, coralNormal, coralHover, coralPressed, pixelFont,
                        "▶  RESUME", 15f, new Vector2(0f, startY - (idx++ * btnSpacing)), new Vector2(340f, 52f));
                }

                // 2. Graphics
                if (pauseGraphicsBtn != null)
                {
                    pauseGraphicsBtn.SetSiblingIndex(2);
                    StyleButton(pauseGraphicsBtn, coralNormal, coralHover, coralPressed, pixelFont,
                        "GRAPHICS: HIGH", 13f, new Vector2(0f, startY - (idx++ * btnSpacing)), new Vector2(340f, 52f));
                }

                // 3. Copy Code
                var copyT = card.Find("PauseCopyCodeButton");
                if (copyT != null)
                {
                    copyT.SetSiblingIndex(3);
                    StyleButton(copyT, coralNormal, coralHover, coralPressed, pixelFont,
                        "COPY LOBBY CODE", 13f, new Vector2(0f, startY - (idx++ * btnSpacing)), new Vector2(340f, 52f));
                }

                // 4. Disconnect
                var discT = card.Find("DisconnectButton");
                if (discT != null)
                {
                    discT.SetSiblingIndex(4);
                    StyleButton(discT, coralNormal, coralHover, coralPressed, pixelFont,
                        "DISCONNECT", 14f, new Vector2(0f, startY - (idx++ * btnSpacing)), new Vector2(340f, 52f));
                }

                // 5. Quit
                var quitT = card.Find("PauseQuitButton");
                if (quitT != null)
                {
                    quitT.SetSiblingIndex(5);
                    StyleButton(quitT, quitNormal, quitHover, quitPressed, pixelFont,
                        "QUIT GAME", 14f, new Vector2(0f, startY - (idx++ * btnSpacing)), new Vector2(340f, 52f));
                }

                cardRt.sizeDelta = new Vector2(400f, Mathf.Abs(startY - (idx * btnSpacing)) + 26f);
            }
        }

        // 2. STYLE HUD TOP CENTER WIDGET (5-MINUTE TIMER & BEACON)
        var hudPanel = canvas.transform.Find("HudPanel");
        if (hudPanel != null)
        {
            var topCenter = hudPanel.Find("HudTopCenterWidget");
            if (topCenter != null)
            {
                var oldOutline = topCenter.GetComponent<Outline>();
                if (oldOutline != null) Object.DestroyImmediate(oldOutline);

                var tcRt = topCenter.GetComponent<RectTransform>();
                tcRt.anchorMin = new Vector2(0.5f, 1f);
                tcRt.anchorMax = new Vector2(0.5f, 1f);
                tcRt.pivot = new Vector2(0.5f, 1f);
                tcRt.anchoredPosition = new Vector2(0f, -16f);
                tcRt.sizeDelta = new Vector2(420f, 140f);

                var tcImg = topCenter.GetComponent<UnityEngine.UI.Image>();
                if (tcImg != null)
                {
                    tcImg.sprite = panelCard;
                    tcImg.type = UnityEngine.UI.Image.Type.Sliced;
                    tcImg.color = Color.white;
                }

                // Find or create TimerPill
                var timerPill = topCenter.Find("TimerPill");
                if (timerPill == null)
                {
                    var pillGo = new GameObject("TimerPill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    pillGo.transform.SetParent(topCenter, false);
                    timerPill = pillGo.transform;
                }
                timerPill.SetSiblingIndex(0);

                var pillRt = timerPill.GetComponent<RectTransform>();
                pillRt.anchorMin = new Vector2(0.5f, 1f);
                pillRt.anchorMax = new Vector2(0.5f, 1f);
                pillRt.pivot = new Vector2(0.5f, 1f);
                pillRt.anchoredPosition = new Vector2(0f, -14f);
                pillRt.sizeDelta = new Vector2(220f, 46f);

                var pillImg = timerPill.GetComponent<UnityEngine.UI.Image>();
                pillImg.sprite = coralNormal;
                pillImg.type = UnityEngine.UI.Image.Type.Sliced;
                pillImg.color = Color.white;

                // Move timer text inside TimerPill
                var timerTxt = topCenter.Find("HudMatchTimerText") ?? timerPill.Find("HudMatchTimerText");
                if (timerTxt != null)
                {
                    timerTxt.SetParent(timerPill, false);
                    var txtComp = timerTxt.GetComponent<TMP_Text>();
                    if (txtComp != null)
                    {
                        txtComp.font = pixelFont;
                        txtComp.fontSize = 22f;
                        txtComp.fontStyle = FontStyles.Bold;
                        txtComp.color = Color.white;
                        txtComp.alignment = TextAlignmentOptions.Center;
                    }
                    var txtRt = timerTxt.GetComponent<RectTransform>();
                    txtRt.anchorMin = Vector2.zero;
                    txtRt.anchorMax = Vector2.one;
                    txtRt.offsetMin = new Vector2(0f, 4f);
                    txtRt.offsetMax = new Vector2(0f, 2f);
                }

                // Beacon Progress Text
                var beaconTxt = topCenter.Find("HudBeaconProgressText");
                if (beaconTxt != null)
                {
                    beaconTxt.SetSiblingIndex(1);
                    var bRt = beaconTxt.GetComponent<RectTransform>();
                    bRt.anchorMin = new Vector2(0.5f, 1f);
                    bRt.anchorMax = new Vector2(0.5f, 1f);
                    bRt.pivot = new Vector2(0.5f, 1f);
                    bRt.anchoredPosition = new Vector2(0f, -70f);
                    bRt.sizeDelta = new Vector2(390f, 20f);

                    var bComp = beaconTxt.GetComponent<TMP_Text>();
                    if (bComp != null)
                    {
                        bComp.font = pixelFont;
                        bComp.fontSize = 11f;
                        bComp.color = new Color(1f, 0.78f, 0.88f);
                        bComp.alignment = TextAlignmentOptions.Center;
                    }
                }

                // Beacon Progress Bar Background & Fill
                var barBgT = topCenter.Find("BeaconProgressBarBg");
                if (barBgT != null)
                {
                    barBgT.SetSiblingIndex(2);
                    var bgRt = barBgT.GetComponent<RectTransform>();
                    bgRt.anchorMin = new Vector2(0.5f, 1f);
                    bgRt.anchorMax = new Vector2(0.5f, 1f);
                    bgRt.pivot = new Vector2(0.5f, 1f);
                    bgRt.anchoredPosition = new Vector2(0f, -98f);
                    bgRt.sizeDelta = new Vector2(380f, 22f);

                    var bgImg = barBgT.GetComponent<UnityEngine.UI.Image>();
                    if (bgImg != null)
                    {
                        bgImg.sprite = barBg;
                        bgImg.type = UnityEngine.UI.Image.Type.Sliced;
                        bgImg.color = Color.white;
                    }

                    var barFillT = barBgT.Find("BeaconProgressBarFill");
                    if (barFillT != null)
                    {
                        var fillRt = barFillT.GetComponent<RectTransform>();
                        fillRt.anchorMin = Vector2.zero;
                        fillRt.anchorMax = Vector2.one;
                        fillRt.offsetMin = new Vector2(3f, 3f);
                        fillRt.offsetMax = new Vector2(-3f, -3f);

                        var fillImg = barFillT.GetComponent<UnityEngine.UI.Image>();
                        if (fillImg != null)
                        {
                            fillImg.sprite = barFill;
                            fillImg.type = UnityEngine.UI.Image.Type.Filled;
                            fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                            fillImg.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
                            fillImg.color = Color.white;
                        }
                    }
                }
            }

            // 3. STYLE TOP LEFT INFO CARD
            var topLeft = hudPanel.Find("HudTopLeftCard");
            if (topLeft != null)
            {
                var tlRt = topLeft.GetComponent<RectTransform>();
                tlRt.anchorMin = new Vector2(0f, 1f);
                tlRt.anchorMax = new Vector2(0f, 1f);
                tlRt.pivot = new Vector2(0f, 1f);
                tlRt.anchoredPosition = new Vector2(20f, -16f);
                tlRt.sizeDelta = new Vector2(260f, 100f);

                var tlImg = topLeft.GetComponent<UnityEngine.UI.Image>();
                if (tlImg != null)
                {
                    tlImg.sprite = panelCard;
                    tlImg.type = UnityEngine.UI.Image.Type.Sliced;
                    tlImg.color = Color.white;
                }

                foreach (var txt in topLeft.GetComponentsInChildren<TMP_Text>(true))
                {
                    txt.font = pixelFont;
                    txt.fontSize = 10f;
                    txt.color = Color.white;
                }
            }

            // 4. STYLE BOTTOM ROLE & STATUS BAR
            var bottomBar = hudPanel.Find("HudBottomBar");
            if (bottomBar != null)
            {
                var bbRt = bottomBar.GetComponent<RectTransform>();
                bbRt.anchorMin = new Vector2(0.5f, 0f);
                bbRt.anchorMax = new Vector2(0.5f, 0f);
                bbRt.pivot = new Vector2(0.5f, 0f);
                bbRt.anchoredPosition = new Vector2(0f, 16f);
                bbRt.sizeDelta = new Vector2(800f, 85f);

                var bbImg = bottomBar.GetComponent<UnityEngine.UI.Image>();
                if (bbImg != null)
                {
                    bbImg.sprite = panelCard;
                    bbImg.type = UnityEngine.UI.Image.Type.Sliced;
                    bbImg.color = Color.white;
                }

                foreach (var txt in bottomBar.GetComponentsInChildren<TMP_Text>(true))
                {
                    txt.font = pixelFont;
                    if (txt.name.Contains("Role") || txt.name.Contains("Score"))
                        txt.fontSize = 12f;
                    else
                        txt.fontSize = 10f;
                }
            }
        }

        EditorUtility.SetDirty(canvas);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ApplyPixelUIStyle] Successfully applied Image 3 pixel style to Pause Menu and In-Match HUD!");
    }

    static void StyleButton(Transform btnTrans, Sprite normal, Sprite hover, Sprite pressed, TMP_FontAsset font, string defaultText, float fontSize, Vector2 anchoredPos, Vector2 size)
    {
        if (btnTrans == null) return;

        var rt = btnTrans.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var img = btnTrans.GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            img.sprite = normal;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = Color.white;
        }

        var btn = btnTrans.GetComponent<Button>();
        if (btn != null)
        {
            btn.transition = Selectable.Transition.SpriteSwap;
            var ss = new SpriteState
            {
                highlightedSprite = hover,
                pressedSprite = pressed,
                selectedSprite = hover,
                disabledSprite = normal
            };
            btn.spriteState = ss;
        }

        var txt = btnTrans.GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            txt.font = font;
            if (!string.IsNullOrEmpty(defaultText))
                txt.text = defaultText;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;

            var txtRt = txt.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(10f, 4f);
            txtRt.offsetMax = new Vector2(-10f, 2f);
        }
    }
}
