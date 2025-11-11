-- Script para crear la tabla de cola de correos electrónicos
-- Esta tabla registra todos los intentos de envío de correos con su estado y errores

CREATE TABLE IF NOT EXISTS email_queue (
    id SERIAL PRIMARY KEY,
    to_email VARCHAR(255) NOT NULL,
    subject VARCHAR(500) NOT NULL,
    body TEXT NOT NULL,
    email_type VARCHAR(50) NOT NULL, -- 'welcome', 'password_recovery', etc.
    status VARCHAR(20) NOT NULL DEFAULT 'pending', -- 'pending', 'processing', 'sent', 'failed'
    error_message TEXT,
    error_type VARCHAR(100), -- 'smtp_auth', 'network', 'invalid_email', etc.
    retry_count INTEGER NOT NULL DEFAULT 0,
    max_retries INTEGER NOT NULL DEFAULT 3,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    processed_at TIMESTAMP,
    sent_at TIMESTAMP,
    failed_at TIMESTAMP,
    metadata JSONB, -- Información adicional como userId, userName, etc.
    CONSTRAINT chk_status CHECK (status IN ('pending', 'processing', 'sent', 'failed'))
);

-- Índices para mejorar el rendimiento
CREATE INDEX IF NOT EXISTS idx_email_queue_status ON email_queue(status);
CREATE INDEX IF NOT EXISTS idx_email_queue_created_at ON email_queue(created_at);
CREATE INDEX IF NOT EXISTS idx_email_queue_email_type ON email_queue(email_type);
CREATE INDEX IF NOT EXISTS idx_email_queue_retry_count ON email_queue(retry_count, max_retries) WHERE status = 'failed';

-- Comentarios
COMMENT ON TABLE email_queue IS 'Cola de correos electrónicos para envío asíncrono';
COMMENT ON COLUMN email_queue.email_type IS 'Tipo de correo: welcome, password_recovery, etc.';
COMMENT ON COLUMN email_queue.status IS 'Estado del correo: pending, processing, sent, failed';
COMMENT ON COLUMN email_queue.error_type IS 'Tipo de error si falla: smtp_auth, network, invalid_email, etc.';
COMMENT ON COLUMN email_queue.metadata IS 'Información adicional en formato JSON (userId, userName, etc.)';

