using System.Collections.Generic;
using System.Text.Json.Serialization;
using UnityEngine;

namespace Api.Models
{
    [SerializeField]
    public class GameSession
    {
        [JsonPropertyName("game_id")]
        public string GameId { get; set; }
        [JsonPropertyName("game_drop")]
        public IEnumerable<GameDrop> GameDrop { get; set; }
        [JsonPropertyName("is_new")]
        public bool IsNew { get; set; }
    }

    [SerializeField]
    public class GameDrop
    {
        [JsonPropertyName("boss")]
        public int Boss { get; set; }
        [JsonPropertyName("token")]
        public int Token { get; set; }
    }
}