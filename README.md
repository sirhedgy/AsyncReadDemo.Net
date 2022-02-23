#  AsyncReadDemo.Net

This is a simple demo to show perils of doing a blocking operation in a Task-based code that handles async I/O.

The general idea is that there is a single `AsyncReader` and 2 consumers:
- `UseAsyncReaderBackground` which should read a new value every 2 seconds
- `UseAsyncReaderInteractive` which should read a new value after the user has entered something from the keyboard

But this is not what usually happens in practice. Probably the most interesting demo case is

     await AsyncDemo2(delayRead: false, fixAsync: false);

because it usually produces the most surprising behavior:
- Initially the "Bg Read" part works reading a new value every 2 seconds
- After you try to enter something it usually still continues to work 
- After you try to enter something for the second time the "Bg Read" usually stops reading anything but it reads something every time you enter something

Note that if `delayRead` is `true`, most probably the "Bg Read" becomes broken after the first attempt to enter a value.

If the `fixAsync` is `true` the issue should never happen irrespective of the `delayRead` value. It uses a very simple hack to force running the continuation of `UseAsyncReaderInteractive` on a `ThreadPool` thread (see also the [Explanation](#explanation) section below)  

The `AsyncDemo1` is a simplified version for debugging without `UseAsyncReaderBackground`.

## AsyncReader

The `AsyncReader` class tries to emulate a very simple async I/O implementation. 
The main idea is that you call the `Task<int> ReadAsync()` method and the `Task` is resolved asynchronously on a background thread which emulates an OS/kernel thread that can notify you about your async IO success. 
Depending on the `delayRead` flag, the background thread either tries to satisfy the request (`Task`) immediately or waits a bit before that effectively simulating some I/O latency.  


## Explanation

To understand this behavior you need to answer following important question: On which thread is the code of the `UseAsyncReaderInteractive` method actually gets executed?

The code of the `UseAsyncReaderInteractive` is transformed into a state machine. So who (which thread) is running that state machine? 
On the very first run, it will be some random `ThreadPool`-thread that was scheduled to run after the `await Task.Delay(5);` call (and if not for that call, it would have been run on the "main" thread). 
But it is clear that there is no way for the `AsyncReader` to switch back to that thread after the `var read = await reader.ReadAsync();` call.
What actually happens is that there is a race between that ThreadPool-thread that runs `UseAsyncReaderInteractive` to subscribe a continuation to the `Task` returned by the `ReadAsync` call and the background thread owned by the `AsyncReader` to resolve the same `Task` via its `TaskCompletionSource`. 
If it is the `UseAsyncReaderInteractive`-thread that wins the race, the `AsyncReader` has no other choice but to execute the rest of the `UseAsyncReaderInteractive` on its own thread. 
And when the `UseAsyncReaderInteractive` continues to run it hits the

    var message = Console.ReadLine();

and gets stuck blocking the whole `AsyncReader`. 
At this point the call stack becomes effectively inverted: it is not `UseAsyncReaderInteractive` calling `ReadAsync`. It is a continuation of the `ReadAsync` (i.e. `AsyncReader.ThreadFunc`) calling the continuation of the `UseAsyncReaderInteractive`.
So every time you enter something new, the `UseAsyncReaderInteractive` unblocks from the `Console.ReadLine()` call and allows the stack to wind up to the `AsyncReader.ThreadFunc` so it can service a `UseAsyncReaderBackground` continuation as well before getting stuck again on the `ReadLine` call. 