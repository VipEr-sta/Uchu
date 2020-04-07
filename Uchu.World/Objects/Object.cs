using System;
using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World
{
    public class Object : ObjectBase
    {
        public bool Started { get; private set; }

        public Zone Zone { get; protected set; }

        public Server Server => Zone.Server;

        public AsyncEvent OnStart { get; }

        public AsyncEvent OnDestroyed { get; }

        protected Object()
        {
            OnStart = new AsyncEvent();
            
            OnDestroyed = new AsyncEvent();
        }
        
        public static Object Instantiate(Type type, Zone zone)
        {
            if (Activator.CreateInstance(type, true) is Object instance)
            {
                instance.Zone = zone;

                return instance;
            }

            Logger.Error($"{type.FullName} does not inherit from Object but is being Instantiated as one.");
            return null;
        }

        public static T Instantiate<T>(Zone zone) where T : Object
        {
            return Instantiate(typeof(T), zone) as T;
        }
        
        public static Object Instantiate(Zone zone)
        {
            return Instantiate(typeof(Object), zone);
        }

        public static async Task StartAsync(Object obj)
        {
            if (obj?.Started ?? true) return;
            
            obj.Started = true;
            
            await obj.Zone.RegisterObjectAsync(obj);

            try
            {
                await obj.OnStart.InvokeAsync();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public static async Task DestroyAsync(Object obj)
        {
            await obj.Zone.UnregisterObjectAsync(obj);
            
            await obj.OnDestroyed.InvokeAsync();
            
            obj.OnStart.Clear();
            obj.OnDestroyed.Clear();

            obj.ClearListeners();
        }
    }
}