using System.Text.Json.Serialization;

namespace Trarizon.Bangumi.CommandPalette.Utilities.Serialization;
[JsonSerializable(typeof(int))]
internal sealed partial class JsonSerializationContext : JsonSerializerContext;
