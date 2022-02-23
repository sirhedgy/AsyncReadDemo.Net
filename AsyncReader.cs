// Copyright (c) 2022 Sodalic/aNUma. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncReadDemo
{
    public class AsyncReader
    {
        private readonly bool _delayRead;
        private readonly Thread _thread;
        private readonly BlockingCollection<TaskCompletionSource<int>> _queue = new BlockingCollection<TaskCompletionSource<int>>();
        private readonly TaskCompletionSource<bool> _stoppedTcs = new TaskCompletionSource<bool>();
        private volatile bool _run = true;
        private int _next = 0;


        public AsyncReader(bool delayRead)
        {
            _delayRead = delayRead;
            // assume this is an OS/kernel thread that fires a callback with IO completion event
            _thread = new Thread(ThreadFunc)
            {
                IsBackground = true
            };
            _thread.Start();
        }

        #region Public API

        public Task<int> ReadAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            _queue.Add(tcs);
            return tcs.Task;
        }

        public async Task Stop()
        {
            _run = false;
            await _stoppedTcs.Task;
        }

        #endregion


        private void ThreadFunc()
        {
            Console.WriteLine($"Starting AsyncReader on thread {Thread.CurrentThread.ManagedThreadId}");
            while (_run)
            {
                if (!_queue.TryTake(out var request, TimeSpan.FromMilliseconds(100)))
                    continue;
                if (_delayRead)
                    Thread.Sleep(100);
                request.SetResult(_next++);
            }
            Console.WriteLine($"Stopping AsyncReader on thread {Thread.CurrentThread.ManagedThreadId}");
            _stoppedTcs.SetResult(true);
        }
    }
}