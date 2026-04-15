#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MidiPlayerTK
{
    public class MessagesEditor
    {
        public class Message
        {
            public string Text;
            public MessageType Type;
            public DateTime Start;
            public int LenghtMs;
        }
        private List<Message> Messages;

        public MessagesEditor()
        {
            Messages = new List<Message>();
        }

        public void Add(string Text, MessageType Type = MessageType.Info, int LenghtMs = 5000)
        {
            Messages.Add(new Message() { Text = Text, Start = DateTime.Now, Type = Type, LenghtMs = LenghtMs });
        }

        public void Display()
        {
            for (int i = 0; i < Messages.Count;)
            {
                EditorGUILayout.HelpBox(Messages[i].Text, Messages[i].Type, true);
                if (Messages[i].Start.AddMilliseconds(Messages[i].LenghtMs) < DateTime.Now)
                    Messages.RemoveAt(i);
                else
                    i++;
            }
        }
    }
}
#endif
