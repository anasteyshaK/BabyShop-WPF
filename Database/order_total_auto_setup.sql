USE baby_shop;

DROP FUNCTION IF EXISTS fn_calculate_order_total;

DELIMITER $$
CREATE FUNCTION fn_calculate_order_total(p_order_id INT)
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

DROP TRIGGER IF EXISTS trg_ORDER_PRODUCT_RecalcOrderTotal_INSERT;
DROP TRIGGER IF EXISTS trg_ORDER_PRODUCT_RecalcOrderTotal_UPDATE;
DROP TRIGGER IF EXISTS trg_ORDER_PRODUCT_RecalcOrderTotal_DELETE;

DELIMITER $$
CREATE TRIGGER trg_ORDER_PRODUCT_RecalcOrderTotal_INSERT
AFTER INSERT ON order_product
FOR EACH ROW
BEGIN
    UPDATE customer_order
    SET total_cost = fn_calculate_order_total(NEW.order_id)
    WHERE order_id = NEW.order_id;
END $$
DELIMITER ;

DELIMITER $$
CREATE TRIGGER trg_ORDER_PRODUCT_RecalcOrderTotal_UPDATE
AFTER UPDATE ON order_product
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
CREATE TRIGGER trg_ORDER_PRODUCT_RecalcOrderTotal_DELETE
AFTER DELETE ON order_product
FOR EACH ROW
BEGIN
    UPDATE customer_order
    SET total_cost = fn_calculate_order_total(OLD.order_id)
    WHERE order_id = OLD.order_id;
END $$
DELIMITER ;

DROP PROCEDURE IF EXISTS AddCustomerOrder;
DROP PROCEDURE IF EXISTS UpdateCustomerOrder;

DELIMITER $$
CREATE PROCEDURE AddCustomerOrder(
    IN pl_N_Order_id INT,
    IN pl_N_Customer_id INT,
    IN pl_S_Delivery_address VARCHAR(75),
    IN pl_D_Start_date DATE,
    IN pl_D_End_date DATE,
    IN pl_S_Order_status VARCHAR(10),
    IN pl_N_Total_cost DECIMAL(10,2)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR:', @errno, ': ', @msg) AS message;
    END;

    IF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_Customer_id) THEN
        SELECT 'ERROR: ������ � ��������� ID �� ������' AS message;
    ELSEIF pl_S_Order_status NOT IN ('Pending', 'Shipped', 'Completed') THEN
        SELECT 'ERROR: ������������ ������ ������' AS message;
    ELSE
        INSERT INTO customer_order
            (order_id, customer_id, delivery_address, start_date, end_date, order_status, total_cost)
        VALUES
            (pl_N_Order_id, pl_N_Customer_id, pl_S_Delivery_address, pl_D_Start_date, pl_D_End_date, pl_S_Order_status, 0);

        UPDATE customer_order
        SET total_cost = fn_calculate_order_total(pl_N_Order_id)
        WHERE order_id = pl_N_Order_id;

        SELECT '����� ������� ��������' AS message;
    END IF;
END $$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE UpdateCustomerOrder(
    IN pl_N_order_id INT,
    IN pl_N_customer_id INT,
    IN pl_S_delivery_address VARCHAR(75),
    IN pl_D_start_date DATE,
    IN pl_D_end_date DATE,
    IN pl_S_order_status VARCHAR(10),
    IN pl_N_total_cost DECIMAL(10,2)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        GET DIAGNOSTICS CONDITION 1 @errno = MYSQL_ERRNO, @msg = MESSAGE_TEXT;
        SELECT CONCAT('ERROR ', @errno, ': ', @msg) AS message;
    END;

    IF NOT EXISTS (SELECT 1 FROM customer_order WHERE order_id = pl_N_order_id) THEN
        SELECT 'ERROR: ����� � ��������� ID �� ������.' AS message;
    ELSEIF NOT EXISTS (SELECT 1 FROM customer WHERE customer_id = pl_N_customer_id) THEN
        SELECT 'ERROR: ������ � ��������� ID �� ������.' AS message;
    ELSEIF pl_S_order_status NOT IN ('Pending', 'Shipped', 'Completed') THEN
        SELECT 'ERROR: ������������ ������ ������' AS message;
    ELSE
        UPDATE customer_order
        SET customer_id = pl_N_customer_id,
            delivery_address = pl_S_delivery_address,
            start_date = pl_D_start_date,
            end_date = pl_D_end_date,
            order_status = pl_S_order_status,
            total_cost = fn_calculate_order_total(pl_N_order_id)
        WHERE order_id = pl_N_order_id;

        SELECT '����� ������� ��������.' AS message;
    END IF;
END $$
DELIMITER ;

UPDATE customer_order
SET total_cost = fn_calculate_order_total(order_id);
