using ProtoBuf;
using System.Text;

namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    /// <summary>
    /// Comprises the body of the frame. Contains the payload and all information needed to deserialize it.
    /// </summary>
    [Serializable]
    [ProtoContract]
    internal class FrameBody
    {
        /// <summary>
        /// The unique ID of the frame body. This is also used to pair query replies with waiting queries.
        /// </summary>
        [ProtoMember(1)]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The full assembly qualified name of the type of the payload.
        /// </summary>
        [ProtoMember(2)]
        public string ObjectType { get; set; } = string.Empty;

        /// <summary>
        /// The full assembly qualified name of the type expected if this is a query frame.
        /// </summary>
        [ProtoMember(3)]
        public string? ExpectedReplyType { get; set; }

        /// <summary>
        /// Sometimes we just need to send a byte array without all the overhead of json, that's when we use BytesPayload.
        /// </summary>
        [ProtoMember(4)]
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Instantiates a frame payload with a serialized payload.
        /// </summary>
        public FrameBody(IRmSerializationProvider? serializationProvider, IRmPayload framePayload)
        {
            var assemblyQualifiedName = framePayload.GetType()?.AssemblyQualifiedName ?? string.Empty;
            var parts = assemblyQualifiedName.Split(','); //We only want the first two parts, not the version and such.
            ObjectType = parts.Length > 1 ? $"{parts[0]},{parts[1].Trim()}" : assemblyQualifiedName;

            if (serializationProvider == null) //Using default serialization?
            {
                Bytes = Encoding.UTF8.GetBytes(Utility.RmSerializeFramePayloadToText(framePayload));
            }
            else //Using custom serialization?
            {
                Bytes = Encoding.UTF8.GetBytes(serializationProvider.SerializeToText(framePayload));
            }
        }

        /// <summary>
        /// Instantiates a frame payload with a serialized payload and expected reply type (for queries).
        /// </summary>
        public FrameBody(IRmSerializationProvider? serializationProvider, IRmPayload framePayload, Type expectedReplyType)
        {
            var ertAssemblyQualifiedName = expectedReplyType?.AssemblyQualifiedName ?? string.Empty;
            var ertParts = ertAssemblyQualifiedName.Split(','); //We only want the first two parts, not the version and such.
            ExpectedReplyType = ertParts.Length > 1 ? $"{ertParts[0]},{ertParts[1].Trim()}" : ertAssemblyQualifiedName;

            var otAssemblyQualifiedName = framePayload.GetType()?.AssemblyQualifiedName ?? string.Empty;
            var otParts = otAssemblyQualifiedName.Split(','); //We only want the first two parts, not the version and such.
            ObjectType = otParts.Length > 1 ? $"{otParts[0]},{otParts[1].Trim()}" : otAssemblyQualifiedName;

            if (serializationProvider == null) //Using custom serialization?
            {
                Bytes = Encoding.UTF8.GetBytes(Utility.RmSerializeFramePayloadToText(framePayload));
            }
            else //Using default serialization?
            {
                Bytes = Encoding.UTF8.GetBytes(serializationProvider.SerializeToText(framePayload));
            }
        }

        /// <summary>
        /// Instantiates a frame payload using a raw byte array.
        /// </summary>
        /// <param name="bytesPayload"></param>
        public FrameBody(byte[] bytesPayload)
        {
            ObjectType = "byte[]";
            Bytes = bytesPayload;
        }

        /// <summary>
        /// Instantiates a frame payload.
        /// </summary>
        public FrameBody()
        {
        }
    }
}
