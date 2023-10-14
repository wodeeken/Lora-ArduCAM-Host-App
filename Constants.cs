using System.Runtime.InteropServices.Marshalling;

namespace LoraArduCAMHostApp{
    static class Constants{
        // Receiver PING 
        public const string Receiver_Ping_Command = "<PING>";
        public const string Receiver_Ping_Response = "<PING_RESPONSE>";
        public const string Ping_Camera_Command = "<PING_CAMERA>";
        public const string Ping_Camera_Timeout = "<PING_CAMERA_TIMEOUT>";
        public const string Ping_Camera_Response = "<PING_CAMERA_RESPONSE>";
        public const string Trigger_Camera_Command = "<TRIGGER_CAMERA>";
        public const string Trigger_Camera_Timeout = "<TRIGGER_CAMERA_TIMEOUT>";
        public const string Trigger_Camera_Response = "<TRIGGER_CAMERA_RESPONSE>";
        public const string Image_Header_Command = "<IMAGE_HEADER>";
        public const string Image_Header_Timeout = "<IMAGE_HEADER_TIMEOUT>";
        public const string Image_Header_Response = "<IMAGE_HEADER_RESPONSE>";
        public const string Image_Body_Command = "<IMAGE_BODY>";


        public enum HostAppState{
            ReceiverDisconnected,
            ReceiverDisconnected_WaitingForRetry,
            Wait,
            PingCamera,
            WaitForPingReceiverConfirmation,
            Trigger,
            WaitForTriggerConfirmation,
            WaitForImageCaptureHeader,
            WaitForImage

        }
    }
}