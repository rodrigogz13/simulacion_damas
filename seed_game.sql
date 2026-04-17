USE damas;

INSERT INTO juegos (creado_en, estado, ganador)
VALUES ('2026-04-16 10:30:00', 'abandonada', 'Rojo');

SET @gid = LAST_INSERT_ID();

-- Mov 1  Rojo   a3-b4            (5,0)->(4,1)
-- Mov 2  Negro  h6-g5            (2,7)->(3,6)
-- Mov 3  Rojo   c3-d4            (5,2)->(4,3)
-- Mov 4  Negro  f6-e5            (2,5)->(3,4)
-- Mov 5  Rojo   d4xf6  cap e5   (4,3)->(2,5)  captura (3,4)
-- Mov 6  Negro  g7xe5  cap f6   (1,6)->(3,4)  captura (2,5)
INSERT INTO movimientos (juego_id, num, jugador, fr, fc, tr, tc, capturas, corona) VALUES
(@gid, 1, 'Rojo',  5, 0, 4, 1, NULL,                FALSE),
(@gid, 2, 'Negro', 2, 7, 3, 6, NULL,                FALSE),
(@gid, 3, 'Rojo',  5, 2, 4, 3, NULL,                FALSE),
(@gid, 4, 'Negro', 2, 5, 3, 4, NULL,                FALSE),
(@gid, 5, 'Rojo',  4, 3, 2, 5, '[{"r":3,"c":4}]',  FALSE),
(@gid, 6, 'Negro', 1, 6, 3, 4, '[{"r":2,"c":5}]',  FALSE);

SELECT CONCAT('Partida #', @gid, ' insertada con 6 movimientos.') AS resultado;
