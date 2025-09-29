namespace Receiver
{
    public static class Config
    {
        public const string LISTEN_ADDRESS = "127.0.0.1";
        public const int LISTEN_PORT = 14562;

        public const int MAX_TABLE_ROWS = 1000;
        public const int AUTO_CLEAR_THRESHOLD = 950;

        public const int RECEIVE_TIMEOUT_MS = 5000;
        public const int MAX_PACKET_SIZE = 263;

        public const bool SHOW_HEX_DATA = true;
        public const int HEX_PREVIEW_LENGTH = 32;
        public const bool AUTO_SCROLL_ENABLED = true;

        public const bool ENABLE_CONSOLE_LOGGING = true;
        public const bool ENABLE_DETAILED_LOGGING = true;
    }
}