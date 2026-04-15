using System;
using System.Collections.Generic;
using UnityEngine;

namespace MidiPlayerTK
{
    public class PopupListItem
    {
        public bool Show = false;
        public string Title;
        public int ColCount;
        public int ColWidth = 200;
        public int ColHeight = 30;
        public bool KeepOpen;
        public bool Option;
        public object Tag;
        public int EspaceX = 5;
        public int EspaceY = 5;
        public int TitleHeight = 30;
        public int itemHeight = 25;
        //public Color BackgroundColor = new Color(.5f, .5f, .5f, 1f);
        /// <summary>@brief
        /// Selected item in list
        /// </summary>
        private int selectedItem;

        private GUIContent labelOption;
        private CustomStyle myStyle;
        private Vector2 positionbt;
        private List<MPTKListItem> list;
        private Vector2 scrollPosSoundFont;
        private int resizedWidth;
        private int resizedHeight;
        private int calculatedColCount;
        private int realItemCount;
        private int countRow;
        private string filterItem = "";

        //// the method call int+bool and retur string
        //Func<int, bool, string> myMethodName1;

        //// the method call string+int and retur bool
        //Func<string, int, bool> myMethodName;

        public Action<object, int, int> OnSelect;

        public int CountRow { get { return countRow; } }

        private Rect windowRect = new Rect(0, 0, 100, 100);
        private GUIContent buttonClose;


        public void Draw(List<MPTKListItem> plist, int pselected, CustomStyle style, GUIContent pLabelOption = null)
        {
            list = plist;
            selectedItem = pselected;
            myStyle = style;
            labelOption = pLabelOption;

            if (buttonClose == null)
                buttonClose = new GUIContent(Resources.Load<Texture2D>("Textures/Delete_32x32"));

            if (Show)
            {
                realItemCount = 0;
                foreach (MPTKListItem item in list)
                    if (item != null && (string.IsNullOrWhiteSpace(filterItem) || item.Label.ToLower().Contains(filterItem.ToLower())))
                        realItemCount++;

                // Min, one column
                if (realItemCount < 3) realItemCount = 3;
                if (ColCount < 1) ColCount = 1;
                if (ColWidth < 20) ColWidth = 20;
                if (ColHeight < 10) ColHeight = 10;

                calculatedColCount = realItemCount < ColCount ? realItemCount : ColCount;
                countRow = calculatedColCount > 1 ? (int)((float)realItemCount / (float)calculatedColCount + 1f) : realItemCount;

                // Try to fit all col without H scroll but with a minimum for the title
                resizedWidth = calculatedColCount * (ColWidth + EspaceX) + EspaceX;
                if (labelOption != null)
                {
                    if (resizedWidth < 600) resizedWidth = 600;
                }
                else
                {
                    if (resizedWidth < 300) resizedWidth = 300;
                }
                if (resizedHeight < 35) resizedHeight = 35;

                // Try to fit all row without V scroll
                resizedHeight = countRow * ColHeight + EspaceY;
                resizedHeight += TitleHeight + 2 * EspaceY;
                if (resizedHeight > Screen.height) resizedHeight = Screen.height;

                windowRect.width = resizedWidth;
                windowRect.height = resizedHeight;
                //Debug.Log($"Draw:{KeepOpen}");
                windowRect = GUI.Window(10, windowRect, DrawWindow, "", myStyle.BackgPopupList);
            }
        }
        // Make the contents of the window
        private void DrawWindow(int windowID)
        {
            int localstartX = 0;
            int localstartY = 0;
            int boxX = 0;
            int boxY = 0;

            Rect zone = new Rect(localstartX, localstartY, resizedWidth, resizedHeight + EspaceY);
            GUI.Box(zone, "");

            // Draw title list box
            GUI.Box(new Rect(localstartX, localstartY, resizedWidth, TitleHeight), "", new GUIStyle("box"));

            localstartX += EspaceX;

            // Draw text title list box
            if (resizedWidth > 250)
                GUI.Label(new Rect(localstartX, localstartY, 200, TitleHeight), new GUIContent(Title), myStyle.TitleLabel2);

            // Draw X to close the popup at the right corner
            int width = 30;
            boxX = resizedWidth - width;
            if (GUI.Button(new Rect(boxX, localstartY, width, TitleHeight), buttonClose, myStyle.BtStandard))
                Show = false;
            // GUI.Label(rectClear, new GUIContent(MPTKGui.IconDeleteGray, "Clear Filter"), MPTKGui.Label);

            // Draw toggle to keep open from the right corner 
            width = 100;
            boxX = boxX - EspaceX - width;
            //Debug.Log($"DrawWindow:{KeepOpen}");
            KeepOpen = GUI.Toggle(new Rect(boxX, localstartY + 4, width, TitleHeight), KeepOpen, new GUIContent("Keep Open", "Whether to keep this popup open when a soundfont is selected"));

            if (labelOption != null)
            {
                // Draw toggle option
                width = 100;
                boxX = boxX - EspaceX - width;
                Option = GUI.Toggle(new Rect(boxX, localstartY + 4, width, TitleHeight), Option, labelOption);
            }

            // Draw text field to filter from the right corner
            width = 150;
            boxX = boxX - 3 * EspaceX - width;
            filterItem = GUI.TextField(new Rect(boxX, localstartY + 4, width, 20), filterItem, 10);//, myStyle.TitleLabel2);
            GUI.Label(new Rect(boxX - 33, localstartY + 4, 30, 20), "filter:");

            localstartY += TitleHeight + EspaceY;

            Rect listVisibleRect = new Rect(localstartX, localstartY, resizedWidth - localstartX, resizedHeight - 2 * EspaceY - TitleHeight);
            Rect listContentRect = new Rect(0, 0, calculatedColCount * (ColWidth + EspaceX) + 0, countRow * ColHeight + EspaceY);

            scrollPosSoundFont = GUI.BeginScrollView(listVisibleRect, scrollPosSoundFont, listContentRect);

            boxX = 0;
            boxY = 0;

            int indexList = -1;
            int indexItem = -1;
            foreach (MPTKListItem item in list)
            {
                indexList++;
                if (item != null)
                {
                    if (item != null && (string.IsNullOrWhiteSpace(filterItem) || item.Label.ToLower().Contains(filterItem.ToLower())))
                    {
                        GUIStyle style = myStyle.BtStandard;
                        if (item.Index == selectedItem) style = myStyle.BtSelected;

                        Rect rect = new Rect(boxX, boxY, ColWidth, ColHeight);

                        if (GUI.Button(rect, item.Label, style))
                        {
                            //Debug.Log($"Selected indexItem:{item.Index} labelItem{item.Label} indexList:{indexList}");
                            if (OnSelect != null)
                                OnSelect(Tag, item.Index, indexList);
                            if (!KeepOpen)
                                Show = false;
                            break;
                        }
                        indexItem++;
                        if (calculatedColCount <= 1 || indexItem % calculatedColCount == calculatedColCount - 1)
                        {
                            // New row
                            boxY += ColHeight;
                            boxX = 0;
                        }
                        else
                            boxX += ColWidth + EspaceX;
                    }
                }
            }
            GUI.EndScrollView();

            // Make a very long rect that is 20 pixels tall.
            // This will make the window be resizable by the top
            // title bar - no matter how wide it gets.
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        // Not used
        public void PositionWithScroll(ref Vector2 scrollerWindow)
        {
            Event e = Event.current;
            if (e == null || e.type == EventType.Repaint)
            {
                // Get the position of the button to set the position popup near the button : same X and above
                Rect lastRect = GUILayoutUtility.GetLastRect();
                //Debug.Log(lastRect);
                // Set popup above the button
                positionbt = new Vector2(lastRect.x - scrollerWindow.x, lastRect.y + lastRect.height - scrollerWindow.y);
                windowRect.position = positionbt;
            }
        }
        public void Position(Vector2 positionbt)
        {
            windowRect.position = positionbt;
        }
    }
}
