#if UNITY_EDITOR
//#define MPTK_PRO
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{
    public class MPTKGui
    {
        // https://github.com/halak/unity-editor-icons
        // https://github.com/nukadelic/UnityEditorIcons
        static private Texture iconComboBox; static public Texture IconComboBox { get { if (iconComboBox == null) iconComboBox = EditorGUIUtility.IconContent("d_icon dropdown").image; return iconComboBox; } }
        static private Texture iconFirst; static public Texture IconFirst { get { if (iconFirst == null) iconFirst = EditorGUIUtility.IconContent("d_Animation.FirstKey").image; return iconFirst; } }
        static private Texture iconPrevious; static public Texture IconPrevious { get { if (iconPrevious == null) iconPrevious = EditorGUIUtility.IconContent("d_Animation.PrevKey").image; return iconPrevious; } }
        static private Texture iconNext; static public Texture IconNext { get { if (iconNext == null) iconNext = EditorGUIUtility.IconContent("d_Animation.NextKey").image; return iconNext; } }
        static private Texture iconLast; static public Texture IconLast { get { if (iconLast == null) iconLast = EditorGUIUtility.IconContent("d_Animation.LastKey").image; return iconLast; } }
        static private Texture iconHelp; static public Texture IconHelp { get { if (iconHelp == null) iconHelp = Resources.Load<Texture2D>("Textures/question"); return iconHelp; } }
        static private Texture iconHelpBlack; static public Texture IconHelpBlack { get { if (iconHelpBlack == null) iconHelpBlack = Resources.Load<Texture2D>("Textures/questionBlack"); return iconHelpBlack; } }
        static private Texture iconEye; static public Texture IconEye { get { if (iconEye == null) iconEye = EditorGUIUtility.IconContent("d_ViewToolOrbit").image; return iconEye; } }
        static private Texture iconSave; static public Texture IconSave { get { if (iconSave == null) iconSave = EditorGUIUtility.IconContent("SaveActive").image; return iconSave; } }
        static private Texture iconFolders; static public Texture IconFolders { get { if (iconFolders == null) iconFolders = EditorGUIUtility.IconContent("Folder On Icon").image; return iconFolders; } }
        static private Texture iconDeleteGray; static public Texture IconDeleteGray { get { if (iconDeleteGray == null) iconDeleteGray = Resources.Load<Texture2D>("Textures/Delete_32x32_gray"); return iconDeleteGray; } }
        static private Texture iconDeleteRed; static public Texture IconDeleteRed { get { if (iconDeleteRed == null) iconDeleteRed = Resources.Load<Texture2D>("Textures/Delete_32x32"); return iconDeleteRed; } }
        static private Texture iconClose; static public Texture IconClose { get { if (iconClose == null) iconClose = EditorGUIUtility.IconContent("winbtn_win_close").image; return iconClose; } }
        static private Texture iconRefresh; static public Texture IconRefresh { get { if (iconRefresh == null) iconRefresh = EditorGUIUtility.IconContent("d_TreeEditor.Refresh").image; return iconRefresh; } }
        static private Texture tabNext; static public Texture IconTabNext { get { if (tabNext == null) tabNext = EditorGUIUtility.IconContent("d_forward").image; return tabNext; } }
        static private Texture tabPrev; static public Texture IconTabPrev { get { if (tabPrev == null) tabPrev = EditorGUIUtility.IconContent("d_back").image; return tabPrev; } }
        static private Texture iconClear; static public Texture IconClear { get { if (iconClear == null) iconClear = EditorGUIUtility.IconContent("clear").image; return iconClear; } }
        


        //static public Texture IconGoToEnd = LoadIcon(IconGoToEnd, "");

        public static Color ButtonColor = new Color(.7f, .9f, .7f, 1f);

        public static GUIStyle Label { get { return MaestroSkin.GetStyle("label"); } }

        private static GUIStyle labelLeft;
        public static GUIStyle LabelLeft { get { if (labelLeft == null) labelLeft = MPTKGui.BuildStyle(inheritedStyle: MPTKGui.MaestroSkin.GetStyle("label"), fontSize: 12, textAnchor: TextAnchor.MiddleLeft); return labelLeft; } }

        private static GUIStyle labelRight;
        public static GUIStyle LabelRight { get { if (labelRight == null) labelRight = MPTKGui.BuildStyle(inheritedStyle: MPTKGui.MaestroSkin.GetStyle("label"), fontSize: 12, textAnchor: TextAnchor.MiddleRight); return labelRight; } }

        private static GUIStyle labelCenter;
        public static GUIStyle LabelCenter { get { if (labelCenter == null) labelCenter = MPTKGui.BuildStyle(inheritedStyle: MPTKGui.MaestroSkin.GetStyle("label"), fontSize: 12, textAnchor: TextAnchor.MiddleCenter); return labelCenter; } }

        private static GUIStyle labelCenterSmall;
        public static GUIStyle LabelCenterSmall { get { if (labelCenterSmall == null) labelCenterSmall = MPTKGui.BuildStyle(inheritedStyle: MPTKGui.MaestroSkin.GetStyle("label"), fontSize: 9, textAnchor: TextAnchor.MiddleCenter); return labelCenterSmall; } }

        public static GUIStyle LabelListPlayed { get { return MaestroSkin.GetStyle("LabelListPlayed"); } }
        public static GUIStyle LabelListSelected { get { return MaestroSkin.GetStyle("LabelListSelected"); } }
        //public static GUIStyle LabelListNormal { get { return MidiCommonEditor.MaestroSkin.GetStyle("LabelListNormal"); } }
        public static GUIStyle LabelListNormal { get { return MaestroSkin.GetStyle("LabelListNormal"); } }
        public static GUIStyle ButtonCombo { get { return MaestroSkin.GetStyle("ButtonCombo"); } }
        public static GUIStyle ButtonHighLight { get { return MaestroSkin.GetStyle("ButtonHighLight"); } }
        public static GUIStyle Button { get { return MaestroSkin.GetStyle("button"); } }

        private static GUIStyle buttonSmall;
        public static GUIStyle ButtonSmall 
        {  
            get 
            {
                if (buttonSmall == null)
                {
                    buttonSmall = MPTKGui.BuildStyle(inheritedStyle: MPTKGui.MaestroSkin.GetStyle("label"), fontSize: 12, textAnchor: TextAnchor.MiddleRight);
                    //buttonSmall.margin= new RectOffset(0,0,0, 0);
                    //buttonSmall.contentOffset = Vector2.zero;
                    //buttonSmall.overflow = new RectOffset(0, 0, 0, 0);
                    //buttonSmall.stretchHeight = false;
                    //buttonSmall.stretchWidth = false;
                    //buttonSmall.border = new RectOffset(0, 0, 0, 0);
                }
                return buttonSmall;
            } 
        }

        public static GUIStyle TextArea { get { GUIStyle style = MaestroSkin.GetStyle("textarea"); return style; } }
        public static GUIStyle TextField { get { GUIStyle style = MaestroSkin.GetStyle("textfield"); return style; } }
        public static GUIStyle HorizontalSlider { get { return MaestroSkin.GetStyle("horizontalslider"); } }
        public static GUIStyle HorizontalThumb { get { return MaestroSkin.GetStyle("horizontalsliderthumb"); } }
        public static GUIStyle VerticalSlider { get { return MaestroSkin.GetStyle("verticalslider"); } }
        public static GUIStyle VerticalThumb { get { return MaestroSkin.GetStyle("verticalsliderthumb"); } }
        public static GUIStyle LabelGray { get { return MaestroSkin.GetStyle("LabelGray"); } }

        static public CustomStyle myStyle;

        static public GUIStyle styleWindow;
        static public GUIStyle stylePanelGrayMiddle;
        static public GUIStyle stylePanelGrayBlack;
        static public GUIStyle stylePanelGrayLight;
        static public GUIStyle styleBold;
        static public GUIStyle styleAlertRed;
        static public GUIStyle styleRichText;
        static public GUIStyle styleLabelLeft;
        static public GUIStyle styleMiniPullDown;

        static public GUIStyle styleLabelCenter { get { return MaestroSkin.GetStyle("Label"); } }
        static public GUIStyle styleLabelRight { get { return MaestroSkin.GetStyle("LabelRight"); } }
        static public GUIStyle styleListTitle;
        static public GUIStyle styleListRow;
        static public GUIStyle styleListRowLeft;
        static public GUIStyle styleListRowCenter;
        static public GUIStyle styleListRowSelected;
        static public GUIStyle styleToggle { get { return MaestroSkin.GetStyle("toggle"); } }

        static public GUIStyle styleLabelFontCourier;
        //static public float lineHeight = 0f;

        static public GUISkin MaestroSkin;
        static private System.Diagnostics.Stopwatch watchPerf = new System.Diagnostics.Stopwatch();

        static public Texture LoadIcon(string name, Texture icon = null)
        {
            if (icon == null)
            {
                icon = Resources.Load<Texture2D>("Textures/" + name);
                if (icon == null)
                    Debug.LogWarning($"LoadIcon texture not found {name}");
            }
            return icon;
        }

        static public void LoadSkinAndStyle(bool loadSkin = true)
        {
            if (MaestroSkin == null || MaestroSkin.name != "MaestroSkin")
            {
                MaestroSkin = EditorGUIUtility.Load("Assets/MidiPlayer/MaestroSkin.GUISkin") as GUISkin;
                //Debug.Log($"Loaded skin {MaestroSkin.name} {((double)watchPerf.ElapsedTicks) / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d):F2} ms ");
            }

            //Debug.Log($"Custom skin loaded {MaestroSkin.name}");

            int borderSize = 1; // Border size in pixels
            RectOffset rectBorder = new RectOffset(borderSize, borderSize, borderSize, borderSize);

            styleMiniPullDown = new GUIStyle(EditorStyles.miniPullDown);

            styleBold = new GUIStyle(EditorStyles.boldLabel);
            styleBold.fontStyle = FontStyle.Bold;
            styleBold.alignment = TextAnchor.UpperLeft;
            styleBold.normal.textColor = Color.black;

            float grayBlack = 0.1f;
            float grayMiddle = 0.5f;
            float grayLight = 0.7f;
            //        float grayWhite = 0.8f;

            styleWindow = new GUIStyle("box");
            styleWindow.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayMiddle, grayMiddle, grayMiddle, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleWindow.alignment = TextAnchor.MiddleCenter;

            stylePanelGrayMiddle = new GUIStyle("box");
            stylePanelGrayMiddle.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayMiddle, grayMiddle, grayMiddle, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            stylePanelGrayMiddle.alignment = TextAnchor.MiddleCenter;

            stylePanelGrayBlack = new GUIStyle("box");
            stylePanelGrayBlack.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayBlack, grayBlack, grayBlack, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            stylePanelGrayBlack.alignment = TextAnchor.MiddleCenter;

            stylePanelGrayLight = new GUIStyle("box");
            stylePanelGrayLight.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayLight, grayLight, grayLight, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            stylePanelGrayLight.alignment = TextAnchor.MiddleCenter;

            styleListTitle = new GUIStyle("box");
            styleListTitle.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayMiddle, grayMiddle, grayMiddle, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleListTitle.normal.textColor = Color.black;
            styleListTitle.alignment = TextAnchor.MiddleCenter;

            styleListRow = new GUIStyle("box");
            styleListRow.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayLight, grayLight, grayLight, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleListRow.normal.textColor = Color.black;
            styleListRow.alignment = TextAnchor.MiddleCenter;

            styleListRowLeft = new GUIStyle("box");
            styleListRowLeft.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayLight, grayLight, grayLight, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleListRowLeft.normal.textColor = Color.black;
            styleListRowLeft.alignment = TextAnchor.MiddleLeft;

            styleListRowCenter = new GUIStyle("box");
            styleListRowCenter.normal.background = MPTKGui.MakeTex(10, 10, new Color(grayLight, grayLight, grayLight, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleListRowCenter.normal.textColor = Color.black;
            styleListRowCenter.alignment = TextAnchor.MiddleCenter;

            styleListRowSelected = new GUIStyle("box");
            styleListRowSelected.normal.background = MPTKGui.MakeTex(10, 10, new Color(.6f, .8f, .6f, 1f), rectBorder, new Color(grayBlack, grayBlack, grayBlack, 1f));
            styleListRowSelected.normal.background.name = "bckgname"; // kind hack to check if custom style are loaded
            styleListRowSelected.normal.textColor = Color.black;
            styleListRowSelected.alignment = TextAnchor.MiddleCenter;

            styleAlertRed = new GUIStyle(EditorStyles.label);
            styleAlertRed.normal.textColor = new Color(188f / 255f, 56f / 255f, 56f / 255f);
            styleAlertRed.fontSize = 12;

            styleRichText = new GUIStyle(EditorStyles.label);
            styleRichText.richText = true;
            styleRichText.alignment = TextAnchor.UpperLeft;
            styleRichText.normal.textColor = Color.black;

            styleLabelLeft = new GUIStyle(EditorStyles.label);
            styleLabelLeft.alignment = TextAnchor.MiddleLeft;
            styleLabelLeft.normal.textColor = Color.black;

            // Load and set Font
            Font myFont = (Font)Resources.Load("Courier", typeof(Font));
            styleLabelFontCourier = new GUIStyle(EditorStyles.label);
            styleLabelFontCourier.font = myFont;
            styleLabelFontCourier.alignment = TextAnchor.UpperLeft;
            styleLabelFontCourier.normal.textColor = Color.black;
            styleLabelFontCourier.hover.textColor = Color.black;

            // Debug.Log($"End Custom {((double)watchPerf.ElapsedTicks) / ((double)System.Diagnostics.Stopwatch.Frequency / 1000d):F2} ms ");
        }
        public static GUIStyle BuildStyle(GUIStyle inheritedStyle = null, int fontSize = 10, bool wrapText = false,
                                        FontStyle fontStyle = FontStyle.Normal, TextAnchor textAnchor = TextAnchor.MiddleLeft)
        {
            GUIStyle style = inheritedStyle == null ? new GUIStyle() : new GUIStyle(inheritedStyle);
            style.alignment = textAnchor;
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.wordWrap = wrapText;
            style.clipping = TextClipping.Overflow;
            return style;
        }
        public static GUIStyle ColorStyle(GUIStyle style, Color fontColor, Texture2D backColor = null)
        {
            style.normal.textColor = fontColor;
            style.focused.textColor = fontColor;
            style.normal.background = backColor != null ? backColor : style.onNormal.background;
            style.focused.background = backColor != null ? backColor : style.onNormal.background;
            return style;

        }
        public static GUIStyle LabelBoldCentered
        {
            get
            {
                if (labelBoldCentered == null)
                {
                    labelBoldCentered = new GUIStyle(MaestroSkin.GetStyle("label"));
                    labelBoldCentered.wordWrap = true;
                    labelBoldCentered.fontStyle = FontStyle.Bold;
                    labelBoldCentered.alignment = TextAnchor.MiddleCenter;
                }
                return labelBoldCentered;
            }
        }
        static GUIStyle labelBoldCentered;


        static public int IntField(string label = null, int val = 0, int min = 0, int max = 99999999, int maxLength = 10, int widthLabel = 60, int widthText = -1)
        {
            int newval;
            if (label != null)
                GUILayout.Label(label, MPTKGui.LabelLeft, GUILayout.Width(widthLabel));
            if (val < min) val = min;
            if (val > max) val = max;

            string oldtxt = val.ToString();
            string newtxt;
            if (widthText <= 0)
                newtxt = GUILayout.TextField(oldtxt, maxLength: maxLength, MPTKGui.TextField);
            else
                newtxt = GUILayout.TextField(oldtxt, maxLength: maxLength, MPTKGui.TextField, GUILayout.Width(widthText));
            if (newtxt != oldtxt)
                try
                {
                    newval = newtxt.Length > 0 ? Convert.ToInt32(newtxt) : 0;
                    if (newval < min) newval = min;
                    if (newval > max) newval = max;
                    return newval;
                }
                catch { }

            return val;
        }

        static public long LongField(string label = null, long val = 0, long min = 0, long max = 99999999, int maxLength = 10, int widthLabel = 60, int widthText = -1)
        {
            long newval;
            if (label != null)
                GUILayout.Label(label, MPTKGui.LabelLeft, GUILayout.Width(widthLabel));
            if (val < min) val = min;
            if (val > max) val = max;

            string oldtxt = val.ToString();
            string newtxt;
            if (widthText <= 0)
                newtxt = GUILayout.TextField(oldtxt, maxLength: maxLength, MPTKGui.TextField);
            else
                newtxt = GUILayout.TextField(oldtxt, maxLength: maxLength, MPTKGui.TextField, GUILayout.Width(widthText));

            if (newtxt != oldtxt)
                try
                {
                    newval = newtxt.Length > 0 ? Convert.ToInt64(newtxt) : 0;
                    if (newval < min) newval = min;
                    if (newval > max) newval = max;
                    return newval;
                }
                catch { }

            return val;
        }
        /// <summary>
        /// Combobox with GUILayout
        /// </summary>
        /// <param name="p_popup"></param>
        /// <param name="title"></param>
        /// <param name="items"></param>
        /// <param name="selectedIndex"></param>
        /// <param name="action"></param>
        /// <param name="style"></param>
        /// <param name="widthPopup"></param>
        /// <param name="option"></param>
        static public void ComboBox(ref PopupList p_popup, string title, List<StyleItem> items, bool multiSelection, Action<int> action,
            GUIStyle style = null, float widthPopup = 0, params GUILayoutOption[] option)
        {
            ComboBox(Rect.zero, ref p_popup, title, items, multiSelection, action, style, widthPopup, option);
        }

        /// <summary>
        /// Combobox with GUI and rect
        /// </summary>
        /// <param name="rect"></param>
        /// <param name="p_popup"></param>
        /// <param name="title"></param>
        /// <param name="items"></param>
        /// <param name="selectedIndex"></param>
        /// <param name="action"></param>
        /// <param name="style"></param>
        /// <param name="widthPopup"></param>
        /// <param name="option"></param>
        static public void ComboBox(Rect rect, ref PopupList p_popup, string title, List<StyleItem> items, bool multiSelection, Action<int> action,
          GUIStyle style = null, float widthPopup = 0, params GUILayoutOption[] option)
        {
            //Debug.Log(Event.current);
            if (p_popup == null)
            {
                //Debug.Log($"BuildPopup popupLoadType {items.Count}");
                p_popup = new PopupList("", items, multiSelection);
                p_popup.OnSelect = action;
            }

            if (!multiSelection)
            {
                //p_popup.SelectedIndex = selectedIndex;
                // Mono selection
                title = title.Replace("{Label}", p_popup.SelectedLabel).Replace("{Index}", p_popup.SelectedIndex.ToString());
            }
            else
            {
                // Multi selection
                if (title.Contains("{Count}"))
                {
                    string count = $"{p_popup.SelectedCount}/{p_popup.TotalCount}";
                    title = title.Replace("{Count}", count);
                }
                if (title.Contains("{*}"))
                {
                    title = title.Replace("{*}", p_popup.SelectedCount != p_popup.TotalCount ? "*" : "");
                }
            }

            if (style == null)
                // Style for the combo button
                style = MPTKGui.ButtonCombo;
            //else
            //    Debug.Log($"ComboBox style.contentOffset {title} {style.contentOffset}");

            if (rect.width == 0f)
            {
                GUILayout.Label(new GUIContent(title, MPTKGui.IconComboBox), style, option);
                if (Event.current.type == EventType.Repaint)
                {
                    p_popup.RectPopup = GUILayoutUtility.GetLastRect();
                    if (widthPopup != 0)
                        p_popup.RectPopup.width = widthPopup;
                    //Debug.Log($"GetLastRect {title} {p_popup.RectActivation}");
                    p_popup.RectPopup.x += style.contentOffset.x;
                }
            }
            else
            {
                //GUI.Label(rect, new GUIContent(title, MPTKGui.IconComboBox), style);
                GUI.Label(rect, title, style);
                p_popup.RectPopup = rect;
                if (widthPopup != 0)
                    p_popup.RectPopup.width = widthPopup;
                p_popup.RectPopup.x += style.contentOffset.x;
            }
            if (Event.current.type == EventType.MouseDown)
            {
                Rect lastRect = rect.width == 0f ? GUILayoutUtility.GetLastRect() : rect;
                //Debug.Log($"MouseDown style.contentOffset {title} {style.contentOffset}");
                if (lastRect.Contains(Event.current.mousePosition - style.contentOffset))
                {
                    //Debug.Log($"Show PopupWindow {p_popup.RectActivation}");
                    try { PopupWindow.Show(p_popup.RectPopup, p_popup); }
                    catch (ExitGUIException) { } // Unity bug ?
                }
            }
        }

        public class StyleItem
        {
            private float offset;
            private Vector2 offsetV;
            private List<StyleItem> itemPopupContent;

            public string Caption;
            public int Value; // v2.9.0
            public bool Visible;
            public bool Selected;
            public float Width;
            public string Tooltip;
            public bool Hidden;
            public GUIStyle Style;
            /// <summary>
            /// If defined, a popup filter is displayed to filter the list
            /// </summary>
            public PopupList ItemPopup;
            public float ItemPopupWidth;
            public List<StyleItem> ItemPopupContent
            {
                get => itemPopupContent;
                set { itemPopupContent = value; }
            }

            public float Offset { get => offset; set { offset = value; offsetV = new Vector2(value, 0); } }
            public Vector2 OffsetV { get => offsetV; }

            public StyleItem()
            {
                Visible = true;
                Style = MPTKGui.LabelListNormal;
            }

            public StyleItem(string label, bool visible = true, bool selected = false, GUIStyle style = null)
            {
                Caption = label;
                Visible = visible;
                Selected = selected;
                Style = style == null ? MPTKGui.LabelListNormal : style;
            }

            public StyleItem(string label, int value = 0, bool visible = true, bool selected = false, GUIStyle style = null)
            {
                Caption = label;
                Value = value;
                Visible = visible;
                Selected = selected;
                Style = style == null ? MPTKGui.LabelListNormal : style;
            }

        }

        public static Texture2D SetColor(Texture2D tex2, Color32 color)
        {
            var fillColorArray = tex2.GetPixels32();
            for (var i = 0; i < fillColorArray.Length; ++i)
                fillColorArray[i] = color;
            tex2.SetPixels32(fillColorArray);
            tex2.Apply();
            return tex2;
        }

        public static Texture2D MakeTex(float grey, RectOffset border)
        {
            Color color = new Color(grey, grey, grey, 1f);
            return MakeTex(10, 10, color, border, color);
        }

        public static Texture2D MakeTex(Color textureColor, RectOffset border)
        {
            return MakeTex(10, 10, textureColor, border, textureColor);
        }
        public static Texture2D MakeTex(int width, int height, Color textureColor, RectOffset border)
        {
            return MakeTex(width, height, textureColor, border, textureColor);
        }
        public static Texture2D MakeTex(int width, int height, Color textureColor, RectOffset border, Color bordercolor)
        {
            int widthInner = width;
            width += border.left;
            width += border.right;

            Color[] pix = new Color[width * (height + border.top + border.bottom)];

            for (int i = 0; i < pix.Length; i++)
            {
                if (i < (border.bottom * width))
                    pix[i] = bordercolor;
                else if (i >= ((border.bottom * width) + (height * width)))  //Border Top
                    pix[i] = bordercolor;
                else
                { //Center of Texture

                    if ((i % width) < border.left) // Border left
                        pix[i] = bordercolor;
                    else if ((i % width) >= (border.left + widthInner)) //Border right
                        pix[i] = bordercolor;
                    else
                        pix[i] = textureColor;    //Color texture
                }
            }

            Texture2D result = new Texture2D(width, height + border.top + border.bottom);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public static Texture2D MakeTex(Color textureColor)
        {
            Color[] pix = new Color[1];
            pix[0] = textureColor;
            Texture2D result = new Texture2D(1, 1);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public class PopupList : PopupWindowContent
        {
            public int SelectedIndex
            {
                get => selectedIndex;
                set
                {
                    if (value >= 0 && value < listItem.Count && listItem != null)
                    {
                        if (!multiSelection) 
                            listItem.ForEach(item => item.Selected = false);
                        listItem[value].Selected = true;
                        selectedIndex = value;
                        selectedLabel = listItem != null && value >= 0 && value < listItem.Count ? listItem[value].Caption : "unknown";
                    }
                    //else
                    //    Debug.LogWarning($"SelectedIndex {value} not valid");
                }
            }

            public Rect RectPopup;
            public int SelectedValue { get => listItem[selectedIndex].Value; }
            public string SelectedLabel { get => selectedLabel; }
            public int SelectedCount { get => selectedCount; }
            public int TotalCount { get => totalCount; }
            public bool MultiSelection { get => multiSelection; }

            public Action<int> OnSelect;

            private Vector2 scroller;
            private List<StyleItem> listItem;
            private GUIStyle styleLabel;
            private GUIStyle styleboldLabel;
            private int selectedCount;
            private int totalCount;
            private int selectedIndex;
            private string selectedLabel;
            private bool multiSelection;


            public override Vector2 GetWindowSize()
            {
                float winHeight, winWidth;
                winHeight = listItem.Count * (EditorStyles.label.lineHeight + 2f) + 2f;
                if (MultiSelection)
                    winHeight += EditorStyles.miniButtonMid.fixedHeight + 4f;
                //Debug.Log($"EditorStyles.miniButtonMid.fixedHeight={EditorStyles.miniButtonMid.fixedHeight} lineHeight:{EditorStyles.miniButtonMid.lineHeight}");
                //Debug.Log($"EditorStyles.label.fixedHeight={ EditorStyles.label.fixedHeight} lineHeight:{EditorStyles.label.lineHeight}");
                //Debug.Log($"EditorStyles.toggle.fixedHeight={EditorStyles.toggle.fixedHeight} lineHeight:{EditorStyles.toggle.lineHeight}");
                //Debug.Log($"EditorStyles.boldLabel.fixedHeight={EditorStyles.boldLabel.fixedHeight} lineHeight:{EditorStyles.boldLabel.lineHeight}");
                winWidth = RectPopup.width;
                //Debug.Log($"GetWindowSize {winWidth} {winHeight} {Data.Count} {MultiSelection}");
                return new Vector2(winWidth, winHeight);
            }

            public PopupList(string title, List<StyleItem> listItem, bool multiSelect = false)
            {
                multiSelection = multiSelect;
                List<StyleItem> items = new List<StyleItem>(listItem);
                // Search initial selectedInFilterList index
                if (!multiSelection)
                    for (int i = 0; i < items.Count; i++)
                        if (items[i].Selected)
                        {
                            selectedIndex = i;
                            selectedLabel = items[i].Caption;
                            break;
                        }
                this.listItem = items;
                styleLabel = EditorStyles.label;
                styleboldLabel = EditorStyles.boldLabel;
                Count();
            }

            void Count()
            {
                selectedCount = 0;
                foreach (MPTKGui.StyleItem item in listItem) if (item.Selected) selectedCount++;
                totalCount = listItem.Count;
            }

            public override void OnGUI(Rect rect)
            {
                try
                {
                    if (MultiSelection)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("All", EditorStyles.miniButtonMid))
                        {
                            foreach (MPTKGui.StyleItem item in listItem)
                                item.Selected = true;
                            ChangeSelection(-1);
                        }
                        if (GUILayout.Button("None", EditorStyles.miniButtonMid))
                        {
                            foreach (MPTKGui.StyleItem item in listItem)
                                item.Selected = false;
                            ChangeSelection(-2);
                        }
                        GUILayout.Space(15);
                        if (GUILayout.Button(MPTKGui.IconClose, EditorStyles.miniButtonMid))
                            editorWindow.Close();
                        //if (Event.current.type == EventType.Repaint) Debug.Log($"Button {GUILayoutUtility.GetLastRect()}");
                        GUILayout.EndHorizontal();
                    }

                    scroller = GUILayout.BeginScrollView(scroller, false, false);


                    for (int index = 0; index < listItem.Count; index++)
                    {
                        MPTKGui.StyleItem item = listItem[index];
                        if (MultiSelection)
                        {
                            bool select = GUILayout.Toggle(item.Selected, item.Caption);
                            //if (Event.current.type == EventType.Repaint) Debug.Log($"Toggle {GUILayoutUtility.GetLastRect()}");
                            if (select != item.Selected)
                            {
                                item.Selected = select;
                                ChangeSelection(index);
                            }
                        }
                        else
                        {
                            GUIStyle styleRow = index == SelectedIndex && !MultiSelection ? styleboldLabel : styleLabel;
                            GUILayout.Label(item.Caption, styleRow);

                            //if (Event.current.type == EventType.Repaint) Debug.Log($"Label {GUILayoutUtility.GetLastRect()}");
                            if (Event.current.type == EventType.MouseDown)
                                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                                {
                                    ChangeSelection(index);
                                }
                        }
                    }
                    GUILayout.EndScrollView();
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
            }

            private void ChangeSelection(int index)
            {
                if (!MultiSelection)
                    SelectedIndex = index; // update also SelectedLabel
                Count();
                //Debug.Log($"Selected {SelectedIndex} '{SelectedLabel}'");
                if (OnSelect != null) OnSelect(index);
                if (!MultiSelection)
                    editorWindow.Close();
            }
        }
    }
}
#endif
