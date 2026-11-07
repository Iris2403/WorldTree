using System.Collections.Generic;
using System.IO;
using WorldTree.Core.Save;

namespace WorldTree.Tests.Support
{
    /// <summary>
    /// In-memory file system. Completed operations persist even if the "process" later dies, which
    /// matches what the OS guarantees for a process kill (architecture §6.5).
    /// </summary>
    public class InMemoryFileSystem : IFileSystem
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public int FlushedWrites { get; private set; }

        public virtual void EnsureDirectory(string path) { }
        public virtual bool Exists(string path) => Files.ContainsKey(path);

        public virtual string ReadAllText(string path)
        {
            if (!Files.TryGetValue(path, out var text)) throw new FileNotFoundException(path);
            return text;
        }

        public virtual void WriteAllText(string path, string contents, bool flushToDisk)
        {
            Files[path] = contents;
            if (flushToDisk) FlushedWrites++;
        }

        public virtual void Move(string from, string to)
        {
            if (!Files.TryGetValue(from, out var text)) throw new FileNotFoundException(from);
            if (Files.ContainsKey(to)) throw new IOException("Destination exists: " + to);
            Files.Remove(from);
            Files[to] = text;
        }

        public virtual void Delete(string path) => Files.Remove(path);
    }
}
