using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LePack.VFS
{
    public class PackArchive : IDisposable
    {
        #region " Disposer "

        private bool _isDisposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            // Dispose of managed resources here.
            if (disposing)
            {
                var mode = _mode;
                if (mode != PackArchiveMode.Read)
                {
                    try
                    {
                        WriteArchiveFile();
                    }
                    catch (InvalidDataException)
                    {
                        CloseStreams();
                        _isDisposed = true;
                        throw;
                    }
                }
                CloseStreams();
                _isDisposed = true;
            }
        }

        #endregion

        #region " Constants "

        public const uint VFSPackArchiveSignature = 0x4B434150U;

        #endregion

        #region " Messages "

        private static class Messages
        {
            public const string FileHeaderCorrupted‎ = "Pack file header is corrupted.";
            public const string CreateModeCapabilities‎ = "Cannot use create mode on a non-writeable stream.";
            public const string ReadModeCapabilities‎ = "Cannot use read mode on a non-readable stream.";
            public const string UpdateModeCapabilities‎ = "Update mode is not supported.";
            public const string InvalidParameters‎ = "The offsets and parameters are not valid for the archive.";
            public const string InvalidStringTable‎ = "Length of the string table is not correct.";
            public const string InvalidEntriesTable = "Number of entries in the archive is not correct.";
            public const string EntriesInReadMode‎ = "Cannot modify entries in Read mode.";
            public const string EntriesInCreateMode‎ = "Cannot access entries in Create mode.";
            public const string UnexpectedEndOfStream‎ = "Unexpected end of stream reached.";
            public const string EndNotWhereExpected‎ = "End of Index Table is not where indicated.";
        }

        #endregion

        #region " Variables "

        private bool _leaveOpen;
        private bool _readEntries;
        private long _archiveSize;
        private Stream _archiveStream;
        private Stream _backingStream;
        private BinaryReader _archiveReader;
        private PackArchiveMode _mode;

        private long _entriesCount;
        private List<PackArchiveEntry> _entries;
        private ReadOnlyCollection<PackArchiveEntry> _entriesCollection;

        private long _dataOffset;
        private string _stringTable;

        #endregion

        #region " Properties "

        internal BinaryReader ArchiveReader
        {
            get
            {
                return this._archiveReader;
            }
        }

        internal Stream ArchiveStream
        {
            get
            {
                return this._archiveStream;
            }
        }

        public ReadOnlyCollection<PackArchiveEntry> Entries
        {
            get
            {
                if (_mode == PackArchiveMode.Create)
                {
                    throw new NotSupportedException(Messages.EntriesInCreateMode);
                }
                ThrowIfDisposed();
                EnsureRead();
                return _entriesCollection;
            }
        }

        public PackArchiveMode Mode
        {
            get
            {
                return this._mode;
            }
        }

        #endregion

        #region " Constructors "

        public PackArchive(Stream stream, PackArchiveMode mode, bool leaveOpen)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            Init(stream, mode, leaveOpen);
        }

        public PackArchive(Stream stream, PackArchiveMode mode) : this(stream, mode, false) { }

        #endregion

        #region " Private Methods "

        private void Init(Stream stream, PackArchiveMode mode, bool leaveOpen)
        {
            Stream memoryStream = null;
            try
            {
                _backingStream = null;
                switch (mode)
                {
                    case PackArchiveMode.Read:
                        if (!stream.CanRead)
                        {
                            throw new ArgumentException(Messages.ReadModeCapabilities);
                        }
                        if (!stream.CanSeek)
                        {
                            _backingStream = stream;
                            stream = (memoryStream = new MemoryStream());
                            _backingStream.CopyTo(stream);
                            stream.Seek(0L, SeekOrigin.Begin);
                        }
                        break;
                    case PackArchiveMode.Create:
                        if (!stream.CanWrite)
                        {
                            throw new ArgumentException(Messages.CreateModeCapabilities);
                        }
                        break;
                    case PackArchiveMode.Update:
                        throw new ArgumentException(Messages.UpdateModeCapabilities);
                    default:
                        throw new ArgumentOutOfRangeException("mode");
                }
                _mode = mode;
                _archiveStream = stream;
                _archiveSize = stream.Length;
                _archiveReader = (mode == PackArchiveMode.Create) ? null : new BinaryReader(stream);
                _entries = new List<PackArchiveEntry>();
                _entriesCollection = new ReadOnlyCollection<PackArchiveEntry>(_entries);
                _readEntries = false;
                _leaveOpen = leaveOpen;
                _isDisposed = false;
                switch (mode)
                {
                    case PackArchiveMode.Read:
                        ReadArchiveFile();
                        break;
                    case PackArchiveMode.Create:
                        _readEntries = true;
                        break;
                }
            }
            catch
            {
                if (memoryStream != null)
                {
                    memoryStream.Close();
                }
                throw;
            }
        }

        private void CloseStreams()
        {
            if (!_leaveOpen)
            {
                _archiveStream.Close();
                if (_backingStream != null)
                {
                    _backingStream.Close();
                }
                if (_archiveReader != null)
                {
                    _archiveReader.Close();
                    return;
                }
            }
            else if (_backingStream != null)
            {
                _archiveStream.Close();
            }
        }

        private void ReadArchiveFile()
        {
            var header = default(PackArchiveData.Header);
            header.Deserialize(_archiveReader);

            if (header.Signature != VFSPackArchiveSignature)
            {
                throw new Exception(Messages.FileHeaderCorrupted);
            }
            if (header.NumEntries < 0 || header.OffsetString < 0 || header.OffsetData < 0 || header.OffsetData < header.OffsetString)
            {
                throw new Exception(Messages.InvalidParameters);
            }

            long position = _archiveStream.Position;
            _archiveStream.Position = header.OffsetString;
            _dataOffset = header.OffsetData;

            var stringLength = (int)(header.OffsetData - header.OffsetString);
            var entriesCount = (header.OffsetString - position) / PackArchiveData.FileData.Size;
            var stringBuffer = new byte[stringLength];

            if (stringLength != _archiveReader.Read(stringBuffer, 0, stringLength))
            {
                throw new Exception(Messages.InvalidStringTable);
            }
            if (entriesCount != header.NumEntries)
            {
                throw new Exception(Messages.InvalidEntriesTable);
            }

            _stringTable = Encoding.Unicode.GetString(stringBuffer);
            _archiveStream.Position = position;
            _entriesCount = header.NumEntries;

            var fileData = new PackArchiveData.FileData[_entriesCount];
            for (long i = 0; i < _entriesCount; i++)
            {
                fileData[i].Deserialize(_archiveReader);
                var type = (PackArchiveEntry.EntryType)fileData[i].Type;
                var name = _stringTable.Substring(fileData[i].NameOffset, fileData[i].NameLength);
                var entry = new PackArchiveEntry(type, name, name, fileData[i].DataSize, fileData[i].DataOffset);
                _entries.Add(entry);
            }

            _readEntries = true;
            UpdatePath(_entries[0], String.Empty);
        }

        private void WriteArchiveFile()
        {
            var stringBuilder = new StringBuilder();
            var rngCryptoServiceProvider = new RNGCryptoServiceProvider();

            using (MemoryStream streamIndex = new MemoryStream())
            using (BinaryWriter writerIndex = new BinaryWriter(streamIndex))
            using (MemoryStream streamFinal = new MemoryStream())
            using (BinaryWriter writerFinal = new BinaryWriter(streamFinal))
            {
                var header = default(PackArchiveData.Header);
                header.Signature = VFSPackArchiveSignature;
                var buffer = new byte[4];
                rngCryptoServiceProvider.GetBytes(buffer);
                header.Reserved = BitConverter.ToUInt32(buffer, 0);
                header.NumEntries = _entries.Count;
                header.OffsetString = Convert.ToInt64(32 + 28 * _entries.Count);

                var num = 0L;
                for (int i = 0; i < _entries.Count; i++)
                {
                    var entry = _entries[i];
                    var fileData = default(PackArchiveData.FileData);
                    if (entry.Type == PackArchiveEntry.EntryType.File)
                    {
                        fileData.Type = 0;
                        fileData.Flags = 0;
                        fileData.NameOffset = stringBuilder.Length;
                        fileData.NameLength = entry.Name.Length;
                        fileData.DataOffset = num;
                        fileData.DataSize = entry.Size;
                        num += entry.Size;
                    }
                    else
                    {
                        fileData.Type = 1;
                        fileData.Flags = 0;
                        fileData.NameOffset = stringBuilder.Length;
                        fileData.NameLength = entry.Name.Length;
                        fileData.DataOffset = entry.Offset; // ChildStart
                        fileData.DataSize = entry.Count; // NumChildren
                    }
                    fileData.Serialize(writerIndex);
                    writerIndex.Flush();
                    stringBuilder.Append(_entries[i].Name);
                }

                var adler = default(PackArchiveData.Adler32);
                adler.Initialize();
                byte[] arrayIndex = streamIndex.ToArray();
                if (arrayIndex.Length != _entries.Count * 28)
                {
                    throw new DataMisalignedException(Messages.EndNotWhereExpected‎);
                }

                var stringTableText = stringBuilder.ToString();
                var stringTableBytes = Encoding.Unicode.GetBytes(stringTableText);
                header.OffsetData = header.OffsetString + stringTableBytes.Length;
                header.Serialize(writerFinal);

                adler.Update(streamFinal.ToArray());
                writerFinal.Write(arrayIndex);
                adler.Update(arrayIndex);
                writerFinal.Write(stringTableBytes);
                adler.Update(stringTableBytes);

                for (int j = 0; j < _entries.Count; j++)
                {
                    if (_entries[j].Type == PackArchiveEntry.EntryType.File)
                    {
                        byte[] array3 = File.ReadAllBytes(_entries[j].Path);
                        writerFinal.Write(array3, 0, array3.Length);
                        adler.Update(array3);
                    }
                }

                var footer = default(PackArchiveData.Footer);
                footer.Checksum = adler.Finalize();
                footer.Serialize(writerFinal);

                var arrayFinal = streamFinal.ToArray();
                _archiveStream.Write(arrayFinal, 0, arrayFinal.Length);
            }
        }

        private void EnsureRead()
        {
            if (!_readEntries)
            {
                ReadArchiveFile();
                _readEntries = true;
            }
        }

        internal void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(base.GetType().Name);
            }
        }

        private void UpdatePath(PackArchiveEntry entry, string parent)
        {
            if (entry.Offset == 0) return;

            entry.Path = Path.Combine(parent, entry.Name);

            for (int i = 0; i < entry.Count; i++)
            {
                var index = Convert.ToInt32(entry.Offset + i);
                UpdatePath(_entries[index], entry.Path);
            }
        }

        #endregion

        #region " Internal Methods "

        internal void AddEntry(PackArchiveEntry entry)
        {
            if (_mode == PackArchiveMode.Read)
            {
                throw new NotSupportedException(Messages.EntriesInReadMode);
            }
            ThrowIfDisposed();
            _entries.Add(entry);
        }

        internal PackArchiveEntry GetEntry(int index)
        {
            if (index < 0 || index > _entries.Count)
            {
                throw new ArgumentOutOfRangeException("index");
            }
            return _entries[index];
        }

        internal int GetEntryCount()
        {
            return _entries.Count;
        }

        #endregion

        #region " Public Methods "

        public Stream Open(PackArchiveEntry entry)
        {
            if (entry.Type == PackArchiveEntry.EntryType.Directory) return new MemoryStream();

            var size = entry.Size;
            var offset = _dataOffset + entry.Offset;
            var buffer = new byte[size];

            if (offset + size > _archiveSize)
            {
                throw new InvalidOperationException(Messages.UnexpectedEndOfStream);
            }

            _archiveStream.Position = offset;
            _archiveStream.Read(buffer, 0, size);

            return new MemoryStream(buffer);
        }

        #endregion
    }
}
