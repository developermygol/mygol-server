using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using FootballData;
using System.IO;
using System.Runtime.InteropServices;
using System.Data;
using System.Linq;
using Dapper;
using Dapper.Contrib.Extensions;
using webapi.Models.Db;
using contracts;
using webapi.Controllers;

namespace webapi.Importers 
{
    public class FootballDataImporter
    {
        // Create the importer with the api key

        // Import a competition: idTournament

        // - Get the teams

        // - Get each team players
        // - Get the tournament fixtures
        //   - Create matches
        //   - Create match start / end / goals events for the matches as needed. 
        // It seems there is no data for scorer player, so will have to assign it randomly to any player in the team. 

        public const string BaseUrl = "https://api.football-data.org";

        public FootballDataImporter(string apiToken)
        {
            mApiToken = apiToken;
        }

        public async Task<Competition> ImportTournament(long idTournament)
        {
            Downloader.SetHeader("X-Auth-Token", mApiToken);

            var competition = await Downloader.Get<Competition>(BaseUrl + $"/v1/competitions/{idTournament}");

            if (competition.Links == null) throw new Exception("Tournament has no links to teams, fixtures or players. Can't continue.");

            var teams = await Downloader.Get<CompetitionTeams>(competition.Links.Teams.Href);
            competition.Teams = teams.Teams;
            foreach (var team in teams.Teams) competition.TeamsIdx[team.Name] = team;

            var fixtures = await Downloader.Get<CompetitionFixtures>(competition.Links.Fixtures.Href);
            competition.Fixtures = fixtures.Fixtures;
                
            foreach (var team in teams.Teams)
            {
                var teamPlayers = await Downloader.Get<TeamPlayers>(team.Links.Players.Href);
                team.Players = teamPlayers.Players;
            }

            return competition;
        }


        private string mApiToken;
    }

    public class FootballDataExporter
    {
        public async Task<Tournament> Export(IDbConnection c, IDbTransaction t, IStorageProvider storage, Competition competition)
        {
            var coords = await CreateTournamentAndStages(c, t, competition);

            await CreateTeams(c, t, storage, competition, coords);

            await CreateMatches(c, t, competition, coords);

            return coords.Tournament;
        }

        private async Task CreateMatches(IDbConnection c, IDbTransaction t, Competition competition, GroupCoords coords)
        {
            coords.Tournament.Days = new PlayDay[competition.NumberOfMatchdays];

            foreach (var fixture in competition.Fixtures)
            {
                await CreateMatch(c, t, fixture, competition, coords);
            }
        }

        private async Task CreateMatch(IDbConnection c, IDbTransaction t, Fixture fixture, Competition competition, GroupCoords coords)
        {
            var homeTeam = GetTeamByName(coords.Tournament.Teams, fixture.HomeTeamName);
            var visitorTeam = GetTeamByName(coords.Tournament.Teams, fixture.AwayTeamName);
            if (homeTeam == null || visitorTeam == null) throw new Exception("Invalid team name");

            var dbDay = await GetDay(c, t, coords, fixture.Matchday, fixture.Date);

            var dbMatch = new Match
            {
                IdTournament = coords.IdTournament,
                IdStage = coords.IdStage,
                IdGroup = coords.IdGroup,
                IdDay = dbDay.Id,
                IdHomeTeam = homeTeam.Id,
                IdVisitorTeam = visitorTeam.Id,
                HomeTeam = homeTeam,
                VisitorTeam = visitorTeam,
                Status = (int)MatchStatus.Created
            };

            if (fixture.Date != null) dbMatch.StartTime = fixture.Date.Value.DateTime;

            dbMatch.Id = await c.InsertAsync(dbMatch, t);
            dbMatch.Day = dbDay;
            dbDay.Matches.Add(dbMatch);

            await AddMatchEvents(c, t, fixture, dbMatch);
            await AddMatchPlayers(c, t, fixture, dbMatch);
        }

        private static async Task AddMatchPlayers(IDbConnection c, IDbTransaction t, Fixture fixture, Match dbMatch)
        {
            foreach (var p in dbMatch.HomeTeam.Players)
            {
                await CreateMatchPlayer(c, t, dbMatch, dbMatch.HomeTeam, p);
            }

            foreach (var p in dbMatch.VisitorTeam.Players)
            {
                await CreateMatchPlayer(c, t, dbMatch, dbMatch.VisitorTeam, p);
            }
        }

        private static async Task CreateMatchPlayer(IDbConnection c, IDbTransaction t, Match dbMatch, Models.Db.Team team, Models.Db.Player player)
        {
            // Check apparelnumber
            var dbMatchPlayer = new MatchPlayer
            {
                IdMatch = dbMatch.Id,
                IdPlayer = player.Id,
                IdTeam = team.Id,
                IdUser = player.IdUser,
                ApparelNumber = player.TeamData.ApparelNumber,
                IdDay = dbMatch.IdDay,
                Player = player,
                Status = 1
            };

            await c.InsertAsync(dbMatchPlayer, t);
        }

        private static async Task AddMatchEvents(IDbConnection c, IDbTransaction t, Fixture fixture, Match dbMatch)
        {
            var rnd = new Random();
            var events = new List<MatchEvent>();
            dbMatch.Events = events;

            if (fixture.Status != Status.Finished) return;

            // Simulate match. Add home goals in the first half. Visitor goals on the second half.
            await CreateEvent(c, t, dbMatch, events, null, null, MatchEventType.MatchStarted, 0);
            
            for (int i = 0; i < fixture.Result.GoalsHomeTeam; ++i)
            {
                await CreateEvent(c, t, dbMatch, events, dbMatch.HomeTeam, GetRandomPlayer(dbMatch.HomeTeam.Players), MatchEventType.Point, 0);
            }

            await CreateEvent(c, t, dbMatch, events, null, null, MatchEventType.FirstHalfFinish, 45);
            await CreateEvent(c, t, dbMatch, events, null, null, MatchEventType.SecondHalfStarted, 45);

            for (int i = 0; i < fixture.Result.GoalsAwayTeam; ++i)
            {
                await CreateEvent(c, t, dbMatch, events, dbMatch.VisitorTeam, GetRandomPlayer(dbMatch.VisitorTeam.Players), MatchEventType.Point, 45);
            }

            await CreateEvent(c, t, dbMatch, events, null, null, MatchEventType.MatchEnded, 92);
        }

        private static async Task CreateEvent(IDbConnection c, IDbTransaction t, Match dbMatch, List<MatchEvent> events, Models.Db.Team team, Models.Db.Player player, MatchEventType type, int minuteOffset)
        {
            var ev = new MatchEvent
            {
                IdDay = dbMatch.IdDay,
                IdMatch = dbMatch.Id,
                IdTeam = (team != null ? team.Id : 0),
                IdPlayer = (player != null ? player.Id : 0),
                MatchMinute = new Random().Next(0 + minuteOffset, 45 + minuteOffset),
                Type = (int)type,
            };

            ev.Id = await c.InsertAsync(ev, t);
            events.Add(ev);
        }

        private static Models.Db.Player GetRandomPlayer(IEnumerable<Models.Db.Player> players)
        {
            if (players == null || players.Count() == 0) return null;

            var idx = new Random().Next(players.Count());
            return players.ElementAt(idx);
        }

        private static async Task<PlayDay> GetDay(IDbConnection c, IDbTransaction t, GroupCoords coords, int dayIdx, DateTimeOffset? dayDate)
        {
            var days = coords.Tournament.Days as PlayDay[];
            var dbDay = days.ElementAt(dayIdx - 1);

            if (dbDay == null)
            {
                dbDay = new PlayDay
                {
                    IdTournament = coords.IdTournament,
                    IdStage = coords.IdStage, 
                    IdGroup = coords.IdGroup,
                    Name = Localization.Get("Jornada {0}", null, dayIdx),
                    SequenceOrder = dayIdx,
                    Matches = new List<Match>()
                };

                if (dayDate != null)
                {
                    dbDay.DatesList.Add(dayDate.Value.DateTime);
                    dbDay.SetDatesFromDatesList();
                }

                days[dayIdx - 1] = dbDay;

                dbDay.Id = await c.InsertAsync(dbDay, t);
            }

            return dbDay;
        }

        private async Task<GroupCoords> CreateTournamentAndStages(IDbConnection c, IDbTransaction t, Competition competition)
        {
            var result = new GroupCoords();

            var tournament = new Tournament
            {
                Name = competition.Caption,
                Status = (int)TournamentStatus.Playing,
                Type = 1
            };

            tournament.Id = await c.InsertAsync(tournament, t);
            result.IdTournament = tournament.Id;
            result.Tournament = tournament;

            var stage = new TournamentStage
            {
                Name = "Liga",
                IdTournament = result.IdTournament,
                SequenceOrder = 1,
                Type = (int)CalendarType.League
            };

            stage.Id = await c.InsertAsync(stage, t);
            result.IdStage = stage.Id;
            result.Stage = stage;

            var group = new StageGroup
            {
                Name = "Grupo único",
                IdTournament = result.IdTournament,
                IdStage = result.IdStage,
                SequenceOrder = 1,
                NumTeams = competition.NumberOfTeams,
                NumRounds = 1
            };

            group.Id = await c.InsertAsync(group, t);
            result.IdGroup = group.Id;
            result.Group = group;

            return result;
        }

        private async Task CreateTeams(IDbConnection c, IDbTransaction t, IStorageProvider storage, Competition competition, GroupCoords coords)
        {
            var teams = new List<Models.Db.Team>();

            var sequenceOrder = 1;

            foreach (var team in competition.Teams)
            {
                Models.Db.Team dbTeam = await CreateTeam(c, t, storage, coords, team, sequenceOrder++);
                teams.Add(dbTeam);

                await CreateTeamPlayers(c, t, team, dbTeam, coords.IdTournament);
            }

            coords.Tournament.Teams = teams;
        }

        private static async Task<Models.Db.Team> CreateTeam(IDbConnection c, IDbTransaction t, IStorageProvider storage, GroupCoords coords, FootballData.Team team, int sequenceOrder)
        {
            var logoImgUrl = ImportImage(c, t, storage, team.CrestUrl);

            var dbTeam = new Models.Db.Team
            {
                Name = team.Name,
                KeyName = String.IsNullOrWhiteSpace(team.Code) ? team.Name.Substring(0, 3) : team.Code,
                LogoImgUrl = logoImgUrl,
                Status = (int)TeamStatus.Inscribed
            };

            dbTeam.Id = await c.InsertAsync(dbTeam, t);

            await c.InsertAsync(new TournamentTeam
            {
                IdTeam = dbTeam.Id,
                IdTournament = coords.IdTournament
            }, t);

            var teamGroup = new TeamGroup
            {
                IdTeam = dbTeam.Id,
                IdGroup = coords.IdGroup,
                IdStage = coords.IdStage,
                IdTournament = coords.IdTournament,
                SequenceOrder = sequenceOrder
            };

            await c.InsertAsync(teamGroup, t);

            return dbTeam;
        }

        private async Task CreateTeamPlayers(IDbConnection c, IDbTransaction t, FootballData.Team team, Models.Db.Team dbTeam, long idTournament)
        {
            var players = new List<Models.Db.Player>();

            if (team.Players != null)
            {
                foreach (var p in team.Players)
                {
                    var dbPlayer = await CreatePlayer(c, t, p, dbTeam, idTournament);
                    players.Add(dbPlayer);
                }
            }
            else
            {
                Console.WriteLine("Warning: team without players: " + team.Name);
            }

            dbTeam.Players = players;
        }

        private async Task<Models.Db.Player> CreatePlayer(IDbConnection c, IDbTransaction t, FootballData.Player player, Models.Db.Team dbTeam, long idTournament)
        {
            var (name, surname) = SplitPlayerName(player.Name);

            var dbUser = new Models.Db.User
            {
                Name = player.Name,
                Level = (int)UserLevel.Player,
                Salt = "", 
                Password = "",
                Email = "notset@local.host",
                Mobile = "123456789"
            };

            dbUser.Id = await c.InsertAsync(dbUser, t);

            var (fieldPosition, fieldSide) = GetPlayerPosition(player.Position);

            var dbPlayer = new Models.Db.Player
            {
                Name = name,
                Surname = surname,
                IdUser = dbUser.Id,
                BirthDate = player.DateOfBirth.GetValueOrDefault().DateTime,
                Country = player.Nationality,
                UserData = dbUser
            };

            dbPlayer.Id = await c.InsertAsync(dbPlayer, t);
            player.DbId = dbPlayer.Id;

            var dbTeamPlayer = new TeamPlayer
            {
                IdPlayer = dbPlayer.Id, 
                IdTeam = dbTeam.Id,
                ApparelNumber = player.JerseyNumber.GetValueOrDefault(),
                FieldPosition = fieldPosition, 
                FieldSide = fieldSide, 
                IsTeamAdmin = false,
                Status = 256
            };

            dbPlayer.TeamData = dbTeamPlayer;

            await c.InsertAsync(dbTeamPlayer, t);

            return dbPlayer;
        }

        private (int, int) GetPlayerPosition(string position)
        {
            var parts = position.Split('-', ' ');

            var fieldPosition = "";
            var fieldSide = "";

            if (parts.Length == 2)
            {
                fieldSide = parts[0];
                fieldPosition = parts[1];
            }
            else if (parts.Length == 1)
            {
                fieldPosition = parts[0];
            }
            else
                return (0, 0);

            var fsRes = 0;

            switch (fieldSide.ToLower().Trim())
            {
                case "centre": case "central": case "attacking": fsRes = 2; break;
                case "right": fsRes = 3; break;
                case "left": fsRes = 1; break;
            }

            var fpRes = 0;

            switch (fieldPosition.ToLower().Trim())
            {
                case "wing": fpRes = 3; break;
                case "back": fpRes = 2; break;
                case "midfield": fpRes = 3; break;
                case "keeper": fpRes = 1; break;
                case "forward": fpRes = 4; break;
            }

            return (fsRes, fpRes);
        }

        private (string, string) SplitPlayerName(string name)
        {
            var parts = name.Split('-', ' ');

            if (parts.Length == 1) return (parts[0], "      ");
            if (parts.Length == 2) return (parts[0], parts[1]);
            if (parts.Length == 3) return (parts[0] + " " + parts[1], parts[2]);

            return (parts[0] + " " + parts[1], parts[2] + " " + parts[3]);
        }

        private Models.Db.Team GetTeamByName(IEnumerable<Models.Db.Team> teams, string name)
        {
            foreach (var t in teams) if (t.Name == name) return t;

            return null;
        }

        private static string ImportImage(IDbConnection c, IDbTransaction t, IStorageProvider storage, string url)
        {
            if (url == null) return null;

            if (url.ToLower().Contains(".svg"))
            {
                Console.WriteLine("Warning: SVG icon, ignored. " + url);
                return null;
            }

            return url;

            // if (String.IsNullOrEmpty(url)) return null;

            // Download the image
            // Convert it to png
            // Resize to the correct size
            // Create the upload record

            // return url of the uploaded record
        }
    }


    public class Downloader
    {
        public static void SetHeader(string key, string value)
        {
            mHeaders[key] = value;
        }

        public static async Task<T> Get<T>(string url, params object[] args) where T: class
        {
            var fileName = GetFileName(url);

            using (var c = new HttpClient())
            {
                string data = null;

                if (File.Exists(fileName))
                {
                    data = File.ReadAllText(fileName);
                }
                else
                {
                    foreach (var h in mHeaders) c.DefaultRequestHeaders.Add(h.Key, h.Value);

                    var conn = await c.GetAsync(string.Format(url, args));

                    using (var content = conn.Content)
                    {
                        data = await content.ReadAsStringAsync();

                        Directory.CreateDirectory(Path.GetDirectoryName(fileName));
                        using (var w = File.CreateText(fileName)) await w.WriteAsync(data);
                    }
                }

                return JsonConvert.DeserializeObject<T>(data);
            }
        }

        private static string GetFileName(string url)
        {
            var hashedName = url
                .Replace("https://", "")
                .Replace("http://", "")
                .Replace('/', '_')
                + ".txt";

            var appDataDir = GetAppDataDir();
            var dir = Path.Combine(appDataDir, "football-data-importer");
            return Path.Combine(dir, hashedName);
        }

        private static string GetAppDataDir()
        {
            return Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LocalAppData" : "HOME");
        }

        private static Dictionary<string, string> mHeaders = new Dictionary<string, string>();
    }
}