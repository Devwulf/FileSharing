using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileSharing.Utils
{
    public static class PeriodicTask
    {
        public static async Task RunAsync(Task action, TimeSpan period, CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                await Task.Delay((int)Math.Ceiling(period.TotalMilliseconds));

                if (!cancelToken.IsCancellationRequested)
                    await action;
            }
        }

        public static async Task RunAsync(Task action, TimeSpan period)
        {
            await RunAsync(action, period, CancellationToken.None);
        }
    }
}
