using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace MidiPlayerTK
{
    /// @ingroup runtime_catalogs
    /// <summary>
    /// This class is useful when a list of paired value string+id is needed.\n
    /// 
    /// This is also the entry point to display a popup for selecting a value by user: midi, preset, bank, drum, generator, ...
    /// </summary>
    public class MPTKListItem
    {
        /// <summary>@brief
        /// Index associated to the label (not to mix up with Position in list): 
        /// @li Patch number if patch list, 
        /// @li Bank number if bank list, 
        /// @li Midi Index for selecting a Midi from the MidiDB.
        /// @li Generator Id for selecting a generator to apply.
        /// </summary>
        public int Index;

        /// <summary>@brief
        /// Label associated to the index.
        /// @li Patch Label if patch list (Piano, Violin, ...), 
        /// @li Midi File Name for selecting a Midi from the MidiDB.
        /// @li Generator Name for selecting a generator to apply.
        /// </summary>
        public string Label;

        /// <summary>@brief
        /// Position in a list (not to mix up with Index which is a value associated to the Label)
        /// </summary>
        public int Position;
        
        [Preserve]
        public MPTKListItem() { }
    }

    /// @ingroup runtime_catalogs
    // Not used, perhaps for the V3?
    public class MPTKItems : IEnumerable<MPTKListItem>
    {
        private List<MPTKListItem> Items { get; set; }

        [Preserve]
        public MPTKItems()
        {
        }
        public IEnumerator<MPTKListItem> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Items.GetEnumerator();
        }
        /// <summary>@brief 
        /// Channel count. Classically 16 when MIDI is read from a MIDI file.\n
        /// Can be extended but not compliant with MIDI file, only for internal use (experimental)
        /// </summary>
        public int Length { get { return Items.Count; } }
        public MPTKListItem this[int index]
        {
            get
            {
                try
                {
                    return Items[index];
                }
                catch (Exception)
                {
                    Debug.LogError($"Error when trying access to Items, index {index}");
                }
                return null;
            }
            set
            {
                try
                {
                    Items[index] = value;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when trying access to Items, index {index}");
                    if (Items == null)
                        Debug.LogException(ex);
                }
            }
        }
    }
}
