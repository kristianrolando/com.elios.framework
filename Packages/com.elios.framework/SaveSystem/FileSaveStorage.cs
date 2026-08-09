using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// One file per slot storage. When an <see cref="ISaveEncryptor"/> is supplied, payloads are
    /// written encrypted (tagged with a magic header). Reads auto-detect the header, so plaintext
    /// files written before encryption was enabled still load transparently.
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        // Prefix identifying an encrypted payload. Plain JSON never starts with these bytes.
        private static readonly byte[] EncryptedMagic = { 0x47, 0x53, 0x41, 0x56, 0x45, 0x43, 0x31, 0x00 }; // "GSAVEC1\0"
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        // Marker used in temporary files during atomic writes.
        private const string TempFileMarker = ".tmp_";

        private readonly ISaveEncryptor _encryptor;

        public string RootPath { get; }

        public FileSaveStorage(ISaveEncryptor encryptor = null, string rootFolderName = null)
        {
            _encryptor = encryptor;

            string folder = string.IsNullOrWhiteSpace(rootFolderName)
                ? SaveConstants.RootFolderName
                : rootFolderName;

            RootPath = Path.Combine(UnityEngine.Application.persistentDataPath, folder);
            Directory.CreateDirectory(RootPath);

            // Remove leftovers from writes interrupted by a previous crash/kill.
            CleanupOrphanedTempFiles();
        }

        public bool TryReadSlot(SaveSlotId slotId, out string json)
        {
            if (TryReadFile(GetSlotFilePath(slotId), out json))
                return true;

            // Main file missing/corrupt: fall back to the last known-good backup.
            return TryReadFile(GetSlotBackupPath(slotId), out json);
        }

        public void WriteSlotAtomic(SaveSlotId slotId, string json)
        {
            Directory.CreateDirectory(RootPath);

            string path = GetSlotFilePath(slotId);
            string backupPath = GetSlotBackupPath(slotId);
            string tmpPath = path + TempFileMarker + Guid.NewGuid().ToString("N");

            File.WriteAllBytes(tmpPath, Encode(json));

            try
            {
                if (File.Exists(path))
                {
                    // File.Replace isn't supported on WebGL/Emscripten, so swap manually there.
                    if (SaveFileSystem.IsWebGLRuntime)
                    {
                        SwapWithBackup(tmpPath, path, backupPath);
                    }
                    else
                    {
                        try
                        {
                            // Atomic on most platforms and keeps the previous good file as .bak.
                            File.Replace(tmpPath, path, backupPath, ignoreMetadataErrors: true);
                        }
                        catch
                        {
                            SwapWithBackup(tmpPath, path, backupPath);
                        }
                    }
                }
                else
                {
                    File.Move(tmpPath, path);
                }
            }
            finally
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }

            // On WebGL this persists the write to IndexedDB; no-op elsewhere.
            SaveFileSystem.PersistToDisk();
        }

        public bool SlotExists(SaveSlotId slotId)
        {
            // A slot still counts as existing if only its backup survived a failed write.
            return File.Exists(GetSlotFilePath(slotId)) || File.Exists(GetSlotBackupPath(slotId));
        }

        public bool DeleteSlot(SaveSlotId slotId)
        {
            string path = GetSlotFilePath(slotId);
            string backupPath = GetSlotBackupPath(slotId);

            bool existed = File.Exists(path) || File.Exists(backupPath);
            if (!existed)
                return false;

            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(backupPath)) File.Delete(backupPath);
                SaveFileSystem.PersistToDisk();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<SaveSlotId> ListSlots()
        {
            if (!Directory.Exists(RootPath))
                yield break;

            var files = Directory.EnumerateFiles(RootPath, "*" + SaveConstants.SlotFileExtension, SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                // Skip backup/temp companions so they are not reported as real slots.
                if (file.EndsWith(SaveConstants.BackupFileExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(fileName)) continue;
                yield return new SaveSlotId(fileName);
            }
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private void CleanupOrphanedTempFiles()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(RootPath))
                {
                    if (!Path.GetFileName(file).Contains(TempFileMarker))
                        continue;

                    try { File.Delete(file); }
                    catch { /* best effort; ignore files locked by another process */ }
                }
            }
            catch { /* best effort */ }
        }

        // Preserve the old file as backup before swapping in the new one, so a crash mid-swap
        // never leaves us with no recoverable data. Used as the WebGL path and as the fallback
        // when File.Replace is unavailable.
        private static void SwapWithBackup(string tmpPath, string path, string backupPath)
        {
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Move(path, backupPath);
            File.Move(tmpPath, path);
        }

        private byte[] Encode(string json)
        {
            byte[] payload = Utf8NoBom.GetBytes(json ?? string.Empty);

            if (_encryptor == null)
                return payload;

            byte[] cipher = _encryptor.Encrypt(payload);
            byte[] tagged = new byte[EncryptedMagic.Length + cipher.Length];
            Buffer.BlockCopy(EncryptedMagic, 0, tagged, 0, EncryptedMagic.Length);
            Buffer.BlockCopy(cipher, 0, tagged, EncryptedMagic.Length, cipher.Length);
            return tagged;
        }

        private bool TryReadFile(string path, out string json)
        {
            json = null;
            if (!File.Exists(path))
                return false;

            try
            {
                byte[] raw = File.ReadAllBytes(path);
                if (raw.Length == 0)
                    return false;

                if (HasEncryptedMagic(raw))
                {
                    // Encrypted payload but no key available to read it.
                    if (_encryptor == null)
                        return false;

                    int headerSize = EncryptedMagic.Length;
                    byte[] cipher = new byte[raw.Length - headerSize];
                    Buffer.BlockCopy(raw, headerSize, cipher, 0, cipher.Length);

                    byte[] plain = _encryptor.Decrypt(cipher);
                    json = Utf8NoBom.GetString(plain);
                }
                else
                {
                    json = DecodePlain(raw);
                }

                return !string.IsNullOrWhiteSpace(json);
            }
            catch
            {
                json = null;
                return false;
            }
        }

        private static bool HasEncryptedMagic(byte[] raw)
        {
            if (raw.Length < EncryptedMagic.Length)
                return false;

            for (int i = 0; i < EncryptedMagic.Length; i++)
            {
                if (raw[i] != EncryptedMagic[i])
                    return false;
            }
            return true;
        }

        private static string DecodePlain(byte[] raw)
        {
            // Skip a UTF-8 BOM if a legacy file was written with one.
            int offset = (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) ? 3 : 0;
            return Utf8NoBom.GetString(raw, offset, raw.Length - offset);
        }

        private string GetSlotFilePath(SaveSlotId slotId)
        {
            string fileName = slotId.Value + SaveConstants.SlotFileExtension;
            return Path.Combine(RootPath, fileName);
        }

        private string GetSlotBackupPath(SaveSlotId slotId)
        {
            string fileName = slotId.Value + SaveConstants.BackupFileExtension;
            return Path.Combine(RootPath, fileName);
        }
    }
}
