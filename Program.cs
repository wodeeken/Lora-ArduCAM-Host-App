using System;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Formats.Asn1;
using System.Timers;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.ComponentModel;
namespace LoraArduCAMHostApp
{
    class Program
    {
        
        public static Constants.HostAppState CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
        private static ConsolePrinter printer;
        private static System.Timers.Timer ReceiverConnectTimer = new System.Timers.Timer(5000);
        private static System.Timers.Timer CameraTimeoutTimer = new System.Timers.Timer(1000);
        private static bool ReceiverConnectTimer_Ticked = false;
        private static string ReceiverSerialResponse = "";
        private static List<byte> ReceiverSerialResponse_Bytes = new List<byte>();
        // Image Transfer Variables.
        private static int TotalImagePacketCount = 0;
        private static int CurrentImagePacket = 0;
        private static List<byte> ImageData;
        private static int CameraTimeoutCount = 0;
        public static string ProgressMessage = "";
        private static bool firstRead = true;
        private static SerialPort CurrentSerialPort;
        private static bool ProgramRun = true;
        private static int MaxDataTransferRetries = 5;
        private static int CurrentDataTransferRetry = 0;
        private static string ImageFolder;
        private static void ReceiverConnectTimer_ElapsedHandler(object? sender, ElapsedEventArgs e){
            ReceiverConnectTimer.Stop();
            ReceiverConnectTimer_Ticked = true;
        }
        private static void CameraTimeoutTimer_ElapsedHandler(object? sender, ElapsedEventArgs e){
         
            CameraTimeoutCount++;
        }
        
        private static void CurrentSerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e){
            try{
                
                // Determine what state we are in.
                switch(CurrentCameraState){
                    case Constants.HostAppState.ReceiverDisconnected:
                        // This was from an attempted connection / or Ping. Save response.
                        ReceiverSerialResponse  = CurrentSerialPort.ReadLine().Trim();
                        CurrentSerialPort.DiscardInBuffer();
                        break;
                    case Constants.HostAppState.PingCamera:
                        // This was from an attempted connection / or Ping. Save response.
                        ReceiverSerialResponse  = CurrentSerialPort.ReadLine().Trim();
                        CurrentSerialPort.DiscardInBuffer();
                        break;
                    case Constants.HostAppState.Trigger:
                        ReceiverSerialResponse = CurrentSerialPort.ReadLine().Trim();
                        CurrentSerialPort.DiscardInBuffer();
                    break;
                    case Constants.HostAppState.PacketTransfer:
                        
                        int bytesToRead = CurrentSerialPort.BytesToRead;
                        byte[] ReadBuffer = new byte[bytesToRead];
                        CurrentSerialPort.Read(ReadBuffer, 0, bytesToRead);
                        foreach(var curByte in ReadBuffer){
                            ReceiverSerialResponse_Bytes.Add(curByte);
                        }
                        // Append response to ReceiverSerialResponse.
                        ReceiverSerialResponse += System.Text.Encoding.ASCII.GetString(ReadBuffer);
                        break;
                    default:
                        ReceiverSerialResponse = "";
                        CurrentSerialPort.DiscardInBuffer();
                        
                    break;
                    
                }
            }catch(Exception ex){
                Console.WriteLine("Just got an error thrown WTD.: " + ex.Message);
            }
            
        }
        // Main entry point. 
        static void Main(string[] args)
        {
            // Hook up ReceiverRetryTimer
            ReceiverConnectTimer.Elapsed += ReceiverConnectTimer_ElapsedHandler;
            CameraTimeoutTimer.Elapsed += CameraTimeoutTimer_ElapsedHandler;
            
            // Handle help.
            if(args != null && args.Count() > 0){
                if(args[0] == "-h"){
                    Console.WriteLine("Lora ArduCAM Host Application");
                    Console.WriteLine("Run with no args to use system temporary folder.");
                    Console.WriteLine("Run with one argument to specify a folder to write images to.");
                    return;
                }else if(args[0] != String.Empty){
                    if(Directory.Exists(args[0])){
                        ImageFolder = args[0];
                    }else{
                        ImageFolder = Path.GetTempPath();
                    }
                }
            }
            if(ImageFolder == null || ImageFolder == String.Empty){
                ImageFolder = Path.GetTempPath();
            }
            printer = new ConsolePrinter(ImageFolder);
            while(ProgramRun){
                Thread.Sleep(100);
                switch(CurrentCameraState){
                    case Constants.HostAppState.ReceiverDisconnected:
                        // Console - output staet.
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                        // Connect to the receiver.
                        bool success = FindSerialPort();
                        if(success){
                            // Change state to wait.
                            CurrentCameraState = Constants.HostAppState.Wait;
                            printer.PrintState(ConsolePrinter.CurrentConsoleState.Idle);
                        }else{
                            
                            // Change state to ReceiverDisconnected_WaitingForRetry.
                            CurrentCameraState = Constants.HostAppState.ReceiverDisconnected_WaitingForRetry;
                            printer.PrintState(ConsolePrinter.CurrentConsoleState.Idle);
                            // Begin the timer.
                            ReceiverConnectTimer_Ticked = false;
                            ReceiverConnectTimer.Start();
                        }
                        break;
                    case Constants.HostAppState.ReceiverDisconnected_WaitingForRetry:
                        
                        // Did the retry timer tick?
                        if(ReceiverConnectTimer_Ticked){
                            ReceiverConnectTimer_Ticked = false;
                            // Change state back to disconnected.
                            CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
                        }
                        break;
                    case Constants.HostAppState.Wait:
                        // Do nothing besides listen to console.in for input. 
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.Idle);
                        ConsoleInput();
                        break;
                    case Constants.HostAppState.PingCamera:
                        ProgressMessage = "Pinging Camera";
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                        ReceiverSerialResponse = "";
                        try{
                            CurrentSerialPort.WriteLine(Constants.Ping_Camera_Command);
                            CameraTimeoutCount = 0;
                            CameraTimeoutTimer.Start();
                            bool CameraPingSuccess = false;
                            string CameraPingMessage = "";
                        
                            while(CameraTimeoutCount < 10){
                                
                                Thread.Sleep(200);
                                if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == ""){
                                    ProgressMessage = "Pinging Camera";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }else if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == Constants.Ping_Camera_Response){
                                    CameraPingSuccess = true;
                                    CameraPingMessage = "Camera Ping Successful!";
                                    break;
                                }else if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == Constants.Ping_Camera_Timeout){
                                    CameraPingMessage = "Ping Timed Out on Receiver Side";
                                    break;
                                }else if(ReceiverSerialResponse != null){
                                    CameraPingMessage = "Unrecognized response from ping: " + ReceiverSerialResponse;
                                }else{
                                    ProgressMessage = "Pinging Camera";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }
                            }
                            CameraTimeoutTimer.Stop();
                            if(CameraPingMessage == "" && !CameraPingSuccess){
                                CameraPingMessage = "Ping Timed Out on Host App Side (Camera nor Receiver responded to Ping request)";
                            }
                            ProgressMessage = CameraPingMessage;
                            printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                            Thread.Sleep(2000);
                            CurrentCameraState = Constants.HostAppState.Wait;
                            }catch(Exception)
                            {
                                ProgressMessage = "RECEIVER DISCONNECTED!";
                                CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
                                Thread.Sleep(1000);
                            }
                        break;
                    case Constants.HostAppState.Trigger:
                        ProgressMessage = "Triggering Camera";
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                        ReceiverSerialResponse = "";
                        try{
                            CurrentSerialPort.WriteLine(Constants.Trigger_Camera_Command);
                            CameraTimeoutCount = 0;
                            CameraTimeoutTimer.Start();
                            bool CameraTriggerSuccess = false;
                            string CameraTriggerMessage = "";
                        
                            while(CameraTimeoutCount < 10){
                                // Perform regex on response.
                                Match responseMatch = Regex.Match(ReceiverSerialResponse, Constants.Trigger_Camera_Response);
                                Thread.Sleep(200);
                                if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == ""){
                                    ProgressMessage = "Triggering Camera";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }else if(ReceiverSerialResponse != null && responseMatch.Success){
                                    // Get the digit.
                                    Match packetNumberMatch = Regex.Match(ReceiverSerialResponse, "\\d+");

                                    CameraTriggerSuccess = true;
                                    CameraTriggerMessage = $"Camera Trigger Successful! Image will be sent in {packetNumberMatch.Value} packets.";
                                    TotalImagePacketCount = Int32.Parse(packetNumberMatch.Value);
                                    CurrentImagePacket = 0;
                                    ImageData = new List<byte>();
                                    CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                    break;
                                }else if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == Constants.Trigger_Camera_Timeout){
                                    CameraTriggerMessage = "Camera Trigger Timed Out on Receiver Side";
                                    CurrentCameraState = Constants.HostAppState.Wait;
                                    break;
                                }else if(ReceiverSerialResponse != null){
                                    CameraTriggerMessage = "Unrecognized response from ping: " + ReceiverSerialResponse;
                                    CurrentCameraState = Constants.HostAppState.Wait;
                                }else{
                                    ProgressMessage = "Pinging Camera";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }
                            }
                            CameraTimeoutTimer.Stop();
                            if(CameraTriggerMessage == "" && !CameraTriggerSuccess){
                                CameraTriggerMessage = "Ping Timed Out on Host App Side (Camera nor Receiver responded to Ping request)";
                                CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
                            }
                            ProgressMessage = CameraTriggerMessage;
                            printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                            Thread.Sleep(1000);
                            }catch(Exception)
                            {
                                    ProgressMessage = "RECEIVER DISCONNECTED!";
                                    CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
                                    Thread.Sleep(1000);
                            }
                    break;
                    case Constants.HostAppState.PacketTransfer:
                        if(CurrentImagePacket >= TotalImagePacketCount){
                            CurrentCameraState = Constants.HostAppState.ImageComplete;
                            break;
                        }
                        ProgressMessage = "Data Transfer";
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                        ReceiverSerialResponse = "";
                        try{
                            // Send request for current packet number.
                            CurrentSerialPort.WriteLine(Constants.Data_Transfer_Request.Replace("PACKET_NUM", CurrentImagePacket.ToString()));
                            CurrentSerialPort.DiscardInBuffer();
                            CameraTimeoutCount = 0;
                            CameraTimeoutTimer.Start();
                            bool TransferPacketSuccess = false;
                            string TransferPacketMessage = "";
                        
                            while(CameraTimeoutCount < 10){
                                // Perform regex on response.
                                Match responseMatch = Regex.Match(ReceiverSerialResponse, Constants.Data_Transfer_Response_Header);
                                
                                Thread.Sleep(50);
                                if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == ""){
                                    ProgressMessage = $"Attempt {CurrentDataTransferRetry} of {MaxDataTransferRetries} - Waiting on transfer packet { CurrentImagePacket + 1} of { TotalImagePacketCount}";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }else if(ReceiverSerialResponse != null && responseMatch.Success){
                                    // Get the digit.
                                    Match packetNumberMatch = Regex.Match(ReceiverSerialResponse, "\\d+");
                                    if(packetNumberMatch.Success){
                                        int currentPacketNum = Int32.Parse(packetNumberMatch.Value);
                                        if(currentPacketNum != CurrentImagePacket){
                                            // Packet Received out of order. Retry intended packet.
                                            CurrentDataTransferRetry = 0;
                                            CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                            ReceiverSerialResponse = "";
                                            ReceiverSerialResponse_Bytes = new List<byte>();
                                        }else{
                                            // Wait a second to ensure all trailing data is included.
                                             Thread.Sleep(250);
                                            
                                            // Get the data out.
                                            string dataAdded = "";
                                            
                                            int dataBegin = responseMatch.Index + responseMatch.Length;
                                            string currentPacketData = "";
                                            for(int i = dataBegin; i < ReceiverSerialResponse_Bytes.Count() && (i  < Constants.ImageDataPacketSize + dataBegin); i++){
                                                ImageData.Add(ReceiverSerialResponse_Bytes[i]);
                                                currentPacketData += BitConverter.ToString(new byte[]{ReceiverSerialResponse_Bytes[i]}) + ",";
                                            }
                                            TransferPacketSuccess = true;
                                            
                                            CurrentImagePacket++;
                                            CurrentDataTransferRetry = 0;
                                            CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                            ReceiverSerialResponse = "";
                                            ReceiverSerialResponse_Bytes = new List<byte>();
                                    }
                                    }else{
                                        TransferPacketMessage = "Data Transfer Error Occurred.";
                                        CurrentCameraState = Constants.HostAppState.Wait;
                                    break;
                                    }
                                    
                                    break;
                                }else if(ReceiverSerialResponse != null && ReceiverSerialResponse.Trim() == Constants.Data_Transfer_Error){
                                    
                                    CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                    break;
                                }else if(ReceiverSerialResponse != null){
                                    CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                }else{
                                    ProgressMessage = "Pinging Camera";
                                    for(int i = 0; i < CameraTimeoutCount; i++)
                                        ProgressMessage += ".";
                                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                                }
                            }
                            CameraTimeoutTimer.Stop();
                            if(TransferPacketMessage == "" && !TransferPacketSuccess){
                                // Timeout, retry packet request.
                                CurrentDataTransferRetry++;
                                if(CurrentDataTransferRetry > MaxDataTransferRetries){
                                    CurrentDataTransferRetry = 0;
                                    TransferPacketMessage = $"Data Transfer Request Failed for packet {CurrentImagePacket + 1} of {TotalImagePacketCount}. Quitting transfer.";
                                    CurrentCameraState = Constants.HostAppState.Wait;
                                }else{
                                    TransferPacketMessage = $"Attempt {CurrentDataTransferRetry - 1} of {MaxDataTransferRetries} Failed - Retrying data request for packet {CurrentImagePacket + 1} of {TotalImagePacketCount}";
                                    CurrentCameraState = Constants.HostAppState.PacketTransfer;
                                }
                                
                            }
                            ProgressMessage = TransferPacketMessage;
                            printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                            //Thread.Sleep(2000);
                            }catch(Exception ex)
                            {
                                    ProgressMessage = $"RECEIVER DISCONNECTED! {ex.Message}";
                                    Console.WriteLine(ReceiverSerialResponse + " - " + ex.Message + " - " + ex.StackTrace);
                                    CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
                                    Thread.Sleep(10000);
                            }
                    break;
                    case Constants.HostAppState.ImageComplete:
                        // Write camera data to file.
                        
                        
                        string imageFile = "";
                        // Windows does not allow ':', Linux does.
                         if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            imageFile = Path.Combine(ImageFolder, DateTime.Now.ToString("yyyy-dd-MM hh_mm_ss") + ".jpg");
                        else if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            imageFile  = Path.Combine(ImageFolder, DateTime.Now.ToString("yyyy-dd-MM hh:mm:ss") + ".jpg");
                        File.WriteAllBytes(imageFile, ImageData.ToArray() );
                        ProgressMessage = $"Image Transfer Completed. Image at {imageFile}.";
                        printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                        Console.WriteLine(ProgressMessage );
                        Thread.Sleep(3000);
                        CurrentCameraState = Constants.HostAppState.Wait;
                    break;
                }
            }   
        }
        static bool FindSerialPort(){
            // If CurrentSerialPort is not null and open, close it.
            if(CurrentSerialPort != null && CurrentSerialPort.IsOpen){
                CurrentSerialPort.Close();
            }
            // go through all /dev/ttyUSBXX ports, from 0 to 99, open, and ping it.
            string portSyntax;
            if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                portSyntax = Constants.SerialPortFormat_Windows;
            else if(System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                portSyntax = Constants.SerialPortFormat_Linux; 
            else
                throw new NotImplementedException("Only Windows and Linux OS is supported."); 
            int currentPortNum = 0;
            bool shouldContinue = true;
            while(shouldContinue){
                Thread.Sleep(100);
                try{
                    string portName = portSyntax.Replace("<>", currentPortNum.ToString());
                    ProgressMessage = "Checking port " + portName;
                    printer.PrintState(ConsolePrinter.CurrentConsoleState.BlockingProgress);
                    CurrentSerialPort = new SerialPort(portName, 38400, Parity.None, 8,StopBits.One);
                    CurrentSerialPort.NewLine = "\r";
                    CurrentSerialPort.ReadTimeout = 5000;
                    CurrentSerialPort.Open();
                    CurrentSerialPort.DiscardInBuffer();
                    CurrentSerialPort.WriteLine(Constants.Receiver_Ping_Command);
                    CurrentSerialPort.DiscardInBuffer();
                    Thread.Sleep(1000);
                    CurrentSerialPort.WriteLine(Constants.Receiver_Ping_Command);
                    
                    CurrentSerialPort.DataReceived += CurrentSerialPort_DataReceived;
                    // Waste some time.
                    for(int i = 0; i < 1000000000; i++){
                        double y = (double) i / 898231;

                    }
                   // The first transmission sometimes contains noise. Only check for containment of ping response, not exact. 
                    if(ReceiverSerialResponse == null || !ReceiverSerialResponse.Contains(Constants.Receiver_Ping_Response)){
                        currentPortNum++;
                        CurrentSerialPort.DiscardInBuffer();
                        CurrentSerialPort.DiscardOutBuffer();
                        CurrentSerialPort.Close();
                        CurrentSerialPort.Dispose();
                        ReceiverSerialResponse = "";
                    }
                    else{
                        shouldContinue = false;
                    }
                        
                    if(currentPortNum > 29)
                        shouldContinue = false;

                }catch(Exception){
                    currentPortNum++;
                    if(currentPortNum > 29)
                        shouldContinue = false;
                }
            }
            if(currentPortNum < 30)
            {
              
                return true;
            }else{
                
                return false;
            }
            
        }
        
        static void ConsoleInput(){
            
            string? input = Console.ReadLine();
            
                        if(input != null){
                            if(input.ToLower() == "q"){
                                CurrentSerialPort.Close();
                                CurrentSerialPort.Dispose();
                                Console.WriteLine("Goodbye!");
                                Thread.Sleep(1000);
                                ProgramRun = false;
                            }else if(input.ToLower() == "p"){
                                CurrentCameraState = Constants.HostAppState.PingCamera;

                            }else if(input.ToLower() == "t"){
                                CurrentCameraState = Constants.HostAppState.Trigger;
                            }
                        }
        }

        
        
        
    }
}
