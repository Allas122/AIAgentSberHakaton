using System.Text.Json.Serialization;
using Domain.ValueTypes;

namespace ChatNode.Api.Rest.Messages.Chat;

public record CreateChatRequest(
    string title,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ChatKind>))]
    ChatKind kind = ChatKind.Grant);
