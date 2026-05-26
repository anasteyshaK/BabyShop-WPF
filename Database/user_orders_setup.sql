CREATE TABLE IF NOT EXISTS `user_customer_order` (
    `user_order_link_id` INT NOT NULL AUTO_INCREMENT,
    `user_id` INT NOT NULL,
    `order_id` INT NOT NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`user_order_link_id`),
    UNIQUE KEY `ux_user_customer_order_user_order` (`user_id`, `order_id`),
    KEY `ix_user_customer_order_order` (`order_id`),
    CONSTRAINT `fk_user_customer_order_user`
        FOREIGN KEY (`user_id`) REFERENCES `app_user` (`user_id`)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT `fk_user_customer_order_order`
        FOREIGN KEY (`order_id`) REFERENCES `customer_order` (`order_id`)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS `AddUserCustomerOrderLink`;
DROP PROCEDURE IF EXISTS `GetUserCustomerOrders`;
DROP PROCEDURE IF EXISTS `GetLatestCheckoutCustomerDetailsByUserId`;

DELIMITER $$

CREATE PROCEDURE `AddUserCustomerOrderLink` (
    IN `p_user_id` INT,
    IN `p_order_id` INT
)
BEGIN
    IF p_user_id IS NULL OR p_user_id <= 0 OR p_order_id IS NULL OR p_order_id <= 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A valid user and order are required to link the order.';
    END IF;

    INSERT INTO user_customer_order (
        user_id,
        order_id
    )
    VALUES (
        p_user_id,
        p_order_id
    )
    ON DUPLICATE KEY UPDATE
        created_at = created_at;
END$$

CREATE PROCEDURE `GetUserCustomerOrders` (
    IN `p_user_id` INT
)
BEGIN
    SELECT
        co.order_id,
        co.start_date AS order_date,
        COALESCE(co.order_status, '') AS order_status,
        COALESCE(co.total_cost, 0) AS total_cost
    FROM user_customer_order AS uco
    INNER JOIN customer_order AS co ON co.order_id = uco.order_id
    WHERE uco.user_id = p_user_id
    ORDER BY
        COALESCE(co.start_date, '1900-01-01') DESC,
        co.order_id DESC;
END$$

CREATE PROCEDURE `GetLatestCheckoutCustomerDetailsByUserId` (
    IN `p_user_id` INT
)
BEGIN
    SELECT
        COALESCE(c.c_fullname, '') AS c_fullname,
        COALESCE(c.c_phone_number, '') AS c_phone_number,
        COALESCE(co.delivery_address, '') AS delivery_address
    FROM user_customer_order AS uco
    INNER JOIN customer_order AS co ON co.order_id = uco.order_id
    INNER JOIN customer AS c ON c.customer_id = co.customer_id
    WHERE uco.user_id = p_user_id
    ORDER BY
        COALESCE(co.start_date, '1900-01-01') DESC,
        co.order_id DESC,
        uco.user_order_link_id DESC
    LIMIT 1;
END$$

DELIMITER ;
