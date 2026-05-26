DROP PROCEDURE IF EXISTS `GetCatalogProductsWithPhotos`;
DROP PROCEDURE IF EXISTS `GetCatalogHitProducts`;
DROP PROCEDURE IF EXISTS `GetCatalogProductsUnderPrice`;
DROP PROCEDURE IF EXISTS `GetCatalogMuslinProducts`;

DELIMITER $$

CREATE PROCEDURE `GetCatalogProductsWithPhotos`()
BEGIN
    SELECT p.product_id
    FROM productt AS p
    WHERE TRIM(COALESCE(p.image_path, '')) <> ''
    ORDER BY p.product_id;
END$$

CREATE PROCEDURE `GetCatalogHitProducts`()
BEGIN
    SELECT op.product_id
    FROM order_product AS op
    GROUP BY op.product_id
    HAVING SUM(COALESCE(op.product_count, 0)) > 0
    ORDER BY SUM(COALESCE(op.product_count, 0)) DESC, op.product_id DESC;
END$$

CREATE PROCEDURE `GetCatalogProductsUnderPrice`(
    IN `p_max_total_price` DECIMAL(10,2)
)
BEGIN
    SELECT p.product_id
    FROM productt AS p
    WHERE COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0) <= COALESCE(p_max_total_price, 0)
    ORDER BY (COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0)) ASC, p.product_id DESC;
END$$

CREATE PROCEDURE `GetCatalogMuslinProducts`()
BEGIN
    SELECT p.product_id
    FROM productt AS p
    LEFT JOIN fabric AS f ON f.fabric_id = p.fabric_id
    WHERE LOWER(TRIM(COALESCE(f.fabric_type, ''))) = 'muslin'
       OR LOWER(TRIM(COALESCE(p.product_title, ''))) LIKE '%muslin%'
    ORDER BY p.product_id DESC;
END$$

DELIMITER ;
