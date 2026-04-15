#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MidiPlayerTK
{

    public class SelectMidiWindow : EditorWindow
    {
        static public SelectMidiWindow winSelectMidi;
        static public Vector2 scrollPos;
        public int ColWidth = 255;
        public bool KeepOpen = true;
        public object Tag;
        public int SelectedIndexMidi;
        public List<MPTKListItem> midiList;
        public Action<object, int> OnSelect;

        List<MPTKListItem> filteredList;

        public string midiFilter;
        Rect rectClear;
        int selectedInFilterList;

        private void OnDisable()
        {
            //Debug.Log($"OnDisable SelectMidiWindow {this.GetInstanceID()} {this.}");
            // error: Destroying object multiple times. Don't use DestroyImmediate on the same object in OnDisable or OnDestroy
            //Close();
        }

        void OnGUI()
        {
            try
            {
                if (MPTKGui.MaestroSkin == null || rectClear == null)
                {
                    rectClear = new Rect();
                    MPTKGui.LoadSkinAndStyle();
                }

                // Skin must defined at each OnGUI cycle (certainly a global GUI variable)
                GUI.skin = MPTKGui.MaestroSkin;
                GUI.skin.settings.cursorColor = Color.white;
                GUI.skin.settings.cursorFlashSpeed = 0f;

                midiList = new List<MPTKListItem>();
                try
                {
                    if (MidiPlayerGlobal.CurrentMidiSet != null && MidiPlayerGlobal.CurrentMidiSet.MidiFiles != null)
                        foreach (string midiname in MidiPlayerGlobal.CurrentMidiSet.MidiFiles)
                            midiList.Add(new MPTKListItem() { Label = midiList.Count.ToString() + " - " + midiname, Index = midiList.Count });
                }
                catch (Exception ex)
                {
                    Debug.Log(ex);
                }

                Event currentEvent = Event.current;
                if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Return)
                    ApplySelected(selectedInFilterList);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Filter:", MPTKGui.LabelLeft, GUILayout.Width(40), GUILayout.Height(30));

                // Check clearing the textfield before processing the textfield
                // Is a mouse down and the mouse position inside the X label ?
                if (currentEvent.type == EventType.MouseDown && rectClear.Contains(currentEvent.mousePosition))
                    midiFilter = "";

                string newFilter = GUILayout.TextField(midiFilter, MPTKGui.TextField, GUILayout.MinWidth(200), GUILayout.ExpandWidth(true), GUILayout.Height(30));
                if (newFilter != midiFilter)
                {
                    midiFilter = newFilter;
                    selectedInFilterList = 0;
                }
                if (currentEvent.type != EventType.Layout && currentEvent.type != EventType.Used)
                {
                    // Get the rect position of the textfield
                    Rect last = GUILayoutUtility.GetLastRect();
                    // Build the position of the X label, rectClear must be an instance properties
                    rectClear.x = last.x + last.width - 20;
                    rectClear.y = last.y + 1;
                    rectClear.width = 20;
                    rectClear.height = 30;
                }
                // Build a label which overlap the textfield 
                GUI.Label(rectClear, new GUIContent(MPTKGui.IconDeleteGray, "Clear Filter"), MPTKGui.Label);

                if (midiList != null && midiList.Count > 0 && SelectedIndexMidi >= 0 && SelectedIndexMidi < midiList.Count)
                {
                    string midiName = midiList[SelectedIndexMidi].Label;
                    if (GUILayout.Button("Reload " + midiName, MPTKGui.Button, GUILayout.ExpandWidth(true), GUILayout.Height(30)))
                    {
                        if (OnSelect != null) OnSelect(Tag, SelectedIndexMidi);
                        if (!KeepOpen) this.Close();
                    }
                }
                GUILayout.FlexibleSpace();

                KeepOpen = GUILayout.Toggle(KeepOpen, "Keep Open", MPTKGui.styleToggle, GUILayout.ExpandWidth(false));

                GUILayout.EndHorizontal();

                scrollPos = GUILayout.BeginScrollView(scrollPos);

                try
                {
                    // Build filtered list
                    filteredList = new List<MPTKListItem>();
                    midiList.ForEach(item =>
                    {
                        if (item != null && (string.IsNullOrEmpty(midiFilter) || item.Label.ToUpper().Contains(midiFilter.ToUpper())))
                        {
                            if (item.Index == SelectedIndexMidi)
                                selectedInFilterList = filteredList.Count;
                            filteredList.Add(item);
                        }
                    });

                    if (filteredList.Count > 0)
                    {
                        GUIContent[] listLabel = new GUIContent[filteredList.Count];
                        int i = 0;
                        float maxLen = 10f;

                        filteredList.ForEach(s =>
                        {
                            listLabel[i] = new GUIContent(s.Label);
                            maxLen = Mathf.Max(maxLen, MPTKGui.Button.CalcSize(listLabel[i]).x);
                            i++;
                        });

                        int colCount = Mathf.Clamp(Convert.ToInt32(this.position.size.x / maxLen + 0.5f), 1, 15) - 1;
                        if (colCount <= 0) colCount = 1;
                        int selected = GUILayout.SelectionGrid(-1, listLabel, colCount/*, MPTKGui.Button*/);
                        if (selectedInFilterList != selected)
                        {
                            selectedInFilterList = selected;
                            ApplySelected(selectedInFilterList);
                        }
                    }
                }
                catch (Exception)
                {
                }
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }

        private void ApplySelected(int selected)
        {
            if (selected >= 0 && selected < filteredList.Count)
            {
                SelectedIndexMidi = filteredList[selected].Index;
                if (OnSelect != null)
                    OnSelect(Tag, SelectedIndexMidi);
                if (!KeepOpen)
                    this.Close();
            }
        }
    }
}
#endif
