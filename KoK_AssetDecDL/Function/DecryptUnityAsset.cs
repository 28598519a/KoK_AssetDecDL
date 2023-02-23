using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class DecryptUnityAsset
{
    // EUAB
    public static int GetOffsetFromFilePath(string filePath)
    {
        string v5 = sha1Hashed(Path.GetFileNameWithoutExtension(filePath));
        return getOffset(v5, 8, 16);
    }

    public static int getOffset(string input, int minSize, int maxSize)
    {
        int v9 = 0;
        byte[] v8 = Encoding.ASCII.GetBytes(input);
        int v12 = v8.Length;
        for (int v10 = 0; v10 < v12; v10++)
        {
            v9 += v8[v10];
        }

        return Math.Max(minSize, v9 % maxSize);
    }

    // EEAB
    public static byte[]? DecryptMemory(string strPath, byte[] enryptedMemory)                
    {
        ulong v15;
        int v16, v17, v18, k;
        
        if (enryptedMemory.Length < 8)
            return null;

        ulong[] v6 = new ulong[]{ 0x189E7AC52BF4063D, 0x3C97EA058BF264D1, 0xE86BA20491D5F7C3, 0x569C8B73D01E2FA4 };
        int v7 = enryptedMemory.Length;
        byte[] v8 = new byte[v7 - 8];
        int v9 = (v7 - 8) / 16;
        byte[] v10 = new byte[16];
        byte[] src = v10;
        byte[] dst = new byte[16];

        if (v9 > 0)
        {
            for (int v12=0, v13=0; v12 < v9; v12++, v13 += 16)
            {
                Array.Copy(enryptedMemory, v13 + 8, v10, 0, 16);
                v15 = v6[v12 % v6.Length];            
                shuffle16(dst, src, v15);
                Array.Copy(dst, 0, v8, v13, 16);
                v10 = src;
            }
        }
        
        v16 = 16 * v9;
        
        while (true)
        {
            v17 = v8.Length;
            if ( v16 >= v17 )
                break;
            v18 = v16;

            k = v16 + 8;
            v16++;
            v8[v18] = enryptedMemory[k];
        }
        
        int v11 = 0;
        string v21 = sha1Hashed(Path.GetFileNameWithoutExtension(strPath));
        for (byte[] i = Encoding.ASCII.GetBytes(v21); ; v8[v11++] ^= i[k])
        {
            if ( v11 >= v8.Length)
                break;
            
            k = v11 % i.Length;
        }
        return v8;
    }

    private static string sha1Hashed(string input)
    {
        using var sha1 = SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(Encoding.ASCII.GetBytes(input))).ToLower();
    }

    private static void shuffle16(byte[] dst, byte[] src, ulong mask)
    {
        int v9, v11;
        ulong v10;

        for (int i = 0, v7 = 0; i < 64; i += 4, v7++)
        {
            v9 = i & 0x3F;
            v10 = (mask >> v9) & 0xF;
            v11 = v7;
            dst[v11] = src[v10];
        }
    }
}
