USE baby_shop_restored;

CREATE TABLE IF NOT EXISTS color_lookup (
    color_id INT NOT NULL AUTO_INCREMENT,
    color_name VARCHAR(50) NOT NULL,
    PRIMARY KEY (color_id),
    UNIQUE KEY uq_color_name (color_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT IGNORE INTO color_lookup (color_name)
SELECT DISTINCT TRIM(color)
FROM fabric
WHERE color IS NOT NULL
  AND TRIM(color) <> ''
UNION
SELECT DISTINCT TRIM(color)
FROM productt
WHERE color IS NOT NULL
  AND TRIM(color) <> '';

DROP PROCEDURE IF EXISTS AddColor;

DELIMITER $$
CREATE PROCEDURE AddColor(IN p_color_name VARCHAR(50))
BEGIN
    IF p_color_name IS NOT NULL AND TRIM(p_color_name) <> '' THEN
        INSERT IGNORE INTO color_lookup(color_name)
        VALUES (TRIM(p_color_name));
    END IF;
END $$
DELIMITER ;

DROP TRIGGER IF EXISTS trg_fabric_color_insert;
DROP TRIGGER IF EXISTS trg_fabric_color_update;
DROP TRIGGER IF EXISTS trg_productt_color_insert;
DROP TRIGGER IF EXISTS trg_productt_color_update;

DELIMITER $$
CREATE TRIGGER trg_fabric_color_insert
BEFORE INSERT ON fabric
FOR EACH ROW
BEGIN
    IF NEW.color IS NOT NULL THEN
        SET NEW.color = TRIM(NEW.color);
        IF NEW.color <> '' THEN
            INSERT IGNORE INTO color_lookup(color_name)
            VALUES (NEW.color);
        END IF;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER trg_fabric_color_update
BEFORE UPDATE ON fabric
FOR EACH ROW
BEGIN
    IF NEW.color IS NOT NULL THEN
        SET NEW.color = TRIM(NEW.color);
        IF NEW.color <> '' THEN
            INSERT IGNORE INTO color_lookup(color_name)
            VALUES (NEW.color);
        END IF;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER trg_productt_color_insert
BEFORE INSERT ON productt
FOR EACH ROW
BEGIN
    IF NEW.color IS NOT NULL THEN
        SET NEW.color = TRIM(NEW.color);
        IF NEW.color <> '' THEN
            INSERT IGNORE INTO color_lookup(color_name)
            VALUES (NEW.color);
        END IF;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER trg_productt_color_update
BEFORE UPDATE ON productt
FOR EACH ROW
BEGIN
    IF NEW.color IS NOT NULL THEN
        SET NEW.color = TRIM(NEW.color);
        IF NEW.color <> '' THEN
            INSERT IGNORE INTO color_lookup(color_name)
            VALUES (NEW.color);
        END IF;
    END IF;
END $$
DELIMITER ;
