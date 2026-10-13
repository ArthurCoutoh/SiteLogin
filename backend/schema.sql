CREATE TABLE IF NOT EXISTS users (
 id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
 name VARCHAR(100) NOT NULL,
 email VARCHAR(254) CHARACTER SET ascii COLLATE ascii_bin NOT NULL UNIQUE,
 password_hash VARCHAR(512) NOT NULL,
 role ENUM('admin','student') NOT NULL DEFAULT 'student',
 status ENUM('pending','active','blocked') NOT NULL DEFAULT 'pending',
 failed_attempts INT NOT NULL DEFAULT 0,
 locked_until DATETIME NULL,
 created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS sessions (
 token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin PRIMARY KEY,
 user_id BIGINT NOT NULL,
 expires_at DATETIME NOT NULL,
 FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
 INDEX session_user (user_id), INDEX session_expiry (expires_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
