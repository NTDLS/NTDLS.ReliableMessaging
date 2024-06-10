using ProtoBuf;
using System.Text;

namespace NTDLS.ReliableMessaging.Internal.StreamFraming
{
    /// <summary>
    /// Comprises the bosy of the frame. Contains the payload and all information needed to deserialize it.
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
        /// Sometimes we just need to send a byte array without all the overhead of json, thats when we use BytesPayload.
        /// </summary>
        [ProtoMember(4)]
        public byte[] Bytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Instanciates a frame payload with a serialized payload.
        /// </summary>
        public FrameBody(IRmSerializationProvider? serializationProvider, IRmPayload framePayload)
        {
            ObjectType = framePayload.GetType()?.AssemblyQualifiedName ?? string.Empty;

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
        /// Instanciates a frame payload with a serialized payload and expected reply type (for queries).
        /// </summary>
        /// <param name="framePayload"></param>
        /// <param name="expectedReplyType"></param>
        public FrameBody(IRmPayload framePayload, Type expectedReplyType)
        {
            ExpectedReplyType = expectedReplyType?.AssemblyQualifiedName ?? string.Empty;
            ObjectType = framePayload.GetType()?.AssemblyQualifiedName ?? string.Empty;
            Bytes = Encoding.UTF8.GetBytes(Utility.RmSerializeFramePayloadToText(framePayload));
        }

        /// <summary>
        /// Instanciates a frame payload using a raw byte array.
        /// </summary>
        /// <param name="bytesPayload"></param>
        public FrameBody(byte[] bytesPayload)
        {
            ObjectType = "byte[]";
            Bytes = bytesPayload;
        }

        /// <summary>
        /// Instanciates a frame payload.
        /// </summary>
        public FrameBody()
        {
        }
    }
}
