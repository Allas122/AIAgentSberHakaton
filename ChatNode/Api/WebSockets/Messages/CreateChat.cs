using System.Text.Json.Serialization;
using Domain.ValueTypes;

namespace ChatNode.Api.WebSockets.Messages;

public record CreateChat(
    string title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ChatKind>))]
    ChatKind kind = ChatKind.Grant);
