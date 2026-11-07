using System;
using System.IO;

namespace WorldTree.Tests.Support
{
    /// <summary>Thrown to simulate the process being killed. Deliberately not an IOException.</summary>
    public sealed class SimulatedCrashException : Exception
    {
        public SimulatedCrashException(int operation) : base("Simulated process kill at operation " + operation) { }
    }

    public enum CrashMode
    {
        /// <summary>The k-th mutating operation completes, then the process dies.</summary>
        AfterOperation,

        /// <summary>The process dies during the k-th operation; a write leaves half of its content.</summary>
        DuringOperation,
    }

    /// <summary>
    /// Counts mutating operations (write, move, delete) and kills the "process" at a chosen one.
    /// Can also make writes fail with an IOException (disk full) without crashing.
    /// </summary>
    public sealed class FaultInjectingFileSystem : InMemoryFileSystem
    {
        public int CrashAt { get; set; } = -1;
        public CrashMode Mode { get; set; } = CrashMode.AfterOperation;
        public bool FailWritesWithIOException { get; set; }
        public int Operations { get; private set; }

        public override void WriteAllText(string path, string contents, bool flushToDisk)
        {
            if (FailWritesWithIOException) throw new IOException("Disk full (simulated)");
            int op = ++Operations;
            if (op == CrashAt && Mode == CrashMode.DuringOperation)
            {
                Files[path] = contents.Substring(0, contents.Length / 2);
                throw new SimulatedCrashException(op);
            }
            base.WriteAllText(path, contents, flushToDisk);
            if (op == CrashAt) throw new SimulatedCrashException(op);
        }

        public override void Move(string from, string to)
        {
            int op = ++Operations;
            if (op == CrashAt && Mode == CrashMode.DuringOperation) throw new SimulatedCrashException(op);
            base.Move(from, to);
            if (op == CrashAt) throw new SimulatedCrashException(op);
        }

        public override void Delete(string path)
        {
            int op = ++Operations;
            if (op == CrashAt && Mode == CrashMode.DuringOperation) throw new SimulatedCrashException(op);
            base.Delete(path);
            if (op == CrashAt) throw new SimulatedCrashException(op);
        }

        public void Disarm()
        {
            CrashAt = -1;
            FailWritesWithIOException = false;
        }
    }
}
