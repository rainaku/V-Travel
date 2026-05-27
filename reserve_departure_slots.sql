-- ==========================================================
-- VIETTRAVEL - ATOMIC SLOT RESERVATION (Anti-Overbooking)
-- Deploy this function to Supabase SQL Editor.
-- ==========================================================

-- Atomically reserves slots on a departure using row-level locking.
-- Returns a JSON object with the result.
CREATE OR REPLACE FUNCTION reserve_departure_slots(
    p_departure_id INTEGER,
    p_guest_count INTEGER
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
DECLARE
    v_departure departures%ROWTYPE;
    v_new_available INTEGER;
    v_new_status VARCHAR(50);
BEGIN
    -- Lock the row to prevent concurrent modifications (SELECT FOR UPDATE)
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

    -- Check departure is open for booking
    IF v_departure.status != 'Mở bán' THEN
        RETURN json_build_object(
            'success', false,
            'error', 'NOT_OPEN',
            'message', 'Lịch khởi hành hiện không mở bán.',
            'current_status', v_departure.status
        );
    END IF;

    -- Check available slots
    IF p_guest_count > v_departure.available_slots THEN
        RETURN json_build_object(
            'success', false,
            'error', 'INSUFFICIENT_SLOTS',
            'message', format('Chỉ còn %s chỗ trống.', v_departure.available_slots),
            'available_slots', v_departure.available_slots
        );
    END IF;

    -- Reserve the slots
    v_new_available := v_departure.available_slots - p_guest_count;
    
    IF v_new_available <= 0 THEN
        v_new_available := 0;
        v_new_status := 'Hết chỗ';
    ELSE
        v_new_status := v_departure.status; -- keep 'Mở bán'
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
$$;

-- Atomically releases slots back (for cancellation/rollback).
CREATE OR REPLACE FUNCTION release_departure_slots(
    p_departure_id INTEGER,
    p_guest_count INTEGER
)
RETURNS JSON
LANGUAGE plpgsql
AS $$
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
$$;
