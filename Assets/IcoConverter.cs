using UnityEngine;

public class IcoConverter {

    public static Texture Convert(byte[] icoBytes) {
        
        byte[] pngSignature = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        int pngOffset = -1;

        for (int i = 0; i < icoBytes.Length - 4; i++) {
            if (icoBytes[i] == pngSignature[0] &&
                icoBytes[i + 1] == pngSignature[1] &&
                icoBytes[i + 2] == pngSignature[2] &&
                icoBytes[i + 3] == pngSignature[3]) {
                pngOffset = i;
                break;
            }
        }

        if (pngOffset != -1) {
            byte[] pngData = new byte[icoBytes.Length - pngOffset];
            System.Array.Copy(icoBytes, pngOffset, pngData, 0, pngData.Length);

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.LoadImage(pngData);
            return texture;
        }

        using (var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(icoBytes))) {
            ushort reserved = reader.ReadUInt16();
            ushort type = reader.ReadUInt16();
            ushort count = reader.ReadUInt16();

            if (type != 1 || count == 0) {
                Debug.LogError("Wrong ico format");
                return null;
            }

            IconDirEntry[] entries = new IconDirEntry[count];
            int largestIndex = 0;
            int largestSize = 0;

            for (int i = 0; i < count; i++) {
                entries[i].Width = reader.ReadByte();
                entries[i].Height = reader.ReadByte();
                entries[i].ColorCount = reader.ReadByte();
                entries[i].Reserved = reader.ReadByte();
                entries[i].Planes = reader.ReadUInt16();
                entries[i].BitCount = reader.ReadUInt16();
                entries[i].BytesInRes = reader.ReadInt32();
                entries[i].ImageOffset = reader.ReadInt32();

                int size = (entries[i].Width == 0 ? 256 : entries[i].Width) * (entries[i].Height == 0 ? 256 : entries[i].Height);
                if (size > largestSize) {
                    largestSize = size;
                    largestIndex = i;
                }
            }

            var entry = entries[largestIndex];
            reader.BaseStream.Seek(entry.ImageOffset, System.IO.SeekOrigin.Begin);

            var pngCheck = reader.ReadBytes(4);
            reader.BaseStream.Seek(entry.ImageOffset, System.IO.SeekOrigin.Begin);

            if (pngCheck[0] == 0x89 && pngCheck[1] == 0x50 && pngCheck[2] == 0x4E && pngCheck[3] == 0x47) {
                var pngData = reader.ReadBytes(entry.BytesInRes);
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.LoadImage(pngData);
                return texture;
            }

            int headerSize = reader.ReadInt32();
            int width = reader.ReadInt32();
            int heightRaw = reader.ReadInt32();
            int height = heightRaw / 2;
            reader.BaseStream.Seek(2, System.IO.SeekOrigin.Current);
            ushort bitCount = reader.ReadUInt16();

            Color32[] pixels = new Color32[width * height];

            long pixelDataOffset = entry.ImageOffset + headerSize;
            reader.BaseStream.Seek(pixelDataOffset, System.IO.SeekOrigin.Begin);

            if (bitCount == 32) {
                for (int y = 0; y < height; y++) {
                    for (int x = 0; x < width; x++) {
                        byte b = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte r = reader.ReadByte();
                        byte a = reader.ReadByte();
                        pixels[y * width + x] = new Color32(r, g, b, a);
                    }
                }
            } else if (bitCount == 24) {
                int rowSize = ((width * 3 + 3) / 4) * 4;
                for (int y = 0; y < height; y++) {
                    byte[] row = reader.ReadBytes(rowSize);
                    for (int x = 0; x < width; x++) {
                        int i = x * 3;
                        byte b = row[i];
                        byte g = row[i + 1];
                        byte r = row[i + 2];
                        pixels[y * width + x] = new Color32(r, g, b, 255);
                    }
                }
            } else if (bitCount == 8) {
                int paletteSize = entry.ColorCount;
                if (paletteSize == 0) paletteSize = 256;
                Color32[] palette = new Color32[paletteSize];
                for (int i = 0; i < paletteSize; i++) {
                    byte b = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte r = reader.ReadByte();
                    byte _ = reader.ReadByte();
                    palette[i] = new Color32(r, g, b, 255);
                }
                int rowSize = ((width + 3) / 4) * 4;
                for (int y = 0; y < height; y++) {
                    byte[] row = reader.ReadBytes(rowSize);
                    for (int x = 0; x < width; x++) {
                        byte index = row[x];
                        pixels[y * width + x] = palette[index];
                    }
                }
                ApplyAndMask(reader, pixels, width, height);
            } else if (bitCount == 4) {
                int paletteSize = entry.ColorCount;
                if (paletteSize == 0 || paletteSize > 16) paletteSize = 16;
                Color32[] palette = new Color32[paletteSize];
                for (int i = 0; i < paletteSize; i++) {
                    byte b = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte r = reader.ReadByte();
                    byte _ = reader.ReadByte();
                    palette[i] = new Color32(r, g, b, 255);
                }
                int rowSize = ((width + 1) / 2 + 3) / 4 * 4;
                for (int y = 0; y < height; y++) {
                    byte[] row = reader.ReadBytes(rowSize);
                    int px = y * width;
                    for (int x = 0; x < width; x += 2) {
                        byte bVal = row[x / 2];
                        int idx1 = (bVal >> 4) & 0x0F;
                        pixels[px++] = palette[idx1];
                        if (x + 1 < width) {
                            int idx2 = bVal & 0x0F;
                            pixels[px++] = palette[idx2];
                        }
                    }
                }
                ApplyAndMask(reader, pixels, width, height);
            } else if (bitCount == 1) {
                int paletteSize = entry.ColorCount;
                if (paletteSize == 0 || paletteSize > 2) paletteSize = 2;
                Color32[] palette = new Color32[paletteSize];
                for (int i = 0; i < paletteSize; i++) {
                    byte b = reader.ReadByte();
                    byte g = reader.ReadByte();
                    byte r = reader.ReadByte();
                    byte _ = reader.ReadByte();
                    palette[i] = new Color32(r, g, b, 255);
                }
                int rowSize = ((width + 31) / 32) * 4;
                for (int y = 0; y < height; y++) {
                    byte[] row = reader.ReadBytes(rowSize);
                    int px = y * width;
                    for (int x = 0; x < width; x++) {
                        int byteIndex = x / 8;
                        int bitIndex = 7 - (x % 8);
                        int bit = (row[byteIndex] >> bitIndex) & 1;
                        pixels[px++] = palette[bit];
                    }
                }
                ApplyAndMask(reader, pixels, width, height);
            } else {
                Debug.LogError($"Unsupported bit depth: {bitCount}");
                return null;
            }

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }

    private static void ApplyAndMask(System.IO.BinaryReader reader, Color32[] pixels, int width, int height) {
        int maskRowSize = ((width + 31) / 32) * 4;
        byte[] maskData = reader.ReadBytes(maskRowSize * height);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int byteIndex = y * maskRowSize + (x / 8);
                int bitIndex = 7 - (x % 8);
                bool isTransparent = ((maskData[byteIndex] >> bitIndex) & 1) == 1;
                if (isTransparent) {
                    int pixelIdx = y * width + x;
                    Color32 c = pixels[pixelIdx];
                    c.a = 0;
                    pixels[pixelIdx] = c;
                }
            }
        }
    }

    private struct IconDirEntry {
        public byte Width;
        public byte Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public int BytesInRes;
        public int ImageOffset;
    }
}
