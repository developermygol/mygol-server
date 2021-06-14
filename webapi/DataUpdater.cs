using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using webapi.Models.Db;

namespace webapi
{
    public class DataUpdater : IDisposable
    {
        public DataUpdater(PostgresqlConfig config)
        {
            mDbOptions = config;
            mLogger = ConsoleLogger;

            Init();
        }

        protected virtual void Init()
        {
            AddUpdater(1, Update1, Undo1);
            AddUpdater(2, Update2, Undo2);
            AddUpdater(3, Update3, Undo3);
            AddUpdater(4, Update4, Undo4);
            AddUpdater(5, Update5, Undo5);
            AddUpdater(6, Update6, Undo6);
            AddUpdater(7, Update7, Undo7);
            AddUpdater(8, Update8, Undo8);
            AddUpdater(9, Update9, Undo9);
            AddUpdater(10, Update10, Undo10);
            AddUpdater(11, Update11, Undo11);
            AddUpdater(12, Update12, Undo12);
            AddUpdater(13, Update13, Undo13);
            AddUpdater(14, Update14, Undo14);
            AddUpdater(15, Update15, Undo15);
            AddUpdater(16, Update16, Undo16);
            AddUpdater(17, Update17, Undo17);
            AddUpdater(18, Update18, Undo18);
            AddUpdater(19, Update19, Undo19);
            AddUpdater(20, Update20, Undo20);
            AddUpdater(21, Update21, Undo21);
            AddUpdater(22, Update22, Undo22);
            AddUpdater(23, Update23, Undo23);
            AddUpdater(24, Update24, Undo24);
            AddUpdater(25, Update25, Undo25);
            AddUpdater(26, Update26, Undo26);
            AddUpdater(27, Update27, Undo27);
            AddUpdater(28, Update28, Undo28);
            AddUpdater(29, Update29, Undo29);
            AddUpdater(30, Update30, Undo30);
            AddUpdater(31, Update31, Undo31);
            AddUpdater(32, Update32, Undo32); // 🔎 Not aquired functions
            AddUpdater(33, Update33, Undo33); 
            AddUpdater(34, Update34, Undo34); 
            AddUpdater(35, Update35, Undo35);
            AddUpdater(35, Update36, Undo36);
        }

        public void Dispose()
        {

        }

        public void SetLogger(Action<string> logger)
        {
            mLogger = logger;
        }

        public int GetCurrentVersion()
        {
            using (var conn = GetConn())
            {
                var r = conn.ExecuteScalar<int>("SELECT v FROM version");
                return r;
            }
        }

        public void Upgrade()
        {
            var version = GetCurrentVersion();
            if (version == 0) return;

            mLogger($"Current DB version: {version}");

            try
            {
                while (true)
                {
                    UpdateMethodDelegate func;
                    if (!mUpdaters.TryGetValue(version, out func)) break;

                    version = RunUpdate(func);
                }
            }
            catch (Exception ex)
            {
                mLogger("ERROR: " + ex.Message + ex.StackTrace);
            }

            mLogger($"Final version DB: {GetCurrentVersion()}");
        }

        public void Downgrade(int rollbackToVersion)
        {
            var version = GetCurrentVersion();
            if (version == 0) return;

            mLogger($"Current DB version: {version}");

            try
            {
                while (version > rollbackToVersion)
                {
                    UpdateMethodDelegate func;
                    if (!mUndoers.TryGetValue(version, out func)) break;

                    version = RunUpdate(func);
                }
            }
            catch (Exception ex)
            {
                mLogger("ERROR: " + ex.Message + ex.StackTrace);
            }

            mLogger($"Final version DB: {GetCurrentVersion()}");
        }

        protected void AddUpdater(int level, UpdateMethodDelegate upgrader, UpdateMethodDelegate downgrader)
        {
            mUpdaters.Add(level, upgrader);
            mUndoers.Add(level + 1, downgrader);
        }



        private int Update1(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"1 -> 2: add idTeam to users");

            conn.Execute("ALTER TABLE users ADD COLUMN idteam integer");

            return 2;
        }

        private int Undo1(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"2 -> 1: remove idTeam from users");

            conn.Execute("ALTER TABLE users DROP COLUMN IF EXISTS idteam");

            return 1;
        }



        private int Update2(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"2 -> 3: Adding uploads table");

            string sql = @"
                CREATE TABLE uploads (
	                id				SERIAL, 
	                type			INTEGER NOT NULL, 
	                idObject		INTEGER NOT NULL,
	                repositoryPath	TEXT
                );

                CREATE INDEX uploads_id ON uploads (id);
                CREATE INDEX uploads_object ON uploads (type, idobject);
            ";

            conn.Execute(sql);

            return 3;
        }

        private int Undo2(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"3 -> 2: Removing uploads table");

            conn.Execute("DROP TABLE uploads IF EXISTS");

            return 2;
        }



        private int Update3(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"3 -> 4: Adding player related tables + text blobs + notifications");

            conn.Execute(@"
                CREATE TABLE players (
                    id              SERIAL,
                    iduser          INTEGER not null,
                    name            TEXT,
                    surname         TEXT,
                    
                    birthDate       TIMESTAMP,    

                    largeImgUrl     TEXT,
                    signatureImgUrl TEXT,
                    motto           TEXT,
                    
                    height          REAL,
                    weight          REAL,
                    
                    facebookKey     TEXT,
                    twitterKey      TEXT,
                    instagramKey    TEXT
                );

                CREATE INDEX players_id ON players (id);


                CREATE TABLE teamplayers (
                    idTeam          INTEGER NOT NULL,
                    idPlayer        INTEGER NOT NULL,
                    idTournament    INTEGER NOT NULL,
                    status          INTEGER,
	                apparelNumber   INTEGER,
                    fieldPosition   INTEGER,
                    fieldSide       INTEGER
                );

                CREATE INDEX teamplayers_team ON teamplayers (idteam);
                CREATE INDEX teamplayers_player ON teamplayers (idplayer);
                CREATE INDEX teamplayers_tournament ON teamplayers (idtournament);


                CREATE TABLE textblobs (
                    id          SERIAL,
                    type        INTEGER NOT NULL, 
                    idObject    INTEGER NOT NULL,
                    data        TEXT
                );

                CREATE INDEX textblobs_id ON textblobs (id);
                CREATE INDEX textblobs_object ON textblobs (type, idobject);

                CREATE TABLE notifications (
                    id      SERIAL,
                    iduser  INTEGER NOT NULL, 
                    sender  TEXT,
                    type    INTEGER NOT NULL,
                    target  TEXT,
                    content TEXT
                );

                CREATE INDEX notifications_id ON notifications (id);

                ALTER TABLE users ADD COLUMN mobile TEXT;
            ");


            return 4;
        }

        private int Undo3(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"4 -> 3: Deleting player related tables + text blobs + notifications");

            conn.Execute(@"
                DROP TABLE IF EXISTS teamplayers;
                DROP TABLE IF EXISTS players;
                DROP TABLE IF EXISTS textblobs;
                DROP TABLE IF EXISTS notifications;
                ALTER TABLE users DROP COLUMN IF EXISTS mobile;
            ");

            return 3;
        }



        private int Update4(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"4 -> 5: add team colums: logoconfig, idtactic");

            conn.Execute(@"
                ALTER TABLE teams ADD COLUMN logoConfig text;
	            ALTER TABLE teams ADD COLUMN idtactic integer;
            ");

            return 5;
        }

        private int Undo4(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"5 -> 4: drop team colums: logoconfig, idtactic");

            conn.Execute(@"
                ALTER TABLE teams DROP COLUMN logoConfig;
	            ALTER TABLE teams DROP COLUMN idtactic;
            ");

            return 4;
        }


        private int Update5(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"5 -> 6: Add address to players, add secureuploads table, move isTeamAdmin to teamplayers");

            conn.Execute(@"
                ALTER TABLE players ADD COLUMN address1	TEXT;
                ALTER TABLE players ADD COLUMN address2	TEXT;
                ALTER TABLE players ADD COLUMN city	TEXT;
                ALTER TABLE players ADD COLUMN state TEXT;
                ALTER TABLE players ADD COLUMN cp TEXT;
                ALTER TABLE players ADD COLUMN country TEXT;
                ALTER TABLE players ADD COLUMN idCardNumber	TEXT;

                ALTER TABLE teamplayers ADD COLUMN isTeamAdmin BOOLEAN;
                ALTER TABLE users DROP COLUMN idTeam;

                CREATE TABLE secureuploads (
	                id				SERIAL, 
	                idUser			INTEGER NOT NULL, 
	                type			INTEGER NOT NULL, 
	                description		TEXT,
                    originalFileName TEXT,
	                idSecureUpload	INTEGER,
	                idUpload		INTEGER
                );

                CREATE INDEX secureuploads_id ON secureuploads (id);
                CREATE INDEX secureuploads_iduser ON secureuploads (idUser);
            ");

            return 6;
        }

        private int Undo5(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"6 -> 5: Drop secureuploads, remove player.address fields, move isTeamAdmin back to users");

            conn.Execute(@"
                ALTER TABLE players DROP COLUMN address1;
                ALTER TABLE players DROP COLUMN address2;
                ALTER TABLE players DROP COLUMN city;
                ALTER TABLE players DROP COLUMN state;
                ALTER TABLE players DROP COLUMN cp;
                ALTER TABLE players DROP COLUMN country;
                ALTER TABLE players DROP COLUMN idCardNumber;

                DROP TABLE secureuploads;

                ALTER TABLE teamplayers DROP COLUMN isTeamAdmin;
                ALTER TABLE users ADD COLUMN idteam INTEGER DEFAULT '-1'::integer;
            ");

            return 5;
        }


        private int Update6(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"6 -> 7: Add userevents and match related tables");

            conn.Execute(@"
                CREATE TABLE userevents (
	                id				SERIAL,
	                idUser			INTEGER NOT NULL,
	                type			INTEGER NOT NULL,
	                description		TEXT,
	                timestamp		TIMESTAMP NOT NULL,
	                idSecureUpload	INTEGER,
	                idUpload		INTEGER,
                    idCreator       INTEGER
                );

                CREATE INDEX userevents_id ON userevents (id);
                CREATE INDEX userevents_iduser ON userevents (idUser);


                CREATE TABLE matches (
	                id				SERIAL,
	                idTournament	INTEGER NOT NULL,
	                idHomeTeam		INTEGER NOT NULL,
	                idVisitorTeam	INTEGER NOT NULL,
	                idDay			INTEGER,
	                idField			INTEGER,
	                startTime		TIMESTAMP,
	                duration		INTEGER,
	                status			INTEGER,
	                homeScore		INTEGER, 
	                visitorScore	INTEGER
                );

                CREATE INDEX matches_id ON matches (id);
                CREATE INDEX matches_idtournament ON matches (idTournament);
                CREATE INDEX matches_idhometeam ON matches (idHomeTeam);
                CREATE INDEX matches_idvisitorteam ON matches (idvisitorteam);
                CREATE INDEX matches_idfield ON matches (idfield);
                CREATE INDEX matches_idday ON matches (idday);


                CREATE TABLE matchplayers (
	                idMatch			INTEGER NOT NULL,
	                idPlayer		INTEGER NOT NULL,
	                status			INTEGER
                );

                CREATE INDEX matchplayers_idmatch ON matchplayers (idPlayer);
                CREATE INDEX matchplayers_idplayer ON matchplayers (idMatch);

                CREATE TABLE matchreferees (
	                idMatch			INTEGER NOT NULL,
	                idUser			INTEGER NOT NULL,
	                role			INTEGER
                );

                CREATE INDEX matchreferees_idmatch ON matchreferees (idMatch);
                CREATE INDEX matchreferees_iduser ON matchreferees (idUser);


                CREATE TABLE matchevents (
                    id              SERIAL,
	                idMatch			INTEGER NOT NULL,
	                idCreator		INTEGER NOT NULL, 
	                idTeam			INTEGER, 
	                idPlayer		INTEGER,
	                timeStamp		TIMESTAMP NOT NULL, 
	                type			INTEGER NOT NULL,
	                matchMinute		INTEGER,
	                intData1		INTEGER,
	                intData2		INTEGER,
	                data1			TEXT, 
	                data2			TEXT
                );

                CREATE INDEX matchevents_id ON matchevents (id);
                CREATE INDEX matchevents_idmatch ON matchevents (idMatch);
                CREATE INDEX matchevents_idteam ON matchevents (idTeam);
                CREATE INDEX matchevents_idplayer ON matchevents (idPlayer);

                CREATE TABLE playdays (
	                id			SERIAL,
                    idTournament	INTEGER NOT NULL,
	                name		TEXT,
	                dates		TEXT,
                    sortOrder   INTEGER
                );

                CREATE INDEX playdays_id ON playdays (id);
                CREATE INDEX playdays_idtournament ON playdays (idTournament);
                CREATE INDEX playdays_order ON playdays (sortOrder);

                ALTER TABLE users ADD COLUMN lang TEXT;
            ");

            return 7;
        }

        private int Undo6(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"7 -> 6: Drop UserEvents and match related tables");

            conn.Execute(@"
                DROP TABLE userevents;
                DROP TABLE matches;
                DROP TABLE matchevents;
                DROP TABLE matchreferees;
                DROP TABLE matchplayers;
                DROP TABLE playdays;

                ALTER TABLE users DROP COLUMN lang;
            ");

            return 6;
        }

        private int Update7(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"7 -> 8: Add users notification data + matchplayers fields");

            // Added also 

            conn.Execute(@" 
                ALTER TABLE users ADD COLUMN notificationPushToken TEXT;
                ALTER TABLE matchplayers ADD COLUMN idTeam INTEGER NOT NULL;                
                ALTER TABLE matchplayers ADD COLUMN idUser INTEGER NOT NULL;
                ALTER TABLE matchplayers ADD COLUMN apparelNumber INTEGER;
            ");

            return 8;
        }

        private int Undo7(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"8 -> 7: Drop users notification data");

            conn.Execute(@"
                ALTER TABLE users DROP COLUMN notificationPushToken;
                ALTER TABLE matchplayers DROP COLUMN idTeam;                
                ALTER TABLE matchplayers DROP COLUMN idUser;
                ALTER TABLE matchplayers DROP COLUMN apparelNumber;
            ");

            return 7;
        }


        private int Update8(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"8 -> 9: Add fields to matchEvents and matchPlayers");

            conn.Execute(@"
                ALTER TABLE matchEvents ADD COLUMN idDay INTEGER NOT NULL;

                ALTER TABLE matchPlayers ADD COLUMN idDay INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data1 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data2 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data3 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data4 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data5 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN idDay INTEGER; 
                ALTER TABLE matchPlayers ADD COLUMN points INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN pointsAgainst INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN pointsInOwn INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType1 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType2 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType3 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType4 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType5 INTEGER;

                CREATE INDEX matchplayers_idday ON matchplayers (idDay);

                CREATE TABLE teamdayresults (
	                idDay			INTEGER NOT NULL,
	                idTeam			INTEGER NOT NULL, 
	                idTournament	INTEGER NOT NULL,
	                gamesPlayed		INTEGER,
	                gamesWon		INTEGER,
	                gamesDraw		INTEGER,
	                gamesLost		INTEGER,
	                points			INTEGER,
	                pointsAgainst	INTEGER,
	                pointDiff		INTEGER,
	                sanctions		INTEGER,

	                ranking1		INTEGER, 
	                ranking2		INTEGER,
	                ranking3		INTEGER
                );

                CREATE INDEX teamdayresults_day ON teamdayresults (idDay);
                CREATE INDEX teamdayresults_teams ON teamdayresults (idTeam);
                CREATE INDEX teamdayresults_tournaments ON teamdayresults (idTournament);

                CREATE TABLE playerdayresults (
	                idUser			INTEGER NOT NULL,
	                idPlayer		INTEGER NOT NULL,
	                idTournament	INTEGER NOT NULL, 
	                idDay			INTEGER NOT NULL,
	                apparelNumber	INTEGER,

	                points			INTEGER,
	                pointsAgainst	INTEGER,
	                pointsInOwn		INTEGER,

	                cardsType1		INTEGER,
	                cardsType2		INTEGER,
	                cardsType3		INTEGER,
	                cardsType4		INTEGER,
	                cardsType5		INTEGER,

	                data1			INTEGER,
	                data2			INTEGER,
	                data3			INTEGER,
	                data4			INTEGER,
	                data5			INTEGER
                );

                CREATE INDEX playerdayresults_idday ON playerdayresults (idDay);
                CREATE INDEX playerdayresults_idplayer ON playerdayresults (idPlayer);
                CREATE INDEX playerdayresults_idtournament ON playerdayresults (idTournament);
            ");

            return 9;
        }

        private int Undo8(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"9 -> 8: Remove fields to matchEvents and matchPlayers");

            conn.Execute(@"
                ALTER TABLE matchEvents DROP COLUMN idDay;

                ALTER TABLE matchPlayers DROP COLUMN idDay;
                ALTER TABLE matchPlayers DROP COLUMN data1;
                ALTER TABLE matchPlayers DROP COLUMN data2;
                ALTER TABLE matchPlayers DROP COLUMN data3;
                ALTER TABLE matchPlayers DROP COLUMN data4;
                ALTER TABLE matchPlayers DROP COLUMN data5;
                ALTER TABLE matchPlayers DROP COLUMN idDay; 
                ALTER TABLE matchPlayers DROP COLUMN points;
                ALTER TABLE matchPlayers DROP COLUMN pointsAgainst;
                ALTER TABLE matchPlayers DROP COLUMN pointsInOwn;
                ALTER TABLE matchPlayers DROP COLUMN cardsType1;
                ALTER TABLE matchPlayers DROP COLUMN cardsType2;
                ALTER TABLE matchPlayers DROP COLUMN cardsType3;
                ALTER TABLE matchPlayers DROP COLUMN cardsType4;
                ALTER TABLE matchPlayers DROP COLUMN cardsType5;

                DROP INDEX matchplayers_idday;

                DROP TABLE teamdayresults;
                DROP TABLE playerdayresults;
            ");

            return 8;
        }

        private int Update9(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"9 -> 10: Add contents table");

            conn.Execute(@"            
                CREATE TABLE contents (
	                id				SERIAL,
	                idCreator		INTEGER NOT NULL, 
	                timeStamp		TIMESTAMP,
	                publishDate		TIMESTAMP,
	                path			TEXT,
	                contentType		INTEGER,
	                status			INTEGER,
	                title			TEXT,
	                subtitle		TEXT,
	                rawcontent		TEXT,
	                mainImgUrl		TEXT,
	                thumbImgUrl		TEXT,
	                priority		INTEGER,
	                keywords		TEXT,
	                userData1		TEXT,
	                userData2		TEXT,
	                userData3		TEXT,
	                userData4		TEXT
                );

                CREATE INDEX contents_id ON contents (id);
            ");

            return 10;
        }

        private int Undo9(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"10 -> 9: Drop contents table");

            conn.Execute(@"
                DROP TABLE contents;
            ");

            return 9;
        }

        private int Update10(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"10 -> 11: Update notifications table");

            conn.Execute(@"
                DROP TABLE IF EXISTS notifications;

                CREATE TABLE notifications (
	                id			SERIAL,
	                idCreator	INTEGER NOT NULL, 
	                IdRcptUser	INTEGER NOT NULL, 
	                status		INTEGER, 
	                timestamp	TIMESTAMP,
	                text		TEXT,
	                text2		TEXT,
	                data1		TEXT,
	                data2		TEXT,
	                apiActionLabel1		TEXT,
	                apiActionUrl1		TEXT,
	                apiActionLabel2		TEXT,
	                apiActionUrl2		TEXT,
	                apiActionLabel3		TEXT,
	                apiActionUrl3		TEXT,
	                frontActionLabel1	TEXT,
	                frontActionUrl1		TEXT,
	                frontActionLabel2	TEXT,
	                frontActionUrl2		TEXT,
	                frontActionLabel3	TEXT,
	                frontActionUrl3		TEXT
                );

                CREATE INDEX notifications_id ON notifications (id);
                CREATE INDEX notifications_idRcptUser ON notifications (idRcptUser);
                CREATE INDEX notifications_timestamp ON notifications (timestamp);
            ");

            return 11;
        }

        private int Undo10(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"11 -> 10: Drop notifications table");

            conn.Execute(@"
                DROP TABLE IF EXISTS notifications;
            ");

            return 10;
        }

        private int Update11(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"11 -> 12: Add tournament points to TeamDayResults");

            conn.Execute(@"
                ALTER TABLE teamdayresults ADD COLUMN tournamentPoints INTEGER;
                ALTER TABLE playdays ADD COLUMN sequenceOrder INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN gamesPlayed INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN gamesWon INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN gamesDraw INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN gamesLost INTEGER;
                ALTER TABLE teamplayers ADD COLUMN idTacticPosition INTEGER;

                ALTER TABLE organizations ADD COLUMN address1 TEXT; 
                ALTER TABLE organizations ADD COLUMN address2 TEXT;
                ALTER TABLE organizations ADD COLUMN address3 TEXT;
                ALTER TABLE organizations ADD COLUMN motto    TEXT;
                ALTER TABLE organizations ADD COLUMN social1  TEXT;
                ALTER TABLE organizations ADD COLUMN social1link   TEXT;
                ALTER TABLE organizations ADD COLUMN social2  TEXT;
                ALTER TABLE organizations ADD COLUMN social2link   TEXT;
                ALTER TABLE organizations ADD COLUMN social3  TEXT;
                ALTER TABLE organizations ADD COLUMN social3link   TEXT;
                ALTER TABLE organizations ADD COLUMN social4  TEXT;
                ALTER TABLE organizations ADD COLUMN social4link   TEXT;

                ALTER TABLE matchplayers DROP COLUMN 	points		;
                ALTER TABLE matchplayers DROP COLUMN 	pointsAgainst;
                ALTER TABLE matchplayers DROP COLUMN 	pointsInOwn	;
                ALTER TABLE matchplayers DROP COLUMN 	cardsType1	;
                ALTER TABLE matchplayers DROP COLUMN 	cardsType2	;
                ALTER TABLE matchplayers DROP COLUMN 	cardsType3	;
                ALTER TABLE matchplayers DROP COLUMN 	cardsType4	;
                ALTER TABLE matchplayers DROP COLUMN 	cardsType5	;
                ALTER TABLE matchplayers DROP COLUMN 	data1		;
                ALTER TABLE matchplayers DROP COLUMN 	data2		;
                ALTER TABLE matchplayers DROP COLUMN 	data3		;
                ALTER TABLE matchplayers DROP COLUMN 	data4		;
                ALTER TABLE matchplayers DROP COLUMN 	data5		;

                ALTER TABLE playerdayresults ADD COLUMN ranking1		INTEGER; 
                ALTER TABLE playerdayresults ADD COLUMN ranking2		INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN ranking3		INTEGER; 
                ALTER TABLE playerdayresults ADD COLUMN ranking4		INTEGER; 
                ALTER TABLE playerdayresults ADD COLUMN ranking5		INTEGER;

                CREATE INDEX days_idtournament ON playdays (idTournament);
                CREATE UNIQUE INDEX playerdayresults_key ON playerdayresults (idDay, idPlayer, idTournament);
            ");

            return 12;
        }

        private int Undo11(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"12 -> 11: Remove tournament points to TeamDayResults");

            conn.Execute(@"
                ALTER TABLE teamdayresults DROP COLUMN tournamentPoints;
                ALTER TABLE playdays DROP COLUMN sequenceOrder;

                ALTER TABLE playerdayresults DROP COLUMN gamesPlayed;
                ALTER TABLE playerdayresults DROP COLUMN gamesWon;
                ALTER TABLE playerdayresults DROP COLUMN gamesDraw;
                ALTER TABLE playerdayresults DROP COLUMN gamesLost;
               
                ALTER TABLE teamplayers DROP COLUMN idTacticPosition;

                ALTER TABLE organizations DROP COLUMN address1;
                ALTER TABLE organizations DROP COLUMN address2;
                ALTER TABLE organizations DROP COLUMN address3;
                ALTER TABLE organizations DROP COLUMN motto;
                ALTER TABLE organizations DROP COLUMN social1;
                ALTER TABLE organizations DROP COLUMN social1link;
                ALTER TABLE organizations DROP COLUMN social2;
                ALTER TABLE organizations DROP COLUMN social2link;
                ALTER TABLE organizations DROP COLUMN social3;
                ALTER TABLE organizations DROP COLUMN social3link;
                ALTER TABLE organizations DROP COLUMN social4;
                ALTER TABLE organizations DROP COLUMN social4link;

                ALTER TABLE matchPlayers ADD COLUMN data1 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data2 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data3 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data4 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN data5 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN idDay INTEGER; 
                ALTER TABLE matchPlayers ADD COLUMN points INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN pointsAgainst INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN pointsInOwn INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType1 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType2 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType3 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType4 INTEGER;
                ALTER TABLE matchPlayers ADD COLUMN cardsType5 INTEGER;

                ALTER TABLE playerdayresults DROP COLUMN ranking1; 
                ALTER TABLE playerdayresults DROP COLUMN ranking2;
                ALTER TABLE playerdayresults DROP COLUMN ranking3; 
                ALTER TABLE playerdayresults DROP COLUMN ranking4; 
                ALTER TABLE playerdayresults DROP COLUMN ranking5;

                DROP INDEX playerdayresults_key;
                DROP INDEX days_idtournament;
            ");

            return 11;
        }

        private int Update12(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"12 -> 13: Add Season date range");

            conn.Execute(@"
                ALTER TABLE seasons ADD COLUMN startDate TIMESTAMP;
                ALTER TABLE seasons ADD COLUMN endDate   TIMESTAMP;

                INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F5', 5);
                INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F6', 6);
                INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F7', 7);
                INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F11', 11);

                ALTER TABLE tournamentteams ADD COLUMN idGroup INTEGER;

                CREATE TABLE tournamentgroups (
	                id				SERIAL,
	                idTournament	INTEGER NOT NULL, 
	                name			TEXT
                );

                CREATE UNIQUE INDEX tournamentgroups_id ON tournamentgroups (id);

                ALTER TABLE matches ADD COLUMN comments TEXT;
                ALTER TABLE matches ADD COLUMN videoUrl TEXT;
            ");

            return 13;
        }

        private int Undo12(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"13 -> 12: drop season date range");

            conn.Execute(@"
                ALTER TABLE seasons DROP COLUMN	startDate;
                ALTER TABLE seasons DROP COLUMN	endDate;

                DELETE FROM tournamentModes WHERE name = 'F5';
                DELETE FROM tournamentModes WHERE name = 'F6';
                DELETE FROM tournamentModes WHERE name = 'F7';
                DELETE FROM tournamentModes WHERE name = 'F11';

                ALTER TABLE tournamentteams DROP COLUMN idGroup;

                DROP TABLE tournamentgroups;

                ALTER TABLE matches DROP COLUMN comments;
                ALTER TABLE matches DROP COLUMN videoUrl;
            ");

            return 12;
        }


        private int Update13(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"13 -> 14: Add tournament stages and groups");

            conn.Execute(@"
                CREATE TABLE tournamentstages (
	                id			    SERIAL, 
	                idTournament	INTEGER NOT NULL, 
	                name		    TEXT,
	                description	    TEXT,
                    type            INTEGER,
	                status			INTEGER DEFAULT 1,
	                sequenceOrder	INTEGER NOT NULL
                );

                CREATE INDEX tournamentstages_id ON tournamentstages (id);
                CREATE INDEX tournamentstages_tournament ON tournamentstages (idTournament);

                CREATE TABLE stagegroups (
	                id				SERIAL, 
	                idStage			INTEGER NOT NULL, 
	                idTournament	INTEGER NOT NULL, 
	                name			TEXT NOT NULL, 
	                description		TEXT,
	                numTeams		INTEGER NOT NULL, 
	                numRounds		INTEGER, 
	                flags			INTEGER, 
	                sequenceOrder	INTEGER,
                    colorConfig     TEXT
                );

                CREATE INDEX stagegroups_id ON stagegroups (id);
                CREATE INDEX stagegroups_tournament	ON stagegroups (idTournament);

                CREATE TABLE teamgroups (
	                id				SERIAL, 
	                idTeam			INTEGER NOT NULL, 
	                idTournament	INTEGER NOT NULL, 
	                idStage			INTEGER NOT NULL, 
	                idGroup			INTEGER NOT NULL, 
	                sequenceOrder	INTEGER DEFAULT -1
                );

                CREATE INDEX teamgroups_id ON teamgroups (id);
                CREATE INDEX teamgroups_tournaments ON teamgroups (idTournament);

                CREATE INDEX matchplayers_idteam ON matchplayers (idTeam);

                ALTER TABLE tournamentTeams DROP COLUMN idGroup;

                ALTER TABLE teamDayResults ADD COLUMN idStage INTEGER;
                ALTER TABLE teamDayResults ADD COLUMN idGroup INTEGER;

                ALTER TABLE playerdayresults ADD COLUMN idStage INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN idGroup INTEGER;

                ALTER TABLE matches ADD COLUMN idStage INTEGER;
                ALTER TABLE matches ADD COLUMN idGroup INTEGER;
                ALTER TABLE matches ADD COLUMN homeTeamDescription TEXT;
                ALTER TABLE matches ADD COLUMN visitorTeamDescription TEXT;

                CREATE INDEX matches_idStage ON matches (idstage);
                CREATE INDEX matches_idGroup ON matches (idgroup);

                ALTER TABLE playdays ADD COLUMN idStage INTEGER;
                ALTER TABLE playdays ADD COLUMN idGroup INTEGER;

                ALTER TABLE contents ADD COLUMN idCategory		INTEGER; 
                ALTER TABLE contents ADD COLUMN videoUrl		TEXT; 
                ALTER TABLE contents ADD COLUMN idTournament	INTEGER; 
                ALTER TABLE contents ADD COLUMN idTeam			INTEGER; 
                ALTER TABLE contents ADD COLUMN layoutType		TEXT; 
                ALTER TABLE contents ADD COLUMN categoryPosition1   INTEGER; 
                ALTER TABLE contents ADD COLUMN categoryPosition2	INTEGER; 

                CREATE INDEX contents_tournament ON contents (idTournament);
                CREATE INDEX contents_team ON contents (idTeam);

                CREATE TABLE contentcategories (
	                id				SERIAL, 
	                name			TEXT
                );

                CREATE UNIQUE INDEX contentcategories_id ON contentcategories(id);

                INSERT INTO contentcategories (name) VALUES ('MENU');

                DROP TABLE tournamentgroups;
            ");

            return 14;
        }

        private int Undo13(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"14 -> 13: ");

            conn.Execute(@"
                DROP TABLE tournamentstages;
                DROP TABLE stagegroups;
                DROP TABLE teamgroups;

                ALTER TABLE tournamentTeams ADD COLUMN idGroup INTEGER;

                ALTER TABLE teamDayResults DROP COLUMN idStage;
                ALTER TABLE teamDayResults DROP COLUMN idGroup;

                ALTER TABLE playerdayresults DROP COLUMN idStage;
                ALTER TABLE playerdayresults DROP COLUMN idGroup;

                ALTER TABLE matches DROP COLUMN idStage;
                ALTER TABLE matches DROP COLUMN idGroup;
                ALTER TABLE matches DROP COLUMN homeTeamDescription;
                ALTER TABLE matches DROP COLUMN visitorTeamDescription;

                ALTER TABLE playdays DROP COLUMN idStage;
                ALTER TABLE playdays DROP COLUMN idGroup;

                CREATE TABLE tournamentgroups (
	                id				SERIAL,
	                idTournament	INTEGER NOT NULL, 
	                name			TEXT
                );

                ALTER TABLE contents DROP COLUMN idCategory; 
                ALTER TABLE contents DROP COLUMN videoUrl; 
                ALTER TABLE contents DROP COLUMN idTournament; 
                ALTER TABLE contents DROP COLUMN idTeam; 
                ALTER TABLE contents DROP COLUMN layoutType; 
                ALTER TABLE contents DROP COLUMN categoryPosition1; 
                ALTER TABLE contents DROP COLUMN categoryPosition2; 

                DROP INDEX contents_tournament;
                DROP INDEX contents_team;

                CREATE UNIQUE INDEX tournamentgroups_id ON tournamentgroups (id);

                DROP INDEX matchplayers_idteam ON matchplayers (idTeam);

                DROP TABLE contentcategories;
            ");

            return 13;
        }

        private int Update14(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"14 -> 15: ");

            conn.Execute(@"
                ALTER TABLE teams ADD COLUMN apparelConfig TEXT;

                CREATE TABLE notificationtemplates (
	                id				SERIAL, 
	                type			INTEGER, 
	                key				TEXT, 
                    title			TEXT,
	                contentTemplate	TEXT
                );

                CREATE UNIQUE INDEX notificationtemplates_id ON notificationtemplates (id);
            ");

            return 15;
        }

        private int Undo14(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"15 -> 14: ");

            conn.Execute(@"
                ALTER TABLE teams DROP COLUMN apparelConfig;

                DROP TABLE notificationtemplates;
            ");

            return 14;
        }

        private int Update15(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"15 -> 16: Add primary key constraint to all tables.");

            conn.Execute(@"
                ALTER TABLE categories ADD PRIMARY KEY (id);		
                ALTER TABLE contentcategories ADD PRIMARY KEY (id);		
                ALTER TABLE contents ADD PRIMARY KEY (id);		
                ALTER TABLE fields ADD PRIMARY KEY (id);		
                ALTER TABLE matches ADD PRIMARY KEY (id);		
                ALTER TABLE matchevents ADD PRIMARY KEY (id);		

                ALTER TABLE notifications ADD PRIMARY KEY (id);		
                ALTER TABLE notificationtemplates ADD PRIMARY KEY (id);		
                ALTER TABLE organizations ADD PRIMARY KEY (id);		
                ALTER TABLE playdays ADD PRIMARY KEY (id);		

                ALTER TABLE players ADD PRIMARY KEY (id);		
                ALTER TABLE seasons ADD PRIMARY KEY (id);		
                ALTER TABLE secureuploads ADD PRIMARY KEY (id);		
                ALTER TABLE stagegroups ADD PRIMARY KEY (id);		

                ALTER TABLE teamgroups ADD PRIMARY KEY (id);		

                ALTER TABLE teams ADD PRIMARY KEY (id);		
                ALTER TABLE textblobs ADD PRIMARY KEY (id);		
                ALTER TABLE tournamentmodes ADD PRIMARY KEY (id);		
                ALTER TABLE tournaments ADD PRIMARY KEY (id);		
                ALTER TABLE tournamentstages ADD PRIMARY KEY (id);		

                ALTER TABLE uploads ADD PRIMARY KEY (id);		
                ALTER TABLE userevents ADD PRIMARY KEY (id);		
                ALTER TABLE users ADD PRIMARY KEY (id);
            ");

            return 16;
        }

        private int Undo15(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"16 -> 15: remove primary key constraints");

            conn.Execute(@"
                ALTER TABLE categories DROP CONSTRAINT categories_pkey;
                ALTER TABLE contentcategories DROP CONSTRAINT contentcategories_pkey;
                ALTER TABLE contents DROP CONSTRAINT contents_pkey;
                ALTER TABLE fields DROP CONSTRAINT fields_pkey;
                ALTER TABLE matches DROP CONSTRAINT matches_pkey;
                ALTER TABLE matchevents DROP CONSTRAINT matchevents_pkey;
                ALTER TABLE notifications DROP CONSTRAINT notifications_pkey;
                ALTER TABLE notificationtemplates DROP CONSTRAINT notificationtemplates_pkey;
                ALTER TABLE organizations DROP CONSTRAINT organizations_pkey;
                ALTER TABLE playdays DROP CONSTRAINT playdays_pkey;
                ALTER TABLE players DROP CONSTRAINT players_pkey;
                ALTER TABLE seasons DROP CONSTRAINT seasons_pkey;
                ALTER TABLE secureuploads DROP CONSTRAINT secureuploads_pkey;
                ALTER TABLE stagegroups DROP CONSTRAINT stagegroups_pkey;
                ALTER TABLE teamgroups DROP CONSTRAINT teamgroups_pkey;
                ALTER TABLE teams DROP CONSTRAINT teams_pkey;
                ALTER TABLE textblobs DROP CONSTRAINT textblobs_pkey;
                ALTER TABLE tournamentmodes DROP CONSTRAINT tournamentmodes_pkey;
                ALTER TABLE tournaments DROP CONSTRAINT tournaments_pkey;
                ALTER TABLE tournamentstages DROP CONSTRAINT tournamentstages_pkey;
                ALTER TABLE uploads DROP CONSTRAINT uploads_pkey;
                ALTER TABLE userevents DROP CONSTRAINT userevents_pkey;
                ALTER TABLE users DROP CONSTRAINT users_pkey;
            ");

            return 15;
        }


        private int Update16(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"16 -> 17: Add user devices, sponsors, awards, enrollment data");

            conn.Execute(@"
               CREATE TABLE userdevices (
	                id			SERIAL PRIMARY KEY, 
	                idUser		INTEGER, 
	                name		TEXT,
	                deviceToken	TEXT
                );

                CREATE UNIQUE INDEX userdevices_id ON userdevices (id);
                CREATE INDEX userdevices_user ON userdevices (idUser);
                CREATE UNIQUE INDEX userdevices_device ON userdevices (deviceToken);

                CREATE TABLE sponsors (
	                id				SERIAL PRIMARY KEY, 
	                idOrganization	INTEGER DEFAULT -1,
	                idTournament	INTEGER DEFAULT -1,
	                idTeam			INTEGER DEFAULT -1,
	                name			TEXT,
	                rawCode			TEXT,
	                url				TEXT,
	                imgUrl			TEXT,
                    altText         TEXT,
	                position		INTEGER,
                    sequenceOrder	INTEGER
                );

                CREATE UNIQUE INDEX sponsors_id	ON sponsors (id);
                CREATE INDEX sponsors_tournament ON sponsors (idTournament);
                CREATE INDEX sponsors_team ON sponsors (idTeam);
                CREATE INDEX sponsors_organization ON sponsors (idOrganization);

                CREATE TABLE awards (
	                id				SERIAL PRIMARY KEY, 
	                idPlayer		INTEGER,
                    idTeam			INTEGER,
	                idDay			INTEGER,
	                idTournament	INTEGER,
	                idStage			INTEGER,
	                idGroup			INTEGER,
	                type			INTEGER
                );

                CREATE UNIQUE INDEX awards_id ON awards (id);
                CREATE INDEX awards_player ON awards (idPlayer);
                CREATE INDEX awards_tournament ON awards (idTournament);

                ALTER TABLE teamplayers ADD COLUMN enrollmentStep INTEGER;
                ALTER TABLE teamplayers ADD COLUMN enrollmentData TEXT;

                ALTER TABLE players ADD COLUMN enrollmentStep INTEGER;

                ALTER TABLE organizations ADD COLUMN social5 TEXT;
                ALTER TABLE organizations ADD COLUMN social5link TEXT;
                ALTER TABLE organizations ADD COLUMN social6 TEXT;
                ALTER TABLE organizations ADD COLUMN social6link TEXT;

                CREATE TABLE paymentconfigs (
	                id				SERIAL PRIMARY KEY, 
	                idOrganization	INTEGER DEFAULT -1, 
	                idTournament	INTEGER DEFAULT -1, 
	                idTeam			INTEGER DEFAULT -1,
	                enrollmentWorkflow		TEXT,
	                gatewayConfig			TEXT
                );

                CREATE UNIQUE INDEX paymentconfig_id ON paymentconfigs (id);
                CREATE INDEX paymentconfig_org ON paymentconfigs (idOrganization);
                CREATE INDEX paymentconfig_team ON paymentconfigs (idTeam);
                CREATE INDEX paymentconfig_tournament ON paymentconfigs (idTournament);

                INSERT INTO paymentconfigs (idOrganization, enrollmentWorkflow) VALUES (1, '[]');
            ");

            return 17;
        }

        private int Undo16(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"17 -> 16: drop user devices and sponsors");

            conn.Execute(@"
                DROP TABLE userdevices;
                DROP TABLE sponsors;
                DROP TABLE awards;
                DROP TABLE paymentconfigs;

                ALTER TABLE teamplayers DROP COLUMN enrollmentStep;
                ALTER TABLE teamplayers DROP COLUMN	enrollmentData;
                
                ALTER TABLE players DROP COLUMN enrollmentStep;

                ALTER TABLE organizations DROP COLUMN social5;
                ALTER TABLE organizations DROP COLUMN social5link;
                ALTER TABLE organizations DROP COLUMN social6;
                ALTER TABLE organizations DROP COLUMN social6link;
            ");

            return 16;
        }

        private int Update17(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"17 -> 18: ");

            conn.Execute(@"
                ALTER TABLE organizations ADD COLUMN paymentKey TEXT;
                ALTER TABLE organizations ADD COLUMN paymentKeyPublic TEXT;
                ALTER TABLE organizations ADD COLUMN paymentDescription TEXT;
                ALTER TABLE organizations ADD COLUMN paymentCurrency TEXT;
                ALTER TABLE organizations ADD COLUMN defaultLang TEXT;

                ALTER TABLE userevents ADD COLUMN data1 TEXT;
                ALTER TABLE userevents ADD COLUMN data2 TEXT;
                ALTER TABLE userevents ADD COLUMN data3 TEXT;
            ");

            return 18;
        }

        private int Undo17(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"18 -> 17:");

            conn.Execute(@"
                ALTER TABLE organizations DROP COLUMN paymentKey;
                ALTER TABLE organizations DROP COLUMN paymentKeyPublic;
                ALTER TABLE organizations DROP COLUMN paymentDescription;
                ALTER TABLE organizations DROP COLUMN paymentCurrency;
                ALTER TABLE organizations DROP COLUMN defaultLang;

                ALTER TABLE userevents DROP COLUMN data1;
                ALTER TABLE userevents DROP COLUMN data2;
                ALTER TABLE userevents DROP COLUMN data3;
            ");

            return 17;
        }

        private int Update18(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"18 -> 19: ");

            conn.Execute(@"
                ALTER TABLE organizations ADD COLUMN paymentKeyPublic TEXT;
            ");

            return 19;
        }

        private int Undo18(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"19 -> 18:");

            conn.Execute(@"
                ALTER TABLE organizations DROP COLUMN paymentKeyPublic;
            ");

            return 18;
        }


        private int Update19(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"19 -> 20: update player invitation text to include activation pin");

            UpdateEmailTemplate(conn, "email.player.invite.html", "NotificationTemplates.es.email.player.invite.html");

            return 20;
        }

        private int Undo19(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"20 -> 19: ");

            conn.Execute(@"
            ");

            return 19;
        }


        private int Update20(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"19 -> 20: team img");

            conn.Execute(@"
                ALTER TABLE teams ADD COLUMN teamImgUrl TEXT;
                ALTER TABLE teams ADD COLUMN teamImgUrl2 TEXT;
                ALTER TABLE teams ADD COLUMN teamImgUrl3 TEXT;
            ");

            return 21;
        }

        private int Undo20(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"21 -> 20: team img url");

            conn.Execute(@"
                ALTER TABLE teams DROP COLUMN teamImgUrl;
                ALTER TABLE teams DROP COLUMN teamImgUrl2;
                ALTER TABLE teams DROP COLUMN teamImgUrl3;
            ");

            return 20;
        }

        private int Update21(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"20 -> 21: Add enrollmentDate to teamplayers");

            conn.Execute(@"
                ALTER TABLE teamplayers ADD COLUMN enrollmentDate TIMESTAMP;
            ");

            return 22;
        }

        private int Undo21(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"22 -> 21: ");

            conn.Execute(@"
                ALTER TABLE teamplayers DROP COLUMN enrollmentDate;
            ");

            return 21;
        }


        private int Update22(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"22 -> 23: ");

            conn.Execute(@"
                ALTER TABLE teams ADD COLUMN prefTime TIMESTAMP;
            ");

            return 23;
        }

        private int Undo22(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"23 -> 22: ");

            conn.Execute(@"
                ALTER TABLE teams DROP COLUMN prefTime;
            ");

            return 22;
        }


        private int Update23(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"23 -> 24: ");

            conn.Execute(@"
                ALTER TABLE players ADD COLUMN approved BOOLEAN;
                ALTER TABLE tournaments ADD COLUMN visible BOOLEAN DEFAULT'f';
                UPDATE tournaments SET visible='t';
            ");

            return 24;
        }

        private int Undo23(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"24 -> 23: ");

            conn.Execute(@"
                ALTER TABLE players DROP COLUMN approved;
                ALTER TABLE tournaments DROP COLUMN visible;
            ");

            return 23;
        }

        private int Update24(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"24 -> 25: ");

            conn.Execute(@"
                ALTER TABLE paymentconfigs ADD COLUMN idUser INTEGER DEFAULT -1;
            ");

            return 25;
        }

        private int Undo24(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"25 -> 24: ");

            conn.Execute(@"
                ALTER TABLE paymentconfigs DROP COLUMN idUser;
            ");

            return 24;
        }

        private int Update25(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"25 -> 26: Set all team admins user levels as players (irreversible). Remove teamplayersidTournament. Add playerDayResults.idTeam.");
            mLogger("  - Adding columns...");
            conn.Execute(@"
                UPDATE users SET level = 1 WHERE level = 3;
                
                ALTER TABLE teamplayers ALTER COLUMN idTournament DROP NOT NULL;
                ALTER TABLE teamplayers RENAME COLUMN idTournament TO idTournamentNotUsed;

                ALTER TABLE playerDayResults ADD COLUMN idTeam INTEGER; 
                CREATE INDEX playerdayresults_idteam ON playerdayresults (idTeam);

                DROP INDEX playerdayresults_key;
                CREATE UNIQUE INDEX playerdayresults_key ON playerdayresults (idDay, idPlayer, idTeam, idTournament);
            ");

            mLogger("  - Recalculating all tournaments stats to populate PlayerDayResults.IdTeam");

            var allTournaments = conn.Query<Tournament>("SELECT * FROM tournaments", t);
            foreach (var tnmt in allTournaments)
            {
                mLogger("    - Tournament: " + tnmt.Name);
                MatchEvent.ResetTournamentStats(conn, t, tnmt.Id).Wait();
                MatchEvent.ApplyTournamentStats(conn, t, tnmt.Id).Wait();
            }

            return 26;
        }

        private int Undo25(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"26 -> 25: ");

            conn.Execute(@"
                ALTER TABLE teamplayers RENAME COLUMN idTournamentNotUsed TO idTournament;

                ALTER TABLE playerDayResults DROP COLUMN idTeam INTEGER; 
            ");

            return 25;
        }

        private int Update26(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"26 -> 27: ");

            conn.Execute(@"
                ALTER TABLE teamPlayers ADD COLUMN enrollmentPaymentData TEXT;
                ALTER TABLE tournamentstages ADD COLUMN classificationCriteria TEXT;

                DROP INDEX playerdayresults_key;
                CREATE UNIQUE INDEX playerdayresults_key ON playerdayresults (idDay, idPlayer, idTeam, idTournament, idStage, idGroup);
            ");

            // Add the code to populate the enrollmentpaymentdata field. Have to infer the stripe transaction for single team and multiteam transactions. 


            return 27;
        }

        private int Undo26(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"27 -> 26: add classificationCriteria to stages and enrollmentPaymentData to teamplayers");

            conn.Execute(@"
                ALTER TABLE tournamentstages DROP COLUMN classificationCriteria;
                ALTER TABLE teamPlayers DROP COLUMN enrollmentPaymentData; 

                DROP INDEX playerdayresults_key;
                CREATE UNIQUE INDEX playerdayresults_key ON playerdayresults (idDay, idPlayer, idTeam, idTournament);
            ");

            return 26;
        }

        private int Update27(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"27 -> 28: Add 'forgot password' email template");

            conn.Insert(new NotificationTemplate
            {
                Key = "email.player.forgotpassword.html",
                Lang = "es",
                Title = "Reiniciar contraseña",
                ContentTemplate = PostgresqlDataLayer.ReadResourceFile("NotificationTemplates.es.email.player.forgotpassword.html")
            }, t);

            return 28;
        }

        private int Undo27(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"28 -> 27: ");

            conn.Execute(@"
                DELETE FROM notificationTemplates WHERE key = 'email.player.forgotpassword.html' AND lang = 'es';
            ");

            return 27;
        }

        private int Update28(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"28 -> 29: ");

            conn.Execute(@"
                ALTER TABLE players ADD COLUMN idPhotoImgUrl TEXT;
	            ALTER TABLE players ADD COLUMN idCard1ImgUrl TEXT;
	            ALTER TABLE players ADD COLUMN idCard2ImgUrl TEXT;

                UPDATE players SET idcard1imgurl = u.repositorypath 
                FROM (select distinct idobject, first_value(repositorypath) over (partition by idobject order by idobject, id desc) as repositorypath from uploads where type = 203 order by idobject) AS u
                WHERE iduser = u.idobject;

                UPDATE players SET idcard2imgurl = u.repositorypath 
                FROM (select distinct idobject, first_value(repositorypath) over (partition by idobject order by idobject, id desc) as repositorypath from uploads where type = 204 order by idobject) AS u
                WHERE iduser = u.idobject;

                UPDATE players SET idphotoimgurl = u.repositorypath 
                FROM (select distinct idobject, first_value(repositorypath) over (partition by idobject order by idobject, id desc) as repositorypath from uploads where type = 205 order by idobject) AS u
                WHERE iduser = u.idobject;
            ");


            return 29;
        }

        private int Undo28(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"29 -> 28: ");

            conn.Execute(@"
                ALTER TABLE players DROP COLUMN idPhotoImgUrl;
	            ALTER TABLE players DROP COLUMN idCard1ImgUrl;
	            ALTER TABLE players DROP COLUMN idCard2ImgUrl;                
            ");

            return 28;
        }

        private int Update29(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"29 -> 30: add sanctions and payments entities");

            conn.Execute(@"
                CREATE TABLE sanctions (
	                id			SERIAL PRIMARY KEY,
	                idPlayer	INTEGER DEFAULT -1,
	                idTeam		INTEGER DEFAULT -1,
	                idMatch		INTEGER DEFAULT -1,
	                idTournament	INTEGER DEFAULT -1,
	                idDay		INTEGER DEFAULT -1,
                    title		TEXT,
	                status		INTEGER,
	                numMatches	INTEGER,
	                startDate	TIMESTAMP,
                    isAutomatic	BOOLEAN DEFAULT 'f',
	                idPayment	INTEGER DEFAULT -1
                );

                CREATE UNIQUE INDEX sanctions_id ON sanctions (id);
                CREATE INDEX sanctions_idplayer ON sanctions (idplayer);
                CREATE INDEX sanctions_idteam ON sanctions (idteam);
                CREATE INDEX sanctions_idmatch ON sanctions (idmatch);
                CREATE INDEX sanctions_idday ON sanctions (idday);


                CREATE TABLE sanctionallegations (
	                id			SERIAL PRIMARY KEY,
	                idSanction  INTEGER DEFAULT -1,
	                idUser		INTEGER DEFAULT -1, 
	                status		INTEGER, 
	                date		TIMESTAMP,
	                content		TEXT,
                    title       TEXT, 
                    visible     BOOLEAN DEFAULT 'f'
                );

                CREATE UNIQUE INDEX sanctionallegations_id ON sanctionallegations (id);
                CREATE INDEX sanctionallegations_idsanction ON sanctionallegations (idsanction);
                CREATE INDEX sanctionallegations_iduser ON sanctionallegations (iduser);


                CREATE TABLE payments (
	                id			SERIAL PRIMARY KEY,
	                idUser		INTEGER DEFAULT -1, 
	                idPlayer	INTEGER DEFAULT -1,
                    idTeam      INTEGER DEFAULT -1,
	                createdOn	TIMESTAMP, 
	                status		INTEGER,
	                type		INTEGER,
	                description TEXT, 
	                paymentData	TEXT,
	                data1		TEXT,
	                data2		TEXT,
	                data5		INTEGER,
	                data6		INTEGER
                );

                CREATE UNIQUE INDEX payments_id ON payments (id);
                CREATE INDEX iduser ON payments (iduser);
                CREATE INDEX idplayer ON payments (idplayer);
            ");

            return 30;
        }

        private int Undo29(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"30 -> 29: ");

            conn.Execute(@"
                DROP TABLE sanctions;
                DROP TABLE sanctionallegations;
                DROP TABLE payments;
            ");

            return 29;
        }


        private int Update30(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"30 -> 31: add automatic sanction config entities");

            conn.Execute(@"
                CREATE TABLE autosanctionconfigs (
	                id				SERIAL PRIMARY KEY,
	                idTournament	INTEGER DEFAULT -1, 
	                config			TEXT
                );

                CREATE UNIQUE INDEX autosanctionconfigs_id ON autosanctionconfigs (id);
                CREATE UNIQUE INDEX autosanctionconfig_idtournament ON autosanctionconfigs (idtournament);

                CREATE TABLE sanctionmatches (
	                id				SERIAL PRIMARY KEY,
	                idSanction		INTEGER DEFAULT -1,
	                idPlayer		INTEGER DEFAULT -1,
	                idMatch			INTEGER DEFAULT -1,
	                idTournament	INTEGER DEFAULT -1
                );

                CREATE UNIQUE INDEX sanctionmatches_id ON sanctionmatches (id);
                CREATE INDEX sanctionmatches_idmatch ON sanctionmatches (idmatch);
                CREATE INDEX sanctionmatches_idsanction ON sanctionmatches (idsanction);
    
                ALTER TABLE sanctions ADD COLUMN type INTEGER;
                ALTER TABLE sanctions ADD COLUMN idSanctionConfigRuleId INTEGER DEFAULT -1;
                
                ALTER TABLE sanctions ADD COLUMN lostMatchPenalty INTEGER DEFAULT 0;
                ALTER TABLE sanctions ADD COLUMN tournamentPointsPenalty INTEGER DEFAULT 0;  

                ALTER TABLE sanctions ADD COLUMN sanctionMatchEvents TEXT;
            ");

            return 31;
        }

        private int Undo30(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"31 -> 30: remove automatic sanction config entities");

            conn.Execute(@"
                DROP TABLE autosanctionconfigs;
                DROP TABLE sanctionmatches;

                ALTER TABLE sanctions DROP COLUMN type;                
                ALTER TABLE sanctions DROP COLUMN idSanctionConfigRuleId;

                ALTER TABLE sanctions DROP COLUMN lostMatchPenalty;
                ALTER TABLE sanctions DROP COLUMN tournamentPointsPenalty;  

                ALTER TABLE sanctions DROP COLUMN sanctionMatchEvents;
            ");

            return 30;
        }

        private int Update31(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"31 -> 32: Add more matchevents intdata fields");

            conn.Execute(@"
                ALTER TABLE matchevents ADD COLUMN intData3	INTEGER;
	            ALTER TABLE matchevents ADD COLUMN intData4	INTEGER;
	            ALTER TABLE matchevents ADD COLUMN intData5	INTEGER;
	            ALTER TABLE matchevents ADD COLUMN intData6	INTEGER;

                ALTER TABLE sanctionmatches ADD COLUMN isLast BOOLEAN DEFAULT 'f';
                ALTER TABLE matchevents ADD COLUMN isAutomatic BOOLEAN DEFAULT 'f';
            ");

            return 32;
        }

        private int Undo31(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"32 -> 31: ");

            conn.Execute(@"
                ALTER TABLE matchevents DROP COLUMN intData3;
	            ALTER TABLE matchevents DROP COLUMN intData4;
	            ALTER TABLE matchevents DROP COLUMN intData5;
	            ALTER TABLE matchevents DROP COLUMN intData6;
                ALTER TABLE sanctionmatches DROP COLUMN isLast;
                ALTER TABLE matchevents DROP COLUMN isAutomatic;
            ");

            return 31;
        }

        // 🔎 Unordered updates 

        private int Update32(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"32 -> 33: Not aquired functionalities");

            conn.Execute(@"
                ALTER TABLE awards ADD COLUMN text1	INTEGER;

                ALTER TABLE matches ADD COLUMN visiblehomescore	INTEGER;
                ALTER TABLE matches ADD COLUMN visiblevisitorscore	INTEGER;    

                CREATE TABLE matchplayersnotices (
	                idMatch		INTEGER NOT NULL,
	                idPlayer	INTEGER NOT NULL,
	                idTeam		INTEGER NOT NULL,
	                idUser	    INTEGER NOT NULL,
                    idDay       INTEGER NOT NULL,
                    idNotice    INTEGER NOT NULL,
                    accepted    BOOLEAN NOT NULL,
                    accepteddate TIMESTAMP
                );
                CREATE UNIQUE INDEX matchplayersnotice_idmatch ON matchplayersnotices (idmatch);
                CREATE UNIQUE INDEX matchplayersnotice_idnotice ON matchplayersnotices (idnotice);
                CREATE UNIQUE INDEX matchplayersnotice_idplayer ON matchplayersnotices (idplayer);
                CREATE UNIQUE INDEX matchplayersnotice_idteam ON matchplayersnotices (idteam);
                CREATE UNIQUE INDEX matchplayersnotices_idday ON matchplayersnotices (idday);

                CREATE TABLE notices (
                    id	    SERIAL PRIMARY KEY,
                    name       TEXT NOT NULL,
                    text                        TEXT,
                    confirmationtext1           TEXT,
                    confirmationtext2           TEXT,
                    confirmationtext3           TEXT,
                    accepttext                  TEXT,
                    hoursinadvance  INTEGER NOT NULL,
                    idtournament    INTEGER NOT NULL,
                    enabled                 BOOLEAN
                );
                CREATE UNIQUE INDEX notices_idtournament ON notices (idtournament);

                ALTER TABLE notifications ADD COLUMN text3 TEXT;

                ALTER TABLE organizations ADD COLUMN sponsordata TEXT;
                ALTER TABLE organizations ADD COLUMN dpcompanyname TEXT;
                ALTER TABLE organizations ADD COLUMN dpcompanyid TEXT;
                ALTER TABLE organizations ADD COLUMN dpcompanyaddress TEXT;
                ALTER TABLE organizations ADD COLUMN dpcompanyemail TEXT;
                ALTER TABLE organizations ADD COLUMN dpcompanyphone TEXT;
                ALTER TABLE organizations ADD COLUMN appearancedata TEXT;
                ALTER TABLE organizations ADD COLUMN termsversion INTEGER;
                ALTER TABLE organizations ADD COLUMN paymentgetawaytype TEXT;

                ALTER TABLE playdays ADD COLUMN status INTEGER;
                ALTER TABLE playdays ADD COLUMN lastupdatetimestamp TIMESTAMP;

                ALTER TABLE playerdayresults ADD COLUMN assistances INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN mvppoints INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN penaltypoints INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN dreamteampoints INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN penaltyfailed INTEGER;
                ALTER TABLE playerdayresults ADD COLUMN penaltystopped INTEGER;

                ALTER TABLE teams ADD COLUMN idgoalkeeper INTEGER;

                ALTER TABLE tournaments ADD COLUMN sponsordata TEXT;
                ALTER TABLE tournaments ADD COLUMN appearancedata TEXT;
                ALTER TABLE tournaments ADD COLUMN notificationflags TEXT;                
                ALTER TABLE tournaments ADD COLUMN sequenceorder INTEGER;
                ALTER TABLE tournaments ADD COLUMN dreamteam TEXT;

                ALTER TABLE tournamentstages ADD COLUMN colorconfig text;  
            ");

            return 33;
        }

        private int Undo32(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"33 -> 32: remove not aquired functionalities");

            conn.Execute(@"
                ALTER TABLE awards DROP COLUMN text1;
                
                ALTER TABLE matches DROP COLUMN visiblehomescore;
                ALTER TABLE matches DROP COLUMN visiblevisitorscore;  

                DROP TABLE matchplayersnotices;
                
                DROP TABLE notices;

                ALTER TABLE notifications DROP COLUMN text3;

                ALTER TABLE organizations DROP COLUMN sponsordata;
                ALTER TABLE organizations DROP COLUMN dpcompanyname;
                ALTER TABLE organizations DROP COLUMN dpcompanyid;
                ALTER TABLE organizations DROP COLUMN dpcompanyaddress;
                ALTER TABLE organizations DROP COLUMN dpcompanyemail;
                ALTER TABLE organizations DROP COLUMN dpcompanyphone;
                ALTER TABLE organizations DROP COLUMN appearancedata;
                ALTER TABLE organizations DROP COLUMN termsversion;
                ALTER TABLE organizations DROP COLUMN paymentgetawaytype;

                ALTER TABLE playdays DROP COLUMN status;
                ALTER TABLE playdays DROP COLUMN lastupdatetimestamp;
                
                ALTER TABLE playerdayresults DROP COLUMN assistances;
                ALTER TABLE playerdayresults DROP COLUMN mvppoints;
                ALTER TABLE playerdayresults DROP COLUMN penaltypoints;
                ALTER TABLE playerdayresults DROP COLUMN dreamteampoints;
                ALTER TABLE playerdayresults DROP COLUMN penaltyfailed;
                ALTER TABLE playerdayresults DROP COLUMN penaltystopped;

                ALTER TABLE teams DROP COLUMN idgoalkeeper;
                
                ALTER TABLE tournaments DROP COLUMN sponsordata;
                ALTER TABLE tournaments DROP COLUMN appearancedata;
                ALTER TABLE tournaments DROP COLUMN notificationflags;
                ALTER TABLE tournaments DROP COLUMN sequenceorder;    
                ALTER TABLE tournaments DROP COLUMN dreamteam;
                
                ALTER TABLE tournamentstages DROP COLUMN colorconfig;            
            ");

            return 32;
        }

        private int Update33(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"33 -> 34: Joker"); // Any missing functionalities
            return 34;
        }

        private int Undo33(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"33 -> 32: remove Joker"); // Any missing functionalities
            return 33;
        }

        private int Update34(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"34 -> 35: Add defaultdateformat ");

            conn.Execute(@"
                ALTER TABLE organizations ADD COLUMN defaultdateformat TEXT;                
            ");

            return 35;
        }

        private int Undo34(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"35 -> 34: Remove defaultdateformat"); // Any missing functionalities

            conn.Execute(@"
                ALTER TABLE organizations DROP COLUMN defaultdateformat;
            ");

            return 34;
        }

        private int Update35(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"35 -> 36: Add tutorials ");

            conn.Execute(@"
                 CREATE TABLE tutorials (
                    id	    SERIAL PRIMARY KEY,
                    language           TEXT NOT NULL,
                    title              TEXT NOT NULL,
                    description                 TEXT,
                    data1                       TEXT,
                    data2                       TEXT,
                    status                   INTEGER,
                    type                     INTEGER,
                    sequenceorder            INTEGER
                );
            ");

            return 36;
        }

        private int Undo35(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"36 -> 35: Remove tutorials"); // Any missing functionalities

            conn.Execute(@"
                 DROP TABLE tutorials;
            ");

            return 35;
        }

        private int Update36(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"36 -> 37: Add matchplayers captain");

            conn.Execute(@"
                ALTER TABLE matchplayers ADD COLUMN captain BOOLEAN;                
            ");

            return 37;
        }

        private int Undo36(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"37 -> 36: Remove matchplayers captain"); 

            conn.Execute(@"
                ALTER TABLE matchplayers DROP COLUMN captain;
            ");

            return 36;
        }

        // Template

        private int Update100(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"");

            conn.Execute(@"
            ");

            return 101;
        }

        private int Undo100(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"");

            conn.Execute(@"
            ");

            return 100;
        }




        // __ Helpers ________________________________________________________


        private int RunUpdate(UpdateMethodDelegate updateMethod)
        {
            if (updateMethod == null) throw new ArgumentNullException("updateMethod");

            using (var conn = GetConn())
            {
                var transaction = conn.BeginTransaction();

                try
                {
                    var result = updateMethod(conn, transaction);

                    SetNewVersion(result, conn, transaction);

                    transaction.Commit();

                    return result;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        public static void UpdateEmailTemplate(IDbConnection c, string templateName, string resourceKey)
        {
            var newValue = PostgresqlDataLayer.ReadResourceFile(resourceKey);
            c.Execute("UPDATE notificationtemplates SET contentTemplate = @value WHERE key = @key AND lang = 'es'", new { value = newValue, key = templateName });
        }

        private void SetNewVersion(int version, IDbConnection conn, IDbTransaction t)
        {
            conn.Execute($"UPDATE version SET v = @version", new { version = version }, t);
        }


        private IDbConnection GetConn()
        {
            return new PostgresqlDataLayer(mDbOptions).GetConn();
        }

        private static void ConsoleLogger(string msg)
        {
            Console.WriteLine(msg);
        }


        private Dictionary<int, UpdateMethodDelegate> mUpdaters = new Dictionary<int, UpdateMethodDelegate>();
        private Dictionary<int, UpdateMethodDelegate> mUndoers = new Dictionary<int, UpdateMethodDelegate>();
        private PostgresqlConfig mDbOptions;

        protected delegate int UpdateMethodDelegate(IDbConnection c, IDbTransaction t);
        protected Action<string> mLogger;
    }


    public class GlobalDataUpdater: DataUpdater
    {
        public GlobalDataUpdater(PostgresqlConfig config) : base(config)
        {

        }

        protected override void Init()
        {
            AddUpdater(1, Update1, Undo1);
        }


        // __ Upgrades ________________________________________________________


        private int Update1(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"1 -> 2: add global admins table");

            conn.Execute(@"
                CREATE TABLE globalAdmins (
	                id				SERIAL PRIMARY KEY,
	                email			TEXT NOT NULL,
	                name			TEXT NOT NULL,
	                password		TEXT NOT NULL,
	                salt			TEXT NOT NULL,
	                level			INTEGER NOT NULL,
	                avatarImgUrl	TEXT,
	                emailConfirmed	BOOLEAN NOT NULL,
	                mobile			TEXT,
	                lang			TEXT
                );

                CREATE UNIQUE INDEX globaladmins_id ON globaladmins (id);
                CREATE UNIQUE INDEX globaladmins_email ON globaladmins (email);

                CREATE TABLE users (
                    id              SERIAL PRIMARY KEY,
                    email                       TEXT,
                    password                    TEXT,
                    salt                        TEXT,
                    emailconfirmed              BOOLEAN
                );
                CREATE UNIQUE INDEX users_id ON globaladmins (id);
                CREATE UNIQUE INDEX users_email ON globaladmins (email);

                ALTER SEQUENCE globaladmins_id_seq RESTART WITH 10000000;
            ");

            return 2;
        }

        private int Undo1(IDbConnection conn, IDbTransaction t)
        {
            mLogger($"2 -> 1: drop global admins table");

            conn.Execute(@"
                DROP TABLE globalAdmins;
                DROP TABLE users;
            ");

            return 1;
        }


    }
}
