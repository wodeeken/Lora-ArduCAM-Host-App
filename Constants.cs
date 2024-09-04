using System.Runtime.InteropServices.Marshalling;

namespace LoraArduCAMHostApp{
    static class Constants{
        // Receiver PING 
        public const string Receiver_Ping_Command = "<PING>";
        public const string Receiver_Ping_Response = "<PING_RESPONSE>";
        public const string Ping_Camera_Command = "<PING_CAMERA>";
        public const string Ping_Camera_Timeout = "<PING_CAMERA_TIMEOUT>";
        public const string Ping_Camera_Response = "<PING_CAMERA_RESPONSE>";
        public const string Trigger_Camera_Command = "<CAPTURE_CAMERA>";
        public const string Trigger_Camera_Timeout = "<CAPTURE_CAMERA_TIMEOUT>";
        public const string Trigger_Camera_Response = "<CAPTURE_CAMERA_RESPONSE {\\d+}>";
        public const string Data_Transfer_Request = "<DATA_TRANSFER_REQUEST {PACKET_NUM}>";
        public const string Data_Transfer_Response_Header = "<DATA_TRANSFER_RESPONSE {\\d+}>";
        public const string Data_Transfer_Error = "<DATA_TRANSFER_ERROR>";
        public const string Image_Header_Command = "<IMAGE_HEADER>";
        public const string Image_Header_Timeout = "<IMAGE_HEADER_TIMEOUT>";
        public const string Image_Header_Response = "<IMAGE_HEADER_RESPONSE>";
        public const string Image_Body_Command = "<IMAGE_BODY>";
        public const string SerialPortFormat_Windows = "COM<>";
        public const string SerialPortFormat_Linux = "/dev/ttyUSB<>";
        public const int ImageDataPacketSize = 95;


        public enum HostAppState{
            ReceiverDisconnected,
            ReceiverDisconnected_WaitingForRetry,
            Wait,
            PingCamera,
            WaitForPingReceiverConfirmation,
            Trigger,
            PacketTransfer,
            ImageComplete

        }
    }
}