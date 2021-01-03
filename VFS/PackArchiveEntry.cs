using System.Collections.Specialized;

namespace LePack.VFS
{
    public class PackArchiveEntry
    {
        private BitVector32 Flags;

        public PackArchiveEntry(EntryType type, string path, string name, int size, long start = 0)
        {
            Type = type;
            if (Type == EntryType.File)
            {
                Flags = default;
                Name = name;
                Path = path;
                Size = size;
                Offset = start;
                Count = 0;
            }
            else
            {
                Flags = default;
                Name = name;
                Path = path;
                Size = 0;
                Offset = start;
                Count = size;
            }
        }

        public EntryType Type { get; }
        public string Name { get; }
        public string Path { get; set; }
        public int Size { get; }
        public long Offset { get; set; }
        public int Count { get; set; }

        public bool CheckFlag(int flag)
        {
            return Flags[flag];
        }

        public void SetFlag(int flag, bool value)
        {
            Flags[flag] = value;
        }

        public enum EntryType : int
        {
            File,
            Directory
        }
    }
}
