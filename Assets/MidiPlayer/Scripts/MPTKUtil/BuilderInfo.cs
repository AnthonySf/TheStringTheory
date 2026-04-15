using System.Collections.Generic;
using System.Linq;

namespace MidiPlayerTK
{
    public class BuilderInfo
    {
        private List<string> info1;
        private List<string> info2;
        private List<string> info3;
        public IEnumerable<string> Infos
        {
            get
            {
                try
                {
                    return info1.Concat(info2).Concat(info3);
                }
                catch (System.Exception ex)
                {
                    MidiPlayerGlobal.ErrorDetail(ex);
                }
                return new List<string>();
            }
        }

        public int Count { get { return Infos == null ? 0 : info1.Count + info2.Count + info3.Count; } }


        public BuilderInfo()
        {
            Reset();
        }

        public void Reset()
        {
            info1 = new List<string>();
            info2 = new List<string>();
            info3 = new List<string>();
        }
        public string GetString()
        {
            string Info = "";
            try
            {
                if (string.IsNullOrEmpty(Info))
                {
                    Info = "";
                    foreach (string info in Infos)
                        Info += info + "\n";
                }
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
            return Info;
        }
        public void Add(string line, int mode = 0)
        {
            try
            {
                //Debug.Log(line);
                if (mode == 0)
                    info1.Add(line);
                else if (mode == 1)
                    info2.Add(line);
                else if (mode == 2)
                    info3.Add(line);
            }
            catch (System.Exception ex)
            {
                MidiPlayerGlobal.ErrorDetail(ex);
            }
        }
    }
}
