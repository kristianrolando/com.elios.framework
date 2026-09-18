using NUnit.Framework;

namespace Elios.Framework.SaveSystem
{
    public class SaveKeyBuilderTests
    {
        // ══════════════════════════════════════════════
        // NormalizeSegment
        // ══════════════════════════════════════════════

        [Test]
        public void NormalizeSegment_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.NormalizeSegment(null));
        }

        [Test]
        public void NormalizeSegment_Whitespace_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.NormalizeSegment("   "));
        }

        [Test]
        public void NormalizeSegment_Lowercases()
        {
            Assert.AreEqual("boss_room", SaveKeyBuilder.NormalizeSegment("Boss_Room"));
        }

        [Test]
        public void NormalizeSegment_ConvertsSpacesToUnderscores()
        {
            Assert.AreEqual("boss_room", SaveKeyBuilder.NormalizeSegment("boss room"));
        }

        [Test]
        public void NormalizeSegment_ConvertsIllegalCharactersToUnderscores()
        {
            Assert.AreEqual("a_b_c", SaveKeyBuilder.NormalizeSegment("a:b|c"));
        }

        [Test]
        public void NormalizeSegment_TrimsSurroundingUnderscores()
        {
            Assert.AreEqual("value", SaveKeyBuilder.NormalizeSegment("__value__"));
        }

        [Test]
        public void NormalizeSegment_CanStripAReservedPrefixIntoTheReservedNamespace()
        {
            // Documents why SaveConstants stores the reserved namespace in normalized form:
            // "__sys__" and "sys" land on the same segment once normalized.
            Assert.AreEqual(SaveConstants.ReservedSystemNamespace, SaveKeyBuilder.NormalizeSegment("__sys__"));
        }

        [Test]
        public void NormalizeSegment_KeepsDigitsDashesDotsAndUnderscores()
        {
            Assert.AreEqual("v1.2-beta_3", SaveKeyBuilder.NormalizeSegment("v1.2-beta_3"));
        }

        // ══════════════════════════════════════════════
        // Join
        // ══════════════════════════════════════════════

        [Test]
        public void Join_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.Join(null));
        }

        [Test]
        public void Join_NoSegments_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.Join());
        }

        [Test]
        public void Join_SeparatesSegmentsWithForwardSlashes()
        {
            Assert.AreEqual("a/b/c", SaveKeyBuilder.Join("a", "b", "c"));
        }

        [Test]
        public void Join_SkipsSegmentsThatNormalizeToNothing()
        {
            // The classic off-by-one: a dropped segment must not leave a dangling separator.
            Assert.AreEqual("a/c", SaveKeyBuilder.Join("a", "  ", "c"));
        }

        [Test]
        public void Join_SkipsALeadingEmptySegmentWithoutLeavingALeadingSlash()
        {
            Assert.AreEqual("a/b", SaveKeyBuilder.Join(null, "a", "b"));
        }

        [Test]
        public void Join_SkipsATrailingEmptySegmentWithoutLeavingATrailingSlash()
        {
            Assert.AreEqual("a/b", SaveKeyBuilder.Join("a", "b", ""));
        }

        [Test]
        public void Join_NormalizesEverySegment()
        {
            Assert.AreEqual("boss_room/cleared", SaveKeyBuilder.Join("Boss Room", "Cleared"));
        }

        [Test]
        public void Join_AllSegmentsEmpty_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.Join("", "   ", null));
        }

        // ══════════════════════════════════════════════
        // NormalizeRawKey
        // ══════════════════════════════════════════════

        [Test]
        public void NormalizeRawKey_Null_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.NormalizeRawKey(null));
        }

        [Test]
        public void NormalizeRawKey_Whitespace_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, SaveKeyBuilder.NormalizeRawKey("  "));
        }

        [Test]
        public void NormalizeRawKey_KeepsTheSlashHierarchy()
        {
            Assert.AreEqual("stage/progress/1", SaveKeyBuilder.NormalizeRawKey("stage/progress/1"));
        }

        [Test]
        public void NormalizeRawKey_ConvertsBackslashesToForwardSlashes()
        {
            Assert.AreEqual("stage/progress", SaveKeyBuilder.NormalizeRawKey("stage\\progress"));
        }

        [Test]
        public void NormalizeRawKey_CollapsesRepeatedAndSurroundingSlashes()
        {
            Assert.AreEqual("a/b", SaveKeyBuilder.NormalizeRawKey("//a///b//"));
        }

        [Test]
        public void NormalizeRawKey_IsIdempotent()
        {
            string once = SaveKeyBuilder.NormalizeRawKey("Stage/Boss Room/Cleared");

            Assert.AreEqual(once, SaveKeyBuilder.NormalizeRawKey(once));
        }

        // ══════════════════════════════════════════════
        // Named Keys
        // ══════════════════════════════════════════════

        [Test]
        public void StageCompleted_BuildsTheExpectedPath()
        {
            Assert.AreEqual("stage/progress/level_1/completed", SaveKeyBuilder.StageCompleted("Level 1"));
        }

        [Test]
        public void StageStars_BuildsTheExpectedPath()
        {
            Assert.AreEqual("stage/progress/level_1/stars", SaveKeyBuilder.StageStars("Level 1"));
        }

        [Test]
        public void ObjectState_BuildsTheExpectedPath()
        {
            Assert.AreEqual("object/chest_a/state", SaveKeyBuilder.ObjectState("Chest A"));
        }

        [Test]
        public void ObjectField_BuildsTheExpectedPath()
        {
            Assert.AreEqual("object/chest_a/opened", SaveKeyBuilder.ObjectField("Chest A", "Opened"));
        }

        [Test]
        public void QuestState_BuildsTheExpectedPath()
        {
            Assert.AreEqual("quest/find_the_key/state", SaveKeyBuilder.QuestState("Find The Key"));
        }

        [Test]
        public void InventoryItemCount_BuildsTheExpectedPath()
        {
            Assert.AreEqual("inventory/item/red_potion/count", SaveKeyBuilder.InventoryItemCount("Red Potion"));
        }

        [Test]
        public void Custom_JoinsArbitrarySegments()
        {
            Assert.AreEqual("run/best/time", SaveKeyBuilder.Custom("run", "best", "time"));
        }

        [Test]
        public void NamedKeys_WithAnEmptyId_DoNotProduceADoubleSlash()
        {
            Assert.AreEqual("stage/progress/completed", SaveKeyBuilder.StageCompleted(""));
        }
    }
}
