using ProtoBuf;
using System.Text;

namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    /// <summary>
    /// Comprises the body of the frame. Contains the payload and all information needed to deserialize it.
    /// </summary>
    [Serializable]
    [ProtoContract]
    internal class RmFrameBody
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
        public RmFrameBody(IRmSerializationProvider serializationProvider, IRmPayload framePayload)
        {
            ObjectType = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(framePayload);
            Bytes = Encoding.UTF8.GetBytes(RmSerialization.RmSerializeFramePayloadToText(serializationProvider, framePayload));
        }

        /// <summary>
        /// Instantiates a frame payload with a serialized payload and expected reply type (for queries).
        /// </summary>
        public RmFrameBody(IRmSerializationProvider serializationProvider, IRmPayload framePayload, Type expectedReplyType)
        {
            ExpectedReplyType = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(expectedReplyType);
            ObjectType = RmReflection.GetAssemblyQualifiedTypeNameWithClosedGenerics(framePayload);
            Bytes = Encoding.UTF8.GetBytes(RmSerialization.RmSerializeFramePayloadToText(serializationProvider, framePayload));
        }

        /// <summary>
        /// Instantiates a frame payload.
        /// </summary>
        public RmFrameBody()
        {
        }
    }
}
