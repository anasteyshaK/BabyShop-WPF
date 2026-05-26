USE `baby_shop_restored`;

DROP VIEW IF EXISTS `users_view`;
CREATE ALGORITHM=UNDEFINED
DEFINER=`root`@`localhost`
SQL SECURITY DEFINER
VIEW `users_view` AS
SELECT
    u.user_id,
    u.username,
    u.password_hash,
    u.role_name,
    CASE
        WHEN u.is_active = 1 THEN 'Активен'
        ELSE 'Неактивен'
    END AS status_label,
    u.is_active,
    u.created_at,
    u.last_login_at
FROM app_user AS u;

DROP PROCEDURE IF EXISTS `AddAuditLog`;
DROP PROCEDURE IF EXISTS `AddBackupHistory`;
DROP PROCEDURE IF EXISTS `GetAuditReport`;
DROP PROCEDURE IF EXISTS `GetAuditSummary`;
DROP PROCEDURE IF EXISTS `GetBackupHistory`;
DROP PROCEDURE IF EXISTS `GetLastBackup`;
DROP PROCEDURE IF EXISTS `GetUserPermissions`;
DROP PROCEDURE IF EXISTS `LogoutUser`;
DROP PROCEDURE IF EXISTS `ViewUsers`;
DROP PROCEDURE IF EXISTS `AddUser`;
DROP PROCEDURE IF EXISTS `UpdateUser`;
DROP PROCEDURE IF EXISTS `DeleteUser`;
DROP PROCEDURE IF EXISTS `CreateCheckoutOrderByFullName`;
DROP PROCEDURE IF EXISTS `GetCustomerOrdersByFullName`;
DROP PROCEDURE IF EXISTS `GetCustomerOrderDetailsByFullName`;

DELIMITER $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `AddAuditLog` (
    IN `p_user_id` INT,
    IN `p_username` VARCHAR(50),
    IN `p_action_type` VARCHAR(50),
    IN `p_entity_name` VARCHAR(50),
    IN `p_entity_id` INT,
    IN `p_action_description` VARCHAR(255)
)
BEGIN
    INSERT INTO audit_log
    (
        user_id,
        username,
        action_type,
        entity_name,
        entity_id,
        action_description
    )
    VALUES
    (
        NULLIF(p_user_id, 0),
        NULLIF(TRIM(p_username), ''),
        TRIM(p_action_type),
        NULLIF(TRIM(p_entity_name), ''),
        p_entity_id,
        NULLIF(TRIM(p_action_description), '')
    );
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `AddBackupHistory` (
    IN `p_user_id` INT,
    IN `p_username` VARCHAR(50),
    IN `p_operation_type` VARCHAR(20),
    IN `p_file_name` VARCHAR(255),
    IN `p_file_path` VARCHAR(500),
    IN `p_file_size_kb` DECIMAL(12,2),
    IN `p_database_name` VARCHAR(100),
    IN `p_status` VARCHAR(20),
    IN `p_message` VARCHAR(500)
)
BEGIN
    INSERT INTO backup_history
    (
        user_id,
        username,
        operation_type,
        file_name,
        file_path,
        file_size_kb,
        database_name,
        status,
        message
    )
    VALUES
    (
        NULLIF(p_user_id, 0),
        NULLIF(TRIM(p_username), ''),
        TRIM(p_operation_type),
        TRIM(p_file_name),
        TRIM(p_file_path),
        p_file_size_kb,
        TRIM(p_database_name),
        TRIM(p_status),
        NULLIF(TRIM(p_message), '')
    );
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetAuditReport` (
    IN `p_date_from` DATETIME,
    IN `p_date_to` DATETIME,
    IN `p_username` VARCHAR(50),
    IN `p_action_type` VARCHAR(50)
)
BEGIN
    SELECT
        audit_id,
        created_at,
        user_id,
        username,
        role_name,
        action_type,
        action_label,
        entity_name,
        entity_id,
        action_description
    FROM audit_report_view
    WHERE (p_date_from IS NULL OR created_at >= p_date_from)
      AND (p_date_to IS NULL OR created_at <= p_date_to)
      AND (p_username IS NULL OR TRIM(p_username) = '' OR username = TRIM(p_username))
      AND (p_action_type IS NULL OR TRIM(p_action_type) = '' OR action_type = TRIM(p_action_type))
    ORDER BY created_at DESC, audit_id DESC;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetAuditSummary` (
    IN `p_date_from` DATETIME,
    IN `p_date_to` DATETIME
)
BEGIN
    SELECT
        COUNT(*) AS total_actions,
        SUM(CASE WHEN action_type = 'LOGIN' THEN 1 ELSE 0 END) AS successful_logins,
        SUM(CASE WHEN action_type = 'LOGIN_FAILED' THEN 1 ELSE 0 END) AS failed_logins,
        SUM(CASE WHEN action_type = 'REGISTER' THEN 1 ELSE 0 END) AS registrations,
        (SELECT COUNT(*) FROM app_user WHERE is_active = 1) AS active_users
    FROM audit_log
    WHERE (p_date_from IS NULL OR created_at >= p_date_from)
      AND (p_date_to IS NULL OR created_at <= p_date_to);
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetBackupHistory` ()
BEGIN
    SELECT
        backup_id,
        username,
        operation_type,
        file_name,
        file_path,
        file_size_kb,
        database_name,
        status,
        message,
        created_at
    FROM backup_history
    ORDER BY created_at DESC, backup_id DESC;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetLastBackup` ()
BEGIN
    SELECT
        backup_id,
        username,
        operation_type,
        file_name,
        file_path,
        file_size_kb,
        database_name,
        status,
        message,
        created_at
    FROM backup_history
    WHERE operation_type = 'BACKUP'
    ORDER BY created_at DESC, backup_id DESC
    LIMIT 1;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetUserPermissions` (
    IN `p_user_id` INT
)
BEGIN
    SELECT DISTINCT rp.permission_code
    FROM app_user u
    INNER JOIN role_permission rp ON rp.role_name = u.role_name
    WHERE u.user_id = p_user_id
      AND u.is_active = 1
    ORDER BY rp.permission_code;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `LogoutUser` (
    IN `p_user_id` INT
)
BEGIN
    DECLARE v_username VARCHAR(50);

    SELECT username
    INTO v_username
    FROM app_user
    WHERE user_id = p_user_id
    LIMIT 1;

    INSERT INTO audit_log
    (
        user_id,
        username,
        action_type,
        entity_name,
        entity_id,
        action_description
    )
    VALUES
    (
        p_user_id,
        v_username,
        'LOGOUT',
        'app_user',
        p_user_id,
        CONCAT('User ', COALESCE(v_username, ''), ' logged out of the system')
    );
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `ViewUsers` ()
BEGIN
    SELECT
        user_id,
        username,
        password_hash,
        role_name,
        status_label,
        is_active,
        created_at,
        last_login_at
    FROM users_view
    ORDER BY user_id;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `AddUser` (
    IN `p_actor_user_id` INT,
    IN `p_actor_username` VARCHAR(50),
    IN `p_username` VARCHAR(50),
    IN `p_password` VARCHAR(100),
    IN `p_role_name` VARCHAR(20),
    IN `p_is_active` TINYINT(1)
)
BEGIN
    DECLARE v_new_user_id INT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Failed to add user.' AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM app_user u
        INNER JOIN role_permission rp ON rp.role_name = u.role_name
        WHERE u.user_id = p_actor_user_id
          AND u.is_active = 1
          AND rp.permission_code = 'MANAGE_USERS'
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Access denied.' AS message;
    ELSEIF p_username IS NULL OR TRIM(p_username) = '' THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Username cannot be empty.' AS message;
    ELSEIF p_password IS NULL OR TRIM(p_password) = '' THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Password cannot be empty.' AS message;
    ELSEIF EXISTS (
        SELECT 1
        FROM app_user
        WHERE username = TRIM(p_username)
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Username already exists.' AS message;
    ELSEIF NOT EXISTS (
        SELECT 1
        FROM user_role
        WHERE role_name = TRIM(p_role_name)
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Role not found.' AS message;
    ELSE
        INSERT INTO app_user
        (
            username,
            password_hash,
            role_name,
            is_active,
            created_at
        )
        VALUES
        (
            TRIM(p_username),
            SHA2(p_password, 256),
            TRIM(p_role_name),
            IFNULL(p_is_active, 1),
            NOW()
        );

        SET v_new_user_id = LAST_INSERT_ID();

        CALL AddAuditLog(
            p_actor_user_id,
            p_actor_username,
            'ADD_USER',
            'app_user',
            v_new_user_id,
            CONCAT('Added user ', TRIM(p_username), ' with role ', TRIM(p_role_name))
        );

        COMMIT;

        SELECT 1 AS success, 'User added successfully.' AS message, v_new_user_id AS user_id;
    END IF;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `UpdateUser` (
    IN `p_actor_user_id` INT,
    IN `p_actor_username` VARCHAR(50),
    IN `p_user_id` INT,
    IN `p_username` VARCHAR(50),
    IN `p_password` VARCHAR(100),
    IN `p_role_name` VARCHAR(20),
    IN `p_is_active` TINYINT(1)
)
BEGIN
    DECLARE v_old_username VARCHAR(50);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Failed to update user.' AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM app_user u
        INNER JOIN role_permission rp ON rp.role_name = u.role_name
        WHERE u.user_id = p_actor_user_id
          AND u.is_active = 1
          AND rp.permission_code = 'MANAGE_USERS'
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Access denied.' AS message;
    ELSEIF NOT EXISTS (
        SELECT 1
        FROM app_user
        WHERE user_id = p_user_id
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: User not found.' AS message;
    ELSEIF p_username IS NULL OR TRIM(p_username) = '' THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Username cannot be empty.' AS message;
    ELSEIF EXISTS (
        SELECT 1
        FROM app_user
        WHERE username = TRIM(p_username)
          AND user_id <> p_user_id
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Username already exists.' AS message;
    ELSEIF NOT EXISTS (
        SELECT 1
        FROM user_role
        WHERE role_name = TRIM(p_role_name)
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Role not found.' AS message;
    ELSE
        SELECT username
        INTO v_old_username
        FROM app_user
        WHERE user_id = p_user_id;

        UPDATE app_user
        SET
            username = TRIM(p_username),
            role_name = TRIM(p_role_name),
            is_active = IFNULL(p_is_active, is_active),
            password_hash = CASE
                WHEN p_password IS NULL OR TRIM(p_password) = '' THEN password_hash
                ELSE SHA2(p_password, 256)
            END
        WHERE user_id = p_user_id;

        CALL AddAuditLog(
            p_actor_user_id,
            p_actor_username,
            'EDIT_USER',
            'app_user',
            p_user_id,
            CONCAT('Updated user ', v_old_username, ' -> ', TRIM(p_username))
        );

        COMMIT;

        SELECT 1 AS success, 'User updated successfully.' AS message, p_user_id AS user_id;
    END IF;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `DeleteUser` (
    IN `p_actor_user_id` INT,
    IN `p_actor_username` VARCHAR(50),
    IN `p_user_id` INT
)
BEGIN
    DECLARE v_username VARCHAR(50);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Failed to delete user.' AS message;
    END;

    START TRANSACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM app_user u
        INNER JOIN role_permission rp ON rp.role_name = u.role_name
        WHERE u.user_id = p_actor_user_id
          AND u.is_active = 1
          AND rp.permission_code = 'MANAGE_USERS'
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Access denied.' AS message;
    ELSEIF NOT EXISTS (
        SELECT 1
        FROM app_user
        WHERE user_id = p_user_id
    ) THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: User not found.' AS message;
    ELSE
        SELECT username
        INTO v_username
        FROM app_user
        WHERE user_id = p_user_id;

        DELETE FROM app_user
        WHERE user_id = p_user_id;

        CALL AddAuditLog(
            p_actor_user_id,
            p_actor_username,
            'DELETE_USER',
            'app_user',
            p_user_id,
            CONCAT('Deleted user ', v_username)
        );

        COMMIT;

        SELECT 1 AS success, 'User deleted successfully.' AS message, p_user_id AS user_id;
    END IF;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `CreateCheckoutOrderByFullName` (
    IN `p_full_name` VARCHAR(255),
    IN `p_phone_digits` VARCHAR(30),
    IN `p_delivery_address` VARCHAR(255),
    IN `p_start_date` DATETIME,
    IN `p_end_date` DATETIME,
    IN `p_order_status` VARCHAR(50)
)
BEGIN
    DECLARE v_customer_id INT;
    DECLARE v_order_id INT;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Could not create order.' AS message, NULL AS customer_id, NULL AS order_id;
    END;

    START TRANSACTION;

    IF p_full_name IS NULL OR TRIM(p_full_name) = '' THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Full name cannot be empty.' AS message, NULL AS customer_id, NULL AS order_id;
    ELSEIF p_delivery_address IS NULL OR TRIM(p_delivery_address) = '' THEN
        ROLLBACK;
        SELECT 0 AS success, 'ERROR: Delivery address cannot be empty.' AS message, NULL AS customer_id, NULL AS order_id;
    ELSE
        SELECT c.customer_id
        INTO v_customer_id
        FROM customer c
        WHERE LOWER(TRIM(c.c_fullname)) = LOWER(TRIM(p_full_name))
        ORDER BY c.customer_id
        LIMIT 1;

        IF v_customer_id IS NULL THEN
            SELECT COALESCE(MAX(customer_id), 0) + 1
            INTO v_customer_id
            FROM customer;

            INSERT INTO customer
            (
                customer_id,
                c_fullname,
                c_phone_number
            )
            VALUES
            (
                v_customer_id,
                TRIM(p_full_name),
                COALESCE(TRIM(p_phone_digits), '')
            );
        ELSE
            UPDATE customer
            SET c_phone_number = CASE
                    WHEN p_phone_digits IS NULL OR TRIM(p_phone_digits) = '' THEN c_phone_number
                    ELSE TRIM(p_phone_digits)
                END
            WHERE customer_id = v_customer_id;
        END IF;

        SELECT COALESCE(MAX(order_id), 0) + 1
        INTO v_order_id
        FROM customer_order;

        INSERT INTO customer_order
        (
            order_id,
            customer_id,
            delivery_address,
            start_date,
            end_date,
            order_status,
            total_cost
        )
        VALUES
        (
            v_order_id,
            v_customer_id,
            TRIM(p_delivery_address),
            p_start_date,
            p_end_date,
            TRIM(p_order_status),
            0
        );

        COMMIT;

        SELECT 1 AS success, 'Order created successfully.' AS message, v_customer_id AS customer_id, v_order_id AS order_id;
    END IF;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetCustomerOrdersByFullName` (
    IN `p_full_name` VARCHAR(255)
)
BEGIN
    SELECT
        co.order_id,
        co.start_date AS order_date,
        co.end_date,
        co.order_status,
        co.total_cost
    FROM customer_order co
    INNER JOIN customer c ON c.customer_id = co.customer_id
    WHERE LOWER(TRIM(c.c_fullname)) = LOWER(TRIM(p_full_name))
    ORDER BY co.start_date DESC, co.order_id DESC;
END $$

CREATE DEFINER=`root`@`localhost` PROCEDURE `GetCustomerOrderDetailsByFullName` (
    IN `p_full_name` VARCHAR(255),
    IN `p_order_id` INT
)
BEGIN
    SELECT
        co.order_id,
        co.customer_id,
        c.c_fullname,
        co.delivery_address,
        co.start_date,
        co.end_date,
        co.order_status,
        co.total_cost,
        op.product_id,
        p.product_title,
        cat.category_name,
        f.fabric_type,
        p.color,
        p.image_path,
        op.product_count,
        COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0) AS unit_price,
        COALESCE(op.product_count, 0) * COALESCE(p.price_per_m, 0) * COALESCE(p.fabric_amount, 0) AS line_total
    FROM customer_order co
    INNER JOIN customer c ON c.customer_id = co.customer_id
    LEFT JOIN order_product op ON op.order_id = co.order_id
    LEFT JOIN productt p ON p.product_id = op.product_id
    LEFT JOIN category cat ON cat.category_id = p.category_id
    LEFT JOIN fabric f ON f.fabric_id = p.fabric_id
    WHERE co.order_id = p_order_id
      AND LOWER(TRIM(c.c_fullname)) = LOWER(TRIM(p_full_name))
    ORDER BY op.order_product_id, p.product_title;
END $$

DELIMITER ;
