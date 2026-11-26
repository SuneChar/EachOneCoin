using System;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace EachOneCoin.Networking
{
    // Windows DPAPI protects the session for the current OS account. The data path hash keeps ParrelSync editors separate.
    public static class ReconnectSessionStore
    {
        [Serializable] public sealed class Session { public string address; public string token; }

        private static string FilePath
        {
            get
            {
                using (var hash = SHA256.Create())
                {
                    var key = hash.ComputeHash(Encoding.UTF8.GetBytes(Application.dataPath.ToLowerInvariant()));
                    return Path.Combine(Application.persistentDataPath, "NetworkSessions", BitConverter.ToString(key, 0, 12).Replace("-", "") + ".dat");
                }
            }
        }

        public static bool Save(string address, string token) => SaveAt(FilePath, address, token);

        internal static bool SaveAt(string path, string address, string token)
        {
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrEmpty(token)) return false;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Session { address = address, token = token }));
                var protectedBytes = ProtectBytes(bytes, true);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllBytes(temp, protectedBytes);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("无法安全保存重连会话：" + e.GetType().Name); return false; }
        }

        public static bool TryLoad(out Session session) => TryLoadAt(FilePath, out session);

        internal static bool TryLoadAt(string path, out Session session)
        {
            session = null;
            try
            {
                if (!File.Exists(path)) return false;
                var bytes = ProtectBytes(File.ReadAllBytes(path), false);
                var result = JsonUtility.FromJson<Session>(Encoding.UTF8.GetString(bytes));
                if (result == null || string.IsNullOrWhiteSpace(result.address) || string.IsNullOrEmpty(result.token)) return false;
                session = result;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("无法读取重连会话：" + e.GetType().Name); return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob { public int Length; public IntPtr Data; }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
#endif

        private static byte[] ProtectBytes(byte[] bytes, bool encrypt)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var input = new DataBlob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
            var output = new DataBlob();
            try
            {
                Marshal.Copy(bytes, 0, input.Data, bytes.Length);
                bool success = encrypt ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
                if (!success) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[output.Length];
                Marshal.Copy(output.Data, result, 0, output.Length);
                return result;
            }
            finally
            {
                if (input.Data != IntPtr.Zero) Marshal.FreeHGlobal(input.Data);
                if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            }
#else
            throw new PlatformNotSupportedException("Reconnect session storage currently supports Windows only.");
#endif
        }

        public static void Clear()
        {
            try { string path = FilePath; if (File.Exists(path)) File.Delete(path); }
            catch (Exception e) { Debug.LogWarning("无法清理重连会话：" + e.GetType().Name); }
        }
    }
}
