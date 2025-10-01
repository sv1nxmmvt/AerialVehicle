using System;
using System.Collections.Generic;
using System.Text;

namespace Receiver
{
    public class MavlinkDecoder
    {
        private static readonly Dictionary<uint, string> MessageNames = new Dictionary<uint, string>
        {
            { 0, "HEARTBEAT" },
            { 1, "SYS_STATUS" },
            { 2, "SYSTEM_TIME" },
            { 24, "GPS_RAW_INT" },
            { 27, "RAW_IMU" },
            { 29, "RAW_PRESSURE" },
            { 30, "ATTITUDE" },
            { 33, "GLOBAL_POSITION_INT" },
            { 74, "VFR_HUD" },
            { 111, "TIMESYNC" },
            { 163, "UNKNOWN_MSG_163" },
            { 137, "UNKNOWN_MSG_137" },
            { 178, "UNKNOWN_MSG_178" }
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
            public byte Sequence { get; set; }
            public string DecodedContent { get; set; }
            public byte[] Payload { get; set; }
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

                if (info.IsValid && info.Payload != null)
                {
                    info.DecodedContent = DecodePayload(info.MessageIdExtended, info.Payload);
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
                {
                    info.ErrorMessage = "Packet too short for MAVLink v1";
                    return false;
                }

                info.Version = "MAVLink v1.0";
                info.PayloadLength = data[1];
                info.Sequence = data[2];
                info.SystemId = data[3];
                info.ComponentId = data[4];
                info.MessageId = data[5];
                info.MessageIdExtended = info.MessageId;

                int expectedLength = 6 + info.PayloadLength + 2;
                if (data.Length < expectedLength)
                {
                    info.ErrorMessage = $"Packet too short: expected {expectedLength}, got {data.Length}";
                    return false;
                }

                info.Payload = new byte[info.PayloadLength];
                Array.Copy(data, 6, info.Payload, 0, info.PayloadLength);

                info.MessageName = GetMessageName(info.MessageId);
                info.IsValid = true;
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
                {
                    info.ErrorMessage = "Packet too short for MAVLink v2";
                    return false;
                }

                info.Version = "MAVLink v2.0";
                info.PayloadLength = data[1];
                byte incompatFlags = data[2];
                info.Sequence = data[4];
                info.SystemId = data[5];
                info.ComponentId = data[6];

                info.MessageIdExtended = (uint)(data[7] | (data[8] << 8) | (data[9] << 16));
                info.MessageId = (byte)(info.MessageIdExtended & 0xFF);

                int expectedLength = 10 + info.PayloadLength + 2;
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

                info.Payload = new byte[info.PayloadLength];
                Array.Copy(data, 10, info.Payload, 0, info.PayloadLength);

                info.MessageName = GetMessageName(info.MessageIdExtended);
                info.IsValid = true;
                return true;
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"MAVLink v2 decode error: {ex.Message}";
                return false;
            }
        }

        private string DecodePayload(uint messageId, byte[] payload)
        {
            try
            {
                switch (messageId)
                {
                    case 0:
                        return DecodeHeartbeat(payload);

                    case 1:
                        return DecodeSysStatus(payload);

                    case 24:
                        return DecodeGpsRawInt(payload);

                    case 30:
                        return DecodeAttitude(payload);

                    case 33:
                        return DecodeGlobalPositionInt(payload);

                    case 74:
                        return DecodeVfrHud(payload);

                    case 111:
                        return DecodeTimesync(payload);

                    case 29:
                        return DecodeRawPressure(payload);

                    default:
                        return $"MsgID: {messageId} | Payload: {payload.Length}B | Data: {BitConverter.ToString(payload, 0, Math.Min(20, payload.Length)).Replace("-", " ")}";
                }
            }
            catch (Exception ex)
            {
                return $"Decode error: {ex.Message}";
            }
        }

        private string DecodeHeartbeat(byte[] p)
        {
            if (p.Length < 9) return "Invalid payload";

            uint customMode = BitConverter.ToUInt32(p, 0);
            byte type = p[4];
            byte autopilot = p[5];
            byte baseMode = p[6];
            byte systemStatus = p[7];

            return $"Type: {type}, Autopilot: {autopilot}, Mode: {baseMode}, Status: {systemStatus}, CustomMode: {customMode}";
        }

        private string DecodeSysStatus(byte[] p)
        {
            if (p.Length < 31) return "Invalid payload";

            ushort voltageBattery = BitConverter.ToUInt16(p, 12);
            short currentBattery = BitConverter.ToInt16(p, 14);
            sbyte batteryRemaining = (sbyte)p[16];

            return $"Battery: {voltageBattery / 1000.0:F2}V, Current: {currentBattery / 100.0:F1}A, Remaining: {batteryRemaining}%";
        }

        private string DecodeGpsRawInt(byte[] p)
        {
            if (p.Length < 30) return "Invalid payload";

            int lat = BitConverter.ToInt32(p, 8);
            int lon = BitConverter.ToInt32(p, 12);
            int alt = BitConverter.ToInt32(p, 16);
            byte fixType = p[24];
            byte satellitesVisible = p[25];

            return $"Lat: {lat / 1e7:F7}°, Lon: {lon / 1e7:F7}°, Alt: {alt / 1000.0:F1}m, Fix: {fixType}, Sats: {satellitesVisible}";
        }

        private string DecodeAttitude(byte[] p)
        {
            if (p.Length < 28) return "Invalid payload";

            float roll = BitConverter.ToSingle(p, 4);
            float pitch = BitConverter.ToSingle(p, 8);
            float yaw = BitConverter.ToSingle(p, 12);

            return $"Roll: {roll * 180 / Math.PI:F2}°, Pitch: {pitch * 180 / Math.PI:F2}°, Yaw: {yaw * 180 / Math.PI:F2}°";
        }

        private string DecodeGlobalPositionInt(byte[] p)
        {
            if (p.Length < 28) return "Invalid payload";

            int lat = BitConverter.ToInt32(p, 4);
            int lon = BitConverter.ToInt32(p, 8);
            int alt = BitConverter.ToInt32(p, 12);
            int relativeAlt = BitConverter.ToInt32(p, 16);

            return $"Lat: {lat / 1e7:F7}°, Lon: {lon / 1e7:F7}°, Alt: {alt / 1000.0:F1}m, RelAlt: {relativeAlt / 1000.0:F1}m";
        }

        private string DecodeVfrHud(byte[] p)
        {
            if (p.Length < 20) return "Invalid payload";

            float airspeed = BitConverter.ToSingle(p, 0);
            float groundspeed = BitConverter.ToSingle(p, 4);
            short heading = BitConverter.ToInt16(p, 8);
            ushort throttle = BitConverter.ToUInt16(p, 10);
            float alt = BitConverter.ToSingle(p, 12);
            float climb = BitConverter.ToSingle(p, 16);

            return $"AirSpd: {airspeed:F1}m/s, GndSpd: {groundspeed:F1}m/s, Hdg: {heading}°, Thr: {throttle}%, Alt: {alt:F1}m, Climb: {climb:F1}m/s";
        }

        private string DecodeTimesync(byte[] p)
        {
            if (p.Length < 16) return "Invalid payload";

            long tc1 = BitConverter.ToInt64(p, 0);
            long ts1 = BitConverter.ToInt64(p, 8);

            return $"TC1: {tc1}, TS1: {ts1}";
        }

        private string DecodeRawPressure(byte[] p)
        {
            if (p.Length < 16) return "Invalid payload";

            short pressAbs = BitConverter.ToInt16(p, 8);
            short pressDiff1 = BitConverter.ToInt16(p, 10);
            short temperature = BitConverter.ToInt16(p, 14);

            return $"PressAbs: {pressAbs}, PressDiff: {pressDiff1}, Temp: {temperature / 100.0:F1}°C";
        }

        private string GetMessageName(uint messageId)
        {
            if (MessageNames.TryGetValue(messageId, out string name))
            {
                return name;
            }
            return $"UNKNOWN_MSG_{messageId}";
        }
    }
}