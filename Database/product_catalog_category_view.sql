DROP VIEW IF EXISTS `productfabricview`;

CREATE ALGORITHM=UNDEFINED
SQL SECURITY DEFINER
VIEW `productfabricview` AS
SELECT
    `p`.`product_id` AS `product_id`,
    `p`.`image_path` AS `image_path`,
    `p`.`product_title` AS `product_title`,
    COALESCE(`c`.`category_name`, '') AS `category`,
    `f`.`fabric_type` AS `fabric_type`,
    `p`.`color` AS `color`,
    `p`.`fabric_amount` AS `fabric_amount`,
    `p`.`price_per_m` AS `price_per_m`
FROM `productt` `p`
LEFT JOIN `category` `c` ON `p`.`category_id` = `c`.`category_id`
LEFT JOIN `fabric` `f` ON `p`.`fabric_id` = `f`.`fabric_id`;
