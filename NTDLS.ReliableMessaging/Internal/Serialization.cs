using ProtoBuf;
using System.Text.Json;

namespace NTDLS.ReliableMessaging.Internal
{
    internal class Serialization
    {
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
    }
}
