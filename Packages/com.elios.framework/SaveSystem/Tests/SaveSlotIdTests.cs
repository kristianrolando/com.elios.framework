using NUnit.Framework;

namespace Elios.Framework.SaveSystem
{
    public class SaveSlotIdTests
    {
        // ══════════════════════════════════════════════
        // Sanitize
        // ══════════════════════════════════════════════

        [Test]
        public void Sanitize_Null_FallsBackToDefaultSlot()
        {
            Assert.AreEqual(SaveConstants.DefaultSlotId, SaveSlotId.Sanitize(null));
        }

        [Test]
        public void Sanitize_Whitespace_FallsBackToDefaultSlot()
        {
            Assert.AreEqual(SaveConstants.DefaultSlotId, SaveSlotId.Sanitize("   "));
        }

        [Test]
        public void Sanitize_OnlyIllegalCharacters_FallsBackToDefaultSlot()
        {
            // Every character becomes '_', and trimming underscores leaves nothing behind.
            Assert.AreEqual(SaveConstants.DefaultSlotId, SaveSlotId.Sanitize("///"));
        }

        [Test]
        public void Sanitize_Lowercases()
        {
            Assert.AreEqual("slot_a", SaveSlotId.Sanitize("SLOT_A"));
        }

        [Test]
        public void Sanitize_ReplacesPathSeparatorsSoASlotCannotEscapeItsFolder()
        {
            // Only '/' is illegal here: the dots survive as ordinary filename characters, but
            // without a separator they can no longer form a traversal segment.
            string sanitized = SaveSlotId.Sanitize("../etc/passwd");

            Assert.AreEqual(".._etc_passwd", sanitized);
            Assert.IsFalse(sanitized.Contains("/"), "a path separator survived sanitization");
        }

        [Test]
        public void Sanitize_KeepsDigitsDashesDotsAndUnderscores()
        {
            Assert.AreEqual("slot-1.2_3", SaveSlotId.Sanitize("slot-1.2_3"));
        }

        [Test]
        public void Sanitize_TrimsSurroundingWhitespaceBeforeConverting()
        {
            Assert.AreEqual("slot_1", SaveSlotId.Sanitize("  slot_1  "));
        }

        [Test]
        public void Sanitize_TrimsLeadingAndTrailingUnderscores()
        {
            Assert.AreEqual("slot", SaveSlotId.Sanitize("__slot__"));
        }

        [Test]
        public void Sanitize_KeepsInnerUnderscoresThatCameFromIllegalCharacters()
        {
            Assert.AreEqual("my_slot", SaveSlotId.Sanitize("my slot"));
        }

        // ══════════════════════════════════════════════
        // Construction and Equality
        // ══════════════════════════════════════════════

        [Test]
        public void Constructor_AppliesTheSameSanitizationAsSanitize()
        {
            Assert.AreEqual(SaveSlotId.Sanitize("Slot 2"), new SaveSlotId("Slot 2").Value);
        }

        [Test]
        public void From_MatchesTheConstructor()
        {
            Assert.AreEqual(new SaveSlotId("slot_2"), SaveSlotId.From("slot_2"));
        }

        [Test]
        public void Default_StructHasNullValue_AndDoesNotThrowOnGetHashCode()
        {
            // A default(SaveSlotId) never goes through the constructor, so Value stays null.
            SaveSlotId uninitialized = default;

            Assert.IsNull(uninitialized.Value);
            Assert.DoesNotThrow(() => uninitialized.GetHashCode());
        }

        [Test]
        public void Equality_IgnoresFormattingDifferencesThatSanitizeAwayTheSame()
        {
            Assert.IsTrue(new SaveSlotId("Slot 1") == new SaveSlotId("slot_1"));
        }

        [Test]
        public void Inequality_SeparatesDifferentSlots()
        {
            Assert.IsTrue(new SaveSlotId("slot_1") != new SaveSlotId("slot_2"));
        }

        [Test]
        public void GetHashCode_MatchesForEqualIds()
        {
            Assert.AreEqual(new SaveSlotId("Slot 1").GetHashCode(), new SaveSlotId("slot_1").GetHashCode());
        }

        [Test]
        public void ToString_ReturnsTheSanitizedValue()
        {
            Assert.AreEqual("slot_1", new SaveSlotId("Slot 1").ToString());
        }
    }
}
