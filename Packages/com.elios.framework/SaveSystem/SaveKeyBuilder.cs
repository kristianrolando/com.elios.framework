using System;
using System.Text;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// Helper to build dynamic namespaced keys consistently.
    /// </summary>
    public static class SaveKeyBuilder
    {
        public static string StageCompleted(string stageId)
            => Join("stage", "progress", stageId, "completed");

        public static string StageStars(string stageId)
            => Join("stage", "progress", stageId, "stars");

        public static string ObjectState(string uniqueId)
            => Join("object", uniqueId, "state");

        public static string ObjectField(string uniqueId, string fieldName)
            => Join("object", uniqueId, fieldName);

        public static string QuestState(string questId)
            => Join("quest", questId, "state");

        public static string InventoryItemCount(string itemId)
            => Join("inventory", "item", itemId, "count");

        public static string Custom(params string[] segments)
            => Join(segments);

        /// <summary>
        /// Normalize a fully composed key path. Keeps slash hierarchy, normalizes segments.
        /// </summary>
        public static string NormalizeRawKey(string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                return string.Empty;

            string[] parts = rawKey.Replace('\\', '/')
                                   .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            return Join(parts);
        }

        /// <summary>
        /// Creates a path-like key. Each segment is normalized to reduce typo/format issues.
        /// </summary>
        public static string Join(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(64);
            bool first = true;

            for (int i = 0; i < segments.Length; i++)
            {
                string seg = NormalizeSegment(segments[i]);
                if (string.IsNullOrWhiteSpace(seg)) continue;

                if (!first) sb.Append('/');
                sb.Append(seg);
                first = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Safe segment for key namespaces. Converts spaces and illegal chars to underscore, lowercases.
        /// </summary>
        public static string NormalizeSegment(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];

                bool ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' || c == '-' || c == '.';

                sb.Append(ok ? char.ToLowerInvariant(c) : '_');
            }

            return sb.ToString().Trim('_');
        }
    }
}