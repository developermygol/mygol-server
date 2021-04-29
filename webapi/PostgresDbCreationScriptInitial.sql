CREATE TABLE version (
	v			INTEGER
);

CREATE TABLE organizations (
	id					SERIAL PRIMARY KEY,
	name				TEXT NOT NULL, 
	logoImgUrl			TEXT,
	backgroundImgUrl	TEXT,
	address1			TEXT, 
	address2			TEXT,
	address3			TEXT,
	motto				TEXT,
	social1				TEXT,
	social1link			TEXT,
	social2				TEXT,
	social2link			TEXT,
	social3				TEXT,
	social3link			TEXT,
	social4				TEXT,
	social4link			TEXT,
	social5				TEXT, 
	social5link			TEXT,
	social6				TEXT, 
	social6link			TEXT,
	paymentKey			TEXT,
	paymentKeyPublic	TEXT,
	paymentDescription	TEXT,
	paymentCurrency		TEXT,
	defaultLang			TEXT
);

INSERT INTO organizations (name) VALUES ('Organization name not set');


CREATE TABLE seasons (
	id		SERIAL PRIMARY KEY, 
	name	TEXT NOT NULL, 
	startDate	TIMESTAMP, 
	endDate		TIMESTAMP
);

CREATE UNIQUE INDEX seasons_id ON seasons (id);


CREATE TABLE tournamentmodes (
	id			SERIAL PRIMARY KEY, 
	name		TEXT NOT NULL, 
	numPlayers	INTEGER NOT NULL
);

CREATE UNIQUE INDEX tournamentmodes_id ON tournamentmodes (id);

INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F5', 5);
INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F6', 6);
INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F7', 7);
INSERT INTO tournamentmodes (name, numPlayers) VALUES ('F11', 11);


CREATE TABLE categories (
	id		SERIAL PRIMARY KEY, 
	name	TEXT NOT NULL
);

CREATE UNIQUE INDEX categories_id ON categories (id);


CREATE TABLE tournaments (
	id					SERIAL PRIMARY KEY, 
	name				TEXT NOT NULL,
	type				INTEGER NOT NULL, 
	status				INTEGER NOT NULL, 

	idSeason			INTEGER NOT NULL,
	idCategory			INTEGER, 
	idTournamentMode	INTEGER, 

	logoImgUrl			TEXT,
	visible				BOOLEAN DEFAULT 'f'
);

CREATE UNIQUE INDEX tournaments_id ON tournaments (id);


CREATE TABLE fields (
	id			SERIAL PRIMARY KEY, 
	name		text NOT NULL, 
	address		text, 
	imgUrl		text,
	location	text, 
	description text
);

CREATE UNIQUE INDEX fields_id ON fields (id);


CREATE TABLE teams (
	id			SERIAL PRIMARY KEY, 
	name		text NOT NULL, 
	keyName		text NOT NULL,
	logoImgUrl	text,
	idField		integer,
	status		integer,
	logoConfig  text, 
	idTactic	integer,
	apparelConfig text,
	teamImgUrl	text,
	teamImgUrl2 text, 
	teamImgUrl3 text,
	prefTime	timestamp
);

CREATE UNIQUE INDEX teams_id ON teams (id);


CREATE TABLE tournamentstages (
	id				SERIAL PRIMARY KEY, 
	idTournament	INTEGER NOT NULL, 
	name			TEXT,
	description		TEXT,
	type			INTEGER,
	status			INTEGER DEFAULT 1,
	sequenceOrder	INTEGER NOT NULL,
	classificationCriteria TEXT 
);

CREATE INDEX tournamentstages_id ON tournamentstages (id);
CREATE INDEX tournamentstages_tournament ON tournamentstages (idTournament);

CREATE TABLE stagegroups (
	id				SERIAL PRIMARY KEY, 
	idStage			INTEGER NOT NULL, 
	idTournament	INTEGER NOT NULL, 
	name			TEXT NOT NULL, 
	description		TEXT,
	numTeams		INTEGER NOT NULL, 
	numRounds		INTEGER, 
	flags			INTEGER, 
	sequenceOrder	INTEGER,
	colorConfig		TEXT
);

CREATE INDEX stagegroups_id ON stagegroups (id);
CREATE INDEX stagegroups_tournament	ON stagegroups (idTournament);


CREATE TABLE tournamentteams (
	idTeam			INTEGER NOT NULL, 
	idTournament	INTEGER NOT NULL
);

CREATE INDEX tournamentteams_tournaments ON tournamentteams (idTournament);

CREATE TABLE teamgroups (
	id				SERIAL PRIMARY KEY, 
	idTeam			INTEGER NOT NULL, 
	idTournament	INTEGER NOT NULL, 
	idStage			INTEGER NOT NULL, 
	idGroup			INTEGER NOT NULL, 
	sequenceOrder	INTEGER DEFAULT -1
);

CREATE INDEX teamgroups_id ON teamgroups (id);
CREATE INDEX teamgroups_tournaments ON teamgroups (idTournament);


CREATE TABLE playdays (
	id		SERIAL PRIMARY KEY,
	idTournament INTEGER,
	idStage		 INTEGER,
	idGroup		 INTEGER,
	name	TEXT NOT NULL,
	dates	TEXT NOT NULL,	
	sequenceOrder INTEGER
);

CREATE INDEX playdays_id ON playdays (id);
CREATE INDEX playdays_idtournament ON playdays (idTournament);
CREATE INDEX playdays_order ON playdays (sequenceOrder);


CREATE TABLE users (
	id				SERIAL PRIMARY KEY,
	email			TEXT NOT NULL,
	name			TEXT NOT NULL,
	password		TEXT NOT NULL,
	salt			TEXT NOT NULL,
	level			INTEGER NOT NULL,
	avatarImgUrl	TEXT,
	emailConfirmed	BOOLEAN NOT NULL,
	mobile			TEXT,
	lang			TEXT,
	notificationPushToken	TEXT
);

CREATE INDEX users_id ON users (id);
CREATE INDEX users_email ON users (email);



CREATE TABLE uploads (
	id				SERIAL PRIMARY KEY, 
	type			INTEGER NOT NULL, 
	idobject		INTEGER NOT NULL,
	repositoryPath	TEXT
);

CREATE INDEX uploads_id ON uploads (id);
CREATE INDEX uploads_object ON uploads (type, idobject);


CREATE TABLE players (
    id              SERIAL PRIMARY KEY,
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
    instagramKey    TEXT,

	address1		TEXT,
	address2		TEXT,
	city			TEXT,
	state			TEXT,
	cp				TEXT,
	country			TEXT,

	idCardNumber	TEXT,
	enrollmentStep  INTEGER,
	approved		BOOLEAN,
	idPhotoImgUrl	TEXT,
	idCard1ImgUrl	TEXT,
	idCard2ImgUrl	TEXT
);

CREATE INDEX players_id ON players (id);


CREATE TABLE teamplayers (
    idTeam          INTEGER NOT NULL,
    idPlayer        INTEGER NOT NULL,
    status          INTEGER,
	apparelNumber   INTEGER,
    fieldPosition   INTEGER,
    fieldSide       INTEGER,
	isTeamAdmin		BOOLEAN,
	idTacticPosition INTEGER,
	enrollmentStep	INTEGER, 
	enrollmentData	TEXT,
	enrollmentDate  TIMESTAMP,
	enrollmentPaymentData	TEXT
);

CREATE INDEX teamplayers_team ON teamplayers (idteam);
CREATE INDEX teamplayers_player ON teamplayers (idplayer);


CREATE TABLE textblobs (
    id          SERIAL PRIMARY KEY,
    type        INTEGER NOT NULL, 
    idObject    INTEGER NOT NULL,
    data        TEXT
);

CREATE INDEX textblobs_id ON textblobs (id);
CREATE INDEX textblobs_object ON textblobs (type, idObject);

CREATE TABLE notifications (
	id			SERIAL PRIMARY KEY,
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


CREATE TABLE secureuploads (
	id				SERIAL PRIMARY KEY, 
	idUser			INTEGER NOT NULL, 
	type			INTEGER NOT NULL, 
	description		TEXT,
	originalFileName TEXT,
	idSecureUpload	INTEGER,
	idUpload		INTEGER
);

CREATE INDEX secureuploads_id ON secureuploads (id);
CREATE INDEX secureuploads_iduser ON secureuploads (idUser);


CREATE TABLE userevents (
	id				SERIAL PRIMARY KEY,
	idUser			INTEGER NOT NULL,
	type			INTEGER NOT NULL,
	description		TEXT,
	timestamp		TIMESTAMP NOT NULL,
	idSecureUpload	INTEGER,
	idUpload		INTEGER,
	idCreator		INTEGER NOT NULL,
	data1			TEXT,
	data2			TEXT,
	data3			TEXT
);

CREATE INDEX userevents_id ON userevents (id);
CREATE INDEX userevents_iduser ON userevents (idUser);


CREATE TABLE matches (
	id				SERIAL PRIMARY KEY,
	idTournament	INTEGER NOT NULL,
	idStage			INTEGER, 
	idGroup			INTEGER,
	idHomeTeam		INTEGER NOT NULL,
	idVisitorTeam	INTEGER NOT NULL,
	idDay			INTEGER,
	idField			INTEGER,
	startTime		TIMESTAMP,
	duration		INTEGER,
	status			INTEGER,
	homeScore		INTEGER, 
	visitorScore	INTEGER,
	comments		TEXT, 
	videoUrl		TEXT,
	homeTeamDescription		TEXT, 
	visitorTeamDescription	TEXT
);

CREATE INDEX matches_id ON matches (id);
CREATE INDEX matches_idtournament ON matches (idTournament);
CREATE INDEX matches_idhometeam ON matches (idHomeTeam);
CREATE INDEX matches_idvisitorteam ON matches (idvisitorteam);
CREATE INDEX matches_idfield ON matches (idfield);
CREATE INDEX matches_idday ON matches (idday);
CREATE INDEX matches_idStage ON matches (idstage);
CREATE INDEX matches_idGroup ON matches (idgroup);


CREATE TABLE matchreferees (
	idMatch			INTEGER NOT NULL,
	idUser			INTEGER NOT NULL,
	role			INTEGER
);

CREATE INDEX matchreferees_idmatch ON matchreferees (idMatch);
CREATE INDEX matchreferees_iduser ON matchreferees (idUser);


CREATE TABLE matchevents (
	id				SERIAL PRIMARY KEY,
	idMatch			INTEGER NOT NULL,
	idDay			INTEGER NOT NULL,
	idCreator		INTEGER NOT NULL, 
	idTeam			INTEGER, 
	idPlayer		INTEGER,
	timeStamp		TIMESTAMP NOT NULL, 
	type			INTEGER NOT NULL,
	matchMinute		INTEGER,
	intData1		INTEGER,
	intData2		INTEGER,
	intData3		INTEGER,
	intData4		INTEGER,
	intData5		INTEGER,
	intData6		INTEGER,
	data1			TEXT, 
	data2			TEXT,
	isAutomatic		BOOLEAN DEFAULT 'f'
);

CREATE INDEX matchevents_id ON matchevents (id);
CREATE INDEX matchevents_idmatch ON matchevents (idMatch);
CREATE INDEX matchevents_idteam ON matchevents (idTeam);
CREATE INDEX matchevents_idplayer ON matchevents (idPlayer);
--CREATE INDEX matchevents_idcreator ON matchevents (idCreator);




-- -- Stats related tables ----------------------------------------------------



-- Holds player data associated to the match (Match results, non-cumulative) 
CREATE TABLE matchplayers (
	idMatch			INTEGER NOT NULL,
	idPlayer		INTEGER NOT NULL,
	idTeam			INTEGER NOT NULL,
	idUser			INTEGER NOT NULL,
	idDay			INTEGER NOT NULL,
	apparelNumber	INTEGER,
	status			INTEGER
);

CREATE INDEX matchplayers_idmatch ON matchplayers (idPlayer);
CREATE INDEX matchplayers_idplayer ON matchplayers (idMatch);
CREATE INDEX matchplayers_idday ON matchplayers (idDay);
CREATE INDEX matchplayers_idteam ON matchplayers (idTeam);


-- Holds team results for the day + tournament (tournament cummulative)

-- Duplicating a little bit from the tournament teams table, but enough to speed up stats queries.
-- With this we can have queries for
--   * Tournament classification in any given day (SELECT * FROM teamdayresults WHERE idTournament = @tournamentId AND idDay = @dayId)
--   * Team historic stats in this tournament (SELECT * FROM teamdayresults WHERE idTeam = @teamId AND idTournament = @tournamentId)
CREATE TABLE teamdayresults (
	idDay			INTEGER NOT NULL,
	idTeam			INTEGER NOT NULL, 
	idTournament	INTEGER NOT NULL,
	idStage			INTEGER,
	idGroup			INTEGER,
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
	ranking3		INTEGER, 
	tournamentPoints INTEGER
);

CREATE INDEX teamdayresults_day ON teamdayresults (idDay);
CREATE INDEX teamdayresults_teams ON teamdayresults (idTeam);
CREATE INDEX teamdayresults_tournaments ON teamdayresults (idTournament);


-- Holds player stats associated to the day + tournament 
CREATE TABLE playerdayresults (
	idPlayer		INTEGER NOT NULL,
	idTournament	INTEGER NOT NULL, 
	idTeam			INTEGER, 
	idStage			INTEGER,
	idGroup			INTEGER,
	idDay			INTEGER NOT NULL,
	idUser			INTEGER NOT NULL,

	gamesPlayed		INTEGER,
	gamesWon		INTEGER,
	gamesDraw		INTEGER,
	gamesLost		INTEGER,

	points			INTEGER,
	pointsAgainst	INTEGER,
	pointsInOwn		INTEGER,

	cardsType1		INTEGER,
	cardsType2		INTEGER,
	cardsType3		INTEGER,
	cardsType4		INTEGER,
	cardsType5		INTEGER,

	ranking1		INTEGER, 
	ranking2		INTEGER,
	ranking3		INTEGER, 
	ranking4		INTEGER, 
	ranking5		INTEGER,

	data1			INTEGER,	-- Num. of accumulated yellow cards for automatic cycle sanctions, filled with each yellow card event, but discounted with sanction rules. Maybe the generated sanction can generate a hidden event that discounts yellow cards in this field. 
	data2			INTEGER,
	data3			INTEGER,
	data4			INTEGER,
	data5			INTEGER
);

CREATE INDEX playerdayresults_idday ON playerdayresults (idDay);
CREATE INDEX playerdayresults_idplayer ON playerdayresults (idPlayer);
CREATE INDEX playerdayresults_idteam ON playerdayresults (idTeam);
CREATE INDEX playerdayresults_idtournament ON playerdayresults (idTournament);
CREATE UNIQUE INDEX playerdayresults_key ON playerdayresults (idDay, idPlayer, idTeam, idTournament, idStage, idGroup);

CREATE TABLE contents (
	id				SERIAL PRIMARY KEY,
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
	userData4		TEXT,
	
	idCategory		INTEGER, 
	videoUrl		TEXT, 
	idTournament	INTEGER, 
	idTeam			INTEGER, 
	layoutType		TEXT, 
	categoryPosition1   INTEGER, 
	categoryPosition2	INTEGER
);

CREATE INDEX contents_id ON contents (id);
CREATE INDEX contents_tournament ON contents (idTournament);
CREATE INDEX contents_team ON contents (idTeam);

CREATE TABLE contentcategories (
	id				SERIAL PRIMARY KEY, 
	name			TEXT
);

CREATE UNIQUE INDEX contentcategories_id ON contentcategories(id);

INSERT INTO contentcategories (name) VALUES ('MENU');


CREATE TABLE notificationtemplates (
	id				SERIAL PRIMARY KEY, 
	lang			TEXT, 
	key				TEXT, 
	title			TEXT,
	contentTemplate	TEXT
);

CREATE UNIQUE INDEX notificationtemplates_id ON notificationtemplates (id);


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
	altText			TEXT, 
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


CREATE TABLE paymentconfigs (
	id				SERIAL PRIMARY KEY, 
	idOrganization	INTEGER DEFAULT -1, 
	idTournament	INTEGER DEFAULT -1, 
	idTeam			INTEGER DEFAULT -1,
	idUser			INTEGER DEFAULT -1,
	enrollmentWorkflow		TEXT,
	gatewayConfig			TEXT
);

CREATE UNIQUE INDEX paymentconfig_id ON paymentconfigs (id);
CREATE INDEX paymentconfig_org ON paymentconfigs (idOrganization);
CREATE INDEX paymentconfig_team ON paymentconfigs (idTeam);
CREATE INDEX paymentconfig_tournament ON paymentconfigs (idTournament);

INSERT INTO paymentconfigs (idOrganization, enrollmentWorkflow) VALUES (1, '[]');


CREATE TABLE sanctions (
	id			SERIAL PRIMARY KEY,
	idPlayer	INTEGER DEFAULT -1,
	idTeam		INTEGER DEFAULT -1,
	idMatch		INTEGER DEFAULT -1,
	idTournament	INTEGER DEFAULT -1,
	idDay		INTEGER DEFAULT -1,
	title		TEXT,
	status		INTEGER,
	startDate	TIMESTAMP,
	isAutomatic	BOOLEAN DEFAULT 'f',
	idPayment	INTEGER DEFAULT -1,
	idSanctionConfigRuleId INTEGER DEFAULT -1,
	type		INTEGER DEFAULT 1,					-- Player (0) or Team (2)
	
	numMatches				INTEGER DEFAULT 0,
	lostMatchPenalty		INTEGER DEFAULT 0,
	tournamentPointsPenalty	INTEGER DEFAULT 0,

	sanctionMatchEvents		TEXT
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
	idTeam		INTEGER DEFAULT -1,
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
	idTournament	INTEGER DEFAULT -1,
	isLast			BOOLEAN DEFAULT 'f',
	isAutomatic		BOOLEAN DEFAULT 'f'
);

CREATE UNIQUE INDEX sanctionmatches_id ON sanctionmatches (id);
CREATE INDEX sanctionmatches_idmatch ON sanctionmatches (idmatch);
CREATE INDEX sanctionmatches_idsanction ON sanctionmatches (idsanction);


