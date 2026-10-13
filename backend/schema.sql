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

CREATE TABLE IF NOT EXISTS user_settings (
 user_id BIGINT PRIMARY KEY,
 share_with_teacher BOOLEAN NOT NULL DEFAULT FALSE,
 terms_version VARCHAR(30) NULL,
 accepted_at DATETIME NULL,
 FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS submissions (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 user_id BIGINT NOT NULL,
 exercise_number INT NOT NULL,
 title VARCHAR(150) NOT NULL,
 solution MEDIUMTEXT NOT NULL,
 feedback TEXT NULL,
 feedback_at DATETIME NULL,
 created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
 FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
 INDEX submission_user(user_id,created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS password_requests (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 user_id BIGINT NOT NULL,
 status ENUM('pending','approved','completed','denied') NOT NULL DEFAULT 'pending',
 token_hash CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL UNIQUE,
 expires_at DATETIME NULL,
 created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
 FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE,
 INDEX request_user(user_id,status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS user_profiles (
 user_id BIGINT PRIMARY KEY,
 biography VARCHAR(600) NOT NULL DEFAULT '',
 avatar MEDIUMTEXT NULL,
 discoverable BOOLEAN NOT NULL DEFAULT FALSE,
 FOREIGN KEY(user_id) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS friendships (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 low_user BIGINT NOT NULL,
 high_user BIGINT NOT NULL,
 requested_by BIGINT NOT NULL,
 status ENUM('pending','accepted','declined','removed','blocked') NOT NULL DEFAULT 'pending',
 blocked_by BIGINT NULL,
 updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
 UNIQUE friendship_pair(low_user,high_user),
 FOREIGN KEY(low_user) REFERENCES users(id) ON DELETE CASCADE,
 FOREIGN KEY(high_user) REFERENCES users(id) ON DELETE CASCADE,
 FOREIGN KEY(requested_by) REFERENCES users(id) ON DELETE CASCADE,
 FOREIGN KEY(blocked_by) REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS messages (
 id BIGINT AUTO_INCREMENT PRIMARY KEY,
 friendship_id BIGINT NOT NULL,
 sender_id BIGINT NOT NULL,
 content VARCHAR(2000) NOT NULL,
 sent_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
 FOREIGN KEY(friendship_id) REFERENCES friendships(id) ON DELETE CASCADE,
 FOREIGN KEY(sender_id) REFERENCES users(id) ON DELETE CASCADE,
 INDEX message_thread(friendship_id,id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
