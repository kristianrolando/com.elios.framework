using System;
using System.Text;

namespace Elios.Framework.SaveSystem
{
    // File-system safe slot identifier.
    public readonly struct SaveSlotId : IEquatable<SaveSlotId>
    {
        public string Value { get; }

        public SaveSlotId(string raw)
        {
            Value = Sanitize(raw);
        }

        public static SaveSlotId From(string raw) => new SaveSlotId(raw);

        public static string Sanitize(string raw)
        {
            raw = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return SaveConstants.DefaultSlotId;

            var sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                bool ok =
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' || c == '-' || c == '.';

                sb.Append(ok ? c : '_');
            }

            string result = sb.ToString().Trim('_').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(result))
                result = SaveConstants.DefaultSlotId;

            return result;
        }

        public bool Equals(SaveSlotId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SaveSlotId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value;

        public static bool operator ==(SaveSlotId a, SaveSlotId b) => a.Equals(b);
        public static bool operator !=(SaveSlotId a, SaveSlotId b) => !a.Equals(b);
    }
}