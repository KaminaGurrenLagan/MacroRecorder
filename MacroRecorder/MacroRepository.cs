using System.Collections.Generic;
using System.Linq;
using MacroRecorderPro.Interfaces;
using MacroRecorderPro.Models;

namespace MacroRecorderPro.Core
{
    // Repository Pattern (SRP - отвечает только за хранение данных)
    public class MacroRepository : IMacroRepository
    {
        private readonly List<MacroAction> actions = new List<MacroAction>();
        private readonly object lockObject = new object();

        public int Count
        {
            get
            {
                lock (lockObject)
                {
                    return actions.Count;
                }
            }
        }

        public void Add(MacroAction action)
        {
            lock (lockObject)
            {
                actions.Add(action);
            }
        }

        public void Clear()
        {
            lock (lockObject)
            {
                actions.Clear();
            }
        }

        public List<MacroAction> GetAll()
        {
            lock (lockObject)
            {
                return actions.Select(a => a.Clone()).ToList();
            }
        }

        public void SetAll(List<MacroAction> newActions)
        {
            lock (lockObject)
            {
                actions.Clear();
                actions.AddRange(newActions.Select(a => a.Clone()));
            }
        }
    }
}