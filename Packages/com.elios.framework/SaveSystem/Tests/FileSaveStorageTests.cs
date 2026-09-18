using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Elios.Framework.SaveSystem
{
    public class FileSaveStorageTests
    {
        private const string Password = "storage-test-password";
        private const string FirstPayload = "{\"version\":1}";
        private const string SecondPayload = "{\"version\":2}";

        private static readonly SaveSlotId Slot = new SaveSlotId("slot_test");

        private string _folderName;
        private string _rootPath;

        // ══════════════════════════════════════════════
        // Fixture
        // ══════════════════════════════════════════════

        [SetUp]
        public void SetUp()
        {
            // A folder per test keeps the suite off the real save directory and off each other.
            _folderName = "save_tests_" + Guid.NewGuid().ToString("N");
            _rootPath = Path.Combine(Application.persistentDataPath, _folderName);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }

        // ══════════════════════════════════════════════
        // Read and Write
        // ══════════════════════════════════════════════

        [Test]
        public void WriteSlotAtomic_ThenTryReadSlot_ReturnsTheSamePayload()
        {
            FileSaveStorage storage = CreateStorage();

            storage.WriteSlotAtomic(Slot, FirstPayload);

            Assert.IsTrue(storage.TryReadSlot(Slot, out string json));
            Assert.AreEqual(FirstPayload, json);
        }

        [Test]
        public void TryReadSlot_MissingSlot_ReturnsFalse()
        {
            FileSaveStorage storage = CreateStorage();

            Assert.IsFalse(storage.TryReadSlot(new SaveSlotId("never_written"), out string json));
            Assert.IsNull(json);
        }

        [Test]
        public void TryReadSlot_EmptyFile_ReturnsFalse()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            File.WriteAllBytes(SlotPath(), Array.Empty<byte>());

            Assert.IsFalse(storage.TryReadSlot(Slot, out _));
        }

        [Test]
        public void WriteSlotAtomic_LeavesNoTemporaryFilesBehind()
        {
            FileSaveStorage storage = CreateStorage();

            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            foreach (string file in Directory.EnumerateFiles(_rootPath))
                Assert.IsFalse(Path.GetFileName(file).Contains(".tmp_"), $"a temp file survived the write: {file}");
        }

        [Test]
        public void RootPath_IsCreatedByTheConstructor()
        {
            CreateStorage();

            Assert.IsTrue(Directory.Exists(_rootPath));
        }

        // ══════════════════════════════════════════════
        // Backup and Recovery
        // ══════════════════════════════════════════════

        [Test]
        public void WriteSlotAtomic_SecondWrite_KeepsThePreviousPayloadAsBackup()
        {
            FileSaveStorage storage = CreateStorage();

            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            Assert.IsTrue(File.Exists(BackupPath()), "no .bak was written");
            Assert.AreEqual(FirstPayload, File.ReadAllText(BackupPath(), Encoding.UTF8));
        }

        [Test]
        public void TryReadSlot_WhenTheMainFileIsUnreadable_RecoversFromTheBackup()
        {
            // This is the whole point of the atomic write: a write interrupted by a crash must
            // still leave the previous good save reachable.
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            File.WriteAllBytes(SlotPath(), Array.Empty<byte>());

            Assert.IsTrue(storage.TryReadSlot(Slot, out string json));
            Assert.AreEqual(FirstPayload, json);
        }

        [Test]
        public void TryReadSlot_WhenTheMainFileIsGone_RecoversFromTheBackup()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            File.Delete(SlotPath());

            Assert.IsTrue(storage.TryReadSlot(Slot, out string json));
            Assert.AreEqual(FirstPayload, json);
        }

        [Test]
        public void SlotExists_TrueWhenOnlyTheBackupSurvived()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            File.Delete(SlotPath());

            Assert.IsTrue(storage.SlotExists(Slot));
        }

        [Test]
        public void Constructor_DeletesTemporaryFilesLeftByAnInterruptedWrite()
        {
            Directory.CreateDirectory(_rootPath);
            string orphan = Path.Combine(_rootPath, "slot_test.json.tmp_deadbeef");
            File.WriteAllText(orphan, "half-written");

            CreateStorage();

            Assert.IsFalse(File.Exists(orphan), "an orphaned temp file survived startup");
        }

        // ══════════════════════════════════════════════
        // Slot Management
        // ══════════════════════════════════════════════

        [Test]
        public void SlotExists_FalseForAnUnwrittenSlot()
        {
            Assert.IsFalse(CreateStorage().SlotExists(new SaveSlotId("never_written")));
        }

        [Test]
        public void DeleteSlot_RemovesBothTheSlotAndItsBackup()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            Assert.IsTrue(storage.DeleteSlot(Slot));
            Assert.IsFalse(File.Exists(SlotPath()));
            Assert.IsFalse(File.Exists(BackupPath()));
        }

        [Test]
        public void DeleteSlot_ReturnsFalseWhenThereIsNothingToDelete()
        {
            Assert.IsFalse(CreateStorage().DeleteSlot(new SaveSlotId("never_written")));
        }

        [Test]
        public void ListSlots_ReturnsEverySlotThatWasWritten()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(new SaveSlotId("slot_1"), FirstPayload);
            storage.WriteSlotAtomic(new SaveSlotId("slot_2"), FirstPayload);

            List<string> slots = ListSlotValues(storage);

            Assert.AreEqual(2, slots.Count);
            Assert.Contains("slot_1", slots);
            Assert.Contains("slot_2", slots);
        }

        [Test]
        public void ListSlots_DoesNotReportBackupsAsSlots()
        {
            FileSaveStorage storage = CreateStorage();
            storage.WriteSlotAtomic(Slot, FirstPayload);
            storage.WriteSlotAtomic(Slot, SecondPayload);

            List<string> slots = ListSlotValues(storage);

            Assert.AreEqual(1, slots.Count, "the .bak companion was reported as a slot");
            Assert.AreEqual(Slot.Value, slots[0]);
        }

        [Test]
        public void ListSlots_EmptyFolder_ReturnsNothing()
        {
            Assert.AreEqual(0, ListSlotValues(CreateStorage()).Count);
        }

        // ══════════════════════════════════════════════
        // Encryption Boundary
        // ══════════════════════════════════════════════

        [Test]
        public void EncryptedStorage_RoundTripsThroughDisk()
        {
            FileSaveStorage storage = CreateStorage(new AesSaveEncryptor(Password));

            storage.WriteSlotAtomic(Slot, FirstPayload);

            Assert.IsTrue(storage.TryReadSlot(Slot, out string json));
            Assert.AreEqual(FirstPayload, json);
        }

        [Test]
        public void EncryptedStorage_WritesTheMagicHeaderAndHidesThePayload()
        {
            FileSaveStorage storage = CreateStorage(new AesSaveEncryptor(Password));
            storage.WriteSlotAtomic(Slot, FirstPayload);

            byte[] raw = File.ReadAllBytes(SlotPath());

            Assert.AreEqual(Encoding.ASCII.GetBytes("GSAVEC1"), Slice(raw, 0, 7));
            Assert.AreEqual(0x00, raw[7]);
            Assert.IsFalse(Encoding.UTF8.GetString(raw).Contains("version"));
        }

        [Test]
        public void PlaintextSave_StillLoads_AfterEncryptionIsTurnedOn()
        {
            // Auto-detect on read: enabling encryption must not lock a player out of existing saves.
            CreateStorage().WriteSlotAtomic(Slot, FirstPayload);

            FileSaveStorage encrypted = CreateStorage(new AesSaveEncryptor(Password));

            Assert.IsTrue(encrypted.TryReadSlot(Slot, out string json));
            Assert.AreEqual(FirstPayload, json);
        }

        [Test]
        public void EncryptedSave_IsNotReadableWithoutTheEncryptor()
        {
            CreateStorage(new AesSaveEncryptor(Password)).WriteSlotAtomic(Slot, FirstPayload);

            Assert.IsFalse(CreateStorage().TryReadSlot(Slot, out _));
        }

        [Test]
        public void EncryptedSave_IsNotReadableWithTheWrongPassword()
        {
            CreateStorage(new AesSaveEncryptor(Password)).WriteSlotAtomic(Slot, FirstPayload);

            FileSaveStorage other = CreateStorage(new AesSaveEncryptor("a-different-password"));

            // The integrity check throws inside the storage layer, which reports it as a failed read.
            Assert.IsFalse(other.TryReadSlot(Slot, out _));
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private FileSaveStorage CreateStorage(ISaveEncryptor encryptor = null)
        {
            return new FileSaveStorage(encryptor, _folderName);
        }

        private string SlotPath() => Path.Combine(_rootPath, Slot.Value + SaveConstants.SlotFileExtension);

        private string BackupPath() => Path.Combine(_rootPath, Slot.Value + SaveConstants.BackupFileExtension);

        private static List<string> ListSlotValues(FileSaveStorage storage)
        {
            var values = new List<string>();
            foreach (SaveSlotId slotId in storage.ListSlots())
                values.Add(slotId.Value);

            return values;
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            byte[] slice = new byte[length];
            Buffer.BlockCopy(source, offset, slice, 0, length);
            return slice;
        }
    }
}
