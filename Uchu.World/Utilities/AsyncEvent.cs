using System;
using System.Linq;
using System.Threading.Tasks;

namespace Uchu.World
{
    public class AsyncEvent : EventBase<Func<Task>>
    {
        public async Task InvokeAsync()
        {
            foreach (var action in Actions.ToArray())
            {
                await TaskHelper.TryTask(async () =>
                {
                    await action.Invoke();
                });
            }
        }
    }
    
    public class AsyncEvent<T> : EventBase<Func<T, Task>>
    {
        public async Task InvokeAsync(T value)
        {
            foreach (var action in Actions.ToArray())
            {
                await TaskHelper.TryTask(async () =>
                {
                    await action.Invoke(value);
                });
            }
        }
    }
    
    public class AsyncEvent<T, T2> : EventBase<Func<T, T2, Task>>
    {
        public async Task InvokeAsync(T value, T2 value2)
        {
            foreach (var action in Actions.ToArray())
            {
                await TaskHelper.TryTask(async () =>
                {
                    await action.Invoke(value, value2);
                });
            }
        }
    }
    
    public class AsyncEvent<T, T2, T3> : EventBase<Func<T, T2, T3, Task>>
    {
        public async Task InvokeAsync(T value, T2 value2, T3 value3)
        {
            foreach (var action in Actions.ToArray())
            {
                await TaskHelper.TryTask(async () =>
                {
                    await action.Invoke(value, value2, value3);
                });
            }
        }
    }
    
    public class AsyncEvent<T, T2, T3, T4> : EventBase<Func<T, T2, T3, T4, Task>>
    {
        public async Task InvokeAsync(T value, T2 value2, T3 value3, T4 value4)
        {
            foreach (var action in Actions.ToArray())
            {
                await TaskHelper.TryTask(async () =>
                {
                    await action.Invoke(value, value2, value3, value4);
                });
            }
        }
    }
}