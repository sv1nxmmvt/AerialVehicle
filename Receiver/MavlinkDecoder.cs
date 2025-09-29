using System;
using System.Collections.Generic;

namespace Receiver
{
    public class MavlinkDecoder
    {
        private static readonly Dictionary<byte, string> MessageNames = new Dictionary<byte, string>
        {
            { 0, "HEARTBEAT" },
            { 1, "SYS_STATUS" },
            { 2, "SYSTEM_TIME" },
            { 4, "PING" },
            { 11, "SET_MODE" },
            { 20, "PARAM_REQUEST_READ" },
            { 21, "PARAM_REQUEST_LIST" },
            { 22, "PARAM_VALUE" },
            { 24, "GPS_RAW_INT" },
            { 27, "RAW_IMU" },
            { 30, "ATTITUDE" },
            { 31, "ATTITUDE_QUATERNION" },
            { 32, "LOCAL_POSITION_NED" },
            { 33, "GLOBAL_POSITION_INT" },
            { 36, "RC_CHANNELS_SCALED" },
            { 39, "MISSION_ITEM" },
            { 40, "MISSION_REQUEST" },
            { 42, "MISSION_CURRENT" },
            { 43, "MISSION_REQUEST_LIST" },
            { 44, "MISSION_COUNT" },
            { 62, "NAV_CONTROLLER_OUTPUT" },
            { 74, "VFR_HUD" },
            { 76, "COMMAND_LONG" },
            { 77, "COMMAND_ACK" },
            { 87, "POSITION_TARGET_GLOBAL_INT" },
            { 105, "HIGHRES_IMU" },
            { 109, "RADIO_STATUS" },
            { 116, "SCALED_IMU2" },
            { 125, "POWER_STATUS" },
            { 147, "BATTERY_STATUS" },
            { 148, "AUTOPILOT_VERSION" },
            { 230, "ESTIMATOR_STATUS" },
            { 241, "VIBRATION" },
            { 242, "HOME_POSITION" },
            { 253, "STATUSTEXT" }
        };

        public class MavlinkPacketInfo
        {
            public string Version { get; set; }
            public byte MessageId { get; set; }
            public uint MessageIdExtended { get; set; }
            public string MessageName { get; set; }
            public int PayloadLength { get; set; }
            public byte SystemId { get; set; }
            public byte ComponentId { get; set; }
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
        }

        public MavlinkPacketInfo DecodePacket(byte[] data)
        {
            var info = new MavlinkPacketInfo { IsValid = false };

            try
            {
                if (data == null || data.Length < 8)
                {
                    info.ErrorMessage = "Packet too short";
                    return info;
                }

                byte magicByte = data[0];

                if (magicByte == 0xFE)
                {
                    info.IsValid = DecodeMavlinkV1(data, info);
                }
                else if (magicByte == 0xFD)
                {
                    info.IsValid = DecodeMavlinkV2(data, info);
                }
                else
                {
                    info.ErrorMessage = $"Invalid magic byte: 0x{magicByte:X2}";
                }
            }
            catch (Exception ex)
            {
                info.IsValid = false;
                info.ErrorMessage = $"Decode error: {ex.Message}";
            }

            return info;
        }

        private bool DecodeMavlinkV1(byte[] data, MavlinkPacketInfo info)
        {
            try
            {
                if (data.Length < 8)
                    return false;

                info.Version = "MAVLink v1.0";
                info.PayloadLength = data[1];
                byte sequence = data[2];
                info.SystemId = data[3];
                info.ComponentId = data[4];
                info.MessageId = data[5];
                info.MessageIdExtended = info.MessageId;

                int expectedLength = 8 + info.PayloadLength;
                if (data.Length < expectedLength)
                {
                    info.ErrorMessage = $"Packet too short: expected {expectedLength}, got {data.Length}";
                    return false;
                }

                info.MessageName = GetMessageName(info.MessageId);
                return true;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"MAVLink v1 decode error: {ex.Message}";
                return false;
            }
        }

        private bool DecodeMavlinkV2(byte[] data, MavlinkPacketInfo info)
        {
            try
            {
                if (data.Length < 12)
                    return false;

                info.Version = "MAVLink v2.0";
                info.PayloadLength = data[1];
                byte incompatFlags = data[2];
                byte compatFlags = data[3];
                byte sequence = data[4];
                info.SystemId = data[5];
                info.ComponentId = data[6];

                info.MessageIdExtended = (uint)(data[7] | (data[8] << 8) | (data[9] << 16));
                info.MessageId = (byte)(info.MessageIdExtended & 0xFF);

                int expectedLength = 12 + info.PayloadLength;

                bool hasSigning = (incompatFlags & 0x01) != 0;
                if (hasSigning)
                {
                    expectedLength += 13;
                }

                if (data.Length < expectedLength)
                {
                    info.ErrorMessage = $"Packet too short: expected {expectedLength}, got {data.Length}";
                    return false;
                }

                info.MessageName = GetMessageName(info.MessageId);
                return true;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"MAVLink v2 decode error: {ex.Message}";
                return false;
            }
        }

        private string GetMessageName(byte messageId)
        {
            if (MessageNames.TryGetValue(messageId, out string name))
            {
                return name;
            }
            return $"UNKNOWN_MSG_{messageId}";
        }

        public static Dictionary<byte, string> GetAllMessageTypes()
        {
            return new Dictionary<byte, string>(MessageNames);
        }
    }
}