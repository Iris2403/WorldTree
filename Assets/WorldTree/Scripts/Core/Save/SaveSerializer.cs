using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace WorldTree.Core.Save
{
    /// <summary>Converts saves to and from text and verifies their integrity.</summary>
    public static class SaveSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
        };

        public static string Serialize(SaveData data, long sequence, DateTime savedAtUtc)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string payload = JsonConvert.SerializeObject(data, Settings);
            var envelope = new SaveEnvelope
            {
                FormatVersion = SaveEnvelope.CurrentFormatVersion,
                Sequence = sequence,
                SavedAtUtcTicks = savedAtUtc.Ticks,
                Sha256 = Hash(payload),
                Payload = payload,
            };
            return JsonConvert.SerializeObject(envelope, Settings);
        }

        /// <summary>
        /// Parses and verifies a save file. Returns false (never throws) for anything malformed:
        /// truncated text, bad JSON, unknown format version, or a checksum mismatch.
        /// </summary>
        public static bool TryParse(string text, out SaveEnvelope envelope, out SaveData data)
        {
            envelope = null;
            data = null;
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                var env = JsonConvert.DeserializeObject<SaveEnvelope>(text, Settings);
                if (env == null || env.Payload == null || env.Sha256 == null) return false;
                if (env.FormatVersion != SaveEnvelope.CurrentFormatVersion) return false;
                if (!string.Equals(env.Sha256, Hash(env.Payload), StringComparison.Ordinal)) return false;
                var payload = JsonConvert.DeserializeObject<SaveData>(env.Payload, Settings);
                if (payload == null) return false;
                envelope = env;
                data = payload;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string Hash(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var sb = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
