using System;
using System.IO;
using System.Security.Cryptography;

namespace Game.Framework.SaveSystem
{
    /// <summary>
    /// AES encryption for save payloads with authentication (encrypt-then-MAC).
    /// Uses AES-CBC + PKCS7 with a per-write random salt and IV, keys derived from the password
    /// with PBKDF2 (Rfc2898DeriveBytes), and an HMAC-SHA256 over the whole payload so tampering or
    /// corruption is detected on decrypt. Output layout is [salt][iv][ciphertext][hmac].
    ///
    /// Note: a password compiled into the build only deters casual editing — a determined user can
    /// still recover it by decompiling. Treat it as tamper *friction*, not server-grade anti-cheat.
    /// </summary>
    public sealed class AesSaveEncryptor : ISaveEncryptor
    {
        private const int KeySize = 16;    // 128-bit AES key
        private const int IvSize = 16;
        private const int SaltSize = 16;
        private const int MacKeySize = 32; // HMAC-SHA256 key
        private const int MacSize = 32;    // HMAC-SHA256 output
        private const int Iterations = 10000;

        private readonly string _password;

        public AesSaveEncryptor(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Encryption password cannot be null or empty.", nameof(password));

            _password = password;
        }

        // ══════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════

        public byte[] Encrypt(byte[] plain)
        {
            if (plain == null)
                throw new ArgumentNullException(nameof(plain));

            byte[] salt = GenerateRandomBytes(SaltSize);
            DeriveKeys(_password, salt, out byte[] encKey, out byte[] macKey);

            using (var aes = CreateAes())
            {
                aes.GenerateIV();
                aes.Key = encKey;

                byte[] cipherText;
                using (var ms = new MemoryStream())
                {
                    using (var encryptor = aes.CreateEncryptor())
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        cs.Write(plain, 0, plain.Length);
                    cipherText = ms.ToArray();
                }

                // signed = [salt][iv][ciphertext]; the MAC authenticates all of it.
                byte[] signed = Concat(salt, aes.IV, cipherText);
                byte[] mac = ComputeMac(macKey, signed);
                return Concat(signed, mac);
            }
        }

        public byte[] Decrypt(byte[] cipher)
        {
            if (cipher == null)
                throw new ArgumentNullException(nameof(cipher));
            if (cipher.Length < SaltSize + IvSize + MacSize)
                throw new CryptographicException("Encrypted payload is too short to be valid.");

            int signedLength = cipher.Length - MacSize;

            byte[] signed = new byte[signedLength];
            byte[] mac = new byte[MacSize];
            Buffer.BlockCopy(cipher, 0, signed, 0, signedLength);
            Buffer.BlockCopy(cipher, signedLength, mac, 0, MacSize);

            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(signed, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(signed, SaltSize, iv, 0, IvSize);

            DeriveKeys(_password, salt, out byte[] encKey, out byte[] macKey);

            // Authenticate before decrypting so tampered/corrupt data is rejected.
            byte[] expectedMac = ComputeMac(macKey, signed);
            if (!ConstantTimeEquals(mac, expectedMac))
                throw new CryptographicException("Save integrity check failed (wrong password or tampered/corrupt data).");

            int cipherTextOffset = SaltSize + IvSize;
            int cipherTextLength = signedLength - cipherTextOffset;

            using (var aes = CreateAes())
            {
                aes.Key = encKey;
                aes.IV = iv;

                using (var input = new MemoryStream(signed, cipherTextOffset, cipherTextLength))
                using (var output = new MemoryStream())
                {
                    using (var decryptor = aes.CreateDecryptor())
                    using (var cs = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                        cs.CopyTo(output);

                    return output.ToArray();
                }
            }
        }

        // ══════════════════════════════════════════════
        // Internal Helpers
        // ══════════════════════════════════════════════

        private static Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        // PBKDF2 emits a continuous byte stream, so the encryption and MAC keys are distinct slices.
        private static void DeriveKeys(string password, byte[] salt, out byte[] encKey, out byte[] macKey)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                encKey = kdf.GetBytes(KeySize);
                macKey = kdf.GetBytes(MacKeySize);
            }
        }

        private static byte[] ComputeMac(byte[] macKey, byte[] data)
        {
            using (var hmac = new HMACSHA256(macKey))
                return hmac.ComputeHash(data);
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] GenerateRandomBytes(int length)
        {
            byte[] bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return bytes;
        }

        private static byte[] Concat(params byte[][] arrays)
        {
            int total = 0;
            for (int i = 0; i < arrays.Length; i++)
                total += arrays[i].Length;

            byte[] result = new byte[total];
            int offset = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                Buffer.BlockCopy(arrays[i], 0, result, offset, arrays[i].Length);
                offset += arrays[i].Length;
            }
            return result;
        }
    }
}
