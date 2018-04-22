﻿using PokeSharp.Models.Common;
using Newtonsoft.Json;

namespace PokeSharp.Models
{
    public class EncounterConditions
    {
        [JsonProperty(PropertyName = "id")]
        public int? ID { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "names")]
        public ListName[] Names { get; set; }
        [JsonProperty(PropertyName = "values")]
        public NamedAPIResource[] Values { get; set; }
    }
}