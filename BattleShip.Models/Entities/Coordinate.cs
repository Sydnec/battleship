using System.Text.Json.Serialization;
using BattleShip.Models.Converters;

namespace BattleShip.Models.Entities;

[JsonConverter(typeof(CoordinateDictionaryKeyConverter))]
public record Coordinate(int Row, int Column);
