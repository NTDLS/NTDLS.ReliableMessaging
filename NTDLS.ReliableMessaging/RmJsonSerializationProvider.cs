using System.Text.Json;

namespace NTDLS.ReliableMessaging
{
    /// <summary>
    /// Default serialization provider that uses System.Text.Json.
    /// </summary>
    public class RmJsonSerializationProvider
        : IRmSerializationProvider
    {
        /// <summary>
        /// Deserializes the specified JSON string into an object of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="json">The JSON string to deserialize. Cannot be <see langword="null"/> or empty.</param>
        /// <returns>An object of type <typeparamref name="T"/> deserialized from the JSON string, or <see langword="null"/>  if
        /// the JSON string is invalid or represents a <see langword="null"/> value.</returns>
        public T? DeserializeToObject<T>(string json)
           => JsonSerializer.Deserialize<T>(json);

        /// <summary>
        /// Serializes the specified object to a JSON-formatted string.
        /// </summary>
        /// <remarks>This method uses the <see cref="System.Text.Json.JsonSerializer"/> for serialization.
        /// Ensure that the type <typeparamref name="T"/> is supported by the serializer.</remarks>
        /// <typeparam name="T">The type of the object to serialize.</typeparam>
        /// <param name="obj">The object to serialize. Cannot be null.</param>
        /// <returns>A JSON-formatted string representation of the object.</returns>
        public string SerializeToText<T>(T obj)
            => JsonSerializer.Serialize((object?)obj);
    }
}
