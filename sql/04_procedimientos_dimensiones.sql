SET NAMES utf8mb4;

CREATE DATABASE IF NOT EXISTS opiniones_dw
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE opiniones_dw;

CREATE TABLE IF NOT EXISTS dim_tiempo (
    sk_tiempo   INT         NOT NULL PRIMARY KEY,
    fecha       DATE        NOT NULL UNIQUE,
    anio        SMALLINT    NOT NULL,
    trimestre   TINYINT     NOT NULL,
    mes         TINYINT     NOT NULL,
    nombre_mes  VARCHAR(12) NOT NULL,
    dia         TINYINT     NOT NULL,
    dia_semana  VARCHAR(12) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS dim_producto (
    sk_producto INT          NOT NULL PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL,
    categoria   VARCHAR(50)  NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS dim_cliente (
    sk_cliente  INT          NOT NULL PRIMARY KEY,
    nombre      VARCHAR(100) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS dim_fuente (
    sk_fuente   INT         NOT NULL PRIMARY KEY,
    tipo_fuente VARCHAR(20) NOT NULL,
    canal       VARCHAR(20) NOT NULL UNIQUE,
    ambito      VARCHAR(10) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS dim_sentimiento (
    sk_sentimiento INT         NOT NULL PRIMARY KEY,
    nombre         VARCHAR(20) NOT NULL UNIQUE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS sp_cargar_dim_sentimiento;
DELIMITER $$
CREATE PROCEDURE sp_cargar_dim_sentimiento(
    IN  p_id_ejecucion CHAR(36),
    OUT p_leidas       INT,
    OUT p_insertadas   INT,
    OUT p_actualizadas INT,
    OUT p_rechazadas   INT
)
BEGIN
    DECLARE v_act INT DEFAULT 0;
    DECLARE v_ins INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET p_leidas = 0;
    SET p_insertadas = 0;
    SET p_actualizadas = 0;
    SET p_rechazadas = 0;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_sentimiento;
    CREATE TEMPORARY TABLE tmp_dim_sentimiento (
        sk_sentimiento INT PRIMARY KEY,
        nombre         VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO tmp_dim_sentimiento (sk_sentimiento, nombre) VALUES
        (1, 'Positiva'),
        (2, 'Neutra'),
        (3, 'Negativa'),
        (4, 'Sin clasificar');

    SELECT COUNT(*) INTO p_leidas FROM tmp_dim_sentimiento;

    START TRANSACTION;

    UPDATE dim_sentimiento d
    JOIN tmp_dim_sentimiento t ON t.sk_sentimiento = d.sk_sentimiento
    SET d.nombre = t.nombre
    WHERE d.nombre <> t.nombre;
    SET v_act = ROW_COUNT();

    INSERT INTO dim_sentimiento (sk_sentimiento, nombre)
    SELECT t.sk_sentimiento, t.nombre
    FROM tmp_dim_sentimiento t
    LEFT JOIN dim_sentimiento d ON d.sk_sentimiento = t.sk_sentimiento
    WHERE d.sk_sentimiento IS NULL;
    SET v_ins = ROW_COUNT();

    COMMIT;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_sentimiento;
    SET p_actualizadas = v_act;
    SET p_insertadas   = v_ins;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_cargar_dim_fuente;
DELIMITER $$
CREATE PROCEDURE sp_cargar_dim_fuente(
    IN  p_id_ejecucion CHAR(36),
    OUT p_leidas       INT,
    OUT p_insertadas   INT,
    OUT p_actualizadas INT,
    OUT p_rechazadas   INT
)
BEGIN
    DECLARE v_act INT DEFAULT 0;
    DECLARE v_ins INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET p_leidas = 0;
    SET p_insertadas = 0;
    SET p_actualizadas = 0;
    SET p_rechazadas = 0;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_fuente;
    CREATE TEMPORARY TABLE tmp_dim_fuente (
        sk_fuente   INT PRIMARY KEY,
        tipo_fuente VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        canal       VARCHAR(20) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        ambito      VARCHAR(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO tmp_dim_fuente (sk_fuente, tipo_fuente, canal, ambito) VALUES
        (1, 'Encuesta',   'EncuestaInterna', 'Interno'),
        (2, 'Reseña web', 'SitioWeb',        'Interno'),
        (3, 'Red social', 'Facebook',        'Externo'),
        (4, 'Red social', 'Instagram',       'Externo'),
        (5, 'Red social', 'Twitter',         'Externo');

    SELECT COUNT(*) INTO p_leidas FROM tmp_dim_fuente;

    START TRANSACTION;

    UPDATE dim_fuente d
    JOIN tmp_dim_fuente t ON t.sk_fuente = d.sk_fuente
    SET d.tipo_fuente = t.tipo_fuente,
        d.canal       = t.canal,
        d.ambito      = t.ambito
    WHERE d.tipo_fuente <> t.tipo_fuente
       OR d.canal <> t.canal
       OR d.ambito <> t.ambito;
    SET v_act = ROW_COUNT();

    INSERT INTO dim_fuente (sk_fuente, tipo_fuente, canal, ambito)
    SELECT t.sk_fuente, t.tipo_fuente, t.canal, t.ambito
    FROM tmp_dim_fuente t
    LEFT JOIN dim_fuente d ON d.sk_fuente = t.sk_fuente
    WHERE d.sk_fuente IS NULL;
    SET v_ins = ROW_COUNT();

    COMMIT;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_fuente;
    SET p_actualizadas = v_act;
    SET p_insertadas   = v_ins;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_cargar_dim_tiempo;
DELIMITER $$
CREATE PROCEDURE sp_cargar_dim_tiempo(
    IN  p_id_ejecucion CHAR(36),
    IN  p_desde        DATE,
    IN  p_hasta        DATE,
    OUT p_leidas       INT,
    OUT p_insertadas   INT,
    OUT p_actualizadas INT,
    OUT p_rechazadas   INT
)
BEGIN
    DECLARE v_desde DATE;
    DECLARE v_hasta DATE;
    DECLARE v_cur   DATE;
    DECLARE v_sk    INT;
    DECLARE v_mes   TINYINT;
    DECLARE v_dow   TINYINT;
    DECLARE v_nmes  VARCHAR(12);
    DECLARE v_ndia  VARCHAR(12);
    DECLARE v_act   INT DEFAULT 0;
    DECLARE v_ins   INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET p_leidas = 0;
    SET p_insertadas = 0;
    SET p_actualizadas = 0;
    SET p_rechazadas = 0;

    IF p_desde IS NOT NULL AND p_hasta IS NOT NULL THEN
        SET v_desde = p_desde;
        SET v_hasta = p_hasta;
    ELSE
        SELECT MIN(f), MAX(f) INTO v_desde, v_hasta
        FROM (
            SELECT fecha AS f FROM opiniones_staging.stg_encuesta
            UNION ALL
            SELECT fecha AS f FROM opiniones_staging.stg_resena
            UNION ALL
            SELECT fecha AS f FROM opiniones_staging.stg_comentario_social
        ) fechas;

        IF v_desde IS NULL OR v_hasta IS NULL THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'No hay fechas en staging ni rango explícito (p_desde/p_hasta) para poblar dim_tiempo.';
        END IF;

        SET v_desde = COALESCE(p_desde, MAKEDATE(YEAR(v_desde), 1));
        SET v_hasta = COALESCE(p_hasta, STR_TO_DATE(CONCAT(YEAR(v_hasta), '-12-31'), '%Y-%m-%d'));
    END IF;

    IF v_desde > v_hasta THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rango de fechas inválido en sp_cargar_dim_tiempo: p_desde es posterior a p_hasta.';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_tiempo;
    CREATE TEMPORARY TABLE tmp_dim_tiempo (
        sk_tiempo  INT PRIMARY KEY,
        fecha      DATE NOT NULL,
        anio       SMALLINT NOT NULL,
        trimestre  TINYINT NOT NULL,
        mes        TINYINT NOT NULL,
        nombre_mes VARCHAR(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        dia        TINYINT NOT NULL,
        dia_semana VARCHAR(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    SET v_cur = v_desde;
    WHILE v_cur <= v_hasta DO
        SET v_sk  = YEAR(v_cur) * 10000 + MONTH(v_cur) * 100 + DAY(v_cur);
        SET v_mes = MONTH(v_cur);
        SET v_dow = WEEKDAY(v_cur) + 1;

        SET v_nmes = CASE v_mes
            WHEN 1 THEN 'Enero'      WHEN 2 THEN 'Febrero'   WHEN 3 THEN 'Marzo'
            WHEN 4 THEN 'Abril'      WHEN 5 THEN 'Mayo'      WHEN 6 THEN 'Junio'
            WHEN 7 THEN 'Julio'      WHEN 8 THEN 'Agosto'    WHEN 9 THEN 'Septiembre'
            WHEN 10 THEN 'Octubre'   WHEN 11 THEN 'Noviembre' ELSE 'Diciembre'
        END;

        SET v_ndia = CASE v_dow
            WHEN 1 THEN 'Lunes'      WHEN 2 THEN 'Martes'    WHEN 3 THEN 'Miércoles'
            WHEN 4 THEN 'Jueves'     WHEN 5 THEN 'Viernes'   WHEN 6 THEN 'Sábado'
            ELSE 'Domingo'
        END;

        INSERT INTO tmp_dim_tiempo (
            sk_tiempo, fecha, anio, trimestre, mes, nombre_mes, dia, dia_semana
        ) VALUES (
            v_sk, v_cur, YEAR(v_cur), QUARTER(v_cur), v_mes, v_nmes, DAY(v_cur), v_ndia
        );

        SET v_cur = DATE_ADD(v_cur, INTERVAL 1 DAY);
    END WHILE;

    SELECT COUNT(*) INTO p_leidas FROM tmp_dim_tiempo;

    START TRANSACTION;

    UPDATE dim_tiempo d
    JOIN tmp_dim_tiempo t ON t.sk_tiempo = d.sk_tiempo
    SET d.fecha      = t.fecha,
        d.anio       = t.anio,
        d.trimestre  = t.trimestre,
        d.mes        = t.mes,
        d.nombre_mes = t.nombre_mes,
        d.dia        = t.dia,
        d.dia_semana = t.dia_semana
    WHERE d.fecha <> t.fecha
       OR d.nombre_mes <> t.nombre_mes
       OR d.dia_semana <> t.dia_semana;
    SET v_act = ROW_COUNT();

    INSERT INTO dim_tiempo (
        sk_tiempo, fecha, anio, trimestre, mes, nombre_mes, dia, dia_semana
    )
    SELECT t.sk_tiempo, t.fecha, t.anio, t.trimestre, t.mes, t.nombre_mes, t.dia, t.dia_semana
    FROM tmp_dim_tiempo t
    LEFT JOIN dim_tiempo d ON d.sk_tiempo = t.sk_tiempo
    WHERE d.sk_tiempo IS NULL;
    SET v_ins = ROW_COUNT();

    COMMIT;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_tiempo;
    SET p_actualizadas = v_act;
    SET p_insertadas   = v_ins;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_cargar_dim_producto;
DELIMITER $$
CREATE PROCEDURE sp_cargar_dim_producto(
    IN  p_id_ejecucion CHAR(36),
    OUT p_leidas       INT,
    OUT p_insertadas   INT,
    OUT p_actualizadas INT,
    OUT p_rechazadas   INT
)
BEGIN
    DECLARE v_act INT DEFAULT 0;
    DECLARE v_ins INT DEFAULT 0;
    DECLARE v_rec INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET p_leidas = 0;
    SET p_insertadas = 0;
    SET p_actualizadas = 0;
    SET p_rechazadas = 0;

    DROP TEMPORARY TABLE IF EXISTS tmp_stg_prod;
    CREATE TEMPORARY TABLE tmp_stg_prod (
        rn          INT AUTO_INCREMENT PRIMARY KEY,
        id_producto VARCHAR(20)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        nombre      VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        categoria   VARCHAR(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        es_valido   TINYINT(1)   NOT NULL,
        sk          INT          NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO tmp_stg_prod (id_producto, nombre, categoria, es_valido, sk)
    SELECT
        TRIM(id_producto),
        TRIM(nombre),
        TRIM(categoria),
        IF(TRIM(id_producto) REGEXP '^[1-9][0-9]{0,8}$' AND TRIM(nombre) <> '' AND TRIM(categoria) <> '', 1, 0),
        IF(TRIM(id_producto) REGEXP '^[1-9][0-9]{0,8}$', CAST(TRIM(id_producto) AS UNSIGNED), NULL)
    FROM opiniones_staging.stg_producto;

    SELECT COUNT(*) INTO p_leidas FROM tmp_stg_prod;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_producto;
    CREATE TEMPORARY TABLE tmp_dim_producto (
        sk_producto INT PRIMARY KEY,
        nombre      VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        categoria   VARCHAR(50)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT IGNORE INTO tmp_dim_producto (sk_producto, nombre, categoria)
    SELECT sk, nombre, categoria
    FROM tmp_stg_prod
    WHERE es_valido = 1
    ORDER BY rn;

    START TRANSACTION;

    INSERT INTO opiniones_staging.stg_rechazo (id_ejecucion, fuente, fila, motivo, fecha_registro)
    SELECT
        p_id_ejecucion,
        'DW: dim_producto',
        s.rn,
        CASE
            WHEN s.es_valido = 0 THEN CONCAT('id_producto inválido o campos vacíos: "', s.id_producto, '"')
            ELSE CONCAT('id_producto duplicado en staging: ', s.id_producto)
        END,
        UTC_TIMESTAMP()
    FROM tmp_stg_prod s
    WHERE s.es_valido = 0
       OR s.rn > (
           SELECT MIN(t2.rn_calc) FROM (
               SELECT TRIM(id_producto) AS pid, ROW_NUMBER() OVER () AS rn_calc
               FROM opiniones_staging.stg_producto
           ) t2 WHERE t2.pid = s.id_producto
       );
    SET v_rec = ROW_COUNT();

    UPDATE dim_producto d
    JOIN tmp_dim_producto t ON t.sk_producto = d.sk_producto
    SET d.nombre    = t.nombre,
        d.categoria = t.categoria
    WHERE d.nombre <> t.nombre OR d.categoria <> t.categoria;
    SET v_act = ROW_COUNT();

    INSERT INTO dim_producto (sk_producto, nombre, categoria)
    SELECT t.sk_producto, t.nombre, t.categoria
    FROM tmp_dim_producto t
    LEFT JOIN dim_producto d ON d.sk_producto = t.sk_producto
    WHERE d.sk_producto IS NULL;
    SET v_ins = ROW_COUNT();

    COMMIT;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_producto;
    DROP TEMPORARY TABLE IF EXISTS tmp_stg_prod;
    SET p_actualizadas = v_act;
    SET p_insertadas   = v_ins;
    SET p_rechazadas   = v_rec;
END$$
DELIMITER ;

DROP PROCEDURE IF EXISTS sp_cargar_dim_cliente;
DELIMITER $$
CREATE PROCEDURE sp_cargar_dim_cliente(
    IN  p_id_ejecucion CHAR(36),
    OUT p_leidas       INT,
    OUT p_insertadas   INT,
    OUT p_actualizadas INT,
    OUT p_rechazadas   INT
)
BEGIN
    DECLARE v_act INT DEFAULT 0;
    DECLARE v_ins INT DEFAULT 0;
    DECLARE v_rec INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET p_leidas = 0;
    SET p_insertadas = 0;
    SET p_actualizadas = 0;
    SET p_rechazadas = 0;

    DROP TEMPORARY TABLE IF EXISTS tmp_stg_cli;
    CREATE TEMPORARY TABLE tmp_stg_cli (
        rn         INT AUTO_INCREMENT PRIMARY KEY,
        id_cliente VARCHAR(20)  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        nombre     VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
        es_valido  TINYINT(1)   NOT NULL,
        sk         INT          NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO tmp_stg_cli (id_cliente, nombre, es_valido, sk)
    SELECT
        TRIM(id_cliente),
        TRIM(nombre),
        IF(TRIM(id_cliente) REGEXP '^[1-9][0-9]{0,8}$' AND TRIM(nombre) <> '', 1, 0),
        IF(TRIM(id_cliente) REGEXP '^[1-9][0-9]{0,8}$', CAST(TRIM(id_cliente) AS UNSIGNED), NULL)
    FROM opiniones_staging.stg_cliente;

    SELECT COUNT(*) + 1 INTO p_leidas FROM tmp_stg_cli;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_cliente;
    CREATE TEMPORARY TABLE tmp_dim_cliente (
        sk_cliente INT PRIMARY KEY,
        nombre     VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO tmp_dim_cliente (sk_cliente, nombre)
    VALUES (0, 'Anónimo');

    INSERT IGNORE INTO tmp_dim_cliente (sk_cliente, nombre)
    SELECT sk, nombre
    FROM tmp_stg_cli
    WHERE es_valido = 1
    ORDER BY rn;

    START TRANSACTION;

    INSERT INTO opiniones_staging.stg_rechazo (id_ejecucion, fuente, fila, motivo, fecha_registro)
    SELECT
        p_id_ejecucion,
        'DW: dim_cliente',
        s.rn,
        CASE
            WHEN s.es_valido = 0 THEN CONCAT('id_cliente inválido o nombre vacío: "', s.id_cliente, '"')
            ELSE CONCAT('id_cliente duplicado en staging: ', s.id_cliente)
        END,
        UTC_TIMESTAMP()
    FROM tmp_stg_cli s
    WHERE s.es_valido = 0
       OR s.rn > (
           SELECT MIN(t2.rn_calc) FROM (
               SELECT TRIM(id_cliente) AS cid, ROW_NUMBER() OVER () AS rn_calc
               FROM opiniones_staging.stg_cliente
           ) t2 WHERE t2.cid = s.id_cliente
       );
    SET v_rec = ROW_COUNT();

    UPDATE dim_cliente d
    JOIN tmp_dim_cliente t ON t.sk_cliente = d.sk_cliente
    SET d.nombre = t.nombre
    WHERE d.nombre <> t.nombre;
    SET v_act = ROW_COUNT();

    INSERT INTO dim_cliente (sk_cliente, nombre)
    SELECT t.sk_cliente, t.nombre
    FROM tmp_dim_cliente t
    LEFT JOIN dim_cliente d ON d.sk_cliente = t.sk_cliente
    WHERE d.sk_cliente IS NULL;
    SET v_ins = ROW_COUNT();

    COMMIT;

    DROP TEMPORARY TABLE IF EXISTS tmp_dim_cliente;
    DROP TEMPORARY TABLE IF EXISTS tmp_stg_cli;
    SET p_actualizadas = v_act;
    SET p_insertadas   = v_ins;
    SET p_rechazadas   = v_rec;
END$$
DELIMITER ;