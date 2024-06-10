using Newtonsoft.Json;
using ProtoBuf;
using System.IO.Compression;

namespace NTDLS.ReliableMessaging.Internal
{
    internal static class Utility
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            TypeNameHandling = TypeNameHandling.All
        };

        public delegate void TryAndIgnoreProc();
        public delegate T TryAndIgnoreProc<T>();

        public static bool ImplementsGenericInterfaceWithArgument(Type type, Type genericInterface, Type argumentType)
        {
            return type.GetInterfaces().Any(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == genericInterface &&
                interfaceType.GetGenericArguments().Any(arg => argumentType.IsAssignableFrom(arg)));
        }

        public static Exception GetBaseException(Exception ex)
        {
            if (ex.InnerException != null)
            {
                return GetBaseException(ex.InnerException);
            }
            return ex;
        }

        /// <summary>
        /// We didn't need that exception! Did we?... DID WE?!
        /// </summary>
        public static void TryAndIgnore(TryAndIgnoreProc func)
        {
            try { func(); } catch { }
        }

        /// <summary>
        /// We didn't need that exception! Did we?... DID WE?!
        /// </summary>
        public static T? TryAndIgnore<T>(TryAndIgnoreProc<T> func)
        {
            try { return func(); } catch { }
            return default;
        }

        public static byte[] SerializeToByteArray(object obj)
        {
            if (obj == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }

        public static string RmSerializeFramePayloadToText<T>(T obj)
            => JsonConvert.SerializeObject(obj, _jsonSettings);

        public static T? RmDeserializeFramePayloadToObject<T>(IRmSerializationProvider? serializationProvider, string json)
        {
            if (serializationProvider != null) //Using custom serialization.
            {
                return serializationProvider.DeserializeToObject<T>(json);
            }
            else //Using built-in default serialization.
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
        }

        public static T DeserializeToObject<T>(byte[] arrBytes)
        {
            using var stream = new MemoryStream();
            stream.Write(arrBytes, 0, arrBytes.Length);
            stream.Seek(0, SeekOrigin.Begin);
            return Serializer.Deserialize<T>(stream);
        }

        public static byte[] Compress(byte[]? bytes)
        {
            if (bytes == null) return Array.Empty<byte>();

            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new DeflateStream(mso, CompressionLevel.Optimal))
            {
                msi.CopyTo(gs);
            }
            return mso.ToArray();
        }

        public static byte[] Decompress(byte[] bytes)
        {
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new DeflateStream(msi, CompressionMode.Decompress))
            {
                gs.CopyTo(mso);
            }
            return mso.ToArray();
        }
    }
}
