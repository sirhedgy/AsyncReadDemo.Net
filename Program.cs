using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncReadDemo
{
    public static class Program
    {
        private static async Task UseAsyncReaderBackground(AsyncReader reader, CancellationToken ct)
        {
            Console.WriteLine($"Starting UseAsyncReaderBackground on thread {Thread.CurrentThread.ManagedThreadId}");
            while (!ct.IsCancellationRequested)
            {
                var read = await reader.ReadAsync();
                Console.WriteLine($"Bg Read {read}  on thread {Thread.CurrentThread.ManagedThreadId}");
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        private static async Task UseAsyncReaderInteractive(AsyncReader reader, bool fixAsync)
        {
            Console.WriteLine($"Starting UseAsyncReaderInteractive on thread {Thread.CurrentThread.ManagedThreadId}");

            while (true)
            {
                Console.WriteLine("Enter some text or type 'exit' to quit");
                var message = Console.ReadLine();
                Console.WriteLine($"Interactive Read '{message}' from Console on thread {Thread.CurrentThread.ManagedThreadId}");
                if ("exit" == message)
                {
                    break;
                }
                int read;
                if (!fixAsync)
                {
                    read = await reader.ReadAsync();
                }
                else
                {
                    read = await reader.ReadAsync()
                        // to fix the issue, force switch of the continuation back to a thread-pool thread 
                        .ContinueWith((task) =>
                        {
                            if (task.IsFaulted)
                                throw task.Exception!; // we probably can afford just re-throwing the same exception here
                            else
                                return task.Result;
                        }, TaskContinuationOptions.RunContinuationsAsynchronously | TaskContinuationOptions.NotOnCanceled);
                }
                Console.WriteLine($"Interactive Read {read} on thread {Thread.CurrentThread.ManagedThreadId}");
            }
        }

        private static async Task AsyncDemo1()
        {
            var reader = new AsyncReader(false);
            await UseAsyncReaderInteractive(reader, false);
            await reader.Stop();
        }

        private static async Task AsyncDemo2(bool delayRead, bool fixAsync)
        {
            var reader = new AsyncReader(delayRead);
            var cts = new CancellationTokenSource();
            var t1 = UseAsyncReaderBackground(reader, cts.Token);
            await Task.Delay(5);
            await UseAsyncReaderInteractive(reader, fixAsync);
            cts.Cancel();
            await t1;
            await reader.Stop();
        }


        public static async Task Main(string[] args)
        {
            Console.WriteLine($"Starting Main on thread {Thread.CurrentThread.ManagedThreadId}");
            // await AsyncDemo1();
            await AsyncDemo2(delayRead: false, fixAsync: false);
            // await AsyncDemo2(delayRead: true, fixAsync: false);
            // await AsyncDemo2(delayRead: true, fixAsync: true);
        }
    }
}