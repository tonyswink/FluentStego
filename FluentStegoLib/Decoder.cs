using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FluentStegoLib
{
    public static class Decoder
    {
        /// <summary>
        /// Asynchronously decodes a hidden message from an image file, optionally decrypting with a password.
        /// </summary>
        public static Task<string> DecodeAsync(string imagePath, string password = null) =>
            Task.Run(() => Decode(imagePath, password));

        /// <summary>
        /// Decodes a hidden message from an image file, optionally decrypting with a password.
        /// </summary>
        public static string Decode(string imagePath, string password = null)
        {
            Debug.WriteLine($"[Decoder] Loading image: {imagePath}");
            using var bmp = new Bitmap(imagePath);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var rgbValues = ExtractRgbValues(bmpData);
                int availableBits = bmp.Width * bmp.Height * 3;
                int payloadLength = ReadPayloadLength(rgbValues);
                Debug.WriteLine($"[Decoder] Payload length: {payloadLength}");
                if (payloadLength <= 0 || payloadLength > (availableBits - 32) / 8)
                    throw new Exception("Invalid or corrupted payload length.");

                var payload = ReadPayload(rgbValues, payloadLength);
                Debug.WriteLine($"[Decoder] Extracted payload (hex): {BitConverter.ToString(payload)}");

                if (!string.IsNullOrEmpty(password))
                {
                    Debug.WriteLine("[Decoder] Decrypting payload with password.");
                    payload = DecryptWithPassword(payload, password);
                }

                return TryDecodeUtf8(payload);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
        }

        private static byte[] ExtractRgbValues(BitmapData bmpData)
        {
            int bytes = Math.Abs(bmpData.Stride) * bmpData.Height;
            var rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
            return rgbValues;
        }

        private static int ReadPayloadLength(byte[] rgbValues)
        {
            int bitIndex = 0;
            var lengthBytes = new byte[4];
            for (int i = 0; i < rgbValues.Length && bitIndex < 32; i++)
            {
                if ((i & 3) == 3) continue; // Skip alpha
                int byteIndex = bitIndex / 8;
                int bitInByte = 7 - (bitIndex % 8);
                int bit = rgbValues[i] & 1;
                lengthBytes[byteIndex] |= (byte)(bit << bitInByte);
                Debug.WriteLine($"[Decoder] Length bit {bitIndex}: {bit}");
                bitIndex++;
            }
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(lengthBytes);
            return BitConverter.ToInt32(lengthBytes, 0);
        }

        private static byte[] ReadPayload(byte[] rgbValues, int length)
        {
            var payload = new byte[length];
            int payloadBit = 0, bitsSeen = 0;
            for (int i = 0; i < rgbValues.Length && payloadBit < length * 8; i++)
            {
                if ((i & 3) == 3) continue; // Skip alpha
                if (bitsSeen++ < 32) continue; // Skip length prefix
                int byteIndex = payloadBit / 8;
                int bitInByte = 7 - (payloadBit % 8);
                int bit = rgbValues[i] & 1;
                payload[byteIndex] |= (byte)(bit << bitInByte);
                if (payloadBit < 32 || payloadBit > (length * 8) - 32)
                    Debug.WriteLine($"[Decoder] Payload bit {payloadBit}: {bit}");
                payloadBit++;
            }
            return payload;
        }

        private static string TryDecodeUtf8(byte[] payload)
        {
            try
            {
                var result = Encoding.UTF8.GetString(payload);
                Debug.WriteLine("[Decoder] Decoded message as UTF-8 string.");
                return result;
            }
            catch
            {
                Debug.WriteLine("[Decoder] Decoded message is not valid UTF-8, returning hex.");
                return BitConverter.ToString(payload);
            }
        }

        private static byte[] DecryptWithPassword(byte[] data, string password)
        {
            if (data is null || data.Length < 16)
                throw new ArgumentException("Invalid encrypted data.", nameof(data));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password must not be empty.", nameof(password));

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = DeriveKey(password, aes.KeySize / 8);
            var iv = new byte[16];
            Array.Copy(data, 0, iv, 0, 16);
            aes.IV = iv;
            using var ms = new MemoryStream(data, 16, data.Length - 16);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var outMs = new MemoryStream();
            cs.CopyTo(outMs);
            Debug.WriteLine("[Decoder] Decryption complete.");
            return outMs.ToArray();
        }

        private static byte[] DeriveKey(string password, int keySize)
        {
            byte[] salt = { 0x4B, 0x65, 0x79, 0x53, 0x61, 0x6C, 0x74, 0x21 }; // "KeySalt!"
            using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            Debug.WriteLine("[Decoder] Key derived from password.");
            return kdf.GetBytes(keySize);
        }
    }
}
