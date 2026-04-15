using UnityEngine;

namespace MidiPlayerTK
{
    public class CustomStyle
    {
        public GUIStyle BackgPopupList;
        public GUIStyle BacgDemosLight;
        public GUIStyle BacgDemosMedium;

        // MidiList Player
        public GUIStyle BackgMidiList;
        public GUIStyle ItemSelected;
        public GUIStyle ItemNotSelected;
        public GUIStyle BtTransparent;
        public GUIStyle VScroll;
        public GUIStyle HScroll;

        /// <summary>
        ///  SceneHandler list,  info midi, TestMidiInputScripting midi events
        /// </summary>
        public GUIStyle TextFieldMultiLine;

        /// <summary>
        ///  USed in Scene Handler
        /// </summary>
        public GUIStyle TextFieldMultiLineCentered;

        /// <summary>
        /// Display synth info and midi info in TestMidiFilePlayerScripting, TestMidiFileLoad
        /// </summary>
        public GUIStyle TextFieldMultiCourier;

        public GUIStyle TitleLabel1Centered;
        public GUIStyle TitleLabel2;
        public GUIStyle TitleLabel2Centered;
        public GUIStyle TitleLabel3;

        /// <summary>
        /// Sub title in header, MainMenu
        /// </summary>
        public GUIStyle TitleLabel3Centered;

        public GUIStyle LabelRight;
        public GUIStyle LabelSmall;
        public GUIStyle LabelAlert;
        public GUIStyle LabelWrap;
        public GUIStyle LabelGreen;
        public GUIStyle LabelLink;
        public GUIStyle LabelLeft;
        public GUIStyle KeyWhite;
        public GUIStyle KeyBlack;
        public GUIStyle BtStandard;
        public GUIStyle BtSelected;
        public GUIStyle BlueText;
        public GUIStyle InfoInspectorBackground;
        public GUIStyle SliderBar;
        public GUIStyle SliderThumb;

        public GUIContent PreviousMidi;
        public GUIContent NextMidi;

        /// <summary>@brief
        /// Set custom Style. Good for background color 3E619800
        /// </summary>
        public CustomStyle()
        {
            Texture2D texKeyWhite = Resources.Load<Texture2D>("Textures/white");
            Texture2D texKeyBlack = Resources.Load<Texture2D>("Textures/black");
            Texture2D texDemoBackground = Resources.Load<Texture2D>("Textures/gray");
            Texture2D texDemoBackgroundGreenDark = Resources.Load<Texture2D>("Textures/greendark");
            Texture2D texDemoBackgroundDark = Resources.Load<Texture2D>("Textures/graydark");
            Texture2D texDemoBackgroundMedium = Resources.Load<Texture2D>("Textures/graymedium");
            Texture2D texItemSelected = Resources.Load<Texture2D>("Textures/greenlight");

            if (PreviousMidi == null) PreviousMidi = new GUIContent("<", "Previous MIDI");
            if (NextMidi == null) NextMidi = new GUIContent(">", "Next MIDI");

            BtStandard = new GUIStyle("Button");

            BtSelected = new GUIStyle("Button");
            BtSelected.fontStyle = FontStyle.Bold;
            BtSelected.normal.textColor = new Color(0.5f, 0.9f, 0.5f);
            BtSelected.hover.textColor = BtSelected.normal.textColor;
            BtSelected.active.textColor = BtSelected.normal.textColor;

            BtTransparent = new GUIStyle("Button");
            BtTransparent.normal.background = null;
            BtTransparent.active.background = null;
            BtTransparent.alignment = TextAnchor.UpperLeft;
            BtTransparent.border = new RectOffset(0, 0, 0, 0);
            BtTransparent.padding = new RectOffset(0, 0, 0, 0);

            ItemSelected = new GUIStyle("label");
            ItemSelected.normal.background = texItemSelected; //greenlight
            ItemSelected.alignment = TextAnchor.UpperLeft;

            ItemNotSelected = new GUIStyle("label");
            ItemNotSelected.alignment = TextAnchor.UpperLeft;

            BackgPopupList = new GUIStyle("box"); // Issue with window: become transparent when get focus.
            BackgPopupList.normal.background = Resources.Load<Texture2D>("Textures/window");

            BackgMidiList = new GUIStyle("textField");

            BacgDemosLight = new GUIStyle("box");
            BacgDemosLight.normal.background = texDemoBackground; // gray - SetColor(new Texture2D(2, 2), new Color(.3f, .4f, .2f, 1f));// Issue with window: become transparent when get focus.
            BacgDemosLight.normal.textColor = Color.black;

            BacgDemosMedium = new GUIStyle("box");
            BacgDemosMedium.normal.background = texDemoBackgroundMedium; // graymedium - SetColor(new Texture2D(2, 2), new Color(.3f, .5f, .2f, 1f));// Issue with window: become transparent when get focus.
            BacgDemosMedium.normal.textColor = Color.black;

            VScroll = new GUIStyle("verticalScrollbar");
            HScroll = new GUIStyle("horizontalScrollbar");

            TitleLabel1Centered = new GUIStyle("label");
            TitleLabel1Centered.fontSize = 20;
            TitleLabel1Centered.alignment = TextAnchor.MiddleCenter;
            
            TitleLabel2 = new GUIStyle("label");
            TitleLabel2.fontSize = 16;
            TitleLabel2.alignment = TextAnchor.MiddleLeft;

            TitleLabel2Centered = new GUIStyle("label");
            TitleLabel2Centered.fontSize = 16;
            TitleLabel2Centered.alignment = TextAnchor.MiddleCenter;

            TitleLabel3 = new GUIStyle("label");
            TitleLabel3.alignment = TextAnchor.UpperLeft;
            TitleLabel3.fontSize = 14;

            TitleLabel3Centered = new GUIStyle("label");
            TitleLabel3Centered.alignment = TextAnchor.MiddleCenter;
            TitleLabel3Centered.fontSize = 14;
            
            LabelRight = new GUIStyle("label");
            LabelRight.alignment = TextAnchor.UpperRight;
            LabelRight.fontSize = 14;

            LabelSmall = new GUIStyle("label");
            LabelSmall.alignment = TextAnchor.UpperLeft;
            LabelSmall.fontSize = 10;

            LabelLeft = new GUIStyle("label");
            LabelLeft.alignment = TextAnchor.UpperLeft;
            LabelLeft.fontSize = 14;

            LabelWrap = new GUIStyle("Label");
            LabelWrap.wordWrap = true;

            LabelAlert = new GUIStyle("Label");
            LabelAlert.alignment = TextAnchor.MiddleLeft;
            LabelAlert.wordWrap = true;
            LabelAlert.normal.textColor = new Color(0.6f, 0.1f, 0.1f);
            LabelAlert.fontSize = 12;

            LabelGreen = new GUIStyle("Label");
            LabelGreen.alignment = TextAnchor.MiddleLeft;
            LabelGreen.wordWrap = true;
            LabelGreen.normal.textColor = new Color(0f, 0.5f, 0f);
            LabelGreen.richText = true;
            LabelGreen.fontSize = 12;

            LabelLink = new GUIStyle("Label");
            LabelLink.alignment = TextAnchor.LowerLeft;
            LabelLink.wordWrap = false;
            LabelLink.normal.textColor = new Color(0f, 0f, 0.5f);
            LabelLink.fontSize = 14;

            SliderBar = new GUIStyle("horizontalslider");
            SliderBar.alignment = TextAnchor.LowerLeft;
            SliderBar.margin = new RectOffset(4, 4, 10, 4);
            SliderThumb = new GUIStyle("horizontalsliderthumb");
            SliderThumb.alignment = TextAnchor.LowerLeft;

            KeyWhite = new GUIStyle("Button");
            KeyWhite.normal.background = texKeyWhite;
            KeyWhite.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
            KeyWhite.alignment = TextAnchor.UpperCenter;
            KeyWhite.fontSize = 14;

            KeyBlack = new GUIStyle("Button");
            KeyBlack.normal.background = texKeyBlack;
            KeyBlack.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            KeyBlack.alignment = TextAnchor.UpperCenter;
            KeyBlack.fontSize = 14;

            TextFieldMultiLine = new GUIStyle("textField");
            TextFieldMultiLine.alignment = TextAnchor.UpperLeft;
            TextFieldMultiLine.wordWrap = true;

            TextFieldMultiLineCentered = new GUIStyle("textField");
            TextFieldMultiLineCentered.alignment = TextAnchor.MiddleCenter;
            TextFieldMultiLineCentered.wordWrap = true;
            

            TextFieldMultiCourier = new GUIStyle("textField");
            TextFieldMultiCourier.alignment = TextAnchor.UpperLeft;
            TextFieldMultiCourier.wordWrap = true;
            TextFieldMultiCourier.richText = true;
            TextFieldMultiCourier.font = Resources.Load<Font>("Courier");

            InfoInspectorBackground = new GUIStyle("textField");
            InfoInspectorBackground.alignment = TextAnchor.MiddleCenter;
            InfoInspectorBackground.normal.background = texDemoBackgroundGreenDark; // graydark
            InfoInspectorBackground.normal.textColor = Color.black;
            InfoInspectorBackground.wordWrap = true;
            InfoInspectorBackground.fontSize = 14;

            BlueText = new GUIStyle("textArea");
            BlueText.normal.textColor = new Color(0, 0, 0.99f);
            BlueText.alignment = TextAnchor.UpperLeft;
            //styleDragZone.border = new RectOffset(2, 2, 2, 2);

            if (Screen.dpi > 400)
            {
                //Set the GUIStyle style to be label
                GUI.skin.GetStyle("verticalscrollbar").fixedWidth = 30;
                GUI.skin.GetStyle("verticalscrollbarthumb").fixedWidth = 30;
                GUI.skin.GetStyle("verticalscrollbarupbutton").fixedWidth = 30;
                GUI.skin.GetStyle("verticalscrollbarupbutton").fixedHeight = 30;
                GUI.skin.GetStyle("verticalscrollbardownbutton").fixedWidth = 30;
                GUI.skin.GetStyle("verticalscrollbardownbutton").fixedHeight = 30;

                GUI.skin.GetStyle("toggle").fontSize = 6;
                GUI.skin.GetStyle("label").fontSize = 12;
                GUI.skin.GetStyle("button").fontSize = 12;
                
                TextFieldMultiLine.fontSize = TextFieldMultiLineCentered.fontSize = BtStandard.fontSize = 18;
                TitleLabel2Centered.fontSize = TitleLabel2.fontSize = TitleLabel3.fontSize = 20;
                TextFieldMultiCourier.fontSize = 12;
                TitleLabel3Centered.fontSize = BtStandard.fontSize = 16;

                LabelRight.fontSize = LabelLeft.fontSize = 20;

                Debug.Log($"isMobilePlatform:{Application.isMobilePlatform} w/h:{Screen.width}/{Screen.height} dpi:{Screen.dpi} resolution:{Screen.currentResolution} orientation:{Screen.orientation}");
            }
        }
    }
}
