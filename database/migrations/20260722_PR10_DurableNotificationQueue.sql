/*
PR-10 - expansion compatible de la cola de notificaciones.

Precondiciones:
  - ejecutar primero en una copia/no-productivo equivalente;
  - validar el inventario de columnas y el plan con DBA;
  - mantener P360_NOTIFICATION_QUEUE_MODE=legacy durante la expansion;
  - no ejecutar una contraccion de columnas como rollback inmediato.

El script es reejecutable y no elimina datos ni objetos existentes.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF SCHEMA_ID(N'P360Insight') IS NULL
    THROW 51000, 'No existe el esquema P360Insight.', 1;

IF OBJECT_ID(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'U') IS NULL
    THROW 51000, 'No existe la tabla de cola esperada.', 1;

IF OBJECT_ID(
    N'P360Insight.V_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'V') IS NULL
    THROW 51000, 'No existe la vista de cola esperada.', 1;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'cola_notificacion_id') IS NULL OR
   COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'report_id') IS NULL OR
   COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'enviado') IS NULL OR
   COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'intentos_envio') IS NULL OR
   COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'fecha_envio') IS NULL
    THROW 51000, 'La tabla de cola no cumple el contrato legado minimo.', 1;

BEGIN TRANSACTION;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_notification_key') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_notification_key uniqueidentifier NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_delivery_status') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_delivery_status varchar(20) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_lease_owner') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_lease_owner nvarchar(128) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_lease_token') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_lease_token uniqueidentifier NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_lease_until_utc') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_lease_until_utc datetime2(3) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_attempt_count') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_attempt_count int NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_next_attempt_utc') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_next_attempt_utc datetime2(3) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_sent_utc') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_sent_utc datetime2(3) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_last_error_code') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_last_error_code varchar(64) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_dead_letter_utc') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_dead_letter_utc datetime2(3) NULL;

IF COL_LENGTH(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES',
    N'p360_modified_utc') IS NULL
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_modified_utc datetime2(3) NULL;

IF NOT EXISTS
(
    SELECT 1
      FROM sys.columns
     WHERE object_id = OBJECT_ID(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES')
       AND system_type_id = 189
)
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD p360_row_version rowversion NOT NULL;
END;

COMMIT TRANSACTION;
GO

BEGIN TRANSACTION;

UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
   SET p360_notification_key = NEWID()
 WHERE p360_notification_key IS NULL;

ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
    ALTER COLUMN p360_notification_key uniqueidentifier NOT NULL;

IF OBJECT_ID(
    N'P360Insight.DF_P360_NotificationQueue_Key',
    N'D') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD CONSTRAINT DF_P360_NotificationQueue_Key
        DEFAULT NEWSEQUENTIALID() FOR p360_notification_key;
END;

UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
   SET p360_delivery_status =
        CASE WHEN ISNULL(enviado, 0) = 1 THEN 'sent' ELSE 'pending' END
 WHERE p360_delivery_status IS NULL;

ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
    ALTER COLUMN p360_delivery_status varchar(20) NOT NULL;

IF OBJECT_ID(
    N'P360Insight.DF_P360_NotificationQueue_Status',
    N'D') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD CONSTRAINT DF_P360_NotificationQueue_Status
        DEFAULT ('pending') FOR p360_delivery_status;
END;

UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
   SET p360_attempt_count =
        CASE
            WHEN ISNULL(intentos_envio, 0) < 0 THEN 0
            ELSE ISNULL(intentos_envio, 0)
        END
 WHERE p360_attempt_count IS NULL;

ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
    ALTER COLUMN p360_attempt_count int NOT NULL;

IF OBJECT_ID(
    N'P360Insight.DF_P360_NotificationQueue_Attempt',
    N'D') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD CONSTRAINT DF_P360_NotificationQueue_Attempt
        DEFAULT (0) FOR p360_attempt_count;
END;

UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
   SET p360_modified_utc = SYSUTCDATETIME()
 WHERE p360_modified_utc IS NULL;

ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
    ALTER COLUMN p360_modified_utc datetime2(3) NOT NULL;

IF OBJECT_ID(
    N'P360Insight.DF_P360_NotificationQueue_Modified',
    N'D') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        ADD CONSTRAINT DF_P360_NotificationQueue_Modified
        DEFAULT SYSUTCDATETIME() FOR p360_modified_utc;
END;

IF OBJECT_ID(
    N'P360Insight.CK_P360_NotificationQueue_Status',
    N'C') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        WITH CHECK ADD CONSTRAINT CK_P360_NotificationQueue_Status CHECK
        (
            p360_delivery_status IN
                ('pending', 'processing', 'retry', 'sent', 'dead_letter')
        );
END;

IF OBJECT_ID(
    N'P360Insight.CK_P360_NotificationQueue_Attempt',
    N'C') IS NULL
BEGIN
    ALTER TABLE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        WITH CHECK ADD CONSTRAINT CK_P360_NotificationQueue_Attempt
        CHECK (p360_attempt_count >= 0);
END;

IF NOT EXISTS
(
    SELECT 1
      FROM sys.indexes
     WHERE object_id = OBJECT_ID(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES')
       AND name = N'UX_P360_NotificationQueue_Key'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_P360_NotificationQueue_Key
        ON P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
            (p360_notification_key);
END;

IF NOT EXISTS
(
    SELECT 1
      FROM sys.indexes
     WHERE object_id = OBJECT_ID(
        N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES')
       AND name = N'IX_P360_NotificationQueue_Claim'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_P360_NotificationQueue_Claim
        ON P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        (
            report_id,
            enviado,
            p360_delivery_status,
            p360_next_attempt_utc,
            p360_lease_until_utc,
            cola_notificacion_id
        )
        INCLUDE (p360_attempt_count, p360_notification_key);
END;

IF OBJECT_ID(
    N'P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT',
    N'U') IS NULL
BEGIN
    CREATE TABLE
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
    (
        audit_id bigint IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_P360_NotificationQueue_Audit PRIMARY KEY,
        cola_notificacion_id int NOT NULL,
        notification_key uniqueidentifier NOT NULL,
        transition varchar(32) NOT NULL,
        status varchar(20) NOT NULL,
        attempt_count int NOT NULL,
        occurred_utc datetime2(3) NOT NULL
            CONSTRAINT DF_P360_NotificationQueue_Audit_Occurred
            DEFAULT SYSUTCDATETIME(),
        actor nvarchar(128) NULL,
        error_code varchar(64) NULL,
        reason nvarchar(256) NULL
    );

    CREATE NONCLUSTERED INDEX IX_P360_NotificationQueue_Audit_Key
        ON P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
            (notification_key, occurred_utc);
END;

COMMIT TRANSACTION;
GO

IF OBJECT_ID(
    N'P360Insight.SP_ClaimScheduledReportNotifications',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_ClaimScheduledReportNotifications AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_ClaimScheduledReportNotifications
    @report_id int,
    @lease_owner nvarchar(128),
    @lease_seconds int,
    @batch_size int,
    @max_attempts int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @report_id <= 0 OR
       NULLIF(LTRIM(RTRIM(@lease_owner)), N'') IS NULL OR
       LEN(@lease_owner) > 128 OR
       @lease_seconds < 30 OR @lease_seconds > 3600 OR
       @batch_size < 1 OR @batch_size > 500 OR
       @max_attempts < 1 OR @max_attempts > 100
        THROW 51000, 'Parametros de claim fuera de contrato.', 1;

    DECLARE @now datetime2(3) = SYSUTCDATETIME();
    DECLARE @claimed TABLE
    (
        cola_notificacion_id int NOT NULL PRIMARY KEY,
        notification_key uniqueidentifier NOT NULL,
        lease_token uniqueidentifier NOT NULL,
        lease_until_utc datetime2(3) NOT NULL,
        attempt_count int NOT NULL
    );
    DECLARE @exhausted TABLE
    (
        cola_notificacion_id int NOT NULL,
        notification_key uniqueidentifier NOT NULL,
        attempt_count int NOT NULL
    );

    BEGIN TRANSACTION;

    UPDATE q
       SET p360_delivery_status = 'dead_letter',
           p360_dead_letter_utc = @now,
           p360_modified_utc = @now,
           p360_last_error_code =
                COALESCE(p360_last_error_code, 'attempts.exhausted'),
           p360_lease_owner = NULL,
           p360_lease_token = NULL,
           p360_lease_until_utc = NULL
    OUTPUT
        inserted.cola_notificacion_id,
        inserted.p360_notification_key,
        inserted.p360_attempt_count
      INTO @exhausted
    FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES q
    WHERE q.report_id = @report_id
      AND ISNULL(q.enviado, 0) = 0
      AND
      (
          q.p360_delivery_status IN ('pending', 'retry')
          OR
          (
              q.p360_delivery_status = 'processing'
              AND q.p360_lease_until_utc < @now
          )
      )
      AND q.p360_attempt_count >= @max_attempts;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor,
            error_code
        )
    SELECT
        cola_notificacion_id,
        notification_key,
        'dead_letter',
        'dead_letter',
        attempt_count,
        @now,
        @lease_owner,
        'attempts.exhausted'
    FROM @exhausted;

    ;WITH candidates AS
    (
        SELECT TOP (@batch_size) q.*
        FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES q
            WITH (UPDLOCK, READPAST, ROWLOCK, READCOMMITTEDLOCK)
        WHERE q.report_id = @report_id
          AND ISNULL(q.enviado, 0) = 0
          AND q.p360_attempt_count < @max_attempts
          AND
          (
              q.p360_delivery_status IN ('pending', 'retry')
              OR
              (
                  q.p360_delivery_status = 'processing'
                  AND q.p360_lease_until_utc < @now
              )
          )
          AND
          (
              q.p360_next_attempt_utc IS NULL
              OR q.p360_next_attempt_utc <= @now
          )
        ORDER BY q.cola_notificacion_id
    )
    UPDATE candidates
       SET p360_delivery_status = 'processing',
           p360_lease_owner = @lease_owner,
           p360_lease_token = NEWID(),
           p360_lease_until_utc = DATEADD(second, @lease_seconds, @now),
           p360_attempt_count = p360_attempt_count + 1,
           p360_modified_utc = @now,
           p360_last_error_code = NULL
    OUTPUT
        inserted.cola_notificacion_id,
        inserted.p360_notification_key,
        inserted.p360_lease_token,
        inserted.p360_lease_until_utc,
        inserted.p360_attempt_count
      INTO @claimed;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor
        )
    SELECT
        cola_notificacion_id,
        notification_key,
        'claim',
        'processing',
        attempt_count,
        @now,
        @lease_owner
    FROM @claimed;

    COMMIT TRANSACTION;

    SELECT
        v.cola_notificacion_id,
        v.report_id,
        v.report_uid,
        v.report_name,
        v.report_insight,
        v.report_type,
        v.referencia_evento,
        v.referencia_evento_id,
        v.cod_colab,
        v.nombre_colab,
        v.email_colab,
        v.cod_sup,
        v.nombre_sup,
        v.email_sup,
        c.notification_key,
        CAST('processing' AS varchar(20)) AS delivery_status,
        @lease_owner AS lease_owner,
        c.lease_token,
        c.lease_until_utc,
        c.attempt_count
    FROM @claimed c
    INNER JOIN
        P360Insight.V_SCHEDULED_REPORTS_COLA_NOTIFICACIONES v
        ON v.cola_notificacion_id = c.cola_notificacion_id
    ORDER BY c.cola_notificacion_id;
END;
GO

IF OBJECT_ID(
    N'P360Insight.SP_RenewScheduledReportNotificationLease',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_RenewScheduledReportNotificationLease AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_RenewScheduledReportNotificationLease
    @notification_id int,
    @lease_owner nvarchar(128),
    @lease_token uniqueidentifier,
    @lease_seconds int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @notification_id <= 0 OR
       NULLIF(LTRIM(RTRIM(@lease_owner)), N'') IS NULL OR
       @lease_seconds < 30 OR @lease_seconds > 3600
        THROW 51000, 'Parametros de renovacion fuera de contrato.', 1;

    DECLARE @now datetime2(3) = SYSUTCDATETIME();
    DECLARE @renewed TABLE
    (
        cola_notificacion_id int NOT NULL,
        notification_key uniqueidentifier NOT NULL,
        attempt_count int NOT NULL
    );

    BEGIN TRANSACTION;

    UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
       SET p360_lease_until_utc = DATEADD(second, @lease_seconds, @now),
           p360_modified_utc = @now
    OUTPUT
        inserted.cola_notificacion_id,
        inserted.p360_notification_key,
        inserted.p360_attempt_count
      INTO @renewed
    WHERE cola_notificacion_id = @notification_id
      AND p360_delivery_status = 'processing'
      AND p360_lease_owner = @lease_owner
      AND p360_lease_token = @lease_token
      AND p360_lease_until_utc >= @now;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor
        )
    SELECT
        cola_notificacion_id,
        notification_key,
        'lease_renewed',
        'processing',
        attempt_count,
        @now,
        @lease_owner
    FROM @renewed;

    COMMIT TRANSACTION;

    SELECT CONVERT(int, CASE WHEN EXISTS(SELECT 1 FROM @renewed)
        THEN 1 ELSE 0 END);
END;
GO

IF OBJECT_ID(
    N'P360Insight.SP_CompleteScheduledReportNotification',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_CompleteScheduledReportNotification AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_CompleteScheduledReportNotification
    @notification_id int,
    @lease_owner nvarchar(128),
    @lease_token uniqueidentifier
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @now datetime2(3) = SYSUTCDATETIME();
    DECLARE @completed TABLE
    (
        cola_notificacion_id int NOT NULL,
        notification_key uniqueidentifier NOT NULL,
        attempt_count int NOT NULL
    );

    BEGIN TRANSACTION;

    UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
       SET p360_delivery_status = 'sent',
           enviado = 1,
           intentos_envio = ISNULL(intentos_envio, 0) + 1,
           fecha_envio = GETDATE(),
           p360_sent_utc = @now,
           p360_modified_utc = @now,
           p360_next_attempt_utc = NULL,
           p360_last_error_code = NULL,
           p360_dead_letter_utc = NULL,
           p360_lease_owner = NULL,
           p360_lease_token = NULL,
           p360_lease_until_utc = NULL
    OUTPUT
        inserted.cola_notificacion_id,
        inserted.p360_notification_key,
        inserted.p360_attempt_count
      INTO @completed
    WHERE cola_notificacion_id = @notification_id
      AND p360_delivery_status = 'processing'
      AND p360_lease_owner = @lease_owner
      AND p360_lease_token = @lease_token;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor
        )
    SELECT
        cola_notificacion_id,
        notification_key,
        'sent',
        'sent',
        attempt_count,
        @now,
        @lease_owner
    FROM @completed;

    COMMIT TRANSACTION;

    SELECT CONVERT(int, CASE WHEN EXISTS(SELECT 1 FROM @completed)
        THEN 1 ELSE 0 END);
END;
GO

IF OBJECT_ID(
    N'P360Insight.SP_FailScheduledReportNotification',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_FailScheduledReportNotification AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_FailScheduledReportNotification
    @notification_id int,
    @lease_owner nvarchar(128),
    @lease_token uniqueidentifier,
    @permanent bit,
    @error_code varchar(64),
    @max_attempts int,
    @retry_base_seconds int,
    @retry_max_seconds int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @notification_id <= 0 OR
       NULLIF(LTRIM(RTRIM(@lease_owner)), N'') IS NULL OR
       NULLIF(LTRIM(RTRIM(@error_code)), '') IS NULL OR
       LEN(@error_code) > 64 OR
       @max_attempts < 1 OR @max_attempts > 100 OR
       @retry_base_seconds < 1 OR @retry_base_seconds > 3600 OR
       @retry_max_seconds < @retry_base_seconds OR
       @retry_max_seconds > 86400
        THROW 51000, 'Parametros de fallo fuera de contrato.', 1;

    DECLARE @now datetime2(3) = SYSUTCDATETIME();
    DECLARE @attempt int;
    DECLARE @notification_key uniqueidentifier;

    BEGIN TRANSACTION;

    SELECT
        @attempt = p360_attempt_count,
        @notification_key = p360_notification_key
    FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
        WITH (UPDLOCK, ROWLOCK)
    WHERE cola_notificacion_id = @notification_id
      AND p360_delivery_status = 'processing'
      AND p360_lease_owner = @lease_owner
      AND p360_lease_token = @lease_token;

    IF @attempt IS NULL
    BEGIN
        COMMIT TRANSACTION;
        SELECT CAST('lease_lost' AS varchar(20));
        RETURN;
    END;

    DECLARE @status varchar(20) =
        CASE
            WHEN @permanent = 1 OR @attempt >= @max_attempts
                THEN 'dead_letter'
            ELSE 'retry'
        END;
    DECLARE @exponent int =
        CASE WHEN @attempt <= 1 THEN 0
             WHEN @attempt > 21 THEN 20
             ELSE @attempt - 1 END;
    DECLARE @raw_delay bigint =
        CONVERT(bigint, @retry_base_seconds) *
        CONVERT(bigint, POWER(CONVERT(float, 2), @exponent));
    DECLARE @bounded_delay int =
        CASE WHEN @raw_delay > @retry_max_seconds
             THEN @retry_max_seconds
             ELSE CONVERT(int, @raw_delay) END;
    DECLARE @jitter_percent int = 80 +
        CONVERT(int,
            (CONVERT(bigint, CHECKSUM(@notification_id, @attempt)) &
             CONVERT(bigint, 2147483647)) % 41);
    DECLARE @delay_seconds int =
        CASE
            WHEN (@bounded_delay * @jitter_percent) / 100 < 1 THEN 1
            WHEN (@bounded_delay * @jitter_percent) / 100 >
                 @retry_max_seconds THEN @retry_max_seconds
            ELSE (@bounded_delay * @jitter_percent) / 100
        END;

    UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
       SET p360_delivery_status = @status,
           p360_next_attempt_utc =
                CASE WHEN @status = 'retry'
                     THEN DATEADD(second, @delay_seconds, @now)
                     ELSE NULL END,
           p360_dead_letter_utc =
                CASE WHEN @status = 'dead_letter' THEN @now ELSE NULL END,
           p360_last_error_code = @error_code,
           p360_modified_utc = @now,
           p360_lease_owner = NULL,
           p360_lease_token = NULL,
           p360_lease_until_utc = NULL
    WHERE cola_notificacion_id = @notification_id;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor,
            error_code
        )
    VALUES
        (
            @notification_id,
            @notification_key,
            @status,
            @status,
            @attempt,
            @now,
            @lease_owner,
            @error_code
        );

    COMMIT TRANSACTION;
    SELECT @status;
END;
GO

IF OBJECT_ID(
    N'P360Insight.SP_RequeueDeadScheduledReportNotification',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_RequeueDeadScheduledReportNotification AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_RequeueDeadScheduledReportNotification
    @notification_key uniqueidentifier,
    @operator nvarchar(128),
    @reason nvarchar(256)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NULLIF(LTRIM(RTRIM(@operator)), N'') IS NULL OR
       LEN(@operator) > 128 OR
       NULLIF(LTRIM(RTRIM(@reason)), N'') IS NULL OR
       LEN(@reason) > 256
        THROW 51000, 'Operador y motivo son obligatorios.', 1;

    DECLARE @now datetime2(3) = SYSUTCDATETIME();
    DECLARE @requeued TABLE
    (
        cola_notificacion_id int NOT NULL,
        notification_key uniqueidentifier NOT NULL
    );

    BEGIN TRANSACTION;

    UPDATE P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
       SET p360_delivery_status = 'retry',
           p360_attempt_count = 0,
           p360_next_attempt_utc = @now,
           p360_dead_letter_utc = NULL,
           p360_last_error_code = NULL,
           p360_modified_utc = @now,
           p360_lease_owner = NULL,
           p360_lease_token = NULL,
           p360_lease_until_utc = NULL
    OUTPUT
        inserted.cola_notificacion_id,
        inserted.p360_notification_key
      INTO @requeued
    WHERE p360_notification_key = @notification_key
      AND p360_delivery_status = 'dead_letter'
      AND ISNULL(enviado, 0) = 0;

    INSERT INTO
        P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES_AUDIT
        (
            cola_notificacion_id,
            notification_key,
            transition,
            status,
            attempt_count,
            occurred_utc,
            actor,
            reason
        )
    SELECT
        cola_notificacion_id,
        notification_key,
        'manual_requeue',
        'retry',
        0,
        @now,
        @operator,
        @reason
    FROM @requeued;

    COMMIT TRANSACTION;

    SELECT CONVERT(int, CASE WHEN EXISTS(SELECT 1 FROM @requeued)
        THEN 1 ELSE 0 END);
END;
GO

IF OBJECT_ID(
    N'P360Insight.SP_GetDeadScheduledReportNotifications',
    N'P') IS NULL
    EXEC(N'CREATE PROCEDURE P360Insight.SP_GetDeadScheduledReportNotifications AS RETURN 0;');
GO

ALTER PROCEDURE P360Insight.SP_GetDeadScheduledReportNotifications
    @report_id int = NULL,
    @limit int = 200
AS
BEGIN
    SET NOCOUNT ON;

    IF @limit < 1 OR @limit > 1000
        THROW 51000, 'El limite debe estar entre 1 y 1000.', 1;

    SELECT TOP (@limit)
        p360_notification_key AS notification_key,
        report_id,
        p360_attempt_count AS attempt_count,
        p360_last_error_code AS error_code,
        p360_dead_letter_utc AS dead_letter_utc,
        p360_modified_utc AS modified_utc
    FROM P360Insight.T_SCHEDULED_REPORTS_COLA_NOTIFICACIONES
    WHERE p360_delivery_status = 'dead_letter'
      AND (@report_id IS NULL OR report_id = @report_id)
    ORDER BY p360_dead_letter_utc, cola_notificacion_id;
END;
GO
