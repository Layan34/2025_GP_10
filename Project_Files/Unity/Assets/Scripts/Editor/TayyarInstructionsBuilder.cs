#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

public class TayyarInstructionsBuilder : EditorWindow
{
    const string TXT_BEFORE_TITLE = "Before you begin"; // Section title shown before gameplay starts.
    const string TXT_ROW1_MAIN   = "1.  Put on the EEG headset";
    const string TXT_ROW1_SUB    = "Wear your EEG headset and confirm the signal is stable before starting.";
    const string TXT_ROW2_MAIN   = "2.  Sit comfortably and stay still";
    const string TXT_ROW2_SUB    = "Minimize movement during the session to keep EEG readings accurate.";

    const string TXT_HOW_TITLE   = "How to play"; // Section title for game controls and rules.
    const string TXT_HOW1        = "Use the arrow keys to move your airplane balloon up and down.";
    const string TXT_HOW2        = "Fly through the ring to earn points.";
    const string TXT_HOW3        = "Avoid the bomb.";
    const string TXT_HOW4        = "You have 300 trials — stay focused for the whole session.";

    const string TXT_DIFF_TITLE  = "Difficulty levels"; // Section title for adaptive difficulty info.
    const string TXT_EASY        = "Easy";
    const string TXT_EASY_SUB   = "Slow pace, easy to react.";
    const string TXT_MED         = "Medium";
    const string TXT_MED_SUB    = "Faster objects, stay alert.";
    const string TXT_HARD        = "Hard";
    const string TXT_HARD_SUB   = "Very fast, full focus needed.";
    const string TXT_DIFF_NOTE   = "Difficulty adjusts automatically based on your performance.";

    const string TXT_EEG_NOTE    = "Your attention level is tracked throughout the session via the EEG headset.";

    static Color COL_GREEN      => HexColor("#2E8664");
    static Color COL_GREEN_BG   => HexColor("#E8F5EE");
    static Color COL_EASY_BG    => HexColor("#D6EFE2");
    static Color COL_MED_BG     => HexColor("#FDF0D5");
    static Color COL_HARD_BG    => HexColor("#FAE0E0");
    static Color COL_EASY_TXT   => HexColor("#1A5C44");
    static Color COL_MED_TXT    => HexColor("#7A4A00");
    static Color COL_HARD_TXT   => HexColor("#8B1A1A");
    static Color COL_DARK       => HexColor("#111111");
    static Color COL_GRAY       => HexColor("#444444");

    const float FS_SECTION = 26f;
    const float FS_MAIN    = 22f;
    const float FS_SUB     = 19f;
    const float FS_NOTE    = 17f;
    const float FS_CARD_LB = 21f;
    const float FS_CARD_SB = 17f;

        [MenuItem("Tools/Tayyar/Build Instructions UI")]
    static void Build()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null || root.name != "Instructions") // Builder must run on the selected Instructions object.
        {
            EditorUtility.DisplayDialog(
                "Tayyar Instructions Builder",
                "Please select the 'Instructions' GameObject in the Hierarchy first.",
                "OK");
            return;
        }

        for (int i = root.transform.childCount - 1; i >= 0; i--) // Remove old generated UI elements.
            DestroyImmediate(root.transform.GetChild(i).gameObject);

        var rootVlg = root.GetComponent<VerticalLayoutGroup>() ?? root.AddComponent<VerticalLayoutGroup>();
        rootVlg.padding              = new RectOffset(24, 24, 20, 20);
        rootVlg.spacing              = 18;
        rootVlg.childControlWidth    = true;  // Let the layout resize children horizontally.
        rootVlg.childControlHeight   = true;  // Let the layout resize children vertically.
        rootVlg.childForceExpandWidth  = true;
        rootVlg.childForceExpandHeight = false;

        var rootFitter = root.GetComponent<ContentSizeFitter>() ?? root.AddComponent<ContentSizeFitter>();
        rootFitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        rootFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var beforePanel = MakePanel(root, "BeforePanel", COL_GREEN_BG, padding: 16, spacing: 10); // Creates the preparation section.
        MakeLabel(beforePanel, "BeforeTitle", TXT_BEFORE_TITLE, FS_SECTION, COL_GREEN, bold: true);
        MakeRow(beforePanel, "Row1", TXT_ROW1_MAIN, TXT_ROW1_SUB);
        MakeRow(beforePanel, "Row2", TXT_ROW2_MAIN, TXT_ROW2_SUB);

        var howPanel = MakePanel(root, "HowToPlayPanel", Color.clear, padding: 4, spacing: 10); // Creates the game rules section.
        MakeLabel(howPanel, "HowTitle", TXT_HOW_TITLE, FS_SECTION, COL_GREEN, bold: true);
        MakeBulletRow(howPanel, "How1", "→", TXT_HOW1);
        MakeBulletRow(howPanel, "How2", "→", TXT_HOW2);
        MakeBulletRow(howPanel, "How3", "→", TXT_HOW3);
        MakeBulletRow(howPanel, "How4", "→", TXT_HOW4);

        var diffPanel = MakePanel(root, "DifficultyPanel", Color.clear, padding: 4, spacing: 12); // Creates the difficulty explanation section.
        MakeLabel(diffPanel, "DiffTitle", TXT_DIFF_TITLE, FS_SECTION, COL_GREEN, bold: true);

        var cardsRow = MakeEmpty(diffPanel, "CardsRow");
        var hlg = cardsRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 12;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;
        var cardsLE = cardsRow.AddComponent<LayoutElement>();
        cardsLE.minHeight       = 90;
        cardsLE.preferredHeight = 90;
        cardsLE.flexibleHeight  = 0;

        MakeDiffCard(cardsRow, "EasyCard",   TXT_EASY, TXT_EASY_SUB, COL_EASY_BG, COL_EASY_TXT);
        MakeDiffCard(cardsRow, "MediumCard", TXT_MED,  TXT_MED_SUB,  COL_MED_BG,  COL_MED_TXT);
        MakeDiffCard(cardsRow, "HardCard",   TXT_HARD, TXT_HARD_SUB, COL_HARD_BG, COL_HARD_TXT);

        MakeLabel(diffPanel, "DiffNote", TXT_DIFF_NOTE, FS_NOTE, COL_GRAY, bold: false);

        var eegGo  = MakeLabel(root, "EegNote", TXT_EEG_NOTE, FS_NOTE, COL_GRAY, bold: false);
        var eegTmp = eegGo.GetComponent<TMP_Text>();
        if (eegTmp != null) eegTmp.alignment = TextAlignmentOptions.Center; // Center the final EEG note.

        EditorUtility.SetDirty(root);
        Debug.Log("[TayyarInstructionsBuilder] Done!");
        EditorUtility.DisplayDialog("Done", "Instructions UI built successfully!", "OK");
    }


    static GameObject MakeEmpty(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); // UI objects need RectTransform.
        go.transform.SetParent(parent.transform, false); // Keep local UI scale and position stable.
        return go;
    }

    static GameObject MakePanel(GameObject parent, string name, Color bg, int padding, int spacing)
    {
        var go  = MakeEmpty(parent, name);
        var img = go.AddComponent<Image>();
        img.color         = bg;
        img.raycastTarget = false; // Decorative panels should not block clicks.

        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(padding, padding, padding, padding);
        vlg.spacing              = spacing;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return go;
    }

    static GameObject MakeLabel(GameObject parent, string name, string text,
        float size, Color color, bool bold)
    {
        var go  = MakeEmpty(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.fontStyle          = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.enableWordWrapping = true; // Wrap long text inside the UI width.
        tmp.raycastTarget      = false; // Text should not block UI clicks.

        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        return go;
    }

    static void MakeRow(GameObject parent, string rowName, string mainText, string subText)
    {
        var row = MakeEmpty(parent, rowName);
        var vlg = row.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 4;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var csf = row.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        MakeLabel(row, rowName + "_Main", mainText, FS_MAIN, COL_DARK, bold: true);
        MakeLabel(row, rowName + "_Sub",  subText,  FS_SUB,  COL_GRAY, bold: false);
    }

    static void MakeBulletRow(GameObject parent, string rowName, string bullet, string text)
    {
        var row = MakeEmpty(parent, rowName);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing              = 10;
        hlg.childControlWidth    = true;
        hlg.childControlHeight   = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var csf = row.AddComponent<ContentSizeFitter>();
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        var bgo  = MakeEmpty(row, "Bullet");
        var btmp = bgo.AddComponent<TextMeshProUGUI>();
        btmp.text               = bullet;
        btmp.fontSize           = FS_MAIN;
        btmp.color              = COL_GREEN;
        btmp.enableWordWrapping = false;
        btmp.raycastTarget      = false; // Text should not block UI clicks.
        var ble = bgo.AddComponent<LayoutElement>();
        ble.minWidth       = 24;
        ble.preferredWidth = 24;
        ble.flexibleWidth  = 0;
        var tgo  = MakeEmpty(row, "Text");
        var ttmp = tgo.AddComponent<TextMeshProUGUI>();
        ttmp.text               = text;
        ttmp.fontSize           = FS_MAIN;
        ttmp.color              = COL_DARK;
        ttmp.enableWordWrapping = true; // Wrap long text inside the UI width.
        ttmp.raycastTarget      = false; // Text should not block UI clicks.
        var tle = tgo.AddComponent<LayoutElement>();
        tle.flexibleWidth = 1;

        var tcsf = tgo.AddComponent<ContentSizeFitter>();
        tcsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        tcsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    static void MakeDiffCard(GameObject parent, string name,
        string label, string sub, Color bg, Color textColor)
    {
        var card = MakeEmpty(parent, name);
        var img  = card.AddComponent<Image>();
        img.color         = bg;
        img.raycastTarget = false; // Decorative panels should not block clicks.

        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding              = new RectOffset(12, 12, 10, 10);
        vlg.spacing              = 5;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        var le = card.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1;
        le.flexibleHeight = 1;

        MakeLabel(card, "Label", label, FS_CARD_LB, textColor, bold: true);
        MakeLabel(card, "Sub",   sub,   FS_CARD_SB, textColor, bold: false);
    }

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
#endif
