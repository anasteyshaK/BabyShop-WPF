CREATE TABLE IF NOT EXISTS `user_favorite_product` (
    `favorite_id` INT NOT NULL AUTO_INCREMENT,
    `user_id` INT NOT NULL,
    `product_id` INT NOT NULL,
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (`favorite_id`),
    UNIQUE KEY `ux_user_favorite_product_user_product` (`user_id`, `product_id`),
    KEY `ix_user_favorite_product_product` (`product_id`),
    CONSTRAINT `fk_user_favorite_product_user`
        FOREIGN KEY (`user_id`) REFERENCES `app_user` (`user_id`)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT `fk_user_favorite_product_product`
        FOREIGN KEY (`product_id`) REFERENCES `productt` (`product_id`)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

DROP PROCEDURE IF EXISTS `GetUserFavoriteProducts`;
DROP PROCEDURE IF EXISTS `AddUserFavoriteProduct`;
DROP PROCEDURE IF EXISTS `RemoveUserFavoriteProduct`;
DROP PROCEDURE IF EXISTS `ClearUserFavoriteProducts`;

DELIMITER $$

CREATE PROCEDURE `GetUserFavoriteProducts` (
    IN `p_user_id` INT
)
BEGIN
    SELECT
        ufp.product_id,
        ufp.created_at
    FROM user_favorite_product AS ufp
    WHERE ufp.user_id = p_user_id
    ORDER BY ufp.created_at DESC, ufp.favorite_id DESC;
END$$

CREATE PROCEDURE `AddUserFavoriteProduct` (
    IN `p_user_id` INT,
    IN `p_product_id` INT
)
BEGIN
    IF p_user_id IS NULL OR p_user_id <= 0 OR p_product_id IS NULL OR p_product_id <= 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A valid user and product are required to save favorites.';
    END IF;

    INSERT INTO user_favorite_product (
        user_id,
        product_id
    )
    VALUES (
        p_user_id,
        p_product_id
    )
    ON DUPLICATE KEY UPDATE
        created_at = created_at;
END$$

CREATE PROCEDURE `RemoveUserFavoriteProduct` (
    IN `p_user_id` INT,
    IN `p_product_id` INT
)
BEGIN
    DELETE FROM user_favorite_product
    WHERE user_id = p_user_id
      AND product_id = p_product_id;
END$$

CREATE PROCEDURE `ClearUserFavoriteProducts` (
    IN `p_user_id` INT
)
BEGIN
    DELETE FROM user_favorite_product
    WHERE user_id = p_user_id;
END$$

DELIMITER ;
