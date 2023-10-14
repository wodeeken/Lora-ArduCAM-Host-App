using System;
using System.IO.Ports;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Formats.Asn1;
using System.Timers;
namespace LoraArduCAMHostApp
{
    class Program
    {
        
        public static Constants.HostAppState CurrentCameraState = Constants.HostAppState.ReceiverDisconnected;
        private static ConsolePrinter printer = new ConsolePrinter();
        private static System.Timers.Timer ReceiverConnectTimer = new System.Timers.Timer(5000);
        private static System.Timers.Timer CameraTimeoutTimer = new System.Timers.Timer(1000);
        private static bool ReceiverConnectTimer_Ticked = false;
        private static string ReceiverSerialResponse = "";
        private static int CameraTimeoutCount = 0;
        public static string ProgressMessage = "";
        private static bool firstRead = true;
        private static SerialPort CurrentSerialPort;
        private static bool ProgramRun = true;
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
                    
                }
                Console.WriteLine(ReceiverSerialResponse);
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
            
            while(ProgramRun){
                Thread.Sleep(500);
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
                            
                            //Console.WriteLine("IM LOOPING!!" + loopCounter.ToString());
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
                }
            }   
        }
        static bool FindSerialPort(){
            // go through all /dev/ttyUSBXX ports, from 0 to 99, open, and ping it.
            string portSyntax = "/dev/ttyUSB<>";
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
        // delegate string? ReadLineDelegate();
        // static string? ReadLine(int timeoutms)
        // {
        //     ReadLineDelegate d = Console.In.ReadLine;
        //     Task<string?> readTask = Task<string?>.Factory.StartNew(()=>d());
        //     //myReadTask.RunSynchronously();
        //     bool completed = readTask.Wait(timeoutms);
        //     //IAsyncResult result = d.BeginInvoke(null, null);
        //     //bool completed = myReadTask.Wait(timeoutms);
            
        //     if (completed)
        //     {
        //         string? resultstr = readTask.Result; //"asdfasd";// d.EndInvoke(result);
        //         //Console.WriteLine("Read: " + resultstr);
        //         return resultstr;
        //     }
        //     else
        //     {
        //         //Console.WriteLine("Timed out!");
        //         return "";
        //     }
        // }
        static void ConsoleInput(){
            
            string? input = Console.ReadLine();// ReadLine(5000);//Console.In.ReadToEnd();
            
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
                                
                            }
                        }
        }

        
        
        
    }
}
