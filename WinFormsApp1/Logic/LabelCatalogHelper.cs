using System;

namespace WinFormsApp1
{
    public static class LabelCatalogHelper
    {
        public static readonly string[] VehicleTypes = { "car", "motorcycle", "e_scooter", "bicycle" };
        public static readonly string[] EventTypes = { "contact", "throw", "final_exchange", "get on", "get off", "suspect", "controlled_delivery" };
        private static readonly string[] LegacyEventTypes = { "contact", "exchange", "board", "final_exchange", "disembark", "controlled_delivery", "camouflage", "throw" };

        public static string[] GetEventComboItems()
        {
            var items = new string[EventTypes.Length];
            for (int i = 0; i < EventTypes.Length; i++)
            {
                items[i] = "event_" + EventTypes[i];
            }

            return items;
        }

        public static string GetVehicleDisplayLabel(
            int vehicleId,
            int vehicleInstanceId,
            string vehiclePartType,
            int? linkedVehicleInstanceId)
        {
            string vehicleType = GetVehicleCategoryName(vehicleId, "body");
            if (string.Equals(vehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
            {
                int linkedId = linkedVehicleInstanceId ?? vehicleInstanceId;
                return $"vehicle_plate->body_{vehicleType}_{linkedId:D2}";
            }

            return $"vehicle_{vehicleType}_{vehicleInstanceId:D2}";
        }
        public static int GetPlateCategoryId() => 33;

        public static int GetEventCategoryId(string eventName)
        {
            int index = Array.IndexOf(EventTypes, eventName);
            return index >= 0 ? 25 + index : 25;
        }

        public static int GetEventIdFromImportedCategory(int categoryId, string? categoryName)
        {
            int eventId = GetEventId(NormalizeImportedEventType(categoryName));
            if (eventId > 0)
            {
                return eventId;
            }

            // JSON without category names predates the renamed catalog, so retain its numeric meaning.
            string? legacyEventType = categoryId >= 25 && categoryId <= 32
                ? LegacyEventTypes[categoryId - 25]
                : null;
            return GetEventId(NormalizeImportedEventType(legacyEventType));
        }

        private static int GetEventId(string? eventType)
        {
            int index = Array.IndexOf(EventTypes, eventType);
            return index >= 0 ? index + 1 : 0;
        }

        private static string? NormalizeImportedEventType(string? eventType)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                return null;
            }

            string normalized = eventType.Trim().ToLowerInvariant();
            if (normalized.StartsWith("event_", StringComparison.Ordinal))
            {
                normalized = normalized.Substring("event_".Length);
            }

            return normalized switch
            {
                "exchange" => "throw",
                "board" => "get on",
                "disembark" => "get off",
                "camouflage" => "suspect",
                _ => normalized
            };
        }

        public static string GetVehicleCategoryName(int vehicleId, string vehiclePartType)
        {
            if (string.Equals(vehiclePartType, "plate", StringComparison.OrdinalIgnoreCase))
            {
                return "plate";
            }

            return vehicleId switch
            {
                1 => "car",
                2 => "motorcycle",
                3 => "e_scooter",
                4 => "bicycle",
                _ => "car"
            };
        }

        public static string GetEventCategoryName(int eventId)
        {
            if (eventId > 0 && eventId <= EventTypes.Length)
            {
                return EventTypes[eventId - 1];
            }

            return EventTypes[0];
        }
    }
}
