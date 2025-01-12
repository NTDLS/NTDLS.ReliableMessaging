using Microsoft.Extensions.Caching.Memory;
using ProtoBuf;
using System.IO.Compression;
using System.Text.Json;

namespace NTDLS.ReliableMessaging.Internal
{
    internal static class Utility
    {
        internal static IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        internal static MemoryCacheEntryOptions _slidingOneMinute = new() { SlidingExpiration = TimeSpan.FromMinutes(1) };

        public static bool CacheGet<T>(string key, out T? value)
            => _cache.TryGetValue(key, out value);

        public static void CacheSet(string key, object value, TimeSpan slidingExpiration)
            => _cache.Set(key, value, new MemoryCacheEntryOptions() { SlidingExpiration = slidingExpiration });

        public static void CacheSet(string key, object value)
            => _cache.Set(key, value, _slidingOneMinute);

        public static string GetAssemblyQualifiedType(object obj)
        {
            return GetAssemblyQualifiedType(obj.GetType());
        }

        public static string GetAssemblyQualifiedType(Type type)
        {
            var assemblyQualifiedName = type.AssemblyQualifiedName
                ?? throw new Exception("The assembly qualified type name is not available.");

            if (CacheGet(assemblyQualifiedName, out string? objectType) && objectType != null)
            {
                return objectType;
            }

            objectType = CompiledRegEx.TypeTagsRegex().Replace(assemblyQualifiedName, string.Empty);
            objectType = CompiledRegEx.TypeCleanupRegex().Replace(objectType, ", ").Trim();

            CacheSet(assemblyQualifiedName, objectType);

            return objectType;
        }

        public static bool ImplementsGenericInterfaceWithArgument(Type type, Type genericInterface, Type argumentType)
        {
            return type.GetInterfaces().Any(interfaceType =>
                interfaceType.IsGenericType &&
                interfaceType.GetGenericTypeDefinition() == genericInterface &&
                interfaceType.GetGenericArguments().Any(arg => argumentType.IsAssignableFrom(arg)));
        }

        public static byte[] SerializeToByteArray(object obj)
        {
            if (obj == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }

        public static string RmSerializeFramePayloadToText<T>(IRmSerializationProvider? serializationProvider, T obj)
        {
            if (serializationProvider != null) //Using custom serialization.
            {
                return serializationProvider.SerializeToText(obj);
            }
            else //Using built-in default serialization.
            {
                return JsonSerializer.Serialize((object?)obj);
            }
        }

        public static T? RmDeserializeFramePayloadToObject<T>(IRmSerializationProvider? serializationProvider, string json)
        {
            if (serializationProvider != null) //Using custom serialization.
            {
                return serializationProvider.DeserializeToObject<T>(json);
            }
            else //Using built-in default serialization.
            {
                return JsonSerializer.Deserialize<T>(json);
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
            if (bytes == null)
                return Array.Empty<byte>();

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
