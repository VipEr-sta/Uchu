using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uchu.World.Filters;

namespace Uchu.World
{
    public class Perspective
    {
        private Stack<ushort> DroppedIds { get; }

        private Dictionary<GameObject, ushort> NetworkDictionary { get; }
        
        private Player Player { get; }

        public GameObject[] LoadedObjects => NetworkDictionary.Keys.ToArray();

        public AsyncEvent OnLoaded { get; }

        private List<IPerspectiveFilter> Filters { get; }

        public Perspective(Player player)
        {
            OnLoaded = new AsyncEvent();

            NetworkDictionary = new Dictionary<GameObject, ushort>();

            DroppedIds = new Stack<ushort>();
            
            Filters =  new List<IPerspectiveFilter>();

            Player = player;
        }

        internal bool Reveal(GameObject gameObject, out ushort networkId)
        {
            lock (NetworkDictionary)
            {
                if (!gameObject.Alive || NetworkDictionary.ContainsKey(gameObject))
                {
                    networkId = 0;

                    return false;
                }

                if (!DroppedIds.TryPop(out networkId))
                {
                    if (NetworkDictionary.Any()) networkId = (ushort) (NetworkDictionary.Values.Max() + 1);
                    else networkId = 1;
                }

                NetworkDictionary[gameObject] = networkId;

                return true;
            }
        }

        internal void Drop(GameObject gameObject)
        {
            lock (NetworkDictionary)
            {
                if (!NetworkDictionary.TryGetValue(gameObject, out var id)) return;
                DroppedIds.Push(id);
                NetworkDictionary.Remove(gameObject);
            }
        }

        internal bool View(GameObject gameObject)
        {
            return Filters.All(filter => filter.View(gameObject));
        }

        internal bool TryGetNetworkId(GameObject gameObject, out ushort id)
        {
            lock (NetworkDictionary)
            {
                return NetworkDictionary.TryGetValue(gameObject, out id);
            }
        }

        internal async Task TickAsync()
        {
            foreach (var filter in Filters)
            {
                await filter.Tick();
            }
        }

        public T GetFilter<T>() where T : IPerspectiveFilter => Filters.OfType<T>().First();

        public bool TryGetFilter<T>(out T value) where T : IPerspectiveFilter
        {
            value = Filters.OfType<T>().FirstOrDefault();

            return value != null;
        }

        public T AddFilter<T>() where T : IPerspectiveFilter, new()
        {
            if (TryGetFilter<T>(out _))
                throw new ArgumentException($"Can only have one {nameof(IPerspectiveFilter)} of {typeof(T)}");

            var instance = new T();

            instance.Initialize(Player);

            Filters.Add(instance);

            return instance;
        }

        public bool TryAddFilter<T>(out T value) where T : IPerspectiveFilter, new()
        {
            if (TryGetFilter<T>(out _))
            {
                value = default;

                return false;
            }

            value = AddFilter<T>();

            return true;
        }
    }
}