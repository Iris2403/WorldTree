using System.IO;
using System.Text;

namespace WorldTree.Core.Save
{
    /// <summary><see cref="IFileSystem"/> over <c>System.IO</c>. Engine-free; works under IL2CPP.</summary>
    public sealed class PhysicalFileSystem : IFileSystem
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
        public bool Exists(string path) => File.Exists(path);
        public string ReadAllText(string path) => File.ReadAllText(path, Utf8NoBom);

        public void WriteAllText(string path, string contents, bool flushToDisk)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Utf8NoBom.GetBytes(contents);
                stream.Write(bytes, 0, bytes.Length);
                // A plain Flush hands the data to the OS, which is enough to survive a process kill.
                // Flush(true) additionally asks for physical persistence (power loss, best effort).
                if (flushToDisk) stream.Flush(true);
                else stream.Flush();
            }
        }

        public void Move(string from, string to) => File.Move(from, to);
        public void Delete(string path) => File.Delete(path);
    }
}
