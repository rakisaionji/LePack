using System;
using System.IO;

namespace LePack.VFS
{
    public static class PackFile
    {
        #region " Messages "

        private static class Messages
        {
            public const string IO_ExistingDirectoryPathIsFile = "Pack entry is specified as a directory but destination path is an existing file.";
            public const string IO_ExtractingResultsInOutside‎ = "Extracting entry would have resulted in a file outside the specified destination directory.";
        }

        #endregion

        #region " Private Methods "

        private static bool CreateNextPath(out string path, string parentPath, string name)
        {
            path = string.Empty;
            if (parentPath.Length <= 0)
            {
                return false;
            }
            parentPath = parentPath.TrimEnd(new char[] { '*' });
            path = parentPath;
            if (parentPath.Length <= 0 || path[parentPath.Length - 1] != '/')
            {
                path += '/';
            }
            path += name;
            return true;
        }

        private static PackArchiveEntry CreateFile(string parentPath, string name, int size)
        {
            if (!CreateNextPath(out string path, parentPath, name))
            {
                return null;
            }
            return new PackArchiveEntry(PackArchiveEntry.EntryType.File, path, name, size);
        }

        private static PackArchiveEntry CreateDirectory(string parentPath, string name, int numChildren)
        {
            if (!CreateNextPath(out string path, parentPath, name))
            {
                return null;
            }
            return new PackArchiveEntry(PackArchiveEntry.EntryType.Directory, path, name, numChildren);
        }

        private static bool IsNormalFile(FileAttributes attr)
        {
            return (attr & FileAttributes.Normal) == FileAttributes.Normal || FileAttributes.Archive == (attr & FileAttributes.Archive);
        }

        private static bool IsNormalDirectory(FileAttributes attr)
        {
            return (FileAttributes)0 == (attr & ~FileAttributes.Directory);
        }

        private static void DoCreateFromDirectory(PackArchive archive, PackArchiveEntry rootEntry, DirectoryInfo rootInfo)
        {
            var countBefore = archive.GetEntryCount();
            var directories = rootInfo.GetDirectories();
            var files = rootInfo.GetFiles();

            var index = 0;

            for (int i = 0; i < directories.Length; i++)
            {
                if (IsNormalDirectory(directories[i].Attributes))
                {
                    archive.AddEntry(CreateDirectory(rootEntry.Path, directories[i].Name, 0));
                    directories[index++] = directories[i];
                }
            }

            for (int j = 0; j < files.Length; j++)
            {
                if (IsNormalFile(files[j].Attributes))
                {
                    archive.AddEntry(CreateFile(rootEntry.Path, files[j].Name, (int)files[j].Length));
                }
            }

            int countAfter = archive.GetEntryCount();
            rootEntry.Offset = countBefore;
            rootEntry.Count = countAfter - countBefore;

            index = 0;

            for (int i = countBefore; i < countAfter; i++)
            {
                if (archive.GetEntry(i).Type == PackArchiveEntry.EntryType.Directory)
                {
                    DoCreateFromDirectory(archive, archive.GetEntry(i), directories[index++]);
                }
            }
        }

        private static void DoCreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
            if (string.IsNullOrEmpty(sourceDirectoryName))
            {
                throw new ArgumentNullException("sourceDirectoryName");
            }

            var sourceDirectory = Path.GetFullPath(sourceDirectoryName);
            var destinationArchive = Path.GetFullPath(destinationArchiveFileName);

            using (var packArchive = PackFile.Open(destinationArchive, PackArchiveMode.Create))
            {
                var attributes = File.GetAttributes(sourceDirectory);
                if ((FileAttributes.Directory & attributes) != FileAttributes.Directory)
                {
                    FileInfo fileInfo = new FileInfo(sourceDirectory);
                    string fileName = Path.GetFileName(sourceDirectory);
                    string parentPath = sourceDirectory.Substring(0, sourceDirectory.Length - fileName.Length);
                    var item = CreateFile(parentPath, fileName, (int)fileInfo.Length);
                    var entry = CreateDirectory("/", string.Empty, 1);
                    packArchive.AddEntry(entry);
                    packArchive.AddEntry(item);
                }
                else
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(sourceDirectory);
                    var entry = CreateDirectory(directoryInfo.FullName, string.Empty, 0);
                    if (entry == null) return;
                    packArchive.AddEntry(entry);
                    DoCreateFromDirectory(packArchive, entry, directoryInfo);
                }
            }
        }

        #endregion

        #region " Public Methods "

        public static PackArchive Open(string archiveFileName, PackArchiveMode mode)
        {
            FileMode fileMode;
            FileAccess fileAccess;
            FileShare fileShare;
            switch (mode)
            {
                case PackArchiveMode.Read:
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.Read;
                    fileShare = FileShare.Read;
                    break;
                case PackArchiveMode.Create:
                    fileMode = FileMode.CreateNew;
                    fileAccess = FileAccess.Write;
                    fileShare = FileShare.None;
                    break;
                case PackArchiveMode.Update:
                    fileMode = FileMode.OpenOrCreate;
                    fileAccess = FileAccess.ReadWrite;
                    fileShare = FileShare.None;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode");
            }
            FileStream fileStream = null;
            PackArchive result;
            try
            {
                fileStream = File.Open(archiveFileName, fileMode, fileAccess, fileShare);
                result = new PackArchive(fileStream, mode, false);
            }
            catch
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                }
                throw;
            }
            return result;
        }

        public static PackArchive OpenRead(string archiveFileName)
        {
            return Open(archiveFileName, PackArchiveMode.Read);
        }

        public static void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
            DoCreateFromDirectory(sourceDirectoryName, destinationArchiveFileName);
        }

        public static void ExtractToDirectory(string sourceArchiveFileName, string destinationDirectoryName)
        {
            if (sourceArchiveFileName == null)
            {
                throw new ArgumentNullException("sourceArchiveFileName");
            }
            using (var archive = Open(sourceArchiveFileName, PackArchiveMode.Read))
            {
                archive.ExtractToDirectory(destinationDirectoryName);
            }
        }

        public static void ExtractToDirectory(this PackArchive source, string destinationDirectoryName)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (destinationDirectoryName == null)
            {
                throw new ArgumentNullException("destinationDirectoryName");
            }
            var directoryInfo = Directory.CreateDirectory(destinationDirectoryName);
            var directoryName = directoryInfo.FullName;

            var length = directoryName.Length;
            if (length != 0 && directoryName[length - 1] != Path.DirectorySeparatorChar)
            {
                directoryName += Path.DirectorySeparatorChar.ToString();
            }

            foreach (var packArchiveEntry in source.Entries)
            {
                var fullPath = Path.GetFullPath(Path.Combine(directoryName, packArchiveEntry.Path));
                if (!fullPath.StartsWith(directoryName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(Messages.IO_ExtractingResultsInOutside);
                }
                if (packArchiveEntry.Type == PackArchiveEntry.EntryType.Directory)
                {
                    if (File.Exists(fullPath)) throw new IOException(Messages.IO_ExistingDirectoryPathIsFile);
                    else if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                }
                else
                {
                    var dirPath = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
                    source.ExtractToFile(packArchiveEntry, fullPath, false);
                }
            }
        }

        public static void ExtractToFile(this PackArchive source, PackArchiveEntry entry, string destinationFileName, bool overwrite)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (destinationFileName == null)
            {
                throw new ArgumentNullException("destinationFileName");
            }

            var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
            using (var stream = File.Open(destinationFileName, mode, FileAccess.Write, FileShare.None))
            {
                using (var stream2 = source.Open(entry))
                {
                    stream2.CopyTo(stream);
                }
            }
        }

        #endregion
    }
}
