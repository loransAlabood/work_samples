using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Serializes CSV writes for one output file. Records stay queued until an append succeeds.
/// A stop-time Flush() shares the same lock as background writes, so the two cannot overlap.
/// </summary>
public sealed class BufferedCsvWriter
{
    private readonly string path;
    private readonly object gate = new object();
    private readonly Queue<string> pending = new Queue<string>();
    private int workerScheduled;
    private Exception lastError;

    public string Path => path;
    public Exception LastError { get { lock (gate) return lastError; } }
    public int PendingCount { get { lock (gate) return pending.Count; } }

    public BufferedCsvWriter(string path, string header)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Missing CSV path.", nameof(path));
        this.path = path;
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        // CreateNew prevents two sessions started in the same second from overwriting each other.
        using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)))
            writer.WriteLine(header);
    }

    public void Enqueue(string record)
    {
        lock (gate) pending.Enqueue(record);
    }

    /// <summary>Schedules at most one background flush; the writer owns the queue and file lock.</summary>
    public void RequestFlush()
    {
        if (Interlocked.CompareExchange(ref workerScheduled, 1, 0) != 0) return;
        Task.Run(() =>
        {
            try { Flush(); }
            finally { Interlocked.Exchange(ref workerScheduled, 0); }
        });
    }

    /// <summary>
    /// Flushes synchronously, blocking until any current background append completes.
    /// Returns false on failure; the queued batch remains available for another attempt.
    /// </summary>
    public bool Flush()
    {
        lock (gate)
        {
            if (pending.Count == 0) return true;
            var batch = pending.ToArray();
            try
            {
                using (var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)))
                {
                    foreach (string record in batch) writer.WriteLine(record);
                }
                for (int i = 0; i < batch.Length; i++) pending.Dequeue();
                lastError = null;
                return true;
            }
            catch (Exception exception)
            {
                lastError = exception;
                return false;
            }
        }
    }
}
