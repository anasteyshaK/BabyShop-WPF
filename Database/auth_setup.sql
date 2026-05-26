USE `baby_shop_restored`;

DROP PROCEDURE IF EXISTS `LoginUser`;
DROP PROCEDURE IF EXISTS `RegisterUser`;

DELIMITER $$

CREATE PROCEDURE `LoginUser` (
    IN `p_username` VARCHAR(50),
    IN `p_password` VARCHAR(100)
)
BEGIN
    DECLARE v_user_id INT DEFAULT NULL;
    DECLARE v_username VARCHAR(50);
    DECLARE v_role_name VARCHAR(20);
    DECLARE v_is_active TINYINT(1);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: Login failed.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    END;

    START TRANSACTION;

    SELECT
        `user_id`,
        `username`,
        `role_name`,
        `is_active`
    INTO
        v_user_id,
        v_username,
        v_role_name,
        v_is_active
    FROM `app_user`
    WHERE `username` = TRIM(p_username)
      AND `password_hash` = SHA2(p_password, 256)
    LIMIT 1;

    IF v_user_id IS NULL THEN
        INSERT INTO `audit_log`
        (
            `user_id`,
            `username`,
            `action_type`,
            `entity_name`,
            `entity_id`,
            `action_description`
        )
        VALUES
        (
            NULL,
            TRIM(p_username),
            'LOGIN_FAILED',
            'app_user',
            NULL,
            CONCAT('Failed login attempt for username ', TRIM(p_username))
        );

        COMMIT;

        SELECT
            0 AS success,
            'ERROR: Invalid username or password.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    ELSEIF v_is_active = 0 THEN
        INSERT INTO `audit_log`
        (
            `user_id`,
            `username`,
            `action_type`,
            `entity_name`,
            `entity_id`,
            `action_description`
        )
        VALUES
        (
            v_user_id,
            v_username,
            'LOGIN_BLOCKED',
            'app_user',
            v_user_id,
            CONCAT('Blocked login attempt for inactive user ', v_username)
        );

        COMMIT;

        SELECT
            0 AS success,
            'ERROR: User account is inactive.' AS message,
            v_user_id AS user_id,
            v_username AS username,
            v_role_name AS role_name;
    ELSE
        UPDATE `app_user`
        SET `last_login_at` = NOW()
        WHERE `user_id` = v_user_id;

        INSERT INTO `audit_log`
        (
            `user_id`,
            `username`,
            `action_type`,
            `entity_name`,
            `entity_id`,
            `action_description`
        )
        VALUES
        (
            v_user_id,
            v_username,
            'LOGIN',
            'app_user',
            v_user_id,
            CONCAT('User ', v_username, ' logged into the system')
        );

        COMMIT;

        SELECT
            1 AS success,
            'Login successful.' AS message,
            v_user_id AS user_id,
            v_username AS username,
            v_role_name AS role_name;
    END IF;
END $$

CREATE PROCEDURE `RegisterUser` (
    IN `p_username` VARCHAR(50),
    IN `p_password` VARCHAR(100),
    IN `p_role_name` VARCHAR(20)
)
BEGIN
    DECLARE v_new_user_id INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: Registration failed.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    END;

    START TRANSACTION;

    IF p_username IS NULL OR TRIM(p_username) = '' THEN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: Username cannot be empty.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    ELSEIF p_password IS NULL OR TRIM(p_password) = '' THEN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: Password cannot be empty.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    ELSEIF EXISTS (
        SELECT 1
        FROM `app_user`
        WHERE `username` = TRIM(p_username)
    ) THEN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: User with this username already exists.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    ELSEIF NOT EXISTS (
        SELECT 1
        FROM `user_role`
        WHERE `role_name` = TRIM(p_role_name)
    ) THEN
        ROLLBACK;
        SELECT
            0 AS success,
            'ERROR: Role was not found.' AS message,
            NULL AS user_id,
            NULL AS username,
            NULL AS role_name;
    ELSE
        INSERT INTO `app_user`
        (
            `username`,
            `password_hash`,
            `role_name`,
            `is_active`,
            `created_at`
        )
        VALUES
        (
            TRIM(p_username),
            SHA2(p_password, 256),
            TRIM(p_role_name),
            1,
            NOW()
        );

        SET v_new_user_id = LAST_INSERT_ID();

        INSERT INTO `audit_log`
        (
            `user_id`,
            `username`,
            `action_type`,
            `entity_name`,
            `entity_id`,
            `action_description`
        )
        VALUES
        (
            v_new_user_id,
            TRIM(p_username),
            'REGISTER',
            'app_user',
            v_new_user_id,
            CONCAT('User ', TRIM(p_username), ' registered with role ', TRIM(p_role_name))
        );

        COMMIT;

        SELECT
            1 AS success,
            'Registration successful.' AS message,
            v_new_user_id AS user_id,
            TRIM(p_username) AS username,
            TRIM(p_role_name) AS role_name;
    END IF;
END $$

DELIMITER ;
