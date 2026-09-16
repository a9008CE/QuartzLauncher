using System.Collections.Concurrent;
using System.IO;
using System.Diagnostics;

namespace QuartzLauncher.Services;

/// <summary>
/// 读取游戏进程的 stdout / stderr。
/// 关键：读取线程只入队、绝不阻塞——UI 繁忙时日志先堆在内存队列里，
/// 避免管道写满后 Java 进程卡在写 stdout 上，导致游戏窗口迟迟不出现。
/// 日志按批次回调，减少 UI 线程的调度次数。
/// </summary>
public static class ProcessOutputService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(120);
    private const int MaxBatchSize = 500;

    public static async Task ReadAsync(
        Process process,
        Action<IReadOnlyList<string>> onOutput,
        Action<IReadOnlyList<string>> onError)
    {
        var outputQueue = new ConcurrentQueue<string>();
        var errorQueue = new ConcurrentQueue<string>();

        var readers = new[]
        {
            ReadStreamIntoQueueAsync(process.StandardOutput, outputQueue),
            ReadStreamIntoQueueAsync(process.StandardError, errorQueue)
        };
        var allReaders = Task.WhenAll(readers);

        var flushTask = Task.Run(async () =>
        {
            while (true)
            {
                Drain(outputQueue, onOutput);
                Drain(errorQueue, onError);

                if (allReaders.IsCompleted && outputQueue.IsEmpty && errorQueue.IsEmpty) break;
                await Task.Delay(FlushInterval);
            }

            Drain(outputQueue, onOutput);
            Drain(errorQueue, onError);
        });

        await allReaders;
        await flushTask;
    }

    private static async Task ReadStreamIntoQueueAsync(StreamReader stream, ConcurrentQueue<string> queue)
    {
        try
        {
            while (await stream.ReadLineAsync() is { } line)
                queue.Enqueue(line);
        }
        catch (IOException)
        {
            // 进程退出时管道被关闭，属于正常情况
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static void Drain(ConcurrentQueue<string> queue, Action<IReadOnlyList<string>> onLines)
    {
        if (queue.IsEmpty) return;
        var batch = new List<string>(Math.Min(MaxBatchSize, queue.Count));
        while (batch.Count < MaxBatchSize && queue.TryDequeue(out var line))
            batch.Add(line);
        if (batch.Count == 0) return;
        try
        {
            onLines(batch);
        }
        catch
        {
            // 单批回调失败不影响后续日志
        }
    }
}
