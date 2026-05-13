using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VietTravel.Data.Services
{
    /// <summary>
    /// Result of an atomic slot reservation/release via Supabase RPC.
    /// </summary>
    public class SlotReservationResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("departure_id")]
        public int DepartureId { get; set; }

        [JsonProperty("reserved_slots")]
        public int ReservedSlots { get; set; }

        [JsonProperty("released_slots")]
        public int ReleasedSlots { get; set; }

        [JsonProperty("available_slots")]
        public int AvailableSlots { get; set; }

        [JsonProperty("new_status")]
        public string? NewStatus { get; set; }

        [JsonProperty("previous_available")]
        public int PreviousAvailable { get; set; }

        [JsonProperty("previous_status")]
        public string? PreviousStatus { get; set; }
    }

    /// <summary>
    /// Provides atomic departure slot reservation using Supabase RPC (PostgreSQL row-level locking).
    /// Requires deploying reserve_departure_slots.sql to the database.
    /// Falls back to client-side update if RPC is not available.
    /// </summary>
    public class DepartureSlotService
    {
        /// <summary>
        /// Atomically reserves slots on a departure. Prevents overbooking via DB-level FOR UPDATE lock.
        /// </summary>
        public async Task<SlotReservationResult> ReserveSlotsAsync(Supabase.Client client, int departureId, int guestCount)
        {
            try
            {
                var response = await client.Rpc("reserve_departure_slots", new Dictionary<string, object>
                {
                    ["p_departure_id"] = departureId,
                    ["p_guest_count"] = guestCount
                });

                var result = JsonConvert.DeserializeObject<SlotReservationResult>(response.Content ?? "{}");
                return result ?? new SlotReservationResult { Success = false, Error = "PARSE_ERROR", Message = "Không thể đọc kết quả từ server." };
            }
            catch (Exception ex)
            {
                return new SlotReservationResult
                {
                    Success = false,
                    Error = "RPC_EXCEPTION",
                    Message = $"Lỗi gọi RPC: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Atomically releases slots back to a departure (for cancellation/rollback).
        /// </summary>
        public async Task<SlotReservationResult> ReleaseSlotsAsync(Supabase.Client client, int departureId, int guestCount)
        {
            try
            {
                var response = await client.Rpc("release_departure_slots", new Dictionary<string, object>
                {
                    ["p_departure_id"] = departureId,
                    ["p_guest_count"] = guestCount
                });

                var result = JsonConvert.DeserializeObject<SlotReservationResult>(response.Content ?? "{}");
                return result ?? new SlotReservationResult { Success = false, Error = "PARSE_ERROR", Message = "Không thể đọc kết quả từ server." };
            }
            catch (Exception ex)
            {
                return new SlotReservationResult
                {
                    Success = false,
                    Error = "RPC_EXCEPTION",
                    Message = $"Lỗi gọi RPC: {ex.Message}"
                };
            }
        }
    }
}
