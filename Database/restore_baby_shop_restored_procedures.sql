USE `baby_shop_restored`;

DROP FUNCTION IF EXISTS `fn_calculate_order_total`;

DELIMITER $$
CREATE FUNCTION `fn_calculate_order_total` (`p_order_id` INT)
RETURNS DECIMAL(10,2)
DETERMINISTIC
READS SQL DATA
BEGIN
    DECLARE v_total DECIMAL(10,2);

    SELECT IFNULL(SUM(op.product_count * IFNULL(p.price_per_m, 0) * IFNULL(p.fabric_amount, 0)), 0)
      INTO v_total
    FROM order_product op
    INNER JOIN productt p ON p.product_id = op.product_id
    WHERE op.order_id = p_order_id;

    RETURN IFNULL(v_total, 0);
END $$
DELIMITER ;

DROP TRIGGER IF EXISTS `trg_ORDER_PRODUCT_RecalcOrderTotal_INSERT`;
DROP TRIGGER IF EXISTS `trg_ORDER_PRODUCT_RecalcOrderTotal_UPDATE`;
DROP TRIGGER IF EXISTS `trg_ORDER_PRODUCT_RecalcOrderTotal_DELETE`;

DELIMITER $$
CREATE TRIGGER `trg_ORDER_PRODUCT_RecalcOrderTotal_INSERT`
AFTER INSERT ON `order_product`
FOR EACH ROW
BEGIN
    UPDATE customer_order
    SET total_cost = fn_calculate_order_total(NEW.order_id)
    WHERE order_id = NEW.order_id;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER `trg_ORDER_PRODUCT_RecalcOrderTotal_UPDATE`
AFTER UPDATE ON `order_product`
FOR EACH ROW
BEGIN
    UPDATE customer_order
    SET total_cost = fn_calculate_order_total(NEW.order_id)
    WHERE order_id = NEW.order_id;

    IF OLD.order_id <> NEW.order_id THEN
        UPDATE customer_order
        SET total_cost = fn_calculate_order_total(OLD.order_id)
        WHERE order_id = OLD.order_id;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER `trg_ORDER_PRODUCT_RecalcOrderTotal_DELETE`
AFTER DELETE ON `order_product`
FOR EACH ROW
BEGIN
    UPDATE customer_order
    SET total_cost = fn_calculate_order_total(OLD.order_id)
    WHERE order_id = OLD.order_id;
END $$
DELIMITER ;

DROP PROCEDURE IF EXISTS `AddCustomer`;
DROP PROCEDURE IF EXISTS `UpdateCustomer`;
DROP PROCEDURE IF EXISTS `DeleteCustomer`;
DROP PROCEDURE IF EXISTS `AddFabric`;
DROP PROCEDURE IF EXISTS `UpdateFabric`;
DROP PROCEDURE IF EXISTS `DeleteFabric`;
DROP PROCEDURE IF EXISTS `AddProduct`;
DROP PROCEDURE IF EXISTS `UpdateProduct`;
DROP PROCEDURE IF EXISTS `DeleteProduct`;
DROP PROCEDURE IF EXISTS `AddCustomerOrder`;
DROP PROCEDURE IF EXISTS `UpdateCustomerOrder`;
DROP PROCEDURE IF EXISTS `DeleteCustomerOrder`;
DROP PROCEDURE IF EXISTS `AddOrderProduct`;
DROP PROCEDURE IF EXISTS `UpdateOrderProduct`;
DROP PROCEDURE IF EXISTS `DeleteOrderProduct`;
DROP PROCEDURE IF EXISTS `ViewCustomers`;
DROP PROCEDURE IF EXISTS `ViewFabrics`;
DROP PROCEDURE IF EXISTS `ViewProducts`;
DROP PROCEDURE IF EXISTS `ViewCustomerOrders`;
DROP PROCEDURE IF EXISTS `ViewOrderProducts`;
DROP PROCEDURE IF EXISTS `GetCustomerByPhone`;
DROP PROCEDURE IF EXISTS `GetFabricByTypeAndColor`;
DROP PROCEDURE IF EXISTS `GetProductById`;
DROP PROCEDURE IF EXISTS `GetCustomerOrderById`;
DROP PROCEDURE IF EXISTS `GetOrderProductByOrderAndProduct`;
DROP PROCEDURE IF EXISTS `GetDashboardData`;
DROP PROCEDURE IF EXISTS `GetDashboardSummary`;
DROP PROCEDURE IF EXISTS `GetDashboardStatusChart`;
DROP PROCEDURE IF EXISTS `GetDashboardProductChart`;
DROP PROCEDURE IF EXISTS `GetDashboardCategoryChart`;
DROP PROCEDURE IF EXISTS `GetDashboardTimelineChart`;
DROP PROCEDURE IF EXISTS `GetDashboardMonthStats`;
DROP PROCEDURE IF EXISTS `GetDashboardAmountBounds`;
DROP PROCEDURE IF EXISTS `GetDashboardClients`;
DROP PROCEDURE IF EXISTS `GetDashboardProducts`;
DROP PROCEDURE IF EXISTS `GetDashboardFabricTypes`;

DELIMITER $$
CREATE PROCEDURE `AddCustomer` (
    IN `p_id` INT,
    IN `p_name` VARCHAR(75),
    IN `p_phone` CHAR(9)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF EXISTS (SELECT 1 FROM customer WHERE customer_id = p_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Customer with this ID already exists.' AS message;
    ELSE
        INSERT INTO customer (customer_id, c_fullname, c_phone_number)
        VALUES (p_id, p_name, p_phone);

        COMMIT;
        SELECT 'Customer added successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `UpdateCustomer` (
    IN `pl_N_customer_id` INT,
    IN `pl_S_c_fullname` VARCHAR(75),
    IN `pl_S_c_phone_number` CHAR(9)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_customer_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Customer with this ID was not found.' AS message;
    ELSE
        UPDATE customer
        SET c_fullname = pl_S_c_fullname,
            c_phone_number = pl_S_c_phone_number
        WHERE customer_id = pl_N_customer_id;

        COMMIT;
        SELECT 'Customer updated successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `DeleteCustomer` (
    IN `pl_N_customer_id` INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_customer_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Customer with this ID was not found.' AS message;
    ELSE
        DELETE FROM customer
        WHERE customer_id = pl_N_customer_id;

        COMMIT;
        SELECT 'Customer deleted successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `AddFabric` (
    IN `pl_N_fabric_id` INT,
    IN `pl_S_fabric_type` VARCHAR(30),
    IN `pl_Price_per_m` DECIMAL(10,2),
    IN `pl_S_color` VARCHAR(30)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF pl_Price_per_m <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Price per meter must be greater than zero.' AS message;
    ELSEIF EXISTS (SELECT 1 FROM fabric WHERE fabric_id = pl_N_fabric_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric with this ID already exists.' AS message;
    ELSE
        INSERT INTO fabric (fabric_id, fabric_type, price_per_m, color)
        VALUES (pl_N_fabric_id, pl_S_fabric_type, pl_Price_per_m, pl_S_color);

        COMMIT;
        SELECT 'Fabric added successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `UpdateFabric` (
    IN `pl_N_fabric_id` INT,
    IN `pl_Fabric_type` VARCHAR(30),
    IN `pl_Price_per_m` DECIMAL(10,2),
    IN `pl_Color` VARCHAR(30)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM fabric WHERE fabric_id = pl_N_fabric_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric with this ID was not found.' AS message;
    ELSEIF pl_Price_per_m <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Price per meter must be greater than zero.' AS message;
    ELSE
        UPDATE fabric
        SET fabric_type = pl_Fabric_type,
            price_per_m = pl_Price_per_m,
            color = pl_Color
        WHERE fabric_id = pl_N_fabric_id;

        COMMIT;
        SELECT 'Fabric updated successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `DeleteFabric` (
    IN `pl_N_fabric_id` INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM fabric WHERE fabric_id = pl_N_fabric_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric with this ID was not found.' AS message;
    ELSE
        DELETE FROM fabric
        WHERE fabric_id = pl_N_fabric_id;

        COMMIT;
        SELECT 'Fabric deleted successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `AddProduct` (
    IN `pl_N_product_id` INT,
    IN `pl_S_product_title` VARCHAR(75),
    IN `pl_N_category_id` INT,
    IN `pl_N_fabric_amount` DECIMAL(10,2),
    IN `pl_N_fabric_id` INT,
    IN `pl_N_price_per_m` DECIMAL(10,2),
    IN `pl_S_color` VARCHAR(50),
    IN `pl_S_image_path` VARCHAR(255)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF EXISTS (SELECT 1 FROM productt WHERE product_id = pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this ID already exists.' AS message;
    ELSEIF pl_N_category_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM category WHERE category_id = pl_N_category_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Category with this ID was not found.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM fabric WHERE fabric_id = pl_N_fabric_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric with this ID was not found.' AS message;
    ELSEIF pl_N_fabric_amount <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric amount must be greater than zero.' AS message;
    ELSEIF pl_N_price_per_m <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Price per meter must be greater than zero.' AS message;
    ELSEIF EXISTS (SELECT 1 FROM productt WHERE product_title = pl_S_product_title) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this title already exists.' AS message;
    ELSE
        INSERT INTO productt (product_id, product_title, category_id, fabric_amount, fabric_id, price_per_m, color, image_path)
        VALUES (pl_N_product_id, pl_S_product_title, pl_N_category_id, pl_N_fabric_amount, pl_N_fabric_id, pl_N_price_per_m, pl_S_color, pl_S_image_path);

        COMMIT;
        SELECT 'Product added successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `UpdateProduct` (
    IN `pl_N_product_id` INT,
    IN `pl_S_product_title` VARCHAR(75),
    IN `pl_N_category_id` INT,
    IN `pl_Fabric_amount` DECIMAL(10,2),
    IN `pl_N_fabric_id` INT,
    IN `pl_Price_per_m` DECIMAL(10,2),
    IN `pl_S_color` VARCHAR(50),
    IN `pl_S_image_path` VARCHAR(255)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM productt WHERE product_id = pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this ID was not found.' AS message;
    ELSEIF pl_N_category_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM category WHERE category_id = pl_N_category_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Category with this ID was not found.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM fabric WHERE fabric_id = pl_N_fabric_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric with this ID was not found.' AS message;
    ELSEIF pl_Fabric_amount <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Fabric amount must be greater than zero.' AS message;
    ELSEIF pl_Price_per_m <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Price per meter must be greater than zero.' AS message;
    ELSEIF EXISTS (SELECT 1 FROM productt WHERE product_title = pl_S_product_title AND product_id <> pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this title already exists.' AS message;
    ELSE
        UPDATE productt
        SET product_title = pl_S_product_title,
            category_id = pl_N_category_id,
            fabric_amount = pl_Fabric_amount,
            fabric_id = pl_N_fabric_id,
            price_per_m = pl_Price_per_m,
            color = pl_S_color,
            image_path = pl_S_image_path
        WHERE product_id = pl_N_product_id;

        COMMIT;
        SELECT 'Product updated successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `DeleteProduct` (
    IN `pl_N_product_id` INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM productt WHERE product_id = pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this ID was not found.' AS message;
    ELSEIF EXISTS (SELECT 1 FROM order_product WHERE product_id = pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product is used in orders and cannot be deleted.' AS message;
    ELSE
        DELETE FROM productt
        WHERE product_id = pl_N_product_id;

        COMMIT;
        SELECT 'Product deleted successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `AddCustomerOrder` (
    IN `pl_N_Order_id` INT,
    IN `pl_N_Customer_id` INT,
    IN `pl_S_Delivery_address` VARCHAR(75),
    IN `pl_D_Start_date` DATE,
    IN `pl_D_End_date` DATE,
    IN `pl_S_Order_status` VARCHAR(10),
    IN `pl_N_Total_cost` DECIMAL(10,2)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF EXISTS (SELECT 1 FROM customer_order WHERE order_id = pl_N_Order_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order with this ID already exists.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_Customer_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Customer with this ID was not found.' AS message;
    ELSEIF pl_S_Order_status NOT IN ('Pending', 'Shipped', 'Completed') THEN
        ROLLBACK;
        SELECT 'ERROR: Invalid order status.' AS message;
    ELSE
        INSERT INTO customer_order (order_id, customer_id, delivery_address, start_date, end_date, order_status, total_cost)
        VALUES (pl_N_Order_id, pl_N_Customer_id, pl_S_Delivery_address, pl_D_Start_date, pl_D_End_date, pl_S_Order_status, 0);

        UPDATE customer_order
        SET total_cost = fn_calculate_order_total(pl_N_Order_id)
        WHERE order_id = pl_N_Order_id;

        COMMIT;
        SELECT 'Order added successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `UpdateCustomerOrder` (
    IN `pl_N_order_id` INT,
    IN `pl_N_customer_id` INT,
    IN `pl_S_delivery_address` VARCHAR(75),
    IN `pl_D_start_date` DATE,
    IN `pl_D_end_date` DATE,
    IN `pl_S_order_status` VARCHAR(10),
    IN `pl_N_total_cost` DECIMAL(10,2)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM customer_order WHERE order_id = pl_N_order_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order with this ID was not found.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_customer_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Customer with this ID was not found.' AS message;
    ELSEIF pl_S_order_status NOT IN ('Pending', 'Shipped', 'Completed') THEN
        ROLLBACK;
        SELECT 'ERROR: Invalid order status.' AS message;
    ELSE
        UPDATE customer_order
        SET customer_id = pl_N_customer_id,
            delivery_address = pl_S_delivery_address,
            start_date = pl_D_start_date,
            end_date = pl_D_end_date,
            order_status = pl_S_order_status,
            total_cost = fn_calculate_order_total(pl_N_order_id)
        WHERE order_id = pl_N_order_id;

        COMMIT;
        SELECT 'Order updated successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `DeleteCustomerOrder` (
    IN `pl_N_order_id` INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM customer_order WHERE order_id = pl_N_order_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order with this ID was not found.' AS message;
    ELSE
        DELETE FROM customer_order
        WHERE order_id = pl_N_order_id;

        COMMIT;
        SELECT 'Order deleted successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `AddOrderProduct` (
    IN `pl_N_order_product_id` INT,
    IN `pl_N_order_id` INT,
    IN `pl_N_product_id` INT,
    IN `pl_N_product_count` SMALLINT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF EXISTS (SELECT 1 FROM order_product WHERE order_product_id = pl_N_order_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order item with this ID already exists.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM customer_order WHERE order_id = pl_N_order_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order with this ID was not found.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM productt WHERE product_id = pl_N_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Product with this ID was not found.' AS message;
    ELSEIF pl_N_product_count <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Product count must be greater than zero.' AS message;
    ELSE
        INSERT INTO order_product (order_product_id, order_id, product_id, product_count)
        VALUES (pl_N_order_product_id, pl_N_order_id, pl_N_product_id, pl_N_product_count);

        COMMIT;
        SELECT 'Order item added successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `UpdateOrderProduct` (
    IN `pl_N_order_product_id` INT,
    IN `pl_N_product_count` SMALLINT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM order_product WHERE order_product_id = pl_N_order_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order item with this ID was not found.' AS message;
    ELSEIF pl_N_product_count <= 0 THEN
        ROLLBACK;
        SELECT 'ERROR: Product count must be greater than zero.' AS message;
    ELSE
        UPDATE order_product
        SET product_count = pl_N_product_count
        WHERE order_product_id = pl_N_order_product_id;

        COMMIT;
        SELECT 'Order item updated successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `DeleteOrderProduct` (
    IN `pl_N_order_product_id` INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM order_product WHERE order_product_id = pl_N_order_product_id) THEN
        ROLLBACK;
        SELECT 'ERROR: Order item with this ID was not found.' AS message;
    ELSE
        DELETE FROM order_product
        WHERE order_product_id = pl_N_order_product_id;

        COMMIT;
        SELECT 'Order item deleted successfully.' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `ViewCustomers` ()
BEGIN
    SELECT * FROM customerview;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `ViewFabrics` ()
BEGIN
    SELECT * FROM fabricview;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `ViewProducts` ()
BEGIN
    SELECT * FROM productfabricview;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `ViewCustomerOrders` ()
BEGIN
    SELECT * FROM customerordersview;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `ViewOrderProducts` ()
BEGIN
    SELECT * FROM orderproductview;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetCustomerByPhone` (
    IN `p_phone` CHAR(9)
)
BEGIN
    SELECT * FROM customer
    WHERE c_phone_number = p_phone;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetFabricByTypeAndColor` (
    IN `p_fabric_type` VARCHAR(30),
    IN `p_color` VARCHAR(30)
)
BEGIN
    SELECT * FROM fabric
    WHERE fabric_type = p_fabric_type
      AND color = p_color;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetProductById` (
    IN `p_product_id` INT
)
BEGIN
    SELECT * FROM productt
    WHERE product_id = p_product_id;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetCustomerOrderById` (
    IN `p_order_id` INT
)
BEGIN
    SELECT * FROM customer_order
    WHERE order_id = p_order_id;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetOrderProductByOrderAndProduct` (
    IN `p_order_id` INT,
    IN `p_product_id` INT
)
BEGIN
    SELECT * FROM order_product
    WHERE order_id = p_order_id
      AND product_id = p_product_id;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardClients` ()
BEGIN
    SELECT DISTINCT c_fullname
    FROM dashboard_main_view
    WHERE c_fullname IS NOT NULL
      AND TRIM(c_fullname) <> ''
    ORDER BY c_fullname;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardProducts` ()
BEGIN
    SELECT DISTINCT product_title
    FROM dashboard_main_view
    WHERE product_title IS NOT NULL
      AND TRIM(product_title) <> ''
    ORDER BY product_title;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardFabricTypes` ()
BEGIN
    SELECT DISTINCT fabric_type
    FROM dashboard_main_view
    WHERE fabric_type IS NOT NULL
      AND TRIM(fabric_type) <> ''
    ORDER BY fabric_type;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardData` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        d.order_id,
        d.c_fullname,
        d.delivery_address,
        d.start_date,
        d.end_date,
        d.order_status,
        d.product_title,
        d.fabric_type,
        d.color,
        d.product_count,
        d.price_per_m,
        d.fabric_amount,
        d.line_total,
        filtered_orders.order_total AS total_cost
    FROM dashboard_main_view d
    INNER JOIN (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        WHERE (p_date_from IS NULL OR start_date >= p_date_from)
          AND (p_date_to IS NULL OR start_date <= p_date_to)
          AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
          AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
          AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
          AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
        GROUP BY order_id
        HAVING (p_min_price IS NULL OR order_total >= p_min_price)
           AND (p_max_price IS NULL OR order_total <= p_max_price)
    ) filtered_orders ON filtered_orders.order_id = d.order_id
    WHERE (p_date_from IS NULL OR d.start_date >= p_date_from)
      AND (p_date_to IS NULL OR d.start_date <= p_date_to)
      AND (p_status IS NULL OR p_status = '' OR d.order_status = p_status)
      AND (p_client_name IS NULL OR p_client_name = '' OR d.c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_product_title IS NULL OR p_product_title = '' OR d.product_title = p_product_title)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR d.fabric_type = p_fabric_type)
    ORDER BY d.start_date, d.order_id, d.product_title;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardSummary` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        COALESCE(ROUND(SUM(order_total), 2), 0) AS total_sum,
        COUNT(*) AS order_count,
        COALESCE(ROUND(AVG(order_total), 2), 0) AS avg_value,
        COALESCE(ROUND(MIN(order_total), 2), 0) AS min_value,
        COALESCE(ROUND(MAX(order_total), 2), 0) AS max_value
    FROM (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        WHERE (p_date_from IS NULL OR start_date >= p_date_from)
          AND (p_date_to IS NULL OR start_date <= p_date_to)
          AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
          AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
          AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
          AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
        GROUP BY order_id
        HAVING (p_min_price IS NULL OR order_total >= p_min_price)
           AND (p_max_price IS NULL OR order_total <= p_max_price)
    ) filtered_orders;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardAmountBounds` ()
BEGIN
    SELECT
        COALESCE(ROUND(MIN(order_total), 2), 0) AS min_value,
        COALESCE(ROUND(MAX(order_total), 2), 0) AS max_value
    FROM (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        GROUP BY order_id
    ) order_totals;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardStatusChart` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        order_status,
        COUNT(DISTINCT order_id) AS order_count,
        ROUND(SUM(line_total), 2) AS total_sum
    FROM dashboard_main_view
    WHERE (p_date_from IS NULL OR start_date >= p_date_from)
      AND (p_date_to IS NULL OR start_date <= p_date_to)
      AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_min_price IS NULL OR line_total >= p_min_price)
      AND (p_max_price IS NULL OR line_total <= p_max_price)
      AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
    GROUP BY order_status
    ORDER BY order_status;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardProductChart` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        product_title,
        SUM(product_count) AS total_quantity,
        ROUND(SUM(line_total), 2) AS total_sum
    FROM dashboard_main_view
    WHERE product_title IS NOT NULL
      AND (p_date_from IS NULL OR start_date >= p_date_from)
      AND (p_date_to IS NULL OR start_date <= p_date_to)
      AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
      AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_min_price IS NULL OR line_total >= p_min_price)
      AND (p_max_price IS NULL OR line_total <= p_max_price)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
    GROUP BY product_title
    ORDER BY total_quantity DESC, product_title;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardCategoryChart` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        COALESCE(NULLIF(TRIM(d.fabric_type), ''), 'Unspecified') AS category_label,
        SUM(d.product_count) AS total_quantity,
        ROUND(SUM(d.line_total), 2) AS total_sum
    FROM dashboard_main_view d
    INNER JOIN (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        WHERE (p_date_from IS NULL OR start_date >= p_date_from)
          AND (p_date_to IS NULL OR start_date <= p_date_to)
          AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
          AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
          AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
          AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
        GROUP BY order_id
        HAVING (p_min_price IS NULL OR order_total >= p_min_price)
           AND (p_max_price IS NULL OR order_total <= p_max_price)
    ) filtered_orders ON filtered_orders.order_id = d.order_id
    WHERE (p_date_from IS NULL OR d.start_date >= p_date_from)
      AND (p_date_to IS NULL OR d.start_date <= p_date_to)
      AND (p_status IS NULL OR p_status = '' OR d.order_status = p_status)
      AND (p_client_name IS NULL OR p_client_name = '' OR d.c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_product_title IS NULL OR p_product_title = '' OR d.product_title = p_product_title)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR d.fabric_type = p_fabric_type)
    GROUP BY category_label
    ORDER BY total_sum DESC, category_label
    LIMIT 8;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardTimelineChart` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        d.start_date,
        COUNT(DISTINCT d.order_id) AS order_count,
        ROUND(SUM(d.line_total), 2) AS total_sum
    FROM dashboard_main_view d
    INNER JOIN (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        WHERE start_date IS NOT NULL
          AND (p_date_from IS NULL OR start_date >= p_date_from)
          AND (p_date_to IS NULL OR start_date <= p_date_to)
          AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
          AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
          AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
          AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
        GROUP BY order_id
        HAVING (p_min_price IS NULL OR order_total >= p_min_price)
           AND (p_max_price IS NULL OR order_total <= p_max_price)
    ) filtered_orders ON filtered_orders.order_id = d.order_id
    WHERE d.start_date IS NOT NULL
      AND (p_date_from IS NULL OR d.start_date >= p_date_from)
      AND (p_date_to IS NULL OR d.start_date <= p_date_to)
      AND (p_status IS NULL OR p_status = '' OR d.order_status = p_status)
      AND (p_client_name IS NULL OR p_client_name = '' OR d.c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_product_title IS NULL OR p_product_title = '' OR d.product_title = p_product_title)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR d.fabric_type = p_fabric_type)
    GROUP BY d.start_date
    ORDER BY d.start_date;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE `GetDashboardMonthStats` (
    IN `p_date_from` DATE,
    IN `p_date_to` DATE,
    IN `p_status` VARCHAR(20),
    IN `p_client_name` VARCHAR(75),
    IN `p_min_price` DECIMAL(10,2),
    IN `p_max_price` DECIMAL(10,2),
    IN `p_product_title` VARCHAR(75),
    IN `p_fabric_type` VARCHAR(30)
)
BEGIN
    SELECT
        YEAR(d.start_date) AS order_year,
        MONTH(d.start_date) AS order_month,
        DATE_FORMAT(d.start_date, '%Y-%m') AS month_label,
        COUNT(DISTINCT d.order_id) AS order_count,
        ROUND(SUM(d.line_total), 2) AS total_sum
    FROM dashboard_main_view d
    INNER JOIN (
        SELECT
            order_id,
            ROUND(SUM(line_total), 2) AS order_total
        FROM dashboard_main_view
        WHERE start_date IS NOT NULL
          AND (p_date_from IS NULL OR start_date >= p_date_from)
          AND (p_date_to IS NULL OR start_date <= p_date_to)
          AND (p_status IS NULL OR p_status = '' OR order_status = p_status)
          AND (p_client_name IS NULL OR p_client_name = '' OR c_fullname LIKE CONCAT('%', p_client_name, '%'))
          AND (p_product_title IS NULL OR p_product_title = '' OR product_title = p_product_title)
          AND (p_fabric_type IS NULL OR p_fabric_type = '' OR fabric_type = p_fabric_type)
        GROUP BY order_id
        HAVING (p_min_price IS NULL OR order_total >= p_min_price)
           AND (p_max_price IS NULL OR order_total <= p_max_price)
    ) filtered_orders ON filtered_orders.order_id = d.order_id
    WHERE d.start_date IS NOT NULL
      AND (p_date_from IS NULL OR d.start_date >= p_date_from)
      AND (p_date_to IS NULL OR d.start_date <= p_date_to)
      AND (p_status IS NULL OR p_status = '' OR d.order_status = p_status)
      AND (p_client_name IS NULL OR p_client_name = '' OR d.c_fullname LIKE CONCAT('%', p_client_name, '%'))
      AND (p_product_title IS NULL OR p_product_title = '' OR d.product_title = p_product_title)
      AND (p_fabric_type IS NULL OR p_fabric_type = '' OR d.fabric_type = p_fabric_type)
    GROUP BY YEAR(d.start_date), MONTH(d.start_date), DATE_FORMAT(d.start_date, '%Y-%m')
    ORDER BY order_year, order_month;
END $$
DELIMITER ;

UPDATE customer_order
SET total_cost = fn_calculate_order_total(order_id);
