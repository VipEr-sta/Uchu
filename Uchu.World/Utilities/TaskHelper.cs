using System;
using System.Threading.Tasks;
using Uchu.Core;

namespace Uchu.World
{
    public static class TaskHelper
    {
        public static async Task TryTask(Func<Task> @delegate)
        {
            try
            {
                await @delegate();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public static async Task TryTask<T>(Func<Task> @delegate, Func<T, Task> @catch) where T : Exception
        {
            try
            {
                await @delegate();
            }
            catch (T e)
            {
                await @catch(e);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public static T ForceWait<T>(Func<Task<T>> @delegate)
        {
            var task = @delegate();
            
            task.Wait();

            return task.Result;
        }
    }
}