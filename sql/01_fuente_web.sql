-- =====================================================================
-- Base de datos FUENTE de reseñas web (simula la base relacional del sitio web de la empresa)
-- El DatabaseExtractor del Worker Service lee de aquí. Conserva los formatos originales (C007, P016, W0001).
-- =====================================================================
CREATE DATABASE IF NOT EXISTS opiniones_web
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE opiniones_web;

CREATE TABLE IF NOT EXISTS resenas (
    id_resena    VARCHAR(10)  NOT NULL,
    id_cliente   VARCHAR(10)  NOT NULL,
    id_producto  VARCHAR(10)  NOT NULL,
    fecha        DATE         NOT NULL,
    comentario   TEXT         NOT NULL,
    rating       TINYINT      NOT NULL,
    PRIMARY KEY (id_resena),
    KEY ix_resenas_fecha (fecha),
    CONSTRAINT ck_resenas_rating CHECK (rating BETWEEN 1 AND 5)
);
