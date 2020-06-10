namespace FootballData
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Diagnostics;


    // __ Competition _________________________________________________________

    public partial class Competition
    {
        [JsonProperty("_links")]
        public Links Links { get; set; }

        [JsonProperty("caption")]
        public string Caption { get; set; }

        [JsonProperty("currentMatchday")]
        public long CurrentMatchday { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("lastUpdated")]
        public DateTimeOffset LastUpdated { get; set; }

        [JsonProperty("league")]
        public string League { get; set; }

        [JsonProperty("numberOfGames")]
        public int NumberOfGames { get; set; }

        [JsonProperty("numberOfMatchdays")]
        public int NumberOfMatchdays { get; set; }

        [JsonProperty("numberOfTeams")]
        public int NumberOfTeams { get; set; }

        [JsonProperty("year")]
        public string Year { get; set; }

        public IList<Team> Teams { get; set; }
        public Dictionary<string, Team> TeamsIdx { get; set; } = new Dictionary<string, Team>();

        public IList<Fixture> Fixtures { get; set; }
    }

    public partial class Links
    {
        [JsonProperty("players")]
        public Link Players { get; set; }

        [JsonProperty("fixtures")]
        public Link Fixtures { get; set; }

        [JsonProperty("leagueTable")]
        public Link LeagueTable { get; set; }

        [JsonProperty("self")]
        public Link Self { get; set; }

        [JsonProperty("teams")]
        public Link Teams { get; set; }

        [JsonProperty("team")]
        public Link Team { get; set; }
    }

    [DebuggerDisplay("Href: {Href}")]
    public partial class Link
    {
        [JsonProperty("href")]
        public string Href { get; set; }
    }



    // __ Team ________________________________________________________________

    public class CompetitionTeams
    {
        public IList<Link> Links { get; set; }
        public IList<Team> Teams { get; set; }
    }

    public partial class Team
    {
        [JsonProperty("_links")]
        public Links Links { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("crestUrl")]
        public string CrestUrl { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("shortName")]
        public string ShortName { get; set; }

        [JsonProperty("squadMarketValue")]
        public object SquadMarketValue { get; set; }

        public IList<Player> Players { get; set; }
    }


    // __ Fixtures ____________________________________________________________
    

    public class CompetitionFixtures
    {
        public IList<Fixture> Fixtures { get; set; }
    }

    public partial class Fixture
    {
        [JsonProperty("_links")]
        public FixtureLinks Links { get; set; }

        [JsonProperty("awayTeamName")]
        public string AwayTeamName { get; set; }

        [JsonProperty("date")]
        public DateTimeOffset? Date { get; set; }

        [JsonProperty("homeTeamName")]
        public string HomeTeamName { get; set; }

        [JsonProperty("matchday")]
        public int Matchday { get; set; }

        [JsonProperty("odds")]
        public Odds Odds { get; set; }

        [JsonProperty("result")]
        public Result Result { get; set; }

        [JsonProperty("status")]
        public Status Status { get; set; }
    }

    public partial class FixtureLinks
    {
        [JsonProperty("awayTeam")]
        public Link AwayTeam { get; set; }

        [JsonProperty("competition")]
        public Link Competition { get; set; }

        [JsonProperty("homeTeam")]
        public Link HomeTeam { get; set; }

        [JsonProperty("self")]
        public Link Self { get; set; }
    }

    public partial class Odds
    {
        [JsonProperty("awayWin")]
        public double AwayWin { get; set; }

        [JsonProperty("draw")]
        public double Draw { get; set; }

        [JsonProperty("homeWin")]
        public double HomeWin { get; set; }
    }

    public partial class Result
    {
        [JsonProperty("goalsAwayTeam")]
        public long? GoalsAwayTeam { get; set; }

        [JsonProperty("goalsHomeTeam")]
        public long? GoalsHomeTeam { get; set; }

        [JsonProperty("halfTime", NullValueHandling = NullValueHandling.Ignore)]
        public HalfTime HalfTime { get; set; }
    }

    public partial class HalfTime
    {
        [JsonProperty("goalsAwayTeam")]
        public long GoalsAwayTeam { get; set; }

        [JsonProperty("goalsHomeTeam")]
        public long GoalsHomeTeam { get; set; }
    }

    public enum Status { Finished, Scheduled, Timed, In_Play };

    internal class Converter: JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Status) || t == typeof(Status?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            if (t == typeof(Status))
                return StatusExtensions.ReadJson(reader, serializer);
            if (t == typeof(Status?))
            {
                if (reader.TokenType == JsonToken.Null) return null;
                return StatusExtensions.ReadJson(reader, serializer);
            }
            throw new Exception("Unknown type");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var t = value.GetType();
            if (t == typeof(Status))
            {
                ((Status)value).WriteJson(writer, serializer);
                return;
            }
            throw new Exception("Unknown type");
        }

        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters = { 
                new Converter(),
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };

        
    }

    static class StatusExtensions
    {
        public static Status? ValueForString(string str)
        {
            switch (str)
            {
                case "FINISHED": return Status.Finished;
                case "SCHEDULED": return Status.Scheduled;
                case "TIMED": return Status.Timed;
                default: return null;
            }
        }

        public static Status ReadJson(JsonReader reader, JsonSerializer serializer)
        {
            var str = serializer.Deserialize<string>(reader);
            var maybeValue = ValueForString(str);
            if (maybeValue.HasValue) return maybeValue.Value;
            throw new Exception("Unknown enum case " + str);
        }

        public static void WriteJson(this Status value, JsonWriter writer, JsonSerializer serializer)
        {
            switch (value)
            {
                case Status.Finished: serializer.Serialize(writer, "FINISHED"); break;
                case Status.Scheduled: serializer.Serialize(writer, "SCHEDULED"); break;
                case Status.Timed: serializer.Serialize(writer, "TIMED"); break;
            }
        }
    }


    // Players 

    public partial class TeamPlayers
    {
        [JsonProperty("_links")]
        public Links Links { get; set; }

        [JsonProperty("count")]
        public long Count { get; set; }

        [JsonProperty("players")]
        public List<Player> Players { get; set; }
    }

    public partial class Player
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("position")]
        public string Position { get; set; }

        [JsonProperty("jerseyNumber")]
        public int? JerseyNumber { get; set; }

        [JsonProperty("dateOfBirth")]
        public DateTimeOffset? DateOfBirth { get; set; }

        [JsonProperty("nationality")]
        public string Nationality { get; set; }

        [JsonProperty("contractUntil")]
        public DateTimeOffset? ContractUntil { get; set; }

        public long DbId { get; set; }
    }
}
