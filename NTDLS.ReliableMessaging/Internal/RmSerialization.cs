using NTDLS.ReliableMessaging.Internal.StreamFraming;
using ProtoBuf;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace NTDLS.ReliableMessaging.Internal
{
    internal class RmSerialization
    {
        private static readonly ConcurrentDictionary<string, Func<IRmSerializationProvider?, string, IRmPayload>> _deserializationCache = new();

        public static byte[] SerializeToByteArray(object obj)
        {
            if (obj == null) return Array.Empty<byte>();
            using var stream = new MemoryStream();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }

        public static T DeserializeToObject<T>(byte[] arrBytes)
        {
            using var stream = new MemoryStream();
            stream.Write(arrBytes, 0, arrBytes.Length);
            stream.Seek(0, SeekOrigin.Begin);
            return Serializer.Deserialize<T>(stream);
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

        /// <summary>
        /// Deserializes a payload to an object. This is called via reflection via Framing.ExtractFramePayload.
        /// </summary>
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

        /// <summary>
        /// Uses the "EnclosedPayloadType" to determine the type of the payload and deserialize the json to that type.
        /// </summary>
        public static IRmPayload ExtractFramePayload(IRmSerializationProvider? serializationProvider, RmFrameBody frame)
        {
            var deserializeMethod = _deserializationCache.GetOrAdd(frame.ObjectType, typeName =>
            {
                var genericType = Type.GetType(typeName)
                    ?? throw new Exception($"Unknown extraction payload type [{typeName}].");

                var methodInfo = typeof(RmSerialization).GetMethod(nameof(RmDeserializeFramePayloadToObject),
                    new[] { typeof(IRmSerializationProvider), typeof(string) })
                    ?? throw new Exception("Could not resolve RmDeserializeFramePayloadToObject().");

                var genericMethod = methodInfo.MakeGenericMethod(genericType);

                return (Func<IRmSerializationProvider?, string, IRmPayload>)
                    Delegate.CreateDelegate(typeof(Func<IRmSerializationProvider?, string, IRmPayload>), genericMethod);
            });

            return deserializeMethod(serializationProvider, Encoding.UTF8.GetString(frame.Bytes))
                ?? throw new Exception("Extraction payload cannot be null.");
        }
    }
}
