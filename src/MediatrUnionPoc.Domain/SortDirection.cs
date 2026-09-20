using System.Text.Json.Serialization;

namespace MediatrUnionPoc.Domain;

/// <summary>Which way a sort key orders its rows. Serialized as its lower-case name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    /// <summary>Smallest / earliest / alphabetically first row first.</summary>
    [JsonStringEnumMemberName("ascending")]
    Ascending,

    /// <summary>Largest / latest / alphabetically last row first.</summary>
    [JsonStringEnumMemberName("descending")]
    Descending,
}
