-- ==========================================================
-- VIETTRAVEL - CƠ SỞ DỮ LIỆU HOÀN CHỈNH
-- File này chứa toàn bộ SQL cần thiết để tạo database hoàn chỉnh.
-- Chạy trên PostgreSQL (Supabase).
-- ==========================================================

BEGIN;

-- ==========================================================
-- PHẦN 1: TẠO BẢNG CƠ BẢN
-- ==========================================================

-- 1. Bảng người dùng
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    avatar_url TEXT DEFAULT '',
    role VARCHAR(50) NOT NULL DEFAULT 'Employee',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    banned_by TEXT DEFAULT '',
    banned_at TEXT DEFAULT ''
);

COMMENT ON COLUMN users.banned_by IS 'Tên đăng nhập của admin đã khóa tài khoản này';
COMMENT ON COLUMN users.banned_at IS 'Thời gian khóa tài khoản (định dạng ISO)';

-- 2. Bảng tour du lịch
CREATE TABLE IF NOT EXISTS tours (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    base_price NUMERIC(18,2) NOT NULL,
    duration_days INTEGER NOT NULL,
    destination VARCHAR(255) NOT NULL,
    image_url TEXT DEFAULT '',
    tour_type VARCHAR(120) NOT NULL DEFAULT 'Tiêu chuẩn'
);

-- 3. Bảng lịch khởi hành
CREATE TABLE IF NOT EXISTS departures (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER REFERENCES tours(id) ON DELETE CASCADE,
    start_date TIMESTAMP NOT NULL,
    max_slots INTEGER NOT NULL,
    available_slots INTEGER NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Mở bán'
);

-- 4. Bảng khách hàng
CREATE TABLE IF NOT EXISTS customers (
    id SERIAL PRIMARY KEY,
    full_name VARCHAR(255) NOT NULL,
    phone_number VARCHAR(50),
    email VARCHAR(255),
    address TEXT
);

-- 5. Bảng đặt tour
CREATE TABLE IF NOT EXISTS bookings (
    id SERIAL PRIMARY KEY,
    customer_id INTEGER REFERENCES customers(id) ON DELETE RESTRICT,
    departure_id INTEGER REFERENCES departures(id) ON DELETE RESTRICT,
    user_id INTEGER REFERENCES users(id) ON DELETE RESTRICT,
    booking_date TIMESTAMP NOT NULL DEFAULT NOW(),
    guest_count INTEGER NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Chờ thanh toán'
);

-- 6. Bảng mã giảm giá (tạo trước payments vì payments tham chiếu đến promo_codes)
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

-- Mã không phân biệt hoa thường
CREATE UNIQUE INDEX IF NOT EXISTS ux_promo_codes_code_upper
ON promo_codes (UPPER(code));

-- 7. Bảng thanh toán
CREATE TABLE IF NOT EXISTS payments (
    id SERIAL PRIMARY KEY,
    booking_id INTEGER UNIQUE REFERENCES bookings(id) ON DELETE CASCADE,
    total_amount NUMERIC(18,2) NOT NULL,
    paid_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Chưa thanh toán',
    payment_date TIMESTAMP,
    payment_method VARCHAR(100) DEFAULT 'Tiền mặt',
    original_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    discount_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    promo_code VARCHAR(64) NOT NULL DEFAULT '',
    promo_code_id INTEGER REFERENCES promo_codes(id) ON DELETE SET NULL
);

-- 8. Bảng thông báo
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

-- Chỉ mục chống trùng thông báo theo người dùng
CREATE UNIQUE INDEX IF NOT EXISTS ux_notifications_user_dedup
ON notifications (user_id, deduplication_key);

-- Chỉ mục truy vấn thông báo theo thời gian
CREATE INDEX IF NOT EXISTS ix_notifications_user_created_at
ON notifications (user_id, created_at DESC);

-- 9. Bảng hồ sơ hướng dẫn viên
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

-- 10. Bảng phân công hướng dẫn viên cho lịch khởi hành
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

-- Mỗi lịch khởi hành chỉ có 1 hướng dẫn viên chính
CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_guide_assignments_departure
ON tour_guide_assignments (departure_id);

CREATE INDEX IF NOT EXISTS ix_tour_guide_assignments_guide
ON tour_guide_assignments (guide_user_id);

-- 11. Bảng phương tiện
CREATE TABLE IF NOT EXISTS transports (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    type VARCHAR(100) NOT NULL,
    capacity INTEGER NOT NULL DEFAULT 0,
    cost NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Hoạt động'
);

-- 12. Bảng khách sạn / nơi ở
CREATE TABLE IF NOT EXISTS hotels (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    star_rating INTEGER NOT NULL DEFAULT 3,
    cost_per_night NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Hoạt động'
);

-- 13. Bảng điểm tham quan
CREATE TABLE IF NOT EXISTS attractions (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    address TEXT NOT NULL,
    ticket_price NUMERIC(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Hoạt động'
);

-- 14. Bảng liên kết tour - phương tiện
CREATE TABLE IF NOT EXISTS tour_transports (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    transport_id INTEGER NOT NULL REFERENCES transports(id) ON DELETE CASCADE,
    notes TEXT DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_transports
ON tour_transports (tour_id, transport_id);

-- 15. Bảng liên kết tour - khách sạn
CREATE TABLE IF NOT EXISTS tour_hotels (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    hotel_id INTEGER NOT NULL REFERENCES hotels(id) ON DELETE CASCADE,
    nights INTEGER NOT NULL DEFAULT 1,
    notes TEXT DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_hotels
ON tour_hotels (tour_id, hotel_id);

-- 16. Bảng liên kết tour - điểm tham quan
CREATE TABLE IF NOT EXISTS tour_attractions (
    id SERIAL PRIMARY KEY,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE,
    attraction_id INTEGER NOT NULL REFERENCES attractions(id) ON DELETE CASCADE,
    order_index INTEGER NOT NULL DEFAULT 0,
    notes TEXT DEFAULT ''
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tour_attractions
ON tour_attractions (tour_id, attraction_id);

-- 17. Bảng liên kết mã giảm giá - tour áp dụng
CREATE TABLE IF NOT EXISTS promo_code_tours (
    id SERIAL PRIMARY KEY,
    promo_code_id INTEGER NOT NULL REFERENCES promo_codes(id) ON DELETE CASCADE,
    tour_id INTEGER NOT NULL REFERENCES tours(id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_promo_code_tours_unique
ON promo_code_tours (promo_code_id, tour_id);

-- 18. Bảng lịch sử sử dụng mã giảm giá
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

-- 19. Bảng đánh giá tour
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

-- 20. Bảng đánh giá hướng dẫn viên
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

-- 21. Bảng nhật ký hệ thống (chỉ thêm, không sửa/xóa)
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL DEFAULT 0,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id INTEGER NOT NULL,
    old_value TEXT NOT NULL DEFAULT '',
    new_value TEXT NOT NULL DEFAULT '',
    details TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Chỉ mục truy vấn theo đối tượng
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
ON audit_logs (entity_type, entity_id, created_at DESC);

-- Chỉ mục truy vấn theo người dùng
CREATE INDEX IF NOT EXISTS ix_audit_logs_user
ON audit_logs (user_id, created_at DESC);

-- Chỉ mục truy vấn theo hành động
CREATE INDEX IF NOT EXISTS ix_audit_logs_action
ON audit_logs (action, created_at DESC);

COMMIT;

-- ==========================================================
-- PHẦN 2: RÀNG BUỘC BỔ SUNG
-- ==========================================================

-- Ràng buộc số tiền thanh toán
DO $
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_total_positive') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_total_positive CHECK (total_amount >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_paid_non_negative') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_paid_non_negative CHECK (paid_amount >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_paid_lte_total') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_paid_lte_total CHECK (paid_amount <= total_amount);
    END IF;
END $;

-- Ràng buộc số khách phải lớn hơn 0
DO $
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_bookings_guest_count_positive') THEN
        ALTER TABLE bookings ADD CONSTRAINT chk_bookings_guest_count_positive CHECK (guest_count > 0);
    END IF;
END $;

-- Ràng buộc số chỗ trống lịch khởi hành
DO $
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_departures_available_slots_non_negative') THEN
        ALTER TABLE departures ADD CONSTRAINT chk_departures_available_slots_non_negative CHECK (available_slots >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_departures_available_lte_max') THEN
        ALTER TABLE departures ADD CONSTRAINT chk_departures_available_lte_max CHECK (available_slots <= max_slots);
    END IF;
END $;

-- ==========================================================
-- PHẦN 3: BẢO MẬT CẤP HÀNG (ROW-LEVEL SECURITY)
-- ==========================================================

ALTER TABLE bookings ENABLE ROW LEVEL SECURITY;
ALTER TABLE payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;

-- Chính sách: Cho phép tất cả thao tác cho ứng dụng đã xác thực
DROP POLICY IF EXISTS customers_all_access ON customers;
CREATE POLICY customers_all_access ON customers
    FOR ALL
    USING (true)
    WITH CHECK (true);

DROP POLICY IF EXISTS bookings_all_access ON bookings;
CREATE POLICY bookings_all_access ON bookings
    FOR ALL
    USING (true)
    WITH CHECK (true);

DROP POLICY IF EXISTS payments_all_access ON payments;
CREATE POLICY payments_all_access ON payments
    FOR ALL
    USING (true)
    WITH CHECK (true);

-- Chính sách: nhật ký chỉ được thêm mới (không cho sửa/xóa qua API)
DROP POLICY IF EXISTS audit_logs_insert_only ON audit_logs;
CREATE POLICY audit_logs_insert_only ON audit_logs
    FOR INSERT
    WITH CHECK (true);

-- Chính sách: admin có thể đọc tất cả nhật ký
DROP POLICY IF EXISTS audit_logs_select_admin ON audit_logs;
CREATE POLICY audit_logs_select_admin ON audit_logs
    FOR SELECT
    USING (true);

-- ==========================================================
-- PHẦN 4: HÀM LƯU TRỮ (STORED FUNCTIONS)
-- ==========================================================

-- 4.1 Hàm ghi nhật ký hệ thống
CREATE OR REPLACE FUNCTION insert_audit_log(
    p_user_id INTEGER,
    p_action VARCHAR(100),
    p_entity_type VARCHAR(100),
    p_entity_id INTEGER,
    p_old_value TEXT DEFAULT '',
    p_new_value TEXT DEFAULT '',
    p_details TEXT DEFAULT ''
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $
BEGIN
    INSERT INTO audit_logs (user_id, action, entity_type, entity_id, old_value, new_value, details)
    VALUES (p_user_id, p_action, p_entity_type, p_entity_id, p_old_value, p_new_value, p_details);
END;
$;

-- 4.2 Hàm đặt chỗ nguyên tử (chống đặt trùng/quá số lượng)
CREATE OR REPLACE FUNCTION reserve_departure_slots(
    p_departure_id INTEGER,
    p_guest_count INTEGER
)
RETURNS JSON
LANGUAGE plpgsql
AS $
DECLARE
    v_departure departures%ROWTYPE;
    v_new_available INTEGER;
    v_new_status VARCHAR(50);
BEGIN
    -- Khóa hàng để ngăn chỉnh sửa đồng thời
    SELECT * INTO v_departure
    FROM departures
    WHERE id = p_departure_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN json_build_object(
            'success', false,
            'error', 'DEPARTURE_NOT_FOUND',
            'message', 'Không tìm thấy lịch khởi hành.'
        );
    END IF;

    IF v_departure.status != 'Mở bán' THEN
        RETURN json_build_object(
            'success', false,
            'error', 'NOT_OPEN',
            'message', 'Lịch khởi hành hiện không mở bán.',
            'current_status', v_departure.status
        );
    END IF;

    IF p_guest_count > v_departure.available_slots THEN
        RETURN json_build_object(
            'success', false,
            'error', 'INSUFFICIENT_SLOTS',
            'message', format('Chỉ còn %s chỗ trống.', v_departure.available_slots),
            'available_slots', v_departure.available_slots
        );
    END IF;

    v_new_available := v_departure.available_slots - p_guest_count;

    IF v_new_available <= 0 THEN
        v_new_available := 0;
        v_new_status := 'Hết chỗ';
    ELSE
        v_new_status := v_departure.status;
    END IF;

    UPDATE departures
    SET available_slots = v_new_available,
        status = v_new_status
    WHERE id = p_departure_id;

    RETURN json_build_object(
        'success', true,
        'departure_id', p_departure_id,
        'reserved_slots', p_guest_count,
        'available_slots', v_new_available,
        'new_status', v_new_status,
        'previous_available', v_departure.available_slots,
        'previous_status', v_departure.status
    );
END;
$;

-- 4.3 Hàm hoàn trả chỗ (dùng khi hủy đặt tour)
CREATE OR REPLACE FUNCTION release_departure_slots(
    p_departure_id INTEGER,
    p_guest_count INTEGER
)
RETURNS JSON
LANGUAGE plpgsql
AS $
DECLARE
    v_departure departures%ROWTYPE;
    v_new_available INTEGER;
    v_new_status VARCHAR(50);
BEGIN
    SELECT * INTO v_departure
    FROM departures
    WHERE id = p_departure_id
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN json_build_object('success', false, 'error', 'DEPARTURE_NOT_FOUND');
    END IF;

    v_new_available := LEAST(v_departure.max_slots, v_departure.available_slots + p_guest_count);

    IF v_departure.status != 'Đóng' THEN
        v_new_status := CASE WHEN v_new_available > 0 THEN 'Mở bán' ELSE 'Hết chỗ' END;
    ELSE
        v_new_status := v_departure.status;
    END IF;

    UPDATE departures
    SET available_slots = v_new_available,
        status = v_new_status
    WHERE id = p_departure_id;

    RETURN json_build_object(
        'success', true,
        'departure_id', p_departure_id,
        'released_slots', p_guest_count,
        'available_slots', v_new_available,
        'new_status', v_new_status
    );
END;
$;

-- 4.4 Hàm chuẩn hóa text tiếng Việt (bỏ dấu, chuyển thường, dùng cho so khớp địa danh)
CREATE OR REPLACE FUNCTION normalize_vn(input_text TEXT)
RETURNS TEXT
LANGUAGE SQL
IMMUTABLE
AS $
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
$;

-- ==========================================================
-- PHẦN 5: DỮ LIỆU MẪU (SEED DATA)
-- ==========================================================

-- 5.1 Tài khoản Admin mặc định (Mật khẩu: 'admin' - mã hóa SHA-256)
INSERT INTO users (username, password_hash, full_name, role, is_active, avatar_url)
SELECT 'admin', '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918', 'Administrator', 'Admin', TRUE, ''
WHERE NOT EXISTS (SELECT 1 FROM users WHERE username = 'admin');

-- 5.2 Dữ liệu mẫu phương tiện
INSERT INTO transports (name, type, capacity, cost, status) VALUES
('Xe Limousine 50 chỗ VIP', 'Xe khách', 50, 2000000, 'Hoạt động'),
('Xe Huyndai 45 chỗ', 'Xe khách', 45, 5000000, 'Hoạt động'),
('Vé Máy Bay Vietnam Airlines', 'Máy bay', 200, 3500000, 'Hoạt động'),
('Tàu hỏa SE1/SE2', 'Tàu hỏa', 100, 800000, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- 5.3 Dữ liệu mẫu khách sạn
INSERT INTO hotels (name, address, star_rating, cost_per_night, status) VALUES
('Khách sạn Mường Thanh', 'Số 1, Đường 2, Thành phố', 5, 1500000, 'Hoạt động'),
('Khách sạn Novotel', 'Khu trung tâm', 4, 1200000, 'Hoạt động'),
('Resort Vinpearl', 'Biển X', 5, 3000000, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- 5.4 Dữ liệu mẫu điểm tham quan
INSERT INTO attractions (name, address, ticket_price, status) VALUES
('Vịnh Hạ Long', 'Quảng Ninh', 300000, 'Hoạt động'),
('Bà Nà Hills', 'Đà Nẵng', 900000, 'Hoạt động'),
('Phố Cổ Hội An', 'Quảng Nam', 120000, 'Hoạt động'),
('Chợ Bến Thành', 'Hồ Chí Minh', 0, 'Hoạt động')
ON CONFLICT DO NOTHING;

-- ==========================================================
-- PHẦN 6: TỰ ĐỘNG GÁN TÀI NGUYÊN CHO TOURS
-- Tự động gán phương tiện, khách sạn, điểm tham quan cho tours
-- dựa trên địa danh và mức giá.
-- ==========================================================

DO $
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

        -- Gán phương tiện ngẫu nhiên
        INSERT INTO tour_transports (tour_id, transport_id, notes)
        SELECT t.id, x.id, 'Gán ngẫu nhiên từ dữ liệu mẫu'
        FROM (
            SELECT id
            FROM transports
            WHERE status = 'Hoạt động'
            ORDER BY random()
            LIMIT 1
        ) x
        ON CONFLICT DO NOTHING;

        -- Gán khách sạn: ưu tiên khớp địa danh, nếu không có thì chọn ngẫu nhiên
        night_count := GREATEST(COALESCE(t.duration_days, 2) - 1, 1);
        INSERT INTO tour_hotels (tour_id, hotel_id, nights, notes)
        SELECT t.id, x.id, night_count, 'Gán theo địa danh từ dữ liệu mẫu'
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
            SELECT t.id, x.id, night_count, 'Gán ngẫu nhiên (không tìm thấy khớp địa danh)'
            FROM (
                SELECT id
                FROM hotels
                WHERE status = 'Hoạt động'
                ORDER BY random()
                LIMIT 1
            ) x
            ON CONFLICT DO NOTHING;
        END IF;

        -- Tính số điểm tham quan theo mức giá tour
        -- < 3.000.000đ: 2-3 điểm
        -- < 6.000.000đ: 4-5 điểm
        -- < 10.000.000đ: 5-7 điểm
        -- >= 10.000.000đ: 7-9 điểm
        IF COALESCE(t.base_price, 0) < 3000000 THEN
            attraction_target := 2 + FLOOR(random() * 2)::INT;
        ELSIF COALESCE(t.base_price, 0) < 6000000 THEN
            attraction_target := 4 + FLOOR(random() * 2)::INT;
        ELSIF COALESCE(t.base_price, 0) < 10000000 THEN
            attraction_target := 5 + FLOOR(random() * 3)::INT;
        ELSE
            attraction_target := 7 + FLOOR(random() * 3)::INT;
        END IF;

        -- Tạo ngữ cảnh để so khớp địa danh
        dest_context := normalize_vn(
            COALESCE(t.destination, '') || ' ' ||
            COALESCE(t.name, '') || ' ' ||
            COALESCE(t.description, '')
        );

        -- Ưu tiên chọn điểm tham quan có tên/địa chỉ liên quan đến tour
        INSERT INTO tour_attractions (tour_id, attraction_id, order_index, notes)
        SELECT
            t.id,
            scored.id,
            ROW_NUMBER() OVER (ORDER BY scored.score DESC, scored.rnd) AS order_index,
            'Gán theo địa danh từ dữ liệu mẫu'
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

        -- Bổ sung nếu chưa đủ số lượng điểm tham quan
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

            -- Nếu không tìm thấy điểm phù hợp, tạo mới theo địa danh tour
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
            VALUES (t.id, selected_attraction_id, next_order, 'Gán theo địa danh từ dữ liệu mẫu')
            ON CONFLICT DO NOTHING;

            inserted_count := inserted_count + 1;
            next_order := next_order + 1;
        END LOOP;
    END LOOP;
END $;

-- ==========================================================
-- PHẦN 7: SỬA MẬT KHẨU ADMIN (nếu đang dùng placeholder)
-- ==========================================================

UPDATE users
SET password_hash = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918'
WHERE username = 'admin'
  AND password_hash = 'admin_hash_placeholder';

-- ==========================================================
-- LÀM MỚI BỘ NHỚ ĐỆM SCHEMA (Supabase)
-- ==========================================================

NOTIFY pgrst, 'reload schema';
