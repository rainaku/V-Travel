-- KẾT NỐI VÀ TẠO BẢNG CHO SUPABASE
-- Chạy script này trong phần SQL Editor trên Supabase Dashboard

-- 1. Table users
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255) NOT NULL,
    role VARCHAR(50) NOT NULL DEFAULT 'Employee',
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- 2. Table tours
CREATE TABLE tours (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    base_price NUMERIC(18,2) NOT NULL,
    duration_days INTEGER NOT NULL,
    destination VARCHAR(255) NOT NULL
);

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
    status VARCHAR(50) NOT NULL DEFAULT 'Chờ xử lý'
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

-- Insert 1 Admin User (Password is 'admin' hashed - just dummy hash for now, you will need to hash properly in code)
INSERT INTO users (username, password_hash, full_name, role)
VALUES ('admin', 'admin_hash_placeholder', 'Administrator', 'Admin');
