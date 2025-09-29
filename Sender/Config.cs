namespace Sender
{
    public static class Config
    {
        public const string SITL_ADDRESS = "127.0.0.1";
        public const int SITL_PORT = 14550;

        public const string FORWARD_ADDRESS = "127.0.0.1";
        public const int FORWARD_PORT = 14562;

        public const int MAX_LISTBOX_ITEMS = 100;
        public const int MAX_LOG_LINES = 200;

        public const int RECEIVE_TIMEOUT_MS = 5000;
        public const int RECONNECT_DELAY_MS = 1000;
        public const int MAX_PACKET_SIZE = 263;

        public const bool ENABLE_DETAILED_LOGGING = true;
        public const bool SHOW_HEX_DATA = true;
        public const int HEX_PREVIEW_LENGTH = 50;
    }
}