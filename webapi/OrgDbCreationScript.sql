CREATE TABLE version (
	v			INTEGER
);

INSERT INTO version(v) VALUES (1);


CREATE TABLE userOrganization (
	idUser		INTEGER,
	email		TEXT,
	organizationName	TEXT
);

CREATE UNIQUE INDEX userorg_email ON userOrganization (email);


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