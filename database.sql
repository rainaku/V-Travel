

-- 1. Table users
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    avatar_url TEXT,
    role VARCHAR(50) NOT NULL DEFAULT 'Employee',
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- Bổ sung cột avatar cho dữ liệu cũ 
ALTER TABLE users
ADD COLUMN IF NOT EXISTS avatar_url TEXT;

-- 2. Table tours
CREATE TABLE tours (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    base_price NUMERIC(18,2) NOT NULL,
    duration_days INTEGER NOT NULL,
    destination VARCHAR(255) NOT NULL,
    image_url TEXT DEFAULT ''
);

-- Bổ sung cột ảnh tour cho dữ liệu cũ 
ALTER TABLE tours
ADD COLUMN IF NOT EXISTS image_url TEXT DEFAULT '';

-- 3. Table departures
CREATE TABLE departures (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER REFERENCES tours(id) ON DELETE CASCADE,
    start_date TIMESTAMP NOT NULL,
    max_slots INTEGER NOT NULL,
    available_slots INTEGER NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Mở bán'
);

-- 4. Table customers
CREATE TABLE customers (
    id SERIAL PRIMARY KEY,
    full_name VARCHAR(255) NOT NULL,
    phone_number VARCHAR(50),
    email VARCHAR(255),
    address TEXT
);

-- 5. Table bookings
CREATE TABLE bookings (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER REFERENCES customers(id) ON DELETE RESTRICT,
    departure_id INTEGER REFERENCES departures(id) ON DELETE RESTRICT,
    user_id INTEGER REFERENCES users(id) ON DELETE RESTRICT,
    booking_date TIMESTAMP NOT NULL DEFAULT NOW(),
    guest_count INTEGER NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Chờ thanh toán'
);

-- 6. Table payments
CREATE TABLE payments (
    id SERIAL PRIMARY KEY,
    booking_id INTEGER UNIQUE REFERENCES bookings(id) ON DELETE CASCADE,
    total_amount NUMERIC(18,2) NOT NULL,
    paid_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Chưa thanh toán',
    payment_date TIMESTAMP,
    payment_method VARCHAR(100) DEFAULT 'Tiền mặt'
);

-- 7. Table notifications (persist notification center by user)
CREATE TABLE IF NOT EXISTS notifications (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    category VARCHAR(100) NOT NULL DEFAULT 'Hệ thống',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    deduplication_key VARCHAR(255) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_notifications_user_dedup
ON notifications (user_id, deduplication_key);

CREATE INDEX IF NOT EXISTS ix_notifications_user_created_at
ON notifications (user_id, created_at DESC);

-- 8. Table guide_profiles (thông tin liên hệ hướng dẫn viên)
CREATE TABLE IF NOT EXISTS guide_profiles (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    phone_number VARCHAR(50) DEFAULT '',
    email VARCHAR(255) DEFAULT '',
    emergency_contact VARCHAR(255) DEFAULT '',
    notes TEXT DEFAULT '',
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_guide_profiles_user_id
ON guide_profiles (user_id);

-- 9. Table tour_guide_assignments (gán guide cho lịch tour + lịch làm việc)
CREATE TABLE IF NOT EXISTS tour_guide_assignments (
    id SERIAL PRIMARY KEY,
    guide_user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    departure_id INTEGER NOT NULL REFERENCES departures(id) ON DELETE CASCADE,
    work_start TIMESTAMP NOT NULL,
    work_end TIMESTAMP NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Đang phân công',
    notes TEXT DEFAULT '',
    assigned_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Mỗi lịch khởi hành chỉ có 1 guide chính.
CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_guide_assignments_departure
ON tour_guide_assignments (departure_id);

CREATE INDEX IF NOT EXISTS ix_tour_guide_assignments_guide
ON tour_guide_assignments (guide_user_id);

-- Không tự động tạo guide hay tự động phân công.
-- Admin tự tạo user role Guide và tự gán phân công trong giao diện quản trị.

-- Insert 1 Admin User (Password is 'admin' hashed - just dummy hash for now, you will need to hash properly in code)
INSERT INTO users (username, password_hash, full_name, role, is_active, avatar_url)
SELECT 'admin', 'admin_hash_placeholder', 'Administrator', 'Admin', TRUE, ''
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username = 'admin');

-- 10. Table transports (Phương tiện)
CREATE TABLE IF NOT EXISTS transports (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL, -- Tên xe, ví dụ: "Xe Ford Transit 16 chỗ", "Máy bay VN Airlines"
    type VARCHAR(100) NOT NULL, -- "Xe khách", "Máy bay", "Tàu hỏa", "Tàu thủy"
    capacity INTEGER NOT NULL DEFAULT 0,
    cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Hoạt động' -- 'Hoạt động', 'Bảo trì', 'Ngừng hoạt động'
);

-- 11. Table hotels (Khách sạn / Nơi ở)
CREATE TABLE IF NOT EXISTS hotels (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    star_rating INTEGER NOT NULL DEFAULT 3,
    cost_per_night NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Hoạt động'
);

-- 12. Table attractions (Địa điểm tham quan)
CREATE TABLE IF NOT EXISTS attractions (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    ticket_price NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Ngưng hoạt động'
);
ALTER TABLE attractions ALTER COLUMN status SET DEFAULT 'Hoạt động';

-- ==========================================================
-- BẢNG MAPPING VỚI TOURS
-- ==========================================================

-- 13. Table tour_transports
CREATE TABLE IF NOT EXISTS tour_transports (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    transport_id INTEGER NOT NULL REFERENCES transports(id) ON DELETE CASCADE,
    notes TEXT DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_transports ON tour_transports (tour_id, transport_id);

-- 14. Table tour_hotels
CREATE TABLE IF NOT EXISTS tour_hotels (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    hotel_id INTEGER NOT NULL REFERENCES hotels(id) ON DELETE CASCADE,
    nights INTEGER NOT NULL DEFAULT 1,
    notes TEXT DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_hotels ON tour_hotels (tour_id, hotel_id);

-- 15. Table tour_attractions
CREATE TABLE IF NOT EXISTS tour_attractions (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    attraction_id INTEGER NOT NULL REFERENCES attractions(id) ON DELETE CASCADE,
    order_index INTEGER NOT NULL DEFAULT 0,
    notes TEXT DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_attractions ON tour_attractions (tour_id, attraction_id);


-- ==========================================================
-- TẠO MOCK DATA & MAPPING CHO TOURS HIỆN TẠI
-- ==========================================================

-- Thêm mock phương tiện
INSERT INTO transports (name, type, capacity, cost, status) VALUES 
('Xe Limousine 50 chỗ VIP', 'Xe khách', 50, 2000000, 'Hoạt động'),
('Xe Huyndai 45 chỗ', 'Xe khách', 45, 5000000, 'Hoạt động'),
('Vé Máy Bay Vietnam Airlines', 'Máy bay', 200, 3500000, 'Hoạt động'),
('Tàu hỏa SE1/SE2', 'Tàu hỏa', 100, 800000, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- Thêm mock khách sạn
INSERT INTO hotels (name, address, star_rating, cost_per_night, status) VALUES 
('Khách sạn Mường Thanh', 'Số 1, Đường 2, Thành phố', 5, 1500000, 'Hoạt động'),
('Khách sạn Novotel', 'Khu trung tâm', 4, 1200000, 'Hoạt động'),
('Resort Vinpearl', 'Biển X', 5, 3000000, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- Thêm mock điểm tham quan
INSERT INTO attractions (name, address, ticket_price, status) VALUES 
('Vịnh Hạ Long', 'Quảng Ninh', 300000, 'Hoạt động'),
('Bà Nà Hills', 'Đà Nẵng', 900000, 'Hoạt động'),
('Phố Cổ Hội An', 'Quảng Nam', 120000, 'Hoạt động'),
('Chợ Bến Thành', 'Hồ Chí Minh', 0, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- Hàm chuẩn hóa text để so khớp địa danh (không dấu, lower-case)
CREATE OR REPLACE FUNCTION normalize_vn(input_text TEXT)
RETURNS TEXT
LANGUAGE SQL
IMMUTABLE
AS $$
    SELECT trim(
        regexp_replace(
            lower(
                translate(
                    coalesce(input_text, ''),
                    'àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđÀÁẠẢÃÂẦẤẬẨẪĂẰẮẶẲẴÈÉẸẺẼÊỀẾỆỂỄÌÍỊỈĨÒÓỌỎÕÔỒỐỘỔỖƠỜỚỢỞỠÙÚỤỦŨƯỪỨỰỬỮỲÝỴỶỸĐ',
                    'aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyydAAAAAAAAAAAAAAAAAEEEEEEEEEEEIIIIIOOOOOOOOOOOOOOOOOUUUUUUUUUUUYYYYYD'
                )
            ),
            '[^a-z0-9]+',
            ' ',
            'g'
        )
    );
$$;

-- Inject dữ liệu theo địa danh + mức giá tour
-- Quy tắc số điểm tham quan:
--  < 3.000.000đ: 2-3 điểm
--  < 6.000.000đ: 4-5 điểm
--  < 10.000.000đ: 5-7 điểm
--  >= 10.000.000đ: 7-9 điểm
DO $$
DECLARE
    t RECORD;
    night_count INTEGER;
    attraction_target INTEGER;
    inserted_count INTEGER;
    next_order INTEGER;
    selected_attraction_id INTEGER;
    dest_context TEXT;
BEGIN
    FOR t IN
        SELECT id, duration_days, base_price, destination, name, description
        FROM tours
    LOOP
        DELETE FROM tour_transports WHERE tour_id = t.id;
        DELETE FROM tour_hotels WHERE tour_id = t.id;
        DELETE FROM tour_attractions WHERE tour_id = t.id;

        -- Phương tiện: vẫn random trong dữ liệu thật
        INSERT INTO tour_transports (tour_id, transport_id, notes)
        SELECT t.id, x.id, 'Inject ngẫu nhiên từ dữ liệu thật'
        FROM (
            SELECT id
            FROM transports
            WHERE status = 'Hoạt động'
            ORDER BY random()
            LIMIT 1
        ) x
        ON CONFLICT DO NOTHING;

        -- Khách sạn: ưu tiên khớp địa danh trong address, nếu không có thì fallback random
        night_count := GREATEST(COALESCE(t.duration_days, 2) - 1, 1);
        INSERT INTO tour_hotels (tour_id, hotel_id, nights, notes)
        SELECT t.id, x.id, night_count, 'Inject theo địa danh từ dữ liệu thật'
        FROM (
            SELECT id
            FROM hotels
            WHERE status = 'Hoạt động'
              AND normalize_vn(address) LIKE '%' || normalize_vn(COALESCE(t.destination, '')) || '%'
            ORDER BY random()
            LIMIT 1
        ) x
        ON CONFLICT DO NOTHING;

        IF NOT EXISTS (SELECT 1 FROM tour_hotels th WHERE th.tour_id = t.id) THEN
            INSERT INTO tour_hotels (tour_id, hotel_id, nights, notes)
            SELECT t.id, x.id, night_count, 'Inject fallback ngẫu nhiên từ dữ liệu thật'
            FROM (
                SELECT id
                FROM hotels
                WHERE status = 'Hoạt động'
                ORDER BY random()
                LIMIT 1
            ) x
            ON CONFLICT DO NOTHING;
        END IF;

        -- Số điểm tham quan theo giá tour
        IF COALESCE(t.base_price, 0) < 3000000 THEN
            attraction_target := 2 + FLOOR(random() * 2)::INT;       -- 2-3
        ELSIF COALESCE(t.base_price, 0) < 6000000 THEN
            attraction_target := 4 + FLOOR(random() * 2)::INT;       -- 4-5
        ELSIF COALESCE(t.base_price, 0) < 10000000 THEN
            attraction_target := 5 + FLOOR(random() * 3)::INT;       -- 5-7
        ELSE
            attraction_target := 7 + FLOOR(random() * 3)::INT;       -- 7-9
        END IF;

        -- Context để match địa danh từ destination + name + description
        dest_context := normalize_vn(
            COALESCE(t.destination, '') || ' ' ||
            COALESCE(t.name, '') || ' ' ||
            COALESCE(t.description, '')
        );

        -- Ưu tiên chọn điểm tham quan có text chứa token liên quan tour
        INSERT INTO tour_attractions (tour_id, attraction_id, order_index, notes)
        SELECT
            t.id,
            scored.id,
            ROW_NUMBER() OVER (ORDER BY scored.score DESC, scored.rnd) AS order_index,
            'Inject theo địa danh từ dữ liệu thật'
        FROM (
            WITH tokens AS (
                SELECT DISTINCT tok
                FROM regexp_split_to_table(dest_context, '\s+') AS tok
                WHERE length(tok) >= 3
                  AND tok NOT IN ('tour', 'du', 'lich', 'tham', 'quan', 'ngay', 'dem', 'mua', 'he', 'dong', 'xuan')
            )
            SELECT
                a.id,
                SUM(CASE WHEN normalize_vn(a.name || ' ' || a.address) LIKE '%' || tk.tok || '%' THEN 1 ELSE 0 END) AS score,
                random() AS rnd
            FROM attractions a
            CROSS JOIN tokens tk
            WHERE a.status = 'Hoạt động'
            GROUP BY a.id
        ) scored
        WHERE scored.score > 0
        ORDER BY scored.score DESC, scored.rnd
        LIMIT attraction_target
        ON CONFLICT DO NOTHING;

        GET DIAGNOSTICS inserted_count = ROW_COUNT;
        next_order := inserted_count + 1;

        -- Nếu chưa đủ số lượng, bổ sung từ attraction cùng địa danh (address chứa destination)
        WHILE inserted_count < attraction_target LOOP
            SELECT a.id
            INTO selected_attraction_id
            FROM attractions a
            WHERE a.status = 'Hoạt động'
              AND normalize_vn(a.address) LIKE '%' || normalize_vn(COALESCE(t.destination, '')) || '%'
              AND NOT EXISTS (
                  SELECT 1
                  FROM tour_attractions ta
                  WHERE ta.tour_id = t.id
                    AND ta.attraction_id = a.id
              )
            ORDER BY random()
            LIMIT 1;

            -- Nếu vẫn không có dữ liệu thật phù hợp, tạo điểm tham quan theo chính địa danh tour
            IF selected_attraction_id IS NULL THEN
                INSERT INTO attractions (name, address, ticket_price, status)
                VALUES (
                    'Điểm tham quan nổi bật ' || COALESCE(t.destination, 'Việt Nam') || ' #' || next_order,
                    COALESCE(NULLIF(t.destination, ''), 'Việt Nam'),
                    0,
                    'Hoạt động'
                )
                RETURNING id INTO selected_attraction_id;
            END IF;

            INSERT INTO tour_attractions (tour_id, attraction_id, order_index, notes)
            VALUES (t.id, selected_attraction_id, next_order, 'Inject theo địa danh từ dữ liệu thật')
            ON CONFLICT DO NOTHING;

            inserted_count := inserted_count + 1;
            next_order := next_order + 1;
        END LOOP;
    END LOOP;
END $$;

-- ==========================================================
-- 16. PROMOTION / COUPON MANAGEMENT
-- ==========================================================

-- Bổ sung loại tour để hỗ trợ điều kiện mã giảm giá theo loại tour.
ALTER TABLE tours
ADD COLUMN IF NOT EXISTS tour_type VARCHAR(120) NOT NULL DEFAULT 'Tiêu chuẩn';

-- Bổ sung thông tin giảm giá trong thanh toán.
ALTER TABLE payments
ADD COLUMN IF NOT EXISTS original_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS discount_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS promo_code VARCHAR(64) NOT NULL DEFAULT '';

UPDATE payments
SET original_amount = CASE
        WHEN original_amount <= 0 THEN total_amount
        ELSE original_amount
    END,
    discount_amount = GREATEST(discount_amount, 0),
    promo_code = COALESCE(promo_code, '');

CREATE TABLE IF NOT EXISTS promo_codes (
    id SERIAL PRIMARY KEY,
    code VARCHAR(64) NOT NULL,
    discount_type VARCHAR(20) NOT NULL CHECK (discount_type IN ('Percent', 'Fixed')),
    discount_value NUMERIC(18,2) NOT NULL CHECK (
        (discount_type = 'Percent' AND discount_value BETWEEN 1 AND 100)
        OR (discount_type = 'Fixed' AND discount_value > 0)
    ),
    start_date TIMESTAMP NOT NULL,
    end_date TIMESTAMP NOT NULL,
    max_total_uses INTEGER NULL CHECK (max_total_uses IS NULL OR max_total_uses > 0),
    max_uses_per_user INTEGER NULL CHECK (max_uses_per_user IS NULL OR max_uses_per_user > 0),
    min_order_amount NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (min_order_amount >= 0),
    applicable_tour_type VARCHAR(120),
    only_new_customers BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT chk_promo_codes_date_range CHECK (end_date >= start_date)
);

-- Mã không phân biệt hoa thường.
CREATE UNIQUE INDEX IF NOT EXISTS ux_promo_codes_code_upper
ON promo_codes (UPPER(code));

ALTER TABLE payments
ADD COLUMN IF NOT EXISTS promo_code_id INTEGER REFERENCES promo_codes(id) ON DELETE SET NULL;

CREATE TABLE IF NOT EXISTS promo_code_tours (
    id SERIAL PRIMARY KEY,
    promo_code_id INTEGER NOT NULL REFERENCES promo_codes(id) ON DELETE CASCADE,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_promo_code_tours_unique
ON promo_code_tours (promo_code_id, tour_id);

CREATE TABLE IF NOT EXISTS promo_code_usages (
    id SERIAL PRIMARY KEY,
    promo_code_id INTEGER NOT NULL REFERENCES promo_codes(id) ON DELETE RESTRICT,
    booking_id INTEGER NOT NULL UNIQUE REFERENCES bookings(id) ON DELETE CASCADE,
    customer_id INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    promo_code VARCHAR(64) NOT NULL,
    order_amount NUMERIC(18,2) NOT NULL CHECK (order_amount >= 0),
    discount_amount NUMERIC(18,2) NOT NULL CHECK (discount_amount >= 0),
    final_amount NUMERIC(18,2) NOT NULL CHECK (final_amount >= 0),
    used_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_promo_code_usages_promo_code_id
ON promo_code_usages (promo_code_id);

CREATE INDEX IF NOT EXISTS ix_promo_code_usages_customer_id
ON promo_code_usages (customer_id);

-- ==========================================================
-- 17. TOUR RATING / REVIEW MANAGEMENT
-- ==========================================================

CREATE TABLE IF NOT EXISTS tour_ratings (
    id SERIAL PRIMARY KEY,
    booking_id INTEGER NOT NULL UNIQUE REFERENCES bookings(id) ON DELETE CASCADE,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    customer_id INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    rating_value INTEGER NOT NULL CHECK (rating_value BETWEEN 1 AND 5),
    comment TEXT NOT NULL DEFAULT '',
    status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Approved', 'Hidden')),
    admin_reply TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    moderated_at TIMESTAMP NULL,
    moderated_by_user_id INTEGER NULL REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_tour_ratings_tour_id
ON tour_ratings (tour_id);

CREATE INDEX IF NOT EXISTS ix_tour_ratings_customer_id
ON tour_ratings (customer_id);

CREATE INDEX IF NOT EXISTS ix_tour_ratings_status_created_at
ON tour_ratings (status, created_at DESC);

-- ==========================================================
-- 18. GUIDE RATING / REVIEW MANAGEMENT
-- ==========================================================

CREATE TABLE IF NOT EXISTS guide_ratings (
    id SERIAL PRIMARY KEY,
    booking_id INTEGER NOT NULL UNIQUE REFERENCES bookings(id) ON DELETE CASCADE,
    departure_id INTEGER NOT NULL REFERENCES departures(id) ON DELETE CASCADE,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    guide_user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    customer_id INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    rating_value INTEGER NOT NULL CHECK (rating_value BETWEEN 1 AND 5),
    comment TEXT NOT NULL DEFAULT '',
    status VARCHAR(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending', 'Approved', 'Hidden')),
    admin_reply TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    moderated_at TIMESTAMP NULL,
    moderated_by_user_id INTEGER NULL REFERENCES users(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_guide_ratings_guide_user_id
ON guide_ratings (guide_user_id);

CREATE INDEX IF NOT EXISTS ix_guide_ratings_customer_id
ON guide_ratings (customer_id);

CREATE INDEX IF NOT EXISTS ix_guide_ratings_status_created_at
ON guide_ratings (status, created_at DESC);
