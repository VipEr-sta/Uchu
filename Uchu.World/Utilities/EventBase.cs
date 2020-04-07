using System;
using System.Linq;

namespace Uchu.World
{
    public abstract class EventBase
    {
        public abstract void Clear();

        public abstract void RemoveListener(Delegate @delegate);
    }
    
    public abstract class EventBase<T> : EventBase where T : Delegate
    {
        protected T[] Actions = new T[0];
        
        internal void AddListener(T action)
        {
            Array.Resize(ref Actions, Actions.Length + 1);

            Actions[^1] = action;
        }

        public override void RemoveListener(Delegate @delegate)
        {
            if (!(@delegate is T action) || !Actions.Contains(action)) return;
            
            var array = new T[Actions.Length - 1];

            var index = 0;
            
            foreach (var element in Actions.ToArray())
            {
                if (element == action) continue;

                array[index++] = element;
            }

            Actions = array;
        }

        public override void Clear()
        {
            Actions = new T[0];
        }
    }
}