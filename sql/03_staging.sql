-- =====================================================================
-- Zona de aterrizaje (staging) del proceso de extracción.
-- Guarda los datos tal como se extrajeron, SIN claves foráneas: la integridad referencial se valida
-- en la etapa de carga hacia la base analítica. Cada fila lleva el id de la ejecución que la trajo.
-- =====================================================================
CREATE DATABASE IF NOT EXISTS opiniones_staging
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE opiniones_staging;

CREATE TABLE IF NOT EXISTS stg_ejecucion (
    id_ejecucion CHAR(36)    NOT NULL PRIMARY KEY,
    inicio       DATETIME    NOT NULL,
    fin          DATETIME    NULL,
    estado       VARCHAR(20) NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_rechazo (
    id_rechazo     INT AUTO_INCREMENT PRIMARY KEY,
    id_ejecucion   CHAR(36)     NOT NULL,
    fuente         VARCHAR(40)  NOT NULL,
    fila           INT          NOT NULL,
    motivo         VARCHAR(255) NOT NULL,
    fecha_registro DATETIME     NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_cliente (
    id_cliente       VARCHAR(20)  NOT NULL,
    nombre           VARCHAR(100) NOT NULL,
    email            VARCHAR(150) NOT NULL,
    id_ejecucion     CHAR(36)     NOT NULL,
    fecha_extraccion DATETIME     NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_producto (
    id_producto      VARCHAR(20)  NOT NULL,
    nombre           VARCHAR(100) NOT NULL,
    categoria        VARCHAR(50)  NOT NULL,
    id_ejecucion     CHAR(36)     NOT NULL,
    fecha_extraccion DATETIME     NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_encuesta (
    id_opinion       VARCHAR(20) NOT NULL,
    id_cliente       VARCHAR(20) NOT NULL,
    id_producto      VARCHAR(20) NOT NULL,
    fecha            DATE        NOT NULL,
    comentario       TEXT        NOT NULL,
    clasificacion    VARCHAR(20) NOT NULL,
    puntaje          TINYINT     NOT NULL,
    fuente           VARCHAR(30) NOT NULL,
    id_ejecucion     CHAR(36)    NOT NULL,
    fecha_extraccion DATETIME    NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_resena (
    id_resena        VARCHAR(20) NOT NULL,
    id_cliente       VARCHAR(20) NOT NULL,
    id_producto      VARCHAR(20) NOT NULL,
    fecha            DATE        NOT NULL,
    comentario       TEXT        NOT NULL,
    rating           TINYINT     NOT NULL,
    id_ejecucion     CHAR(36)    NOT NULL,
    fecha_extraccion DATETIME    NOT NULL
);

CREATE TABLE IF NOT EXISTS stg_comentario_social (
    id_comentario    VARCHAR(20) NOT NULL,
    id_cliente       VARCHAR(20) NULL,
    id_producto      VARCHAR(20) NOT NULL,
    canal            VARCHAR(20) NOT NULL,
    fecha            DATE        NOT NULL,
    comentario       TEXT        NOT NULL,
    id_ejecucion     CHAR(36)    NOT NULL,
    fecha_extraccion DATETIME    NOT NULL
);
