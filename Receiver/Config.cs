namespace Receiver
{
    public static class Config
    {
        // Network settings
        public const int LISTEN_PORT = 14562;
        public const int RECEIVE_TIMEOUT_MS = 5000;

        // UI settings
        public const bool AUTO_SCROLL_ENABLED = true;
        public const bool SHOW_HEX_DATA = false;
        public const int HEX_PREVIEW_LENGTH = 16;

        // Table management
        public const int MAX_TABLE_ROWS = 10000;
        public const int AUTO_CLEAR_THRESHOLD = 5000;

        // Logging
        public const bool ENABLE_CONSOLE_LOGGING = true;
        public const bool ENABLE_DETAILED_LOGGING = false;
    }
}