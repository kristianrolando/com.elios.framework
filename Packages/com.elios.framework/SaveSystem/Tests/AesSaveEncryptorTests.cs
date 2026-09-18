using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Elios.Framework.SaveSystem
{
    public class AesSaveEncryptorTests
    {
        private const string Password = "correct-horse-battery-staple";
        private const string Payload = "{\"meta\":{\"schemaVersion\":1},\"entries\":{}}";

        // [salt 16][iv 16][ciphertext][hmac 32]
        private const int HeaderAndMacSize = 16 + 16 + 32;

        // ══════════════════════════════════════════════
        // Guards
        // ══════════════════════════════════════════════

        [Test]
        public void Constructor_NullPassword_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AesSaveEncryptor(null));
        }

        [Test]
        public void Constructor_EmptyPassword_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AesSaveEncryptor(string.Empty));
        }

        [Test]
        public void Encrypt_Null_Throws()
        {
            var encryptor = new AesSaveEncryptor(Password);

            Assert.Throws<ArgumentNullException>(() => encryptor.Encrypt(null));
        }

        [Test]
        public void Decrypt_Null_Throws()
        {
            var encryptor = new AesSaveEncryptor(Password);

            Assert.Throws<ArgumentNullException>(() => encryptor.Decrypt(null));
        }

        // ══════════════════════════════════════════════
        // Round Trip
        // ══════════════════════════════════════════════

        [Test]
        public void RoundTrip_ReturnsTheOriginalBytes()
        {
            var encryptor = new AesSaveEncryptor(Password);
            byte[] plain = Encoding.UTF8.GetBytes(Payload);

            byte[] restored = encryptor.Decrypt(encryptor.Encrypt(plain));

            Assert.AreEqual(plain, restored);
        }

        [Test]
        public void RoundTrip_EmptyPayload_Works()
        {
            // PKCS7 pads an empty payload to a full block; decrypt must unwind it back to empty.
            var encryptor = new AesSaveEncryptor(Password);

            byte[] restored = encryptor.Decrypt(encryptor.Encrypt(Array.Empty<byte>()));

            Assert.AreEqual(0, restored.Length);
        }

        [Test]
        public void RoundTrip_SurvivesAFreshEncryptorWithTheSamePassword()
        {
            // Keys are derived from the password and the stored salt, never held in the instance,
            // so a save written in one session must open in the next.
            byte[] plain = Encoding.UTF8.GetBytes(Payload);
            byte[] cipher = new AesSaveEncryptor(Password).Encrypt(plain);

            Assert.AreEqual(plain, new AesSaveEncryptor(Password).Decrypt(cipher));
        }

        // ══════════════════════════════════════════════
        // Output Shape
        // ══════════════════════════════════════════════

        [Test]
        public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
        {
            // A fresh salt and IV per write; identical output would leak that nothing changed.
            var encryptor = new AesSaveEncryptor(Password);
            byte[] plain = Encoding.UTF8.GetBytes(Payload);

            Assert.AreNotEqual(encryptor.Encrypt(plain), encryptor.Encrypt(plain));
        }

        [Test]
        public void Encrypt_OutputCarriesSaltIvAndMacOnTopOfTheCiphertext()
        {
            var encryptor = new AesSaveEncryptor(Password);
            byte[] plain = Encoding.UTF8.GetBytes(Payload);

            Assert.Greater(encryptor.Encrypt(plain).Length, HeaderAndMacSize);
        }

        [Test]
        public void Encrypt_DoesNotLeaveThePlaintextReadable()
        {
            var encryptor = new AesSaveEncryptor(Password);
            byte[] cipher = encryptor.Encrypt(Encoding.UTF8.GetBytes(Payload));

            Assert.IsFalse(Encoding.UTF8.GetString(cipher).Contains("schemaVersion"));
        }

        // ══════════════════════════════════════════════
        // Rejection
        // ══════════════════════════════════════════════

        [Test]
        public void Decrypt_WithTheWrongPassword_IsRejected()
        {
            byte[] cipher = new AesSaveEncryptor(Password).Encrypt(Encoding.UTF8.GetBytes(Payload));

            Assert.Throws<CryptographicException>(() => new AesSaveEncryptor("another-password").Decrypt(cipher));
        }

        [Test]
        public void Decrypt_TamperedCiphertext_IsRejectedByTheMac()
        {
            var encryptor = new AesSaveEncryptor(Password);
            byte[] cipher = encryptor.Encrypt(Encoding.UTF8.GetBytes(Payload));

            // Flip a bit inside the ciphertext body, past the salt and IV.
            cipher[40] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => encryptor.Decrypt(cipher));
        }

        [Test]
        public void Decrypt_TamperedMac_IsRejected()
        {
            var encryptor = new AesSaveEncryptor(Password);
            byte[] cipher = encryptor.Encrypt(Encoding.UTF8.GetBytes(Payload));

            cipher[cipher.Length - 1] ^= 0xFF;

            Assert.Throws<CryptographicException>(() => encryptor.Decrypt(cipher));
        }

        [Test]
        public void Decrypt_PayloadTooShortToHoldItsOwnHeader_IsRejected()
        {
            var encryptor = new AesSaveEncryptor(Password);

            Assert.Throws<CryptographicException>(() => encryptor.Decrypt(new byte[HeaderAndMacSize - 1]));
        }

        [Test]
        public void Decrypt_PlainJsonMistakenForCiphertext_IsRejected()
        {
            // Guards the storage layer's auto-detect: a plaintext file must never decrypt into noise.
            var encryptor = new AesSaveEncryptor(Password);
            byte[] plainJson = Encoding.UTF8.GetBytes(Payload + Payload);

            Assert.Throws<CryptographicException>(() => encryptor.Decrypt(plainJson));
        }
    }
}
