namespace Game.Framework.SaveSystem
{
    // Transforms save payload bytes so they are not stored as readable plaintext.
    // Implementations must be able to reverse their own output.
    public interface ISaveEncryptor
    {
        byte[] Encrypt(byte[] plain);
        byte[] Decrypt(byte[] cipher);
    }
}
