USE baby_shop_restored;

CREATE TABLE IF NOT EXISTS address (
    address_id INT NOT NULL AUTO_INCREMENT,
    address_text VARCHAR(75) NOT NULL,
    PRIMARY KEY (address_id),
    UNIQUE KEY uq_address_text (address_text)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT IGNORE INTO address (address_text)
SELECT DISTINCT TRIM(delivery_address)
FROM customer_order
WHERE delivery_address IS NOT NULL
  AND TRIM(delivery_address) <> '';

DROP PROCEDURE IF EXISTS AddAddress;

DELIMITER $$
CREATE PROCEDURE AddAddress(IN p_address_text VARCHAR(75))
BEGIN
    IF p_address_text IS NOT NULL AND TRIM(p_address_text) <> '' THEN
        INSERT IGNORE INTO address(address_text)
        VALUES (TRIM(p_address_text));
    END IF;
END $$
DELIMITER ;

DROP TRIGGER IF EXISTS trg_customer_order_address_insert;
DROP TRIGGER IF EXISTS trg_customer_order_address_update;

DELIMITER $$
CREATE TRIGGER trg_customer_order_address_insert
BEFORE INSERT ON customer_order
FOR EACH ROW
BEGIN
    IF NEW.delivery_address IS NOT NULL THEN
        SET NEW.delivery_address = TRIM(NEW.delivery_address);
        IF NEW.delivery_address <> '' THEN
            INSERT IGNORE INTO address(address_text)
            VALUES (NEW.delivery_address);
        END IF;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER trg_customer_order_address_update
BEFORE UPDATE ON customer_order
FOR EACH ROW
BEGIN
    IF NEW.delivery_address IS NOT NULL THEN
        SET NEW.delivery_address = TRIM(NEW.delivery_address);
        IF NEW.delivery_address <> '' THEN
            INSERT IGNORE INTO address(address_text)
            VALUES (NEW.delivery_address);
        END IF;
    END IF;
END $$
DELIMITER ;
