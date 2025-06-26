using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FluentStegoLib
{
    public static class Encoder
    {
        /// <summary>
        /// Encodes a payload into an image, optionally encrypting with a password.
        /// </summary>
        public static void Encode(string inputImagePath, string outputImagePath, byte[] payload, string password = null)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(inputImagePath)) throw new ArgumentNullException(nameof(inputImagePath));
            if (string.IsNullOrWhiteSpace(outputImagePath)) throw new ArgumentNullException(nameof(outputImagePath));

            Debug.WriteLine($"[Encoder] Loading image: {inputImagePath}");
            if (!string.IsNullOrWhiteSpace(password))
            {
                Debug.WriteLine("[Encoder] Encrypting payload with password.");
                payload = EncryptWithPassword(payload, password);
            }

            var data = AddLengthPrefix(payload);

            using var bmp = new Bitmap(inputImagePath);
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var rgbValues = ExtractRgbValues(bmpData);
                int availableBits = bmp.Width * bmp.Height * 3;
                int requiredBits = data.Length * 8;
                Debug.WriteLine($"[Encoder] Available bits: {availableBits}, Required bits: {requiredBits}");
                if (requiredBits > availableBits)
                    throw new ArgumentException("Message too large to encode in image", nameof(payload));

                WritePayloadToRgb(rgbValues, data, requiredBits);
                Marshal.Copy(rgbValues, 0, bmpData.Scan0, rgbValues.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }
            Debug.WriteLine($"[Encoder] Saving encoded image: {outputImagePath}");
            bmp.Save(outputImagePath, ImageFormat.Png);
        }

        /// <summary>
        /// Asynchronously encodes a payload into an image, optionally encrypting with a password.
        /// </summary>
        public static Task EncodeAsync(string inputImagePath, string outputImagePath, byte[] payload, string password = null) =>
            Task.Run(() => Encode(inputImagePath, outputImagePath, payload, password));

        private static byte[] EncryptWithPassword(byte[] data, string password)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password must not be empty.", nameof(password));

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();
            aes.Key = DeriveKey(password, aes.KeySize / 8);

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }
            Debug.WriteLine("[Encoder] Encryption complete.");
            return ms.ToArray();
        }

        private static byte[] DeriveKey(string password, int keySize)
        {
            if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password must not be empty.", nameof(password));
            if (keySize <= 0) throw new ArgumentOutOfRangeException(nameof(keySize));
            byte[] salt = { 0x4B, 0x65, 0x79, 0x53, 0x61, 0x6C, 0x74, 0x21 }; // "KeySalt!"
            using var kdf = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            Debug.WriteLine("[Encoder] Key derived from password.");
            return kdf.GetBytes(keySize);
        }

        private static byte[] AddLengthPrefix(byte[] payload)
        {
            var lengthPrefix = BitConverter.GetBytes(payload.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(lengthPrefix);
            var data = new byte[4 + payload.Length];
            Buffer.BlockCopy(lengthPrefix, 0, data, 0, 4);
            Buffer.BlockCopy(payload, 0, data, 4, payload.Length);
            return data;
        }

        private static byte[] ExtractRgbValues(BitmapData bmpData)
        {
            int bytes = Math.Abs(bmpData.Stride) * bmpData.Height;
            var rgbValues = new byte[bytes];
            Marshal.Copy(bmpData.Scan0, rgbValues, 0, bytes);
            return rgbValues;
        }

        private static void WritePayloadToRgb(byte[] rgbValues, byte[] data, int requiredBits)
        {
            int bitIndex = 0;
            for (int i = 0; i < rgbValues.Length && bitIndex < requiredBits; i++)
            {
                if ((i & 3) == 3) continue; // Skip alpha channel
                int byteIndex = bitIndex / 8;
                int bitInByte = 7 - (bitIndex % 8);
                int bit = (data[byteIndex] >> bitInByte) & 1;
                rgbValues[i] = (byte)((rgbValues[i] & 0xFE) | bit);
                if (bitIndex < 32 || bitIndex > requiredBits - 32)
                    Debug.WriteLine($"[Encoder] Pixel {i}, Bit {bitIndex}: {bit}");
                bitIndex++;
            }
        }
    }
}
