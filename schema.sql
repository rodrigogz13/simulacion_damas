-- =============================================================
-- Simulación de Damas Inglesas - Schema MariaDB
-- =============================================================
CREATE DATABASE IF NOT EXISTS damas
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE damas;

-- Tabla de partidas
CREATE TABLE IF NOT EXISTS juegos (
    id        INT AUTO_INCREMENT PRIMARY KEY,
    creado_en DATETIME     DEFAULT CURRENT_TIMESTAMP,
    estado    VARCHAR(20)  DEFAULT 'en_progreso'
        COMMENT 'en_progreso | rojas_ganan | negras_ganan | empate | abandonada',
    ganador   VARCHAR(10)  NULL
        COMMENT 'Rojo | Negro | NULL'
) ENGINE=InnoDB;

-- Tabla de movimientos
CREATE TABLE IF NOT EXISTS movimientos (
    id                 INT AUTO_INCREMENT PRIMARY KEY,
    juego_id           INT      NOT NULL,
    numero_movimiento  INT      NOT NULL,
    jugador            VARCHAR(10) NOT NULL   COMMENT 'Rojo | Negro',
    origen_fila        TINYINT  NOT NULL      COMMENT '0 = arriba (negro), 7 = abajo (rojo)',
    origen_col         TINYINT  NOT NULL      COMMENT '0 = columna a, 7 = columna h',
    destino_fila       TINYINT  NOT NULL,
    destino_col        TINYINT  NOT NULL,
    capturas           TEXT     NULL          COMMENT 'JSON: [{"row":N,"col":N},...]',
    fue_coronacion     BOOLEAN  DEFAULT FALSE,
    FOREIGN KEY (juego_id) REFERENCES juegos(id) ON DELETE CASCADE
) ENGINE=InnoDB;
